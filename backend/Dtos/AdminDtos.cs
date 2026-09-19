namespace RimettimiInSesto.Api.Dtos;

// Le due dimensioni sono incrociate, non tre categorie parallele: una prestazione
// strumentale in percorso SSN è una cosa diversa dalla stessa in privato, perché nel
// primo caso passa dalla ricetta (ARCHITETTURA.md / mockup admin.html).
public record VoceEntrate(string Percorso, string TipoPrestazione, decimal Importo);

// Una "barra" del grafico Andamento: un mese se il periodo è l'anno, una settimana (anche
// clippata ai bordi del mese) se il periodo è il mese, un giorno se il periodo è la
// settimana. Stessa forma per tutte e tre le granularità, così il frontend disegna un solo
// grafico invece di tre.
//   - ImportoPrivato/ImportoSsn: lo stesso Importo diviso sulle due dimensioni, per colorare
//     la barra in due segmenti invece di lasciare il mix leggibile solo nel totale del
//     periodo (che una torta a parte raccontava prima, ora ridondante).
//   - ImportoAnnoPrecedente: incassato nello stesso intervallo, un anno prima — permette il
//     confronto anche a grana mese/settimana, non solo sul totale del periodo.
//   - ImportoPrenotato: valorizzato solo per una barra interamente nel futuro (vedi
//     AdminController.Andamento). Non è una previsione: è la somma di ciò che è già in
//     agenda (appuntamenti confermati) su quella barra. Il ticket SSN non ci entra — è
//     dovuto una volta per ciclo, non a seduta, quindi una seduta SSN futura non aggiunge
//     nuovo incasso atteso.
//   - SeduteErogate: sedute completate nello stesso intervallo, per la tabella di dettaglio.
public record BarraAndamento(
    DateOnly Da, DateOnly A, string Etichetta, decimal Importo,
    decimal ImportoPrivato, decimal ImportoSsn,
    decimal ImportoAnnoPrecedente, decimal ImportoPrenotato, int SeduteErogate);

public record AndamentoResponse(
    string Periodo, DateOnly Da, DateOnly A, string Etichetta,
    decimal Totale, decimal TotaleAnnoPrecedente,
    // "Da riscuotere" non è per costruzione un dato di periodo (è un arretrato, non
    // un'entrata datata): stessa query di CassaController.DaRiscuotere, senza filtro di
    // data. Niente "giorni di anzianità media": il Pagamento non porta una data di
    // creazione propria (solo DataIncasso, valorizzata a saldo avvenuto), quindi quel
    // numero non è calcolabile onestamente — meglio il conteggio delle voci, che lo è.
    decimal DaRiscuotere, int NumeroVociDaRiscuotere,
    // Sedute realmente erogate nel periodo (Completato): la seduta è l'unità che il
    // titolare confronta con "quanto ho incassato" per ottenere il valore medio a seduta.
    int SeduteErogate,
    // Finestra massima di prenotazione (ImpostazioniAgenda), riportata qui perché il
    // frontend possa spiegare *perché* una barra futura resta a zero invece di lasciarlo
    // intuire: oltre questa soglia nessuno può ancora avere prenotato nulla.
    int FinestraPrenotazioneGiorni,
    List<VoceEntrate> PerCategoria, List<BarraAndamento> Barre);

// Ore erogate a confronto col monte ore da contratto. Volutamente aggregato per periodo:
// non un registro di cosa ha fatto ciascuno minuto per minuto — l'art. 4 dello Statuto
// dei Lavoratori vieta il controllo a distanza dell'attività (requisiti.md).
public record RigaPersonale(
    int FisioterapistaId, string Nome, string Contratto,
    decimal OreErogate, decimal OreContratto, int? Saturazione);

public record ConfigurazioneResponse(
    decimal PrezzoPacchettoPrivato, int SedutePacchettoPrivato, int DurataValiditaPacchettoMesi,
    decimal PrezzoSedutaSingolaManuale, decimal QuotaTicketRegionale, string OrariApertura,
    // Gli stessi orari in forma strutturata: la riga di testo si legge, questi si modificano.
    TimeOnly? MattinaInizio, TimeOnly? MattinaFine, TimeOnly? PomeriggioInizio, TimeOnly? PomeriggioFine,
    List<AccountDto> Account);

public record AccountDto(string Email, string Nome, string Cognome, string Ruolo);

public record AggiornaListinoRequest(
    decimal PrezzoPacchettoPrivato, int SedutePacchettoPrivato, int DurataValiditaPacchettoMesi,
    decimal PrezzoSedutaSingolaManuale, decimal QuotaTicketRegionale);

public record AggiornaOrariRequest(
    TimeOnly MattinaInizio, TimeOnly MattinaFine, TimeOnly PomeriggioInizio, TimeOnly PomeriggioFine);
