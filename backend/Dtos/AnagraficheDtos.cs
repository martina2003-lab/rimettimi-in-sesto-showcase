namespace RimettimiInSesto.Api.Dtos;

// Quel che il paziente può vedere di un fisioterapista mentre lo sceglie: nome e bio.
// NON il tipo di contratto, che requisiti.md dichiara dato interno, mai esposto al paziente.
// Abituale e GiaVisto sono relativi al pazienteId passato in query (vedi FisioterapistiController):
// senza un paziente scelto (nessuno ancora selezionato nel wizard) sono entrambi false.
public record FisioterapistaPubblicoDto(int Id, string Nome, string? Bio, string? FotoProfiloUrl, bool Abituale, bool GiaVisto);

// Le schede collegate all'account: sé stesso, un figlio minore, un familiare a carico.
// È quello che alimenta lo switcher familiare già disegnato in paziente.html.
public record PazienteCollegatoDto(int Id, string Nome, string Cognome, string Titolo);

public record PacchettoAttivoDto(
    int Id, int SeduteTotali, int SeduteResidue, DateOnly? Scadenza, decimal? Prezzo);

public record RicettaAttivaDto(
    int Id, string? NumeroONre, string? DistrettiCorporei, int NumeroSedute,
    DateOnly? FinestraCompletamento, int? CicloId, int? SeduteResidue);

// I due percorsi restano separati anche qui, non appiattiti in una lista unica di
// "cose da cui scalare una seduta": hanno regole diverse.
public record PercorsiAttiviResponse(
    List<PacchettoAttivoDto> PacchettiPrivati,
    List<RicettaAttivaDto> RicetteSsn);

// Ricerca della Coordinatrice per la prenotazione telefonica: nome, contatti e stato
// della scheda. Niente di clinico — chi telefona va solo riconosciuto, non curato.
public record PazienteRicercaDto(
    int Id, string Nome, string Cognome, string? CodiceFiscale, string? Telefono, string Stato);

// Scheda provvisoria creata al telefono: nome, cognome e telefono bastano a bloccare lo
// slot (requisiti.md). Il consenso privacy/dati sanitari NON si raccoglie per telefono,
// quindi la scheda resta "Provvisorio" finché non viene firmato di persona.
public record CreaSchedaProvvisoriaRequest(
    string Nome, string Cognome, string Telefono, string? CodiceFiscale, string? Email);

// Una nota della segreteria sul paziente ("spesso in ritardo", "preferisce il mattino"):
// operativa, mai clinica — la Coordinatrice non ha accesso alla cartella e questa è la
// separazione che glielo impedisce anche quando le servirebbe un appunto.
public record NotaOperativaDto(int Id, string Testo, string Autore, DateTime Data);

public record AggiungiNotaRequest(string Testo);

// Una riga dello storico appuntamenti: quando, con chi, com'è finita. Nessun contenuto
// di seduta — quello vive nella cartella clinica, che lei non vede.
public record VoceStoricoDto(
    DateTime DataOra, string FisioterapistaNome, int DurataMinuti, string Stato, string Percorso);

// La scheda completa come la vede la Coordinatrice: anagrafica, percorsi, storico,
// documenti e note in una sola risposta, perché è una sola schermata a tab.
public record SchedaPazienteResponse(
    int Id, string Nome, string Cognome, string? CodiceFiscale, string? Telefono, string? Email,
    DateOnly? DataNascita, string Stato,
    List<PacchettoAttivoDto> PacchettiPrivati,
    List<RicettaDto> Ricette,
    List<VoceStoricoDto> Storico,
    List<PagamentoDto> Pagamenti,
    List<NotaOperativaDto> Note);

// Un consenso con tutto ciò che serve a dimostrarlo: quando, a quale versione
// dell'informativa, e chi ha firmato (il paziente o chi ne ha la potestà).
public record ConsensoDto(
    int Id, int PazienteId, string Tipo, DateTime Data, string VersioneInformativa,
    string Stato, string Firmatario);

public record PrestaConsensoRequest(int PazienteId, RimettimiInSesto.Api.Models.TipoConsenso Tipo);
public record RegistraConsensiRequest(int PazienteId, string? Firmatario);
