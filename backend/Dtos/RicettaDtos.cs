using RimettimiInSesto.Api.Models;

namespace RimettimiInSesto.Api.Dtos;

public record RicettaDto(
    int Id, int PazienteId, string PazienteNome, string Stato,
    string? NumeroONre, string? MedicoPrescrittore, DateOnly DataEmissione, DateOnly? Scadenza,
    DateOnly? FinestraCompletamento, string? DistrettiCorporei, string? QuesitoDiagnostico,
    bool Esenzione, string? CodiceEsenzione, int NumeroSeduteProscritte, decimal? ImportoTicket,
    string? AppuntoSegreteria,
    int AppuntamentiBloccati,
    // true quando una ricetta "IntegrazioneRichiesta" ha superato la soglia di attesa —
    // chiuso il 12/09/2026 in requisiti.md: nessuna decadenza automatica, ma priorità in coda.
    bool PriorizzataPerAttesa,
    bool DocumentoCaricato);

// Quel che il paziente scrive caricando la ricetta dal wizard di prenotazione: solo dati
// testuali (coerente con la scelta "demo" del progetto, niente storage di file reale — come
// FotoProfiloUrl). DocumentoCaricato dice alla segreteria se c'è un documento da controllare
// o solo numeri digitati, prima di poter validare o respingere.
public record CaricaRicettaRequest(string? NumeroONre, string DistrettiCorporei, bool DocumentoCaricato);

// Dati confermati/corretti dalla Coordinatrice in validazione — "quel che il sistema
// non può sapere" (requisiti.md): li conferma o li corregge lei, non arrivano già giusti.
public record ValidaRicettaRequest(
    int NumeroSedute, decimal ImportoTicket, bool Esenzione, string? CodiceEsenzione,
    string DistrettiCorporei, DateOnly FinestraCompletamento);

public record IntegrazioneRichiestaRequest(string Motivo);

public record RespingiRicettaRequest(
    string Motivo,
    EsitoAppuntamentoRichiesto EsitoRichiesti,
    EsitoAppuntamentoConfermato EsitoConfermati);
