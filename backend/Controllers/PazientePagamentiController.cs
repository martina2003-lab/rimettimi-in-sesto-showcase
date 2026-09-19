using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RimettimiInSesto.Api.Data;
using RimettimiInSesto.Api.Dtos;
using RimettimiInSesto.Api.Services;

namespace RimettimiInSesto.Api.Controllers;

[ApiController]
[Route("api/pazienti/{pazienteId:int}")]
[Authorize(Roles = "Paziente")]
public class PazientePagamentiController(ApplicationDbContext db, PagamentoService pagamentoService)
    : ControllerBase
{
    [HttpGet("pagamenti")]
    public async Task<ActionResult<PagamentiPazienteResponse>> Pagamenti(int pazienteId)
    {
        if (!await EProprietarioAsync(pazienteId)) return Forbid();

        var paziente = await db.Pazienti.FindAsync(pazienteId);
        var nome = $"{paziente!.Nome} {paziente.Cognome}";

        var pagamenti = await db.Pagamenti
            .Where(p => p.PazienteId == pazienteId)
            .ToListAsync();
        var ricevute = await db.Ricevute
            .Where(r => pagamenti.Select(p => p.Id).Contains(r.PagamentoId))
            .ToListAsync();

        List<Dtos.PagamentoDto> ToDtoList(IEnumerable<Models.Pagamento> lista) => lista
            .Select(p => PagamentoService.ToDto(p, nome, ricevute.FirstOrDefault(r => r.PagamentoId == p.Id)))
            .ToList();

        var daSaldare = ToDtoList(pagamenti.Where(p => p.Stato == Models.StatoPagamento.DaSaldare));
        var storico = ToDtoList(pagamenti.Where(p => p.Stato != Models.StatoPagamento.DaSaldare)
            .OrderByDescending(p => p.DataIncasso));

        return Ok(new PagamentiPazienteResponse(daSaldare, storico));
    }

    // Il pacchetto privato — mai collassato con il ciclo SSN (principio guida di CLAUDE.md).
    [HttpPost("pacchetti")]
    public async Task<ActionResult> AcquistaPacchetto(int pazienteId, AcquistaPacchettoRequest request)
    {
        if (!await EProprietarioAsync(pazienteId)) return Forbid();

        var pacchetto = await pagamentoService.AcquistaPacchettoAsync(pazienteId, request);
        return Ok(new { pacchetto.Id, pacchetto.SeduteTotali, pacchetto.Scadenza });
    }

    [HttpPost("sedute-singole")]
    public async Task<ActionResult> AcquistaSedutaSingola(int pazienteId, AcquistaSedutaSingolaRequest request)
    {
        if (!await EProprietarioAsync(pazienteId)) return Forbid();

        var pagamento = await pagamentoService.AcquistaSedutaSingolaAsync(pazienteId, request);
        return Ok(new { pagamento.Id, pagamento.Importo, pagamento.Stato });
    }

    private async Task<bool> EProprietarioAsync(int pazienteId)
    {
        var utenteId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        return await db.UtentiPazienti.AnyAsync(up => up.UtenteId == utenteId && up.PazienteId == pazienteId);
    }
}
