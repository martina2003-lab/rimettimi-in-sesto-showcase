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
[Route("api/pazienti")]
[Authorize]
public class PazientiController(ApplicationDbContext db, RicettaService ricettaService) : ControllerBase
{
    // Le schede collegate all'account: un solo login può gestire sé stesso, un figlio
    // minore, un genitore anziano (requisiti.md). Alimenta lo switcher familiare.
    [HttpGet("miei")]
    [Authorize(Roles = "Paziente")]
    public async Task<ActionResult<List<PazienteCollegatoDto>>> Miei()
    {
        var utenteId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        var collegati = await db.UtentiPazienti
            .Where(up => up.UtenteId == utenteId)
            .Include(up => up.Paziente)
            .Select(up => new PazienteCollegatoDto(
                up.PazienteId, up.Paziente.Nome, up.Paziente.Cognome, up.Titolo.ToString()))
            .ToListAsync();

        return Ok(collegati);
    }

    // Da cosa si può scalare la prossima seduta. I due percorsi restano distinti:
    // il pacchetto privato lo compra il paziente, il ciclo SSN nasce da una ricetta
    // validata e porta con sé durata e finestra di completamento.
    // Vale anche per la Coordinatrice, che prenotando al telefono deve poter scalare la
    // seduta dal pacchetto o dal ciclo giusto: senza, la prenotazione telefonica creerebbe
    // sempre un appuntamento slegato da ogni percorso. Il paziente resta vincolato alle
    // proprie schede; lei no, perché prenota per chiunque chiami.
    [HttpGet("{pazienteId:int}/percorsi-attivi")]
    [Authorize(Roles = "Paziente,Coordinatrice")]
    public async Task<ActionResult<PercorsiAttiviResponse>> PercorsiAttivi(int pazienteId)
    {
        if (User.IsInRole("Paziente") && !await EProprietarioAsync(pazienteId)) return Forbid();

        var oggi = DateOnly.FromDateTime(DateTime.Today);

        var pacchetti = await db.AcquistiPacchetto
            .Where(p => p.PazienteId == pazienteId
                && p.Tipo == TipoPacchetto.Privato
                && p.SeduteResidue > 0
                && (p.Scadenza == null || p.Scadenza >= oggi))
            .Select(p => new PacchettoAttivoDto(p.Id, p.SeduteTotali, p.SeduteResidue, p.Scadenza, p.Prezzo))
            .ToListAsync();

        // Solo le ricette validate: una non ancora validata non apre un ciclo, quindi non
        // c'è nulla da cui scalare. Prenotare un SSN senza ciclo aperto resta possibile
        // (la ricetta arriva spesso alla prima seduta), ma passa dalla segreteria.
        var ricette = await db.Ricette
            .Where(r => r.PazienteId == pazienteId && r.Stato == StatoRicetta.Validata)
            .Select(r => new RicettaAttivaDto(
                r.Id, r.NumeroONre, r.DistrettiCorporei, r.NumeroSeduteProscritte,
                r.FinestraCompletamento,
                r.CicloGenerato != null ? r.CicloGenerato.Id : null,
                r.CicloGenerato != null ? r.CicloGenerato.SeduteResidue : null))
            .ToListAsync();

        return Ok(new PercorsiAttiviResponse(pacchetti, ricette));
    }

    // Tutte le ricette del paziente, qualunque stato — non solo le validate come in
    // percorsi-attivi. Serve al wizard di prenotazione per sapere se mostrare il modulo di
    // caricamento o lo stato "già inviata, in attesa di validazione": senza, riaprendo il
    // wizard prima che la segreteria decida, il paziente si ritroverebbe a ricompilarlo da
    // capo ogni volta. Riusa DiPazienteAsync, già scritto per la scheda della Coordinatrice.
    [HttpGet("{pazienteId:int}/ricette")]
    [Authorize(Roles = "Paziente,Coordinatrice")]
    public async Task<ActionResult<List<RicettaDto>>> RicetteDiPaziente(int pazienteId)
    {
        if (User.IsInRole("Paziente") && !await EProprietarioAsync(pazienteId)) return Forbid();

        return Ok(await ricettaService.DiPazienteAsync(pazienteId));
    }

    // Il paziente carica online i dati/foto della propria ricetta (requisiti.md): nasce
    // "NonAncoraValidata", esattamente come una ricetta portata in studio — la valida solo
    // la Coordinatrice, da qui in poi la ricetta segue lo stesso percorso di tutte le altre.
    [HttpPost("{pazienteId:int}/ricette")]
    [Authorize(Roles = "Paziente")]
    public async Task<ActionResult<RicettaDto>> CaricaRicetta(int pazienteId, CaricaRicettaRequest request)
    {
        if (!await EProprietarioAsync(pazienteId)) return Forbid();

        var ricetta = new Ricetta
        {
            PazienteId = pazienteId,
            NumeroONre = request.NumeroONre,
            DistrettiCorporei = request.DistrettiCorporei,
            DataEmissione = DateOnly.FromDateTime(DateTime.Today),
            DocumentoCaricato = request.DocumentoCaricato,
            Stato = StatoRicetta.NonAncoraValidata,
        };
        db.Ricette.Add(ricetta);
        await db.SaveChangesAsync();

        var creata = (await ricettaService.DiPazienteAsync(pazienteId)).First(r => r.Id == ricetta.Id);
        return Ok(creata);
    }

    // --- Prenotazione telefonica/sportello (Coordinatrice) -----------------------------

    // Si cerca per nome, codice fiscale o telefono, come nel mockup: al telefono si ha in
    // mano uno qualsiasi dei tre. Senza `q` torna le schede più recenti, così la finestra
    // non parte vuota.
    [HttpGet("cerca")]
    [Authorize(Roles = "Coordinatrice")]
    public async Task<ActionResult<List<PazienteRicercaDto>>> Cerca([FromQuery] string? q)
    {
        var query = db.Pazienti.AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var termine = q.Trim();
            query = query.Where(p =>
                (p.Nome + " " + p.Cognome).Contains(termine)
                || (p.CodiceFiscale != null && p.CodiceFiscale.Contains(termine))
                || (p.Telefono != null && p.Telefono.Contains(termine)));
        }

        var risultati = await query
            .OrderBy(p => p.Cognome).ThenBy(p => p.Nome)
            .Take(25)
            .Select(p => new PazienteRicercaDto(
                p.Id, p.Nome, p.Cognome, p.CodiceFiscale, p.Telefono, p.Stato.ToString()))
            .ToListAsync();

        return Ok(risultati);
    }

    // Scheda minima per chi non ha mai messo piede nel portale: serve solo a bloccare lo
    // slot. Nasce "Provvisorio" perché il consenso privacy/dati sanitari non si raccoglie
    // per telefono — non ha forma verificabile — ma di persona alla prima seduta, prima
    // che il fisioterapista scriva qualsiasi dato clinico (requisiti.md).
    [HttpPost("provvisorio")]
    [Authorize(Roles = "Coordinatrice")]
    public async Task<ActionResult<PazienteRicercaDto>> CreaProvvisorio(CreaSchedaProvvisoriaRequest request)
    {
        var nome = request.Nome?.Trim() ?? string.Empty;
        var cognome = request.Cognome?.Trim() ?? string.Empty;
        var telefono = request.Telefono?.Trim() ?? string.Empty;

        if (nome.Length == 0 || cognome.Length == 0 || telefono.Length == 0)
        {
            return BadRequest(new { messaggio = "Nome, cognome e telefono sono obbligatori." });
        }

        var codiceFiscale = string.IsNullOrWhiteSpace(request.CodiceFiscale)
            ? null
            : request.CodiceFiscale.Trim().ToUpperInvariant();

        // Il codice fiscale è la chiave con cui una scheda viene poi collegata all'account
        // che il paziente si crea da sé: due schede con lo stesso CF renderebbero ambiguo
        // quel collegamento, che è esattamente il duplicato che requisiti.md vuole evitare.
        if (codiceFiscale is not null)
        {
            var esistente = await db.Pazienti.FirstOrDefaultAsync(p => p.CodiceFiscale == codiceFiscale);
            if (esistente is not null)
            {
                return BadRequest(new
                {
                    messaggio = $"Esiste già una scheda con questo codice fiscale: {esistente.Nome} {esistente.Cognome}. Cercala invece di crearne una nuova.",
                });
            }
        }

        var paziente = new Paziente
        {
            Nome = nome,
            Cognome = cognome,
            Telefono = telefono,
            CodiceFiscale = codiceFiscale,
            Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
            Stato = StatoPaziente.Provvisorio,
        };

        db.Pazienti.Add(paziente);
        db.NoteOperative.Add(new NotaOperativa
        {
            Paziente = paziente,
            Testo = "Scheda creata al telefono per bloccare lo slot. Consenso privacy da raccogliere alla prima seduta.",
            Autore = AutoreCorrente(),
            Data = DateTime.Now,
        });
        await db.SaveChangesAsync();

        return Ok(new PazienteRicercaDto(
            paziente.Id, paziente.Nome, paziente.Cognome,
            paziente.CodiceFiscale, paziente.Telefono, paziente.Stato.ToString()));
    }

    // --- Scheda paziente (Coordinatrice) -----------------------------------------------

    // Tutto quel che serve alla segreteria su una persona, in una risposta sola perché è
    // una sola schermata a tab. NIENTE di clinico: nessun ciclo di cartella, nessuna
    // valutazione, nessuna nota di seduta — la Coordinatrice non ha accesso alla cartella
    // e questo endpoint non è la scorciatoia per aggirarlo.
    [HttpGet("{pazienteId:int}/scheda")]
    [Authorize(Roles = "Coordinatrice")]
    public async Task<ActionResult<SchedaPazienteResponse>> Scheda(int pazienteId)
    {
        var paziente = await db.Pazienti.FirstOrDefaultAsync(p => p.Id == pazienteId);
        if (paziente is null) return NotFound();

        var pacchetti = await db.AcquistiPacchetto
            .Where(p => p.PazienteId == pazienteId && p.Tipo == TipoPacchetto.Privato)
            .OrderByDescending(p => p.DataAcquisto)
            .Select(p => new PacchettoAttivoDto(p.Id, p.SeduteTotali, p.SeduteResidue, p.Scadenza, p.Prezzo))
            .ToListAsync();

        // Tutte le ricette, non solo quelle validate: qui serve proprio vedere in che stato
        // sono — una mancante o da integrare è l'informazione che fa agire la segreteria.
        var ricette = await ricettaService.DiPazienteAsync(pazienteId);

        var storico = await db.Appuntamenti
            .Include(a => a.Fisioterapista).ThenInclude(f => f.Utente)
            .Where(a => a.PazienteId == pazienteId)
            .OrderByDescending(a => a.DataOra)
            .Select(a => new VoceStoricoDto(
                a.DataOra,
                a.Fisioterapista.Utente.Nome + " " + a.Fisioterapista.Utente.Cognome,
                a.DurataMinuti,
                a.Stato.ToString(),
                a.Percorso.ToString()))
            .ToListAsync();

        var pagamenti = await db.Pagamenti
            .Where(p => p.PazienteId == pazienteId)
            .OrderByDescending(p => p.Id)
            .ToListAsync();
        var idPagamenti = pagamenti.Select(p => p.Id).ToList();
        var ricevute = await db.Ricevute
            .Where(r => idPagamenti.Contains(r.PagamentoId))
            .ToListAsync();

        var note = await db.NoteOperative
            .Where(n => n.PazienteId == pazienteId)
            .OrderByDescending(n => n.Data)
            .Select(n => new NotaOperativaDto(n.Id, n.Testo, n.Autore, n.Data))
            .ToListAsync();

        var nomeCompleto = $"{paziente.Nome} {paziente.Cognome}";

        return Ok(new SchedaPazienteResponse(
            paziente.Id, paziente.Nome, paziente.Cognome, paziente.CodiceFiscale,
            paziente.Telefono, paziente.Email, paziente.DataNascita, paziente.Stato.ToString(),
            pacchetti,
            ricette,
            storico,
            pagamenti.Select(p => PagamentoService.ToDto(
                p, nomeCompleto, ricevute.FirstOrDefault(r => r.PagamentoId == p.Id))).ToList(),
            note));
    }

    [HttpPost("{pazienteId:int}/note")]
    [Authorize(Roles = "Coordinatrice")]
    public async Task<ActionResult<NotaOperativaDto>> AggiungiNota(int pazienteId, AggiungiNotaRequest request)
    {
        var testo = request.Testo?.Trim() ?? string.Empty;
        if (testo.Length == 0) return BadRequest(new { messaggio = "La nota è vuota." });
        if (!await db.Pazienti.AnyAsync(p => p.Id == pazienteId)) return NotFound();

        var nota = new NotaOperativa
        {
            PazienteId = pazienteId,
            Testo = testo,
            Autore = AutoreCorrente(),
            Data = DateTime.Now,
        };
        db.NoteOperative.Add(nota);
        await db.SaveChangesAsync();

        return Ok(new NotaOperativaDto(nota.Id, nota.Testo, nota.Autore, nota.Data));
    }

    // Chi firma la nota. `User.Identity.Name` è vuoto: il token porta nome e cognome come
    // claim separati, e una nota non firmata con un nome vero non serve a niente in un
    // registro che esiste proprio per sapere chi ha scritto cosa.
    private string AutoreCorrente()
    {
        var nome = User.FindFirstValue(ClaimTypes.GivenName);
        var cognome = User.FindFirstValue(ClaimTypes.Surname);
        var completo = $"{nome} {cognome}".Trim();
        return completo.Length > 0 ? completo : "Coordinatrice";
    }

    private async Task<bool> EProprietarioAsync(int pazienteId)
    {
        var utenteId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        return await db.UtentiPazienti.AnyAsync(up => up.UtenteId == utenteId && up.PazienteId == pazienteId);
    }
}
