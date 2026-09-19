namespace RimettimiInSesto.Api.Models;

// Singleton (una sola riga in tabella): parametri configurabili dalla Coordinatrice.
public class ImpostazioniAgenda
{
    public int Id { get; set; }

    public int FinestraPrenotazioneMassimaGiorni { get; set; } = 60;
    public int PreavvisoMinimoCancellazioneOre { get; set; } = 24;
    public int SogliaAvvisoDisponibilitaAnticipataGiorni { get; set; } = 7;
    public int ValiditaPropostaSlotOre { get; set; } = 48;

    // Soglia (in giorni) dopo cui una ricetta "IntegrazioneRichiesta" sale di priorità
    // nella coda Ricette SSN — chiuso il 12/09/2026, vedi requisiti.md. Nessuna decadenza
    // automatica dello slot: resta comunque decisione della Coordinatrice.
    public int SogliaPriorizzazioneRicettaInIntegrazioneGiorni { get; set; } = 20;
}

// Singleton: parametri configurabili dall'Admin.
public class ImpostazioniListino
{
    public int Id { get; set; }

    public decimal PrezzoPacchettoPrivato { get; set; } = 380m;
    public int SedutePacchettoPrivato { get; set; } = 10;
    public int DurataValiditaPacchettoMesi { get; set; } = 12;
    public decimal PrezzoSedutaSingolaManuale { get; set; } = 60m;

    // Precompila il ticket in validazione, dove resta sempre modificabile a mano (chiuso il
    // 12/09/2026, vedi requisiti.md). **Valore provvisorio**: 36 € è la quota che le ricette
    // del seed portano già scritta, non un importo confermato dallo studio — va riverificato
    // prima di andare in produzione, e si cambia da Configurazione senza toccare il codice.
    public decimal QuotaTicketRegionale { get; set; } = 36m;

    // Orari di apertura, testo libero per la visualizzazione in admin.html.
    public string OrariApertura { get; set; } = "Lun-Ven 9:00-13:00, 15:00-19:00";

    // Stessa informazione in forma strutturata, per il calcolo disponibilità (AvailabilityService).
    // Sabato e domenica chiusi: deciso il 9 settembre 2026 nei mockup (calendari settimanali),
    // fisso qui invece che come dato configurabile — non serve per un solo studio mono-tenant.
    public TimeOnly? MattinaInizio { get; set; } = new TimeOnly(9, 0);
    public TimeOnly? MattinaFine { get; set; } = new TimeOnly(13, 0);
    public TimeOnly? PomeriggioInizio { get; set; } = new TimeOnly(15, 0);
    public TimeOnly? PomeriggioFine { get; set; } = new TimeOnly(19, 0);
}
