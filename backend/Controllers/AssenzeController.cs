using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RimettimiInSesto.Api.Data;
using RimettimiInSesto.Api.Dtos;
using RimettimiInSesto.Api.Models;
using RimettimiInSesto.Api.Services;

namespace RimettimiInSesto.Api.Controllers;

[ApiController]
[Route("api/assenze")]
[Authorize]
public class AssenzeController(ApplicationDbContext db, AvailabilityService availabilityService)
    : ControllerBase
{
    [HttpGet("mie")]
    [Authorize(Roles = "Fisioterapista")]
    public async Task<ActionResult<List<AssenzaDto>>> Mie()
    {
        var utenteId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var fisioterapista = await db.Fisioterapisti.FirstOrDefaultAsync(f => f.UtenteId == utenteId);
        if (fisioterapista is null) return Forbid();

        return Ok(await ElencoAsync(a => a.FisioterapistaId == fisioterapista.Id));
    }

    // Dipendente e collaboratore non seguono la stessa strada: il primo chiede un permesso
    // che la Coordinatrice approva e che incide sul monte ore, il secondo gestisce da sé la
    // propria disponibilità e non ha nulla da far approvare (requisiti.md).
    [HttpPost]
    [Authorize(Roles = "Fisioterapista")]
    public async Task<ActionResult<AssenzaDto>> Richiedi(RichiediAssenzaRequest request)
    {
        var utenteId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var fisioterapista = await db.Fisioterapisti.FirstOrDefaultAsync(f => f.UtenteId == utenteId);
        if (fisioterapista is null) return Forbid();

        if (request.DataFine < request.DataInizio)
        {
            return BadRequest(new { messaggio = "La data di fine non può precedere quella di inizio." });
        }

        var assenza = new AssenzaFisioterapista
        {
            FisioterapistaId = fisioterapista.Id,
            DataInizio = request.DataInizio,
            DataFine = request.DataFine,
            Tipo = request.Tipo,
            Motivo = request.Motivo,
            StatoApprovazione = fisioterapista.TipoContratto == TipoContratto.Dipendente
                ? StatoApprovazioneAssenza.InAttesa
                : StatoApprovazioneAssenza.NonRichiesta,
        };

        db.AssenzeFisioterapisti.Add(assenza);
        await db.SaveChangesAsync();

        return Ok((await ElencoAsync(a => a.Id == assenza.Id)).Single());
    }

    [HttpGet]
    [Authorize(Roles = "Coordinatrice")]
    public async Task<ActionResult<List<AssenzaDto>>> Tutte()
    {
        return Ok(await ElencoAsync(_ => true));
    }

    [HttpPut("{id:int}/approva")]
    [Authorize(Roles = "Coordinatrice")]
    public Task<ActionResult> Approva(int id) => DecidiAsync(id, StatoApprovazioneAssenza.Approvata);

    [HttpPut("{id:int}/rifiuta")]
    [Authorize(Roles = "Coordinatrice")]
    public Task<ActionResult> Rifiuta(int id) => DecidiAsync(id, StatoApprovazioneAssenza.Rifiutata);

    // La lista di lavoro sugli appuntamenti che cadono dentro l'assenza.
    //
    // Senza questa, registrare un'assenza lasciava i pazienti convocati per un terapista che
    // non c'è: il fisioterapista non tocca gli appuntamenti già presi (glielo dice la sua
    // schermata) e la Coordinatrice non aveva un posto dove rivederli. Ordinata per urgenza
    // — prima i cicli SSN con la finestra di completamento più vicina, perché è l'unica
    // scadenza che non si può spostare — e poi per data.
    [HttpGet("{id:int}/appuntamenti-impattati")]
    [Authorize(Roles = "Coordinatrice")]
    public async Task<ActionResult<List<AppuntamentoImpattatoDto>>> AppuntamentiImpattati(int id)
    {
        var assenza = await db.AssenzeFisioterapisti.FindAsync(id);
        if (assenza is null) return NotFound();

        var inizio = assenza.DataInizio.ToDateTime(TimeOnly.MinValue);
        var fine = assenza.DataFine.AddDays(1).ToDateTime(TimeOnly.MinValue);

        var appuntamenti = await db.Appuntamenti
            .Include(a => a.Paziente)
            .Include(a => a.Ricetta).ThenInclude(r => r!.CicloGenerato)
            .Where(a => a.FisioterapistaId == assenza.FisioterapistaId
                && a.DataOra >= inizio && a.DataOra < fine
                && (a.Stato == StatoAppuntamento.Richiesto || a.Stato == StatoAppuntamento.Confermato))
            .ToListAsync();

        var colleghi = await db.Fisioterapisti
            .Include(f => f.Utente)
            .Where(f => f.Id != assenza.FisioterapistaId)
            .ToListAsync();

        var risultato = new List<AppuntamentoImpattatoDto>();
        foreach (var appuntamento in appuntamenti)
        {
            var disponibili = new List<CollegaDisponibileDto>();
            foreach (var collega in colleghi)
            {
                if (await ELiberoAsync(collega.Id, appuntamento.DataOra, appuntamento.DurataMinuti))
                {
                    disponibili.Add(new CollegaDisponibileDto(
                        collega.Id,
                        $"{collega.Utente.Nome} {collega.Utente.Cognome}",
                        collega.TipoContratto.ToString()));
                }
            }

            risultato.Add(new AppuntamentoImpattatoDto(
                appuntamento.Id,
                appuntamento.PazienteId,
                $"{appuntamento.Paziente.Nome} {appuntamento.Paziente.Cognome}",
                appuntamento.DataOra,
                appuntamento.DurataMinuti,
                appuntamento.Stato.ToString(),
                appuntamento.Percorso.ToString(),
                appuntamento.Ricetta?.FinestraCompletamento,
                appuntamento.Ricetta?.CicloGenerato?.SeduteResidue,
                disponibili));
        }

        return Ok(risultato
            .OrderBy(a => a.FinestraCompletamentoSsn ?? DateOnly.MaxValue)
            .ThenBy(a => a.DataOra)
            .ToList());
    }

    private async Task<bool> ELiberoAsync(int fisioterapistaId, DateTime dataOra, int durataMinuti)
    {
        var disponibilita = await availabilityService.CalcolaDisponibilitaGiornoAsync(
            fisioterapistaId, DateOnly.FromDateTime(dataOra));
        if (disponibilita.Chiuso) return false;

        var orario = TimeOnly.FromDateTime(dataOra);
        for (var i = 0; i < durataMinuti / 30; i++)
        {
            var slot = disponibilita.Slots.FirstOrDefault(s => s.Orario == orario.AddMinutes(i * 30));
            if (slot is null || !slot.Libero) return false;
        }
        return true;
    }

    private async Task<ActionResult> DecidiAsync(int id, StatoApprovazioneAssenza esito)
    {
        var assenza = await db.AssenzeFisioterapisti.FindAsync(id);
        if (assenza is null) return NotFound();

        if (assenza.StatoApprovazione == StatoApprovazioneAssenza.NonRichiesta)
        {
            return BadRequest(new { messaggio = "Questa assenza è di un collaboratore e non passa da un'approvazione." });
        }

        assenza.StatoApprovazione = esito;
        await db.SaveChangesAsync();
        return Ok();
    }

    private async Task<List<AssenzaDto>> ElencoAsync(
        System.Linq.Expressions.Expression<Func<AssenzaFisioterapista, bool>> filtro)
    {
        return await db.AssenzeFisioterapisti
            .Where(filtro)
            .Include(a => a.Fisioterapista).ThenInclude(f => f.Utente)
            .OrderByDescending(a => a.DataInizio)
            .Select(a => new AssenzaDto(
                a.Id, a.FisioterapistaId,
                a.Fisioterapista.Utente.Nome + " " + a.Fisioterapista.Utente.Cognome,
                a.DataInizio, a.DataFine, a.Tipo.ToString(), a.StatoApprovazione.ToString(), a.Motivo))
            .ToListAsync();
    }
}
