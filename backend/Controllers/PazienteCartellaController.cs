using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RimettimiInSesto.Api.Data;
using RimettimiInSesto.Api.Dtos;

namespace RimettimiInSesto.Api.Controllers;

// Vista del Paziente sui propri dati clinici — regola di accesso DIVERSA da quella del
// fisioterapista: qui non serve ClinicalAccessService (quello autorizza un fisioterapista
// a vedere il paziente di qualcun altro), basta verificare che l'utente che chiama sia
// collegato a questa scheda Paziente (UtentiPazienti) — copre anche chi prenota per un
// figlio minore o un familiare a carico.
[ApiController]
[Route("api/pazienti/{pazienteId:int}")]
[Authorize(Roles = "Paziente")]
public class PazienteCartellaController(ApplicationDbContext db) : ControllerBase
{
    // Vista di default: diagnosi + resoconto sedute, MAI le note tecniche interne del
    // fisioterapista (anamnesi, esame obiettivo) — requisiti.md, "Cartella clinica".
    [HttpGet("riepilogo")]
    public async Task<ActionResult<RiepilogoPazienteResponse>> Riepilogo(int pazienteId)
    {
        if (!await EProprietarioAsync(pazienteId)) return Forbid();

        var cicloAttivo = await db.CartelleCliniche
            .Where(c => c.PazienteId == pazienteId)
            .OrderByDescending(c => c.DataInizioTerapia)
            .FirstOrDefaultAsync();

        var sedute = await db.NoteSeduta
            .Where(n => n.Appuntamento.PazienteId == pazienteId)
            .Include(n => n.Appuntamento).ThenInclude(a => a.Fisioterapista).ThenInclude(f => f.Utente)
            .OrderByDescending(n => n.Appuntamento.DataOra)
            .Select(n => new RiepilogoSedutaVoce(
                n.Appuntamento.DataOra,
                n.Appuntamento.Fisioterapista.Utente.Nome + " " + n.Appuntamento.Fisioterapista.Utente.Cognome,
                n.Testo))
            .ToListAsync();

        return Ok(new RiepilogoPazienteResponse(cicloAttivo?.Diagnosi, cicloAttivo?.ProgrammaRiabilitativo, sedute));
    }

    // Azione esplicita "Richiedi copia completa della cartella clinica" — diritto di accesso
    // GDPR art. 15, anche alle note integrali (requisiti.md).
    [HttpGet("cartella-completa")]
    public async Task<ActionResult<CartellaCompletaResponse>> CartellaCompleta(int pazienteId)
    {
        if (!await EProprietarioAsync(pazienteId)) return Forbid();

        var cicli = await db.CartelleCliniche
            .Where(c => c.PazienteId == pazienteId)
            .OrderByDescending(c => c.DataInizioTerapia)
            .Select(c => new CartellaClinicaDto(
                c.Id, c.PazienteId, c.Provenienza, c.AnamnesiPatologicaRemota, c.EsameObiettivo,
                c.EsamiSpecialistici, c.Diagnosi, c.ProgrammaRiabilitativo, c.IndicazioniPaziente,
                c.NoteSostituzione, c.VasIniziale, c.VasFinale, c.DataInizioTerapia, c.DataFineTerapia,
                c.FirmaFisioterapista, c.FirmaMedicoResponsabile))
            .ToListAsync();

        var sedute = await db.NoteSeduta
            .Where(n => n.Appuntamento.PazienteId == pazienteId)
            .Include(n => n.Appuntamento).ThenInclude(a => a.Fisioterapista).ThenInclude(f => f.Utente)
            .OrderByDescending(n => n.Appuntamento.DataOra)
            .Select(n => new RiepilogoSedutaVoce(
                n.Appuntamento.DataOra,
                n.Appuntamento.Fisioterapista.Utente.Nome + " " + n.Appuntamento.Fisioterapista.Utente.Cognome,
                n.Testo))
            .ToListAsync();

        return Ok(new CartellaCompletaResponse(cicli, sedute));
    }

    private async Task<bool> EProprietarioAsync(int pazienteId)
    {
        var utenteId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        return await db.UtentiPazienti.AnyAsync(up => up.UtenteId == utenteId && up.PazienteId == pazienteId);
    }
}
