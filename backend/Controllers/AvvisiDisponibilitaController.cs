using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RimettimiInSesto.Api.Data;
using RimettimiInSesto.Api.Dtos;
using RimettimiInSesto.Api.Models;
using RimettimiInSesto.Api.Services;

namespace RimettimiInSesto.Api.Controllers;

// La "lista d'attesa" del mockup. Non è una coda di pazienti senza appuntamento: ciascuna
// voce è appesa a un appuntamento già confermato che il paziente vorrebbe anticipare
// (requisiti.md: prenota comunque il primo slot libero e non resta mai scoperto).
[ApiController]
[Route("api/avvisi-disponibilita")]
[Authorize]
public class AvvisiDisponibilitaController(ApplicationDbContext db, NotificaService notificaService)
    : ControllerBase
{
    [HttpGet("miei")]
    [Authorize(Roles = "Paziente")]
    public async Task<ActionResult<List<AvvisoDisponibilitaDto>>> Miei()
    {
        var utenteId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var pazienteIds = await db.UtentiPazienti
            .Where(up => up.UtenteId == utenteId)
            .Select(up => up.PazienteId)
            .ToListAsync();

        return Ok(await ElencoAsync(a =>
            pazienteIds.Contains(a.PazienteId) && a.Stato != StatoAvvisoDisponibilita.Chiuso));
    }

    // La coda della segreteria: chi aspetta da più tempo viene prima, perché è l'unico
    // criterio equo quando lo slot che si libera è uno solo.
    [HttpGet]
    [Authorize(Roles = "Coordinatrice")]
    public async Task<ActionResult<List<AvvisoDisponibilitaDto>>> Coda()
    {
        return Ok(await ElencoAsync(a => a.Stato != StatoAvvisoDisponibilita.Chiuso));
    }

    [HttpPost]
    [Authorize(Roles = "Paziente")]
    public async Task<ActionResult<AvvisoDisponibilitaDto>> Attiva(CreaAvvisoRequest request)
    {
        var utenteId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        var appuntamento = await db.Appuntamenti
            .FirstOrDefaultAsync(a => a.Id == request.AppuntamentoId);
        if (appuntamento is null) return NotFound(new { messaggio = "Appuntamento non trovato." });

        var collegato = await db.UtentiPazienti
            .AnyAsync(up => up.UtenteId == utenteId && up.PazienteId == appuntamento.PazienteId);
        if (!collegato) return Forbid();

        if (appuntamento.Stato != StatoAppuntamento.Confermato)
        {
            return BadRequest(new
            {
                messaggio = "L'avviso si attiva su un appuntamento confermato: una richiesta non ancora decisa può ancora cambiare orario da sé.",
            });
        }

        if (appuntamento.DataOra <= DateTime.Now)
        {
            return BadRequest(new { messaggio = "Questo appuntamento è già passato." });
        }

        var esistente = await db.AvvisiDisponibilita.FirstOrDefaultAsync(a =>
            a.AppuntamentoId == appuntamento.Id && a.Stato != StatoAvvisoDisponibilita.Chiuso);
        if (esistente is not null)
        {
            return BadRequest(new { messaggio = "L'avviso è già attivo su questo appuntamento." });
        }

        var avviso = new AvvisoDisponibilita
        {
            PazienteId = appuntamento.PazienteId,
            AppuntamentoId = appuntamento.Id,
            CreatoIl = DateTime.Now,
            Stato = StatoAvvisoDisponibilita.Attivo,
        };
        db.AvvisiDisponibilita.Add(avviso);
        await db.SaveChangesAsync();

        return Ok((await ElencoAsync(a => a.Id == avviso.Id)).Single());
    }

    // Ci ha ripensato, o la segreteria l'ha spostato: l'avviso si chiude, non si cancella.
    // "È stato avvisato e ha detto di no" è un fatto utile la volta dopo.
    [HttpPut("{id:int}/chiudi")]
    [Authorize(Roles = "Paziente,Coordinatrice")]
    public async Task<ActionResult> Chiudi(int id)
    {
        var avviso = await db.AvvisiDisponibilita.FirstOrDefaultAsync(a => a.Id == id);
        if (avviso is null) return NotFound();

        if (User.IsInRole("Paziente"))
        {
            var utenteId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var collegato = await db.UtentiPazienti
                .AnyAsync(up => up.UtenteId == utenteId && up.PazienteId == avviso.PazienteId);
            if (!collegato) return Forbid();
        }

        avviso.Stato = StatoAvvisoDisponibilita.Chiuso;
        await db.SaveChangesAsync();
        return NoContent();
    }

    // L'avviso vero e proprio resta un gesto della Coordinatrice, non un automatismo:
    // con le notifiche allo stato di stub un invio automatico produrrebbe solo una riga
    // di log, e soprattutto è lei a sapere quale slot si è liberato e a chi conviene.
    [HttpPut("{id:int}/avvisa")]
    [Authorize(Roles = "Coordinatrice")]
    public async Task<ActionResult> Avvisa(int id, AvvisaDisponibilitaRequest request)
    {
        var avviso = await db.AvvisiDisponibilita
            .Include(a => a.Paziente)
            .Include(a => a.Appuntamento)
            .FirstOrDefaultAsync(a => a.Id == id);
        if (avviso is null) return NotFound();

        if (request.NuovoSlot <= DateTime.Now)
        {
            return BadRequest(new { messaggio = "Lo slot che si è liberato è già passato." });
        }

        // La soglia esisteva in ImpostazioniAgenda dal primo giorno senza che nessuno la
        // leggesse. Serve a non disturbare qualcuno per anticipare di poche ore: chi ha
        // preso un permesso al lavoro per quel giorno non ringrazia di una telefonata che
        // gli sposta la seduta di un pomeriggio.
        var sogliaGiorni = (await db.ImpostazioniAgenda.FirstOrDefaultAsync())
            ?.SogliaAvvisoDisponibilitaAnticipataGiorni ?? 7;
        var guadagnoGiorni = (avviso.Appuntamento.DataOra.Date - request.NuovoSlot.Date).TotalDays;

        if (guadagnoGiorni < sogliaGiorni)
        {
            return BadRequest(new
            {
                messaggio = $"Anticipa di {guadagnoGiorni:0} giorni, sotto la soglia di {sogliaGiorni}: non vale la pena disturbarlo. Se ha senso lo stesso, chiamalo e sposta l'appuntamento a mano.",
            });
        }

        var destinatario = avviso.Paziente.Email ?? avviso.Paziente.Telefono;
        if (string.IsNullOrWhiteSpace(destinatario))
        {
            return BadRequest(new
            {
                messaggio = "Questa scheda non ha né email né telefono: l'avviso va dato a voce.",
            });
        }

        await notificaService.NotificaAsync(
            TipoNotifica.AvvisoDisponibilitaAnticipata,
            destinatario,
            $"Si è liberato un posto il {request.NuovoSlot:dd/MM/yyyy} alle {request.NuovoSlot:HH:mm}, prima del suo appuntamento del {avviso.Appuntamento.DataOra:dd/MM/yyyy}."
                + (string.IsNullOrWhiteSpace(request.Testo) ? "" : $" {request.Testo.Trim()}"),
            avviso.PazienteId,
            avviso.AppuntamentoId);

        avviso.Stato = StatoAvvisoDisponibilita.Avvisato;
        avviso.AvvisatoIl = DateTime.Now;
        await db.SaveChangesAsync();

        return NoContent();
    }

    private async Task<List<AvvisoDisponibilitaDto>> ElencoAsync(
        System.Linq.Expressions.Expression<Func<AvvisoDisponibilita, bool>> filtro)
    {
        var avvisi = await db.AvvisiDisponibilita
            .Include(a => a.Paziente)
            .Include(a => a.Appuntamento).ThenInclude(ap => ap.Fisioterapista).ThenInclude(f => f.Utente)
            .Where(filtro)
            .OrderBy(a => a.CreatoIl)
            .ToListAsync();

        return avvisi.Select(a => new AvvisoDisponibilitaDto(
            a.Id,
            a.PazienteId,
            $"{a.Paziente.Nome} {a.Paziente.Cognome}",
            a.AppuntamentoId,
            a.Appuntamento.DataOra,
            $"{a.Appuntamento.Fisioterapista.Utente.Nome} {a.Appuntamento.Fisioterapista.Utente.Cognome}",
            a.Appuntamento.Percorso.ToString(),
            a.CreatoIl,
            (int)(DateTime.Now.Date - a.CreatoIl.Date).TotalDays,
            a.Stato.ToString(),
            a.AvvisatoIl)).ToList();
    }
}
