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
[Route("api/ricette")]
[Authorize(Roles = "Coordinatrice")]
public class RicetteController(ApplicationDbContext db, RicettaService ricettaService) : ControllerBase
{
    // Coda Ricette SSN, ordinata per urgenza d'agenda (requisiti.md) — non per anzianità
    // del documento: solo le ricette ancora da lavorare (NonAncoraValidata / IntegrazioneRichiesta).
    [HttpGet]
    public async Task<ActionResult<List<RicettaDto>>> Coda()
    {
        return Ok(await ricettaService.CodaAsync());
    }

    // Il ticket da proporre quando la ricetta non ne porta uno proprio. Sta qui e non in
    // /api/admin/configurazione perché quella è vista dell'Admin, e la Coordinatrice —
    // l'unica che valida — non ci arriva: il listino esisteva già ma non precompilava
    // niente, e in validazione si vedeva uno zero da riscrivere ogni volta.
    [HttpGet("quota-ticket-predefinita")]
    public async Task<ActionResult> QuotaTicketPredefinita()
    {
        var listino = await db.ImpostazioniListino.FirstOrDefaultAsync() ?? new ImpostazioniListino();
        return Ok(new { importo = listino.QuotaTicketRegionale });
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<RicettaDto>> Dettaglio(int id)
    {
        var ricetta = await db.Ricette.Include(r => r.Paziente).Include(r => r.Appuntamenti)
            .FirstOrDefaultAsync(r => r.Id == id);
        if (ricetta is null) return NotFound();

        var appuntamentiBloccati = ricetta.Appuntamenti.Count(a =>
            a.Stato == Models.StatoAppuntamento.Richiesto || a.Stato == Models.StatoAppuntamento.Confermato);
        var soglia = (await db.ImpostazioniAgenda.FirstOrDefaultAsync())
            ?.SogliaPriorizzazioneRicettaInIntegrazioneGiorni ?? 20;
        var priorizzata = ricetta.Stato == Models.StatoRicetta.IntegrazioneRichiesta
            && ricetta.IntegrazioneRichiestaIl is { } dal && (DateTime.UtcNow - dal).TotalDays >= soglia;

        return Ok(RicettaService.ToDto(ricetta, appuntamentiBloccati, priorizzata));
    }

    [HttpPut("{id:int}/valida")]
    public async Task<ActionResult> Valida(int id, ValidaRicettaRequest request)
    {
        try
        {
            await ricettaService.ValidaAsync(id, request, NomeAutore());
            return Ok();
        }
        catch (RicettaValidationException ex)
        {
            return BadRequest(new { messaggio = ex.Message });
        }
    }

    [HttpPut("{id:int}/integrazione")]
    public async Task<ActionResult> Integrazione(int id, IntegrazioneRichiestaRequest request)
    {
        try
        {
            await ricettaService.RichiediIntegrazioneAsync(id, request.Motivo, NomeAutore());
            return Ok();
        }
        catch (RicettaValidationException ex)
        {
            return BadRequest(new { messaggio = ex.Message });
        }
    }

    [HttpPut("{id:int}/rifiuta")]
    public async Task<ActionResult> Rifiuta(int id, RespingiRicettaRequest request)
    {
        try
        {
            await ricettaService.RespingiAsync(id, request, NomeAutore());
            return Ok();
        }
        catch (RicettaValidationException ex)
        {
            return BadRequest(new { messaggio = ex.Message });
        }
    }

    private string NomeAutore() => User.FindFirstValue(ClaimTypes.GivenName) ?? "Coordinatrice";
}
