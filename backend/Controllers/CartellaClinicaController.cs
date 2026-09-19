using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RimettimiInSesto.Api.Data;
using RimettimiInSesto.Api.Dtos;
using RimettimiInSesto.Api.Models;
using RimettimiInSesto.Api.Services;

namespace RimettimiInSesto.Api.Controllers;

// Solo il fisioterapista accede alla cartella clinica (principio guida di CLAUDE.md):
// la Coordinatrice non la vede mai, nemmeno in parte. Ogni azione qui passa da
// ClinicalAccessService PRIMA di leggere o scrivere — mai un controllo "a vista" in UI,
// e ogni accesso concesso finisce nell'audit log, non è opzionale.
[ApiController]
[Route("api")]
[Authorize(Roles = "Fisioterapista")]
public class CartellaClinicaController(ApplicationDbContext db, ClinicalAccessService clinicalAccess)
    : ControllerBase
{
    [HttpGet("pazienti/{pazienteId:int}/cartella-clinica")]
    public async Task<ActionResult<List<CartellaClinicaDto>>> Cicli(int pazienteId)
    {
        var autorizzazione = await AutorizzaAsync(pazienteId, TipoAccessoClinico.Lettura);
        if (autorizzazione is not null) return autorizzazione;

        var cicli = await db.CartelleCliniche
            .Where(c => c.PazienteId == pazienteId)
            .OrderByDescending(c => c.DataInizioTerapia)
            .Select(c => ToDto(c))
            .ToListAsync();

        return Ok(cicli);
    }

    [HttpPost("pazienti/{pazienteId:int}/cartella-clinica")]
    public async Task<ActionResult<CartellaClinicaDto>> ApriCiclo(int pazienteId, SalvaCicloRequest request)
    {
        var autorizzazione = await AutorizzaAsync(pazienteId, TipoAccessoClinico.Scrittura);
        if (autorizzazione is not null) return autorizzazione;

        var ciclo = new CartellaClinica { PazienteId = pazienteId };
        ApplicaRequest(ciclo, request);
        db.CartelleCliniche.Add(ciclo);
        await db.SaveChangesAsync();

        return Ok(ToDto(ciclo));
    }

    [HttpPut("pazienti/{pazienteId:int}/cartella-clinica/{cicloId:int}")]
    public async Task<ActionResult<CartellaClinicaDto>> AggiornaCiclo(int pazienteId, int cicloId, SalvaCicloRequest request)
    {
        var autorizzazione = await AutorizzaAsync(pazienteId, TipoAccessoClinico.Scrittura);
        if (autorizzazione is not null) return autorizzazione;

        var ciclo = await db.CartelleCliniche.FirstOrDefaultAsync(c => c.Id == cicloId && c.PazienteId == pazienteId);
        if (ciclo is null) return NotFound();

        ApplicaRequest(ciclo, request);
        await db.SaveChangesAsync();

        return Ok(ToDto(ciclo));
    }

    [HttpGet("pazienti/{pazienteId:int}/controindicazioni")]
    public async Task<ActionResult<ControindicazioniDto?>> Controindicazioni(int pazienteId)
    {
        var autorizzazione = await AutorizzaAsync(pazienteId, TipoAccessoClinico.Lettura);
        if (autorizzazione is not null) return autorizzazione;

        var c = await db.Controindicazioni.FirstOrDefaultAsync(x => x.PazienteId == pazienteId);
        if (c is null) return Ok(null);

        // Dato consultabile, SENZA alcun alert automatico (scelta deliberata, requisiti.md) —
        // il fisioterapista lo legge, il sistema non lo giudica per lui.
        return Ok(new ControindicazioniDto(
            c.Pacemaker, c.Gravidanza, c.NeoplasiaInAttoOPregressa, c.Epilessia,
            c.LesioniCutaneeOFratture, c.StatoInfiammatorioAcuto, c.DisturbiCardiocircolatori,
            c.MezziDiSintesiOProtesi, c.GraveOsteoporosi, c.TendenzaEmorragie,
            c.ProtesiAcustiche, c.AllergiaFans, c.InterventiChirurgici, c.TerapieFarmacologicheInAtto));
    }

    [HttpPut("pazienti/{pazienteId:int}/controindicazioni")]
    public async Task<ActionResult<ControindicazioniDto>> SalvaControindicazioni(int pazienteId, ControindicazioniDto request)
    {
        var autorizzazione = await AutorizzaAsync(pazienteId, TipoAccessoClinico.Scrittura);
        if (autorizzazione is not null) return autorizzazione;

        var c = await db.Controindicazioni.FirstOrDefaultAsync(x => x.PazienteId == pazienteId);
        if (c is null)
        {
            c = new Controindicazioni { PazienteId = pazienteId };
            db.Controindicazioni.Add(c);
        }

        c.Pacemaker = request.Pacemaker;
        c.Gravidanza = request.Gravidanza;
        c.NeoplasiaInAttoOPregressa = request.NeoplasiaInAttoOPregressa;
        c.Epilessia = request.Epilessia;
        c.LesioniCutaneeOFratture = request.LesioniCutaneeOFratture;
        c.StatoInfiammatorioAcuto = request.StatoInfiammatorioAcuto;
        c.DisturbiCardiocircolatori = request.DisturbiCardiocircolatori;
        c.MezziDiSintesiOProtesi = request.MezziDiSintesiOProtesi;
        c.GraveOsteoporosi = request.GraveOsteoporosi;
        c.TendenzaEmorragie = request.TendenzaEmorragie;
        c.ProtesiAcustiche = request.ProtesiAcustiche;
        c.AllergiaFans = request.AllergiaFans;
        c.InterventiChirurgici = request.InterventiChirurgici;
        c.TerapieFarmacologicheInAtto = request.TerapieFarmacologicheInAtto;

        await db.SaveChangesAsync();
        return Ok(request);
    }

    // Unico elemento per-singola-seduta della cartella (tutto il resto è per ciclo).
    // L'accesso è giustificato direttamente dall'appuntamento indicato: se il fisioterapista
    // che chiama non è quello assegnato a QUESTO appuntamento, ClinicalAccessService lo rifiuta
    // comunque (a meno che non abbia avuto ALTRI appuntamenti con lo stesso paziente).
    [HttpPost("appuntamenti/{appuntamentoId:int}/riepilogo-seduta")]
    public async Task<ActionResult> RegistraRiepilogoSeduta(int appuntamentoId, RiepilogoSedutaRequest request)
    {
        var appuntamento = await db.Appuntamenti.FindAsync(appuntamentoId);
        if (appuntamento is null) return NotFound();

        var autorizzazione = await AutorizzaAsync(appuntamento.PazienteId, TipoAccessoClinico.Scrittura);
        if (autorizzazione is not null) return autorizzazione;

        var nota = await db.NoteSeduta.FirstOrDefaultAsync(n => n.AppuntamentoId == appuntamentoId);
        if (nota is null)
        {
            nota = new NotaSeduta { AppuntamentoId = appuntamentoId };
            db.NoteSeduta.Add(nota);
        }
        nota.Testo = request.Testo;
        nota.RegistrataIl = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return Ok();
    }

    private async Task<ActionResult?> AutorizzaAsync(int pazienteId, TipoAccessoClinico tipo)
    {
        var utenteId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var risultato = await clinicalAccess.AutorizzaAsync(utenteId, pazienteId, tipo);
        if (!risultato.Autorizzato)
        {
            return Forbid();
        }
        return null;
    }

    private static void ApplicaRequest(CartellaClinica ciclo, SalvaCicloRequest request)
    {
        ciclo.Provenienza = request.Provenienza;
        ciclo.AnamnesiPatologicaRemota = request.AnamnesiPatologicaRemota;
        ciclo.EsameObiettivo = request.EsameObiettivo;
        ciclo.EsamiSpecialistici = request.EsamiSpecialistici;
        ciclo.Diagnosi = request.Diagnosi;
        ciclo.ProgrammaRiabilitativo = request.ProgrammaRiabilitativo;
        ciclo.IndicazioniPaziente = request.IndicazioniPaziente;
        ciclo.NoteSostituzione = request.NoteSostituzione;
        ciclo.VasIniziale = request.VasIniziale;
        ciclo.VasFinale = request.VasFinale;
        ciclo.DataInizioTerapia = request.DataInizioTerapia;
        ciclo.DataFineTerapia = request.DataFineTerapia;
        ciclo.FirmaFisioterapista = request.FirmaFisioterapista;
        ciclo.FirmaMedicoResponsabile = request.FirmaMedicoResponsabile;
    }

    private static CartellaClinicaDto ToDto(CartellaClinica c) => new(
        c.Id, c.PazienteId, c.Provenienza, c.AnamnesiPatologicaRemota, c.EsameObiettivo,
        c.EsamiSpecialistici, c.Diagnosi, c.ProgrammaRiabilitativo, c.IndicazioniPaziente,
        c.NoteSostituzione, c.VasIniziale, c.VasFinale, c.DataInizioTerapia, c.DataFineTerapia,
        c.FirmaFisioterapista, c.FirmaMedicoResponsabile);
}
