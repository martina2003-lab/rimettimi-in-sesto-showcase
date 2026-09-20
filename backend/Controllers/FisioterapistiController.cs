using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RimettimiInSesto.Api.Data;
using RimettimiInSesto.Api.Dtos;
using RimettimiInSesto.Api.Models;

namespace RimettimiInSesto.Api.Controllers;

[ApiController]
[Route("api/fisioterapisti")]
[Authorize]
public class FisioterapistiController(ApplicationDbContext db) : ControllerBase
{
    // Elenco per la scelta del paziente in fase di prenotazione. Espone nome e bio e
    // nient'altro: il tipo di contratto resta un dato interno (requisiti.md).
    // Chi è assente a lungo non è selezionabile, ma il filtro per data non si fa qui:
    // lo dice già il calcolo disponibilità, che per quei giorni risponde "chiuso".
    // pazienteId è opzionale: senza (nessuna scheda ancora scelta nel wizard) l'elenco
    // torna con Abituale/GiaVisto sempre false. Con pazienteId, stessa regola già scritta
    // per MieiPazienti ma vista dal lato opposto: qui si confrontano i fisioterapisti fra
    // loro per un paziente solo, non i pazienti fra loro per un fisioterapista solo.
    [HttpGet]
    public async Task<ActionResult<List<FisioterapistaPubblicoDto>>> Elenco([FromQuery] int? pazienteId)
    {
        if (pazienteId is not null)
        {
            if (User.IsInRole("Paziente") && !await EProprietarioAsync(pazienteId.Value)) return Forbid();
            if (!User.IsInRole("Paziente") && !User.IsInRole("Coordinatrice")) return Forbid();
        }

        var fisioterapisti = await db.Fisioterapisti
            .Include(f => f.Utente)
            .OrderBy(f => f.Utente.Cognome)
            .ToListAsync();

        var conteggi = pazienteId is null
            ? new List<(int FisioterapistaId, int Quanti)>()
            : (await db.Appuntamenti
                .Where(a => a.PazienteId == pazienteId.Value)
                .GroupBy(a => a.FisioterapistaId)
                .Select(g => new { g.Key, Quanti = g.Count() })
                .ToListAsync())
                .Select(g => (FisioterapistaId: g.Key, g.Quanti))
                .ToList();

        var risultato = fisioterapisti.Select(f =>
        {
            var conLui = conteggi.FirstOrDefault(c => c.FisioterapistaId == f.Id).Quanti;
            var conAltri = conteggi.Where(c => c.FisioterapistaId != f.Id).Select(c => c.Quanti).DefaultIfEmpty(0).Max();
            var abituale = conLui > 1 && conLui > conAltri;

            return new FisioterapistaPubblicoDto(
                f.Id,
                f.Utente.Nome + " " + f.Utente.Cognome,
                f.Bio,
                f.FotoProfiloUrl,
                abituale,
                conLui > 0);
        }).ToList();

        return Ok(risultato);
    }

    private async Task<bool> EProprietarioAsync(int pazienteId)
    {
        var utenteId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        return await db.UtentiPazienti.AnyAsync(up => up.UtenteId == utenteId && up.PazienteId == pazienteId);
    }

    [HttpGet("me")]
    [Authorize(Roles = "Fisioterapista")]
    public async Task<ActionResult<ProfiloFisioterapistaDto>> Profilo()
    {
        var fisioterapista = await CaricaMeAsync();
        if (fisioterapista is null) return Forbid();

        return Ok(new ProfiloFisioterapistaDto(
            fisioterapista.Id, fisioterapista.Utente.Nome, fisioterapista.Utente.Cognome,
            fisioterapista.Bio, fisioterapista.FotoProfiloUrl,
            fisioterapista.TipoContratto.ToString(), fisioterapista.PatternDisponibilita));
    }

    // La bio la gestisce il fisioterapista stesso: è quello che il paziente legge mentre
    // lo sceglie, e non ha senso che passi da altri.
    [HttpPut("me/bio")]
    [Authorize(Roles = "Fisioterapista")]
    public async Task<ActionResult> AggiornaBio(AggiornaBioRequest request)
    {
        var fisioterapista = await CaricaMeAsync();
        if (fisioterapista is null) return Forbid();

        fisioterapista.Bio = request.Bio;
        await db.SaveChangesAsync();
        return Ok();
    }

    // I pazienti a cui ho accesso, e perché. Non esiste una lista "i miei pazienti" da
    // qualche parte: si ricava dagli appuntamenti, che è l'unica cosa che autorizza
    // l'accesso clinico.
    [HttpGet("miei-pazienti")]
    [Authorize(Roles = "Fisioterapista")]
    public async Task<ActionResult<List<PazienteDelFisioterapistaDto>>> MieiPazienti()
    {
        var fisioterapista = await CaricaMeAsync();
        if (fisioterapista is null) return Forbid();

        var miei = await db.Appuntamenti
            .Where(a => a.FisioterapistaId == fisioterapista.Id)
            .Include(a => a.Paziente)
            .ToListAsync();

        var pazienteIds = miei.Select(a => a.PazienteId).Distinct().ToList();

        // Per decidere chi è "abituale" serve sapere anche quanti appuntamenti quei pazienti
        // hanno avuto con i colleghi: l'abituale è chi li ha visti più spesso, un valore
        // calcolato e mai memorizzato come legame fisso.
        var conteggiPerTerapista = await db.Appuntamenti
            .Where(a => pazienteIds.Contains(a.PazienteId))
            .GroupBy(a => new { a.PazienteId, a.FisioterapistaId })
            .Select(g => new { g.Key.PazienteId, g.Key.FisioterapistaId, Quanti = g.Count() })
            .ToListAsync();

        var risultato = pazienteIds.Select(pazienteId =>
        {
            var conMe = miei.Where(a => a.PazienteId == pazienteId).ToList();
            var paziente = conMe[0].Paziente;

            // "Abituale" richiede due cose, e servono entrambe per separare i due gruppi
            // come fa il mockup. Primo: averlo visto più di una volta — con un solo
            // appuntamento siamo per definizione nel caso "appuntamento singolo". Secondo:
            // averlo visto **più di ogni collega**, a parità non vale, altrimenti un paziente
            // visto una volta a testa risulterebbe abituale di entrambi.
            var perQuestoPaziente = conteggiPerTerapista.Where(c => c.PazienteId == pazienteId).ToList();
            var conAltri = perQuestoPaziente
                .Where(c => c.FisioterapistaId != fisioterapista.Id)
                .Select(c => c.Quanti)
                .DefaultIfEmpty(0)
                .Max();
            var abituale = conMe.Count > 1 && conMe.Count > conAltri;

            return new PazienteDelFisioterapistaDto(
                pazienteId, paziente.Nome, paziente.Cognome,
                abituale,
                conMe.Count,
                conMe.Max(a => (DateTime?)a.DataOra),
                conMe.OrderByDescending(a => a.DataOra)
                    .Select(a => $"{a.DataOra:dd/MM/yyyy HH:mm} — {a.Stato}")
                    .ToList());
        })
        .OrderByDescending(p => p.Abituale).ThenBy(p => p.Cognome)
        .ToList();

        return Ok(risultato);
    }

    private async Task<Fisioterapista?> CaricaMeAsync()
    {
        var utenteId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        return await db.Fisioterapisti.Include(f => f.Utente).FirstOrDefaultAsync(f => f.UtenteId == utenteId);
    }
}
