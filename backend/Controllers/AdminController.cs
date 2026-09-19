using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RimettimiInSesto.Api.Data;
using RimettimiInSesto.Api.Dtos;
using RimettimiInSesto.Api.Models;

namespace RimettimiInSesto.Api.Controllers;

// Vista di business del titolare: soldi e ore, **nessun accesso clinico** e nessun nome di
// paziente (principio guida di CLAUDE.md). Qui non passa nulla che venga dalla cartella.
[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
public class AdminController(ApplicationDbContext db) : ControllerBase
{
    private static readonly string[] MesiBrevi =
        ["Gen", "Feb", "Mar", "Apr", "Mag", "Giu", "Lug", "Ago", "Set", "Ott", "Nov", "Dic"];
    private static readonly string[] GiorniBrevi = ["Lun", "Mar", "Mer", "Gio", "Ven", "Sab", "Dom"];

    private static DateOnly LunediDellaSettimanaDi(DateOnly data) =>
        data.AddDays(-(data.DayOfWeek == DayOfWeek.Sunday ? 6 : (int)data.DayOfWeek - 1));

    // Un solo endpoint per le tre granularità del grafico Andamento (vedi CLAUDE.md): la
    // domanda "come va" cambia scala — settimana, mese, anno — ma è sempre la stessa domanda,
    // e tre endpoint quasi identici sarebbero tre posti in cui la stessa logica di percorso
    // (SSN/Privato) potrebbe divergere nel tempo.
    [HttpGet("andamento")]
    public async Task<ActionResult<AndamentoResponse>> Andamento(
        [FromQuery] string periodo, [FromQuery] DateOnly riferimento)
    {
        DateOnly da, a;
        switch (periodo)
        {
            case "settimana":
                da = LunediDellaSettimanaDi(riferimento);
                a = da.AddDays(6);
                break;
            case "mese":
                da = new DateOnly(riferimento.Year, riferimento.Month, 1);
                a = da.AddMonths(1).AddDays(-1);
                break;
            case "anno":
                da = new DateOnly(riferimento.Year, 1, 1);
                a = new DateOnly(riferimento.Year, 12, 31);
                break;
            default:
                return BadRequest(new { messaggio = "Periodo non valido: usa settimana, mese o anno." });
        }

        var inizio = da.ToDateTime(TimeOnly.MinValue);
        var fine = a.ToDateTime(TimeOnly.MaxValue);

        // L'anno prima dello stesso periodo, non solo dello stesso mese: uno spostamento di
        // -1 anno sulle date, non sul solo "anno" della barra, così il confronto funziona
        // identico a grana mese e settimana, non solo per la vista annuale.
        var inizioAnnoPrec = inizio.AddYears(-1);
        var fineAnnoPrec = fine.AddYears(-1);

        // Un'unica lettura (come /entrate) invece di una query per periodo: qui serve
        // comunque sia l'intervallo corrente sia quello di un anno prima, e a questa scala
        // di dati filtrare in memoria costa meno che scrivere due query quasi identiche.
        var pagamenti = await db.Pagamenti
            .Include(p => p.AcquistoPacchetto)
            .Where(p => p.Stato == StatoPagamento.Pagato && p.DataIncasso != null
                        && p.DataIncasso >= inizioAnnoPrec && p.DataIncasso <= fine)
            .Select(p => new
            {
                p.Importo,
                p.TipoPrestazione,
                p.DataIncasso,
                TipoPacchetto = p.AcquistoPacchetto != null ? p.AcquistoPacchetto.Tipo : (TipoPacchetto?)null,
                p.Metodo,
            })
            .ToListAsync();

        var delPeriodo = pagamenti.Where(p => p.DataIncasso >= inizio && p.DataIncasso <= fine).ToList();
        var delPeriodoAnnoPrec = pagamenti.Where(p => p.DataIncasso >= inizioAnnoPrec && p.DataIncasso <= fineAnnoPrec).ToList();

        string PercorsoDi(TipoPacchetto? tipo, MetodoPagamento metodo) =>
            tipo == TipoPacchetto.Ssn || metodo == MetodoPagamento.TicketSsn ? "SSN" : "Privato";

        var perCategoria = delPeriodo
            .GroupBy(p => new { Percorso = PercorsoDi(p.TipoPacchetto, p.Metodo), Tipo = p.TipoPrestazione })
            .Select(g => new VoceEntrate(g.Key.Percorso, g.Key.Tipo.ToString(), g.Sum(p => p.Importo)))
            .OrderBy(v => v.Percorso).ThenBy(v => v.TipoPrestazione)
            .ToList();

        // "Già prenotato": solo appuntamenti confermati non ancora avvenuti, valorizzati con
        // una stima ragionevole e non con un incasso reale (che non esiste finché la seduta
        // non è completata e pagata). Vedi il commento su BarraAndamento.ImportoPrenotato per
        // il perché l'SSN non contribuisce.
        var listino = await db.ImpostazioniListino.FirstOrDefaultAsync() ?? new ImpostazioniListino();
        var oraSpartiacque = DateTime.Now;
        var confermatiFuturi = await db.Appuntamenti
            .Include(x => x.AcquistoPacchetto)
            .Where(x => x.Stato == StatoAppuntamento.Confermato && x.DataOra > oraSpartiacque)
            .Select(x => new
            {
                x.DataOra,
                TipoPacchetto = x.AcquistoPacchetto != null ? x.AcquistoPacchetto.Tipo : (TipoPacchetto?)null,
                PrezzoPacchetto = x.AcquistoPacchetto != null ? x.AcquistoPacchetto.Prezzo : null,
                SeduteTotaliPacchetto = x.AcquistoPacchetto != null ? x.AcquistoPacchetto.SeduteTotali : (int?)null,
            })
            .ToListAsync();

        decimal ValoreStimato(TipoPacchetto? tipo, decimal? prezzoPacchetto, int? seduteTotali) => tipo switch
        {
            // Il valore di una singola seduta dentro un pacchetto è una media (prezzo totale
            // diviso sedute): il pacchetto si vende intero, non seduta per seduta.
            TipoPacchetto.Privato => seduteTotali is > 0 ? (prezzoPacchetto ?? 0) / seduteTotali.Value : 0,
            // Il ticket SSN è dovuto una volta per ciclo, non a seduta (CLAUDE.md, Cassa):
            // se il ciclo è già aperto, le sue sedute future non generano nuovo incasso atteso.
            TipoPacchetto.Ssn => 0,
            // Prenotazione privata senza pacchetto (es. telefonica, pagata seduta per
            // seduta): il prezzo di listino è la stima più onesta disponibile.
            _ => listino.PrezzoSedutaSingolaManuale,
        };

        var oggiData = DateOnly.FromDateTime(DateTime.Today);

        // Da riscuotere è un arretrato, non un'entrata datata: stessa lettura di
        // CassaController.DaRiscuotere, indipendente dal periodo selezionato.
        var daSaldare = await db.Pagamenti
            .Where(p => p.Stato == StatoPagamento.DaSaldare)
            .Select(p => p.Importo)
            .ToListAsync();

        var seduteErogate = await db.Appuntamenti
            .CountAsync(x => x.Stato == StatoAppuntamento.Completato && x.DataOra >= inizio && x.DataOra <= fine);

        // Solo le date: la barra ha bisogno soltanto di contare quante ne cadono nel suo
        // intervallo, non dei dettagli della seduta (niente di clinico passa da qui).
        var dateSeduteCompletate = await db.Appuntamenti
            .Where(x => x.Stato == StatoAppuntamento.Completato && x.DataOra >= inizio && x.DataOra <= fine)
            .Select(x => x.DataOra)
            .ToListAsync();

        var finestraPrenotazioneGiorni = (await db.ImpostazioniAgenda.FirstOrDefaultAsync())
            ?.FinestraPrenotazioneMassimaGiorni ?? new ImpostazioniAgenda().FinestraPrenotazioneMassimaGiorni;

        List<BarraAndamento> barre;
        string etichetta;

        BarraAndamento CostruisciBarra(DateOnly barraDa, DateOnly barraA, string barraEtichetta)
        {
            var pagamentiBarra = delPeriodo
                .Where(p => { var g = DateOnly.FromDateTime(p.DataIncasso!.Value); return g >= barraDa && g <= barraA; })
                .ToList();
            var importo = pagamentiBarra.Sum(p => p.Importo);
            // Stessa distinzione dell'incrocio di /entrate: qui serve per colorare la barra
            // in due segmenti, non per una categoria in più.
            var importoPrivato = pagamentiBarra
                .Where(p => PercorsoDi(p.TipoPacchetto, p.Metodo) == "Privato")
                .Sum(p => p.Importo);
            var importoSsn = importo - importoPrivato;

            var barraDaAnnoPrec = barraDa.AddYears(-1);
            var barraAAnnoPrec = barraA.AddYears(-1);
            var importoAnnoPrec = delPeriodoAnnoPrec
                .Where(p => { var g = DateOnly.FromDateTime(p.DataIncasso!.Value); return g >= barraDaAnnoPrec && g <= barraAAnnoPrec; })
                .Sum(p => p.Importo);

            // Solo per una barra interamente nel futuro: il mese/settimana in corso ha già
            // un Importo reale in aggiornamento, sommarci anche il prenotato conterebbe la
            // stessa seduta due volte quando verrà completata e pagata.
            var importoPrenotato = barraDa > oggiData
                ? confermatiFuturi
                    .Where(x => { var g = DateOnly.FromDateTime(x.DataOra); return g >= barraDa && g <= barraA; })
                    .Sum(x => ValoreStimato(x.TipoPacchetto, x.PrezzoPacchetto, x.SeduteTotaliPacchetto))
                : 0;

            var seduteBarra = dateSeduteCompletate
                .Count(d => { var g = DateOnly.FromDateTime(d); return g >= barraDa && g <= barraA; });

            return new BarraAndamento(
                barraDa, barraA, barraEtichetta, importo, importoPrivato, importoSsn,
                importoAnnoPrec, importoPrenotato, seduteBarra);
        }

        if (periodo == "anno")
        {
            etichetta = da.Year.ToString();
            barre = Enumerable.Range(1, 12).Select(mese =>
            {
                var meseDa = new DateOnly(da.Year, mese, 1);
                var meseA = meseDa.AddMonths(1).AddDays(-1);
                return CostruisciBarra(meseDa, meseA, MesiBrevi[mese - 1]);
            }).ToList();
        }
        else if (periodo == "mese")
        {
            var nomeMese = CultureInfo.GetCultureInfo("it-IT").DateTimeFormat.GetMonthName(da.Month);
            etichetta = $"{char.ToUpperInvariant(nomeMese[0])}{nomeMese[1..]} {da.Year}";

            // Settimane intere (Lun-Dom), tagliate ai bordi del mese: una settimana a
            // cavallo fra due mesi conta per intero in ciascuna vista, senza doppiare
            // l'incasso complessivo dell'anno, perché ogni pagamento resta nel proprio giorno.
            barre = [];
            var cursore = LunediDellaSettimanaDi(da);
            while (cursore <= a)
            {
                var fineSettimana = cursore.AddDays(6);
                var clipDa = cursore < da ? da : cursore;
                var clipA = fineSettimana > a ? a : fineSettimana;
                barre.Add(CostruisciBarra(clipDa, clipA, $"{clipDa:dd}–{clipA:dd}"));
                cursore = cursore.AddDays(7);
            }
        }
        else
        {
            etichetta = $"{da:dd/MM} – {a:dd/MM/yyyy}";
            barre = Enumerable.Range(0, 7).Select(i =>
            {
                var giorno = da.AddDays(i);
                return CostruisciBarra(giorno, giorno, GiorniBrevi[i]);
            }).ToList();
        }

        return Ok(new AndamentoResponse(
            periodo, da, a, etichetta,
            delPeriodo.Sum(p => p.Importo), delPeriodoAnnoPrec.Sum(p => p.Importo),
            daSaldare.Sum(), daSaldare.Count,
            seduteErogate, finestraPrenotazioneGiorni,
            perCategoria, barre));
    }

    [HttpGet("personale")]
    public async Task<ActionResult<List<RigaPersonale>>> Personale([FromQuery] DateOnly da, [FromQuery] DateOnly a)
    {
        var inizio = da.ToDateTime(TimeOnly.MinValue);
        var fine = a.ToDateTime(TimeOnly.MaxValue);

        var fisioterapisti = await db.Fisioterapisti.Include(f => f.Utente).ToListAsync();

        // Solo le sedute effettivamente erogate: una richiesta o un no-show non sono lavoro svolto.
        var erogate = await db.Appuntamenti
            .Where(x => x.Stato == StatoAppuntamento.Completato && x.DataOra >= inizio && x.DataOra <= fine)
            .GroupBy(x => x.FisioterapistaId)
            .Select(g => new { FisioterapistaId = g.Key, Minuti = g.Sum(x => x.DurataMinuti) })
            .ToListAsync();

        var settimane = (decimal)Math.Max(1, (a.DayNumber - da.DayNumber + 1) / 7.0);

        var righe = fisioterapisti.Select(f =>
        {
            var minuti = erogate.FirstOrDefault(e => e.FisioterapistaId == f.Id)?.Minuti ?? 0;
            var oreErogate = Math.Round(minuti / 60m, 1);
            var oreContratto = Math.Round(f.OreSettimanaliContratto * settimane, 1);

            // Il tasso ha senso solo se c'è un monte ore con cui confrontarlo.
            int? saturazione = oreContratto > 0
                ? (int)Math.Round(oreErogate / oreContratto * 100m)
                : null;

            return new RigaPersonale(
                f.Id, $"{f.Utente.Nome} {f.Utente.Cognome}", f.TipoContratto.ToString(),
                oreErogate, oreContratto, saturazione);
        })
        .OrderBy(r => r.Nome)
        .ToList();

        return Ok(righe);
    }

    [HttpGet("configurazione")]
    public async Task<ActionResult<ConfigurazioneResponse>> Configurazione()
    {
        var listino = await db.ImpostazioniListino.FirstOrDefaultAsync() ?? new ImpostazioniListino();

        var account = await db.Users
            .OrderBy(u => u.Ruolo).ThenBy(u => u.Cognome)
            .Select(u => new AccountDto(u.Email!, u.Nome, u.Cognome, u.Ruolo.ToString()))
            .ToListAsync();

        return Ok(new ConfigurazioneResponse(
            listino.PrezzoPacchettoPrivato, listino.SedutePacchettoPrivato,
            listino.DurataValiditaPacchettoMesi, listino.PrezzoSedutaSingolaManuale,
            listino.QuotaTicketRegionale, listino.OrariApertura,
            listino.MattinaInizio, listino.MattinaFine, listino.PomeriggioInizio, listino.PomeriggioFine,
            account));
    }

    [HttpPut("listino")]
    public async Task<ActionResult> AggiornaListino(AggiornaListinoRequest request)
    {
        var listino = await db.ImpostazioniListino.FirstOrDefaultAsync();
        if (listino is null)
        {
            listino = new ImpostazioniListino();
            db.ImpostazioniListino.Add(listino);
        }

        listino.PrezzoPacchettoPrivato = request.PrezzoPacchettoPrivato;
        listino.SedutePacchettoPrivato = request.SedutePacchettoPrivato;
        listino.DurataValiditaPacchettoMesi = request.DurataValiditaPacchettoMesi;
        listino.PrezzoSedutaSingolaManuale = request.PrezzoSedutaSingolaManuale;
        listino.QuotaTicketRegionale = request.QuotaTicketRegionale;

        await db.SaveChangesAsync();
        return Ok();
    }

    // Gli orari erano modificabili solo mettendo le mani nel database: la schermata li
    // mostrava e dichiarava di non saperli cambiare. Cambiarli qui cambia davvero quando
    // si può prenotare, perché il calcolo disponibilità legge questi stessi campi.
    [HttpPut("orari")]
    public async Task<ActionResult> AggiornaOrari(AggiornaOrariRequest request)
    {
        if (request.MattinaFine <= request.MattinaInizio || request.PomeriggioFine <= request.PomeriggioInizio)
        {
            return BadRequest(new { messaggio = "Ogni fascia deve finire dopo essere cominciata." });
        }

        if (request.PomeriggioInizio < request.MattinaFine)
        {
            return BadRequest(new { messaggio = "Il pomeriggio non può cominciare prima che finisca la mattina." });
        }

        var listino = await db.ImpostazioniListino.FirstOrDefaultAsync();
        if (listino is null)
        {
            listino = new ImpostazioniListino();
            db.ImpostazioniListino.Add(listino);
        }

        listino.MattinaInizio = request.MattinaInizio;
        listino.MattinaFine = request.MattinaFine;
        listino.PomeriggioInizio = request.PomeriggioInizio;
        listino.PomeriggioFine = request.PomeriggioFine;

        // La riga di testo non si scrive a mano: è la stessa informazione, e lasciarla
        // scrivere a parte vorrebbe dire vederla dire una cosa diversa dagli orari veri.
        listino.OrariApertura =
            $"Lun-Ven {request.MattinaInizio:HH:mm}-{request.MattinaFine:HH:mm}, " +
            $"{request.PomeriggioInizio:HH:mm}-{request.PomeriggioFine:HH:mm}";

        await db.SaveChangesAsync();
        return Ok();
    }
}
