using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RimettimiInSesto.Api.Data;
using RimettimiInSesto.Api.Dtos;
using RimettimiInSesto.Api.Services;

namespace RimettimiInSesto.Api.Controllers;

[ApiController]
[Route("api/appuntamenti")]
[Authorize]
public class AppuntamentiController(
    ApplicationDbContext db, BookingService bookingService, PropostaSlotService propostaSlotService)
    : ControllerBase
{
    // --- Elenchi, uno per prospettiva ---------------------------------------------------

    // Tutti gli appuntamenti dei pazienti collegati all'utente: comprende quindi anche
    // quelli del figlio minore o del familiare a carico, non solo i propri.
    [HttpGet("miei")]
    [Authorize(Roles = "Paziente")]
    public async Task<ActionResult<List<AppuntamentoResponse>>> Miei()
    {
        var utenteId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var pazienteIds = await db.UtentiPazienti
            .Where(up => up.UtenteId == utenteId)
            .Select(up => up.PazienteId)
            .ToListAsync();

        return Ok(await ElencoAsync(a => pazienteIds.Contains(a.PazienteId)));
    }

    // L'agenda del fisioterapista che chiama: solo i suoi, mai quelli dei colleghi. Una
    // settimana alla volta, non tutto lo storico: senza un intervallo la lista cresce per
    // sempre e non c'è modo di guardare avanti o indietro (CLAUDE.md, "Agenda a settimane
    // navigabili").
    [HttpGet("agenda")]
    [Authorize(Roles = "Fisioterapista")]
    public async Task<ActionResult<List<AppuntamentoResponse>>> Agenda([FromQuery] DateOnly da, [FromQuery] DateOnly a)
    {
        var utenteId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var fisioterapista = await db.Fisioterapisti.FirstOrDefaultAsync(f => f.UtenteId == utenteId);
        if (fisioterapista is null) return Forbid();

        var inizio = da.ToDateTime(TimeOnly.MinValue);
        var fine = a.ToDateTime(TimeOnly.MaxValue);

        return Ok(await ElencoAsync(x =>
            x.FisioterapistaId == fisioterapista.Id && x.DataOra >= inizio && x.DataOra <= fine));
    }

    // La coda della Coordinatrice: le richieste ancora da decidere, più gli spostamenti
    // chiesti dai pazienti, che sono l'altra cosa che aspetta una sua decisione.
    [HttpGet("da-decidere")]
    [Authorize(Roles = "Coordinatrice")]
    public async Task<ActionResult<List<AppuntamentoResponse>>> DaDecidere()
    {
        return Ok(await ElencoAsync(a =>
            a.Stato == Models.StatoAppuntamento.Richiesto || a.ModificaRichiestaDataOra != null));
    }

    // La vista d'occhio della Coordinatrice: una settimana, un fisioterapista alla volta
    // (coordinatrice.html). Restano fuori i soli annullati: quell'orario è tornato libero
    // e disegnarlo occupato le farebbe evitare uno slot che può invece dare a qualcuno.
    // I no-show restano, perché il posto l'hanno occupato davvero.
    [HttpGet("calendario")]
    [Authorize(Roles = "Coordinatrice")]
    public async Task<ActionResult<List<AppuntamentoResponse>>> Calendario(
        [FromQuery] int fisioterapistaId, [FromQuery] DateOnly da, [FromQuery] DateOnly a)
    {
        var inizio = da.ToDateTime(TimeOnly.MinValue);
        var fine = a.AddDays(1).ToDateTime(TimeOnly.MinValue);

        return Ok(await ElencoAsync(ap =>
            ap.FisioterapistaId == fisioterapistaId
            && ap.DataOra >= inizio && ap.DataOra < fine
            && ap.Stato != Models.StatoAppuntamento.Annullato));
    }

    private async Task<List<AppuntamentoResponse>> ElencoAsync(
        System.Linq.Expressions.Expression<Func<Models.Appuntamento, bool>> filtro)
    {
        var appuntamenti = await db.Appuntamenti
            .Include(a => a.Paziente)
            .Include(a => a.Fisioterapista).ThenInclude(f => f.Utente)
            .Where(filtro)
            .OrderBy(a => a.DataOra)
            .ToListAsync();

        return appuntamenti.Select(ToResponse).ToList();
    }

    // Percorso Paziente: crea una richiesta, mai auto-confermata.
    [HttpPost("richieste")]
    [Authorize(Roles = "Paziente")]
    public async Task<ActionResult<AppuntamentoResponse>> Richiedi(CreaRichiestaRequest request)
    {
        var utenteId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        try
        {
            var appuntamento = await bookingService.RichiediAsync(utenteId, request);
            return Ok(await ToResponseAsync(appuntamento.Id));
        }
        catch (BookingValidationException ex)
        {
            return BadRequest(new { messaggio = ex.Message });
        }
    }

    // Percorso Coordinatrice: prenotazione telefonica/sportello, nasce già confermata —
    // eccezione dichiarata al principio "nessuna auto-conferma" (è lei stessa il confermatore).
    [HttpPost("prenota-diretto")]
    [Authorize(Roles = "Coordinatrice")]
    public async Task<ActionResult<AppuntamentoResponse>> PrenotaDiretto(CreaRichiestaRequest request)
    {
        try
        {
            var appuntamento = await bookingService.PrenotaDirettoAsync(request);
            return Ok(await ToResponseAsync(appuntamento.Id));
        }
        catch (BookingValidationException ex)
        {
            return BadRequest(new { messaggio = ex.Message });
        }
    }

    [HttpPut("{id:int}/conferma")]
    [Authorize(Roles = "Coordinatrice")]
    public async Task<ActionResult<AppuntamentoResponse>> Conferma(int id)
    {
        try
        {
            var appuntamento = await bookingService.ConfermaAsync(id);
            return Ok(await ToResponseAsync(appuntamento.Id));
        }
        catch (BookingValidationException ex)
        {
            return BadRequest(new { messaggio = ex.Message });
        }
    }

    [HttpPut("{id:int}/rifiuta")]
    [Authorize(Roles = "Coordinatrice")]
    public async Task<ActionResult<AppuntamentoResponse>> Rifiuta(int id, RifiutaRichiestaRequest request)
    {
        try
        {
            var appuntamento = await bookingService.RifiutaAsync(id, request.Motivo);
            return Ok(await ToResponseAsync(appuntamento.Id));
        }
        catch (BookingValidationException ex)
        {
            return BadRequest(new { messaggio = ex.Message });
        }
    }

    // --- Assenza del fisioterapista: cosa ne è degli appuntamenti già presi -----------

    [HttpPut("{id:int}/riassegna")]
    [Authorize(Roles = "Coordinatrice")]
    public async Task<ActionResult<AppuntamentoResponse>> Riassegna(int id, RiassegnaRequest request)
    {
        try
        {
            var appuntamento = await bookingService.RiassegnaAsync(id, request.FisioterapistaId);
            return Ok(await ToResponseAsync(appuntamento.Id));
        }
        catch (BookingValidationException ex)
        {
            return BadRequest(new { messaggio = ex.Message });
        }
    }

    [HttpPut("{id:int}/annulla-per-assenza")]
    [Authorize(Roles = "Coordinatrice")]
    public async Task<ActionResult<AppuntamentoResponse>> AnnullaPerAssenza(
        int id, AnnullaPerAssenzaRequest request)
    {
        try
        {
            var appuntamento = await bookingService.AnnullaPerAssenzaAsync(
                id,
                request.Motivo ?? string.Empty,
                request.NotificaGiaDataAltrove);
            return Ok(await ToResponseAsync(appuntamento.Id));
        }
        catch (BookingValidationException ex)
        {
            return BadRequest(new { messaggio = ex.Message });
        }
    }

    // --- Modifica e cancellazione lato Paziente --------------------------------------

    [HttpPut("{id:int}/richiedi-modifica")]
    [Authorize(Roles = "Paziente")]
    public async Task<ActionResult<AppuntamentoResponse>> RichiediModifica(int id, RichiediModificaRequest request)
    {
        var utenteId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        try
        {
            var appuntamento = await bookingService.RichiediModificaAsync(utenteId, id, request.NuovaDataOra);
            return Ok(await ToResponseAsync(appuntamento.Id));
        }
        catch (BookingValidationException ex)
        {
            return BadRequest(new { messaggio = ex.Message });
        }
    }

    [HttpPut("{id:int}/cancella")]
    [Authorize(Roles = "Paziente")]
    public async Task<ActionResult<AppuntamentoResponse>> Cancella(int id)
    {
        var utenteId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        try
        {
            var appuntamento = await bookingService.CancellaDaPazienteAsync(utenteId, id);
            return Ok(await ToResponseAsync(appuntamento.Id));
        }
        catch (BookingValidationException ex)
        {
            return BadRequest(new { messaggio = ex.Message });
        }
    }

    [HttpPut("{id:int}/approva-modifica")]
    [Authorize(Roles = "Coordinatrice")]
    public async Task<ActionResult<AppuntamentoResponse>> ApprovaModifica(int id)
    {
        try
        {
            var appuntamento = await bookingService.ApprovaModificaAsync(id);
            return Ok(await ToResponseAsync(appuntamento.Id));
        }
        catch (BookingValidationException ex)
        {
            return BadRequest(new { messaggio = ex.Message });
        }
    }

    [HttpPut("{id:int}/rifiuta-modifica")]
    [Authorize(Roles = "Coordinatrice")]
    public async Task<ActionResult<AppuntamentoResponse>> RifiutaModifica(int id)
    {
        try
        {
            var appuntamento = await bookingService.RifiutaModificaAsync(id);
            return Ok(await ToResponseAsync(appuntamento.Id));
        }
        catch (BookingValidationException ex)
        {
            return BadRequest(new { messaggio = ex.Message });
        }
    }

    // --- Chiusura della seduta lato Fisioterapista ------------------------------------

    [HttpPut("{id:int}/completa")]
    [Authorize(Roles = "Fisioterapista")]
    public async Task<ActionResult<AppuntamentoResponse>> Completa(int id)
    {
        try
        {
            var appuntamento = await bookingService.CompletaSedutaAsync(id);
            return Ok(await ToResponseAsync(appuntamento.Id));
        }
        catch (BookingValidationException ex)
        {
            return BadRequest(new { messaggio = ex.Message });
        }
    }

    [HttpPut("{id:int}/no-show")]
    [Authorize(Roles = "Fisioterapista")]
    public async Task<ActionResult<AppuntamentoResponse>> NoShow(int id)
    {
        try
        {
            var appuntamento = await bookingService.RegistraNoShowAsync(id);
            return Ok(await ToResponseAsync(appuntamento.Id));
        }
        catch (BookingValidationException ex)
        {
            return BadRequest(new { messaggio = ex.Message });
        }
    }

    // --- Proposta di slot alternativo --------------------------------------------------

    [HttpPost("{id:int}/proposte-slot")]
    [Authorize(Roles = "Coordinatrice")]
    public async Task<ActionResult> CreaProposta(int id, CreaPropostaSlotRequest request)
    {
        try
        {
            var proposta = await propostaSlotService.CreaAsync(id, request.SlotProposti);
            return Ok(new { proposta.Id, proposta.Scadenza, proposta.SlotProposti });
        }
        catch (BookingValidationException ex)
        {
            return BadRequest(new { messaggio = ex.Message });
        }
    }

    [HttpPut("proposte-slot/{propostaId:int}/accetta")]
    [Authorize(Roles = "Paziente")]
    public async Task<ActionResult> AccettaProposta(int propostaId, AccettaPropostaRequest request)
    {
        var utenteId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        try
        {
            var proposta = await propostaSlotService.AccettaAsync(utenteId, propostaId, request.SlotScelto);
            return Ok(await ToResponseAsync(proposta.AppuntamentoId));
        }
        catch (BookingValidationException ex)
        {
            return BadRequest(new { messaggio = ex.Message });
        }
    }

    [HttpPut("proposte-slot/{propostaId:int}/rifiuta")]
    [Authorize(Roles = "Paziente")]
    public async Task<ActionResult> RifiutaProposta(int propostaId)
    {
        var utenteId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        try
        {
            var proposta = await propostaSlotService.RifiutaAsync(utenteId, propostaId);
            return Ok(await ToResponseAsync(proposta.AppuntamentoId));
        }
        catch (BookingValidationException ex)
        {
            return BadRequest(new { messaggio = ex.Message });
        }
    }

    private async Task<AppuntamentoResponse> ToResponseAsync(int appuntamentoId)
    {
        var a = await db.Appuntamenti
            .Include(x => x.Paziente)
            .Include(x => x.Fisioterapista).ThenInclude(f => f.Utente)
            .FirstAsync(x => x.Id == appuntamentoId);

        return ToResponse(a);
    }

    private static AppuntamentoResponse ToResponse(Models.Appuntamento a) => new(
        a.Id, a.PazienteId, $"{a.Paziente.Nome} {a.Paziente.Cognome}",
        a.FisioterapistaId, $"{a.Fisioterapista.Utente.Nome} {a.Fisioterapista.Utente.Cognome}",
        a.DataOra, a.DurataMinuti, a.Stato.ToString(), a.Percorso.ToString(),
        a.AcquistoPacchettoId, a.RicettaId, a.MotivoAnnullamento, a.ModificaRichiestaDataOra);
}
