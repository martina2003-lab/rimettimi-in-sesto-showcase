using Microsoft.EntityFrameworkCore;
using RimettimiInSesto.Api.Data;
using RimettimiInSesto.Api.Dtos;
using RimettimiInSesto.Api.Models;

namespace RimettimiInSesto.Api.Services;

public class RicettaValidationException(string messaggio) : Exception(messaggio);

// Le due decisioni distinte di requisiti.md ("questo slot glielo do?" vs "questo ciclo lo
// apro?") restano separate: qui non si tocca mai lo stato di un Appuntamento se non
// esplicitamente, nella sola Respingi (dove il destino va deciso, mai automatico).
public class RicettaService(ApplicationDbContext db)
{
    public async Task<Ricetta> ValidaAsync(int ricettaId, ValidaRicettaRequest request, string autore)
    {
        var ricetta = await db.Ricette.FindAsync(ricettaId)
            ?? throw new RicettaValidationException("Ricetta non trovata.");
        if (ricetta.Stato == StatoRicetta.Validata)
        {
            throw new RicettaValidationException("Questa ricetta è già stata validata.");
        }

        ricetta.NumeroSeduteProscritte = request.NumeroSedute;
        ricetta.ImportoTicket = request.ImportoTicket;
        ricetta.Esenzione = request.Esenzione;
        ricetta.CodiceEsenzione = request.CodiceEsenzione;
        ricetta.DistrettiCorporei = request.DistrettiCorporei;
        ricetta.FinestraCompletamento = request.FinestraCompletamento;
        ricetta.Stato = StatoRicetta.Validata;
        ricetta.IntegrazioneRichiestaIl = null;

        // "Si apre il ciclo" — la ricetta smette di essere un documento e diventa un
        // ciclo con effetti economici e di agenda (requisiti.md).
        var ciclo = new AcquistoPacchetto
        {
            PazienteId = ricetta.PazienteId, Tipo = TipoPacchetto.Ssn,
            SeduteTotali = request.NumeroSedute, SeduteResidue = request.NumeroSedute,
            DataAcquisto = DateOnly.FromDateTime(DateTime.UtcNow),
            Scadenza = request.FinestraCompletamento, RicettaId = ricetta.Id,
        };
        db.AcquistiPacchetto.Add(ciclo);

        // "Si genera la voce ticket in Cassa (o si registra l'esenzione a 0)".
        db.Pagamenti.Add(request.ImportoTicket > 0
            ? new Pagamento
            {
                PazienteId = ricetta.PazienteId, Importo = request.ImportoTicket,
                Metodo = MetodoPagamento.TicketSsn, Stato = StatoPagamento.DaSaldare,
                Origine = $"Ticket ciclo SSN — ricetta {ricetta.NumeroONre}, dovuto una volta per ciclo",
            }
            : new Pagamento
            {
                PazienteId = ricetta.PazienteId, Importo = 0m,
                Metodo = MetodoPagamento.TicketSsn, Stato = StatoPagamento.Pagato,
                Origine = $"Ticket ciclo SSN — esenzione {request.CodiceEsenzione}",
            });

        db.NoteOperative.Add(new NotaOperativa
        {
            PazienteId = ricetta.PazienteId, Autore = autore, Data = DateTime.UtcNow,
            Testo = $"Ricetta {ricetta.NumeroONre} validata: {request.NumeroSedute} sedute, finestra fino al {request.FinestraCompletamento:dd/MM/yyyy}.",
        });

        await db.SaveChangesAsync();
        return ricetta;
    }

    // Gli appuntamenti già fissati NON si toccano — il paziente può ancora portare
    // il documento in studio (requisiti.md).
    public async Task<Ricetta> RichiediIntegrazioneAsync(int ricettaId, string motivo, string autore)
    {
        var ricetta = await db.Ricette.FindAsync(ricettaId)
            ?? throw new RicettaValidationException("Ricetta non trovata.");

        ricetta.Stato = StatoRicetta.IntegrazioneRichiesta;
        ricetta.IntegrazioneRichiestaIl = DateTime.UtcNow;
        ricetta.AppuntoSegreteria = motivo;

        db.NoteOperative.Add(new NotaOperativa
        {
            PazienteId = ricetta.PazienteId, Autore = autore, Data = DateTime.UtcNow,
            Testo = $"Integrazione richiesta per ricetta {ricetta.NumeroONre}: {motivo}",
        });

        await db.SaveChangesAsync();
        return ricetta;
    }

    // "Nessuna di queste è automatica" — il destino degli appuntamenti richiesti/confermati
    // va deciso esplicitamente, mai dedotto (requisiti.md).
    public async Task<Ricetta> RespingiAsync(int ricettaId, RespingiRicettaRequest request, string autore)
    {
        var ricetta = await db.Ricette.FindAsync(ricettaId)
            ?? throw new RicettaValidationException("Ricetta non trovata.");

        ricetta.Stato = StatoRicetta.Respinta;
        ricetta.AppuntoSegreteria = request.Motivo;

        var appuntamentiCollegati = await db.Appuntamenti
            .Where(a => a.RicettaId == ricettaId)
            .ToListAsync();

        foreach (var appuntamento in appuntamentiCollegati)
        {
            if (appuntamento.Stato == StatoAppuntamento.Richiesto)
            {
                // "Mantieni" = lasciarlo lì segnalato come senza ricetta valida (derivato
                // da Ricetta.Stato == Respinta), non un semplice non-toccarlo silenzioso.
                if (request.EsitoRichiesti == EsitoAppuntamentoRichiesto.Annulla)
                {
                    appuntamento.Stato = StatoAppuntamento.Annullato;
                    appuntamento.MotivoAnnullamento = "Ricetta SSN respinta.";
                }
            }
            else if (appuntamento.Stato == StatoAppuntamento.Confermato)
            {
                switch (request.EsitoConfermati)
                {
                    case EsitoAppuntamentoConfermato.ConvertiPrivato:
                        appuntamento.Percorso = PercorsoAppuntamento.Privato;
                        appuntamento.RicettaId = null;
                        break;
                    case EsitoAppuntamentoConfermato.Annulla:
                        appuntamento.Stato = StatoAppuntamento.Annullato;
                        appuntamento.MotivoAnnullamento = "Ricetta SSN respinta.";
                        break;
                    case EsitoAppuntamentoConfermato.Mantieni:
                        break; // resta confermato, segnalato senza ricetta valida via Ricetta.Stato
                }
            }
        }

        db.NoteOperative.Add(new NotaOperativa
        {
            PazienteId = ricetta.PazienteId, Autore = autore, Data = DateTime.UtcNow,
            Testo = $"Ricetta {ricetta.NumeroONre} respinta: {request.Motivo}",
        });

        await db.SaveChangesAsync();
        return ricetta;
    }

    // Ordinata per urgenza d'agenda, non per anzianità del documento: si apre prima la
    // ricetta che sta bloccando più slot (requisiti.md).
    public async Task<List<RicettaDto>> CodaAsync()
    {
        var soglia = (await db.ImpostazioniAgenda.FirstOrDefaultAsync())
            ?.SogliaPriorizzazioneRicettaInIntegrazioneGiorni ?? 20;

        var ricette = await db.Ricette
            .Where(r => r.Stato == StatoRicetta.NonAncoraValidata || r.Stato == StatoRicetta.IntegrazioneRichiesta)
            .Include(r => r.Paziente)
            .Include(r => r.Appuntamenti)
            .ToListAsync();

        var risultato = ricette.Select(r => ToDtoCalcolato(r, soglia))
        .OrderByDescending(dto => dto.PriorizzataPerAttesa)
        .ThenByDescending(dto => dto.AppuntamentiBloccati)
        .ThenBy(dto => dto.DataEmissione)
        .ToList();

        return risultato;
    }

    // Tutte le ricette di un paziente, per la sua scheda in segreteria: qui servono anche
    // le validate e le respinte, che nella coda non compaiono perché non c'è più niente da
    // decidere. Vive nel servizio e non nel controller perché la regola di priorità è una
    // sola, e riscriverla altrove significherebbe vederla divergere.
    public async Task<List<RicettaDto>> DiPazienteAsync(int pazienteId)
    {
        var soglia = (await db.ImpostazioniAgenda.FirstOrDefaultAsync())
            ?.SogliaPriorizzazioneRicettaInIntegrazioneGiorni ?? 20;

        var ricette = await db.Ricette
            .Where(r => r.PazienteId == pazienteId)
            .Include(r => r.Paziente)
            .Include(r => r.Appuntamenti)
            .OrderByDescending(r => r.DataEmissione)
            .ToListAsync();

        return ricette.Select(r => ToDtoCalcolato(r, soglia)).ToList();
    }

    private static RicettaDto ToDtoCalcolato(Ricetta r, int sogliaGiorni)
    {
        var appuntamentiBloccati = r.Appuntamenti.Count(a =>
            a.Stato == StatoAppuntamento.Richiesto || a.Stato == StatoAppuntamento.Confermato);
        var priorizzata = r.Stato == StatoRicetta.IntegrazioneRichiesta
            && r.IntegrazioneRichiestaIl is { } dal
            && (DateTime.UtcNow - dal).TotalDays >= sogliaGiorni;

        return ToDto(r, appuntamentiBloccati, priorizzata);
    }

    public static RicettaDto ToDto(Ricetta r, int appuntamentiBloccati, bool priorizzata) => new(
        r.Id, r.PazienteId, $"{r.Paziente.Nome} {r.Paziente.Cognome}", r.Stato.ToString(),
        r.NumeroONre, r.MedicoPrescrittore, r.DataEmissione, r.Scadenza, r.FinestraCompletamento,
        r.DistrettiCorporei, r.QuesitoDiagnostico, r.Esenzione, r.CodiceEsenzione,
        r.NumeroSeduteProscritte, r.ImportoTicket, r.AppuntoSegreteria,
        appuntamentiBloccati, priorizzata, r.DocumentoCaricato);
}
