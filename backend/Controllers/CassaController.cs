using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RimettimiInSesto.Api.Data;
using RimettimiInSesto.Api.Dtos;
using RimettimiInSesto.Api.Models;
using RimettimiInSesto.Api.Services;

namespace RimettimiInSesto.Api.Controllers;

[ApiController]
[Route("api/cassa")]
[Authorize(Roles = "Coordinatrice")]
public class CassaController(ApplicationDbContext db, PagamentoService pagamentoService) : ControllerBase
{
    // Da riscuotere: l'arretrato, ordinato per anzianità — le voci più vecchie prima
    // (requisiti.md, "Cassa").
    [HttpGet("da-riscuotere")]
    public async Task<ActionResult<List<PagamentoDto>>> DaRiscuotere()
    {
        var pagamenti = await db.Pagamenti
            .Include(p => p.Paziente)
            .Where(p => p.Stato == StatoPagamento.DaSaldare)
            .OrderBy(p => p.Id) // proxy di anzianità: nessuna data di creazione tracciata a parte
            .ToListAsync();

        return Ok(pagamenti.Select(p => PagamentoService.ToDto(p, $"{p.Paziente.Nome} {p.Paziente.Cognome}", null)).ToList());
    }

    // Giornata: pagamenti incassati in una data, con la propria origine tracciata.
    [HttpGet("giornata")]
    public async Task<ActionResult<List<PagamentoDto>>> Giornata([FromQuery] DateOnly data)
    {
        var inizio = data.ToDateTime(TimeOnly.MinValue);
        var fine = inizio.AddDays(1);

        var pagamenti = await db.Pagamenti
            .Include(p => p.Paziente)
            .Where(p => p.DataIncasso >= inizio && p.DataIncasso < fine)
            .ToListAsync();
        var ricevute = await db.Ricevute
            .Where(r => pagamenti.Select(p => p.Id).Contains(r.PagamentoId))
            .ToListAsync();

        return Ok(pagamenti.Select(p => PagamentoService.ToDto(
            p, $"{p.Paziente.Nome} {p.Paziente.Cognome}", ricevute.FirstOrDefault(r => r.PagamentoId == p.Id))).ToList());
    }

    [HttpGet("ricevute")]
    public async Task<ActionResult<List<PagamentoDto>>> Ricevute()
    {
        var ricevute = await db.Ricevute
            .Include(r => r.Pagamento).ThenInclude(p => p.Paziente)
            .OrderByDescending(r => r.Anno).ThenByDescending(r => r.NumeroProgressivo)
            .ToListAsync();

        return Ok(ricevute.Select(r => PagamentoService.ToDto(
            r.Pagamento, $"{r.Pagamento.Paziente.Nome} {r.Pagamento.Paziente.Cognome}", r)).ToList());
    }

    [HttpPut("pagamenti/{id:int}/incassa")]
    public async Task<ActionResult> Incassa(int id, IncassaRequest request)
    {
        try
        {
            var ricevuta = await pagamentoService.IncassaAsync(id, request);
            return Ok(new { ricevuta.NumeroProgressivo, ricevuta.Anno });
        }
        catch (PagamentoValidationException ex)
        {
            return BadRequest(new { messaggio = ex.Message });
        }
    }

    [HttpPut("pagamenti/{id:int}/storna")]
    public async Task<ActionResult> Storna(int id)
    {
        try
        {
            await pagamentoService.StornaAsync(id);
            return Ok();
        }
        catch (PagamentoValidationException ex)
        {
            return BadRequest(new { messaggio = ex.Message });
        }
    }

    // Nessuna integrazione diretta col Sistema TS (chiuso il 12/09/2026, vedi requisiti.md):
    // il sistema produce solo il file da caricare altrove, l'invio resta un adempimento
    // operativo dello studio. "InviataSistemaTs" si marca a mano dopo il caricamento reale,
    // non automaticamente qui — l'export non equivale all'invio.
    [HttpGet("export-sistema-ts")]
    public async Task<IActionResult> EsportaSistemaTs([FromQuery] int anno)
    {
        var righe = await db.Ricevute
            .Include(r => r.Pagamento).ThenInclude(p => p.Paziente)
            .Where(r => r.Anno == anno && !r.Stornata && !r.OpposizioneSistemaTs)
            .OrderBy(r => r.NumeroProgressivo)
            .ToListAsync();

        var csv = new System.Text.StringBuilder();
        csv.AppendLine("NumeroRicevuta;Anno;CodiceFiscalePaziente;Paziente;Importo;DataIncasso");
        foreach (var r in righe)
        {
            csv.AppendLine(string.Join(';',
                r.NumeroProgressivo, r.Anno, r.Pagamento.Paziente.CodiceFiscale ?? "",
                $"{r.Pagamento.Paziente.Nome} {r.Pagamento.Paziente.Cognome}",
                r.Importo.ToString("F2"), r.Pagamento.DataIncasso?.ToString("yyyy-MM-dd") ?? ""));
        }

        var bytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());
        return File(bytes, "text/csv", $"sistema-ts-{anno}.csv");
    }

    [HttpPut("ricevute/{id:int}/segna-inviata-ts")]
    public async Task<ActionResult> SegnaInviataTs(int id)
    {
        var ricevuta = await db.Ricevute.FindAsync(id);
        if (ricevuta is null) return NotFound();

        ricevuta.InviataSistemaTs = true;
        await db.SaveChangesAsync();
        return Ok();
    }
}
