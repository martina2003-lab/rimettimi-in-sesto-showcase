namespace RimettimiInSesto.Api.Models;

// Percorso convenzionato SSN — caricata online dal paziente (dati/foto), validata
// manualmente dalla Coordinatrice. Numero sedute, ticket e scadenza vengono da qui,
// mai decisi dallo studio (vedi ARCHITETTURA.md, "due percorsi paziente").
public class Ricetta
{
    public int Id { get; set; }

    public int PazienteId { get; set; }
    public Paziente Paziente { get; set; } = null!;

    public string? NumeroONre { get; set; }
    public string? MedicoPrescrittore { get; set; }
    public DateOnly DataEmissione { get; set; }
    public DateOnly? Scadenza { get; set; }

    // Termine entro cui le sedute vanno esaurite, oltre il quale serve una nuova
    // impegnativa (indicativamente 30gg ortopedica/traumatica, 60gg neurologica acuta).
    public DateOnly? FinestraCompletamento { get; set; }

    public string? CodicePrestazioneBranca { get; set; }

    // Colonna cervicale/dorsale/lombare, arto superiore/inferiore dx/sx — uno o più,
    // ciascuno vale 30 minuti di seduta. Testo libero per l'MVP demo (elenco separato da virgole).
    public string? DistrettiCorporei { get; set; }

    public string? QuesitoDiagnostico { get; set; }
    public bool Esenzione { get; set; }
    public string? CodiceEsenzione { get; set; }

    public int NumeroSeduteProscritte { get; set; }
    public decimal? ImportoTicket { get; set; }

    public StatoRicetta Stato { get; set; } = StatoRicetta.NonAncoraValidata;

    // Vero se il paziente ha allegato foto/scansione in fase di caricamento online (vedi
    // PazientiController.CaricaRicetta). Nessuno storage reale di file nel demo — coerente
    // con FotoProfiloUrl e col resto del progetto — ma la segreteria deve sapere se ha un
    // documento da controllare o solo dei dati digitati, prima di poter validare o respingere.
    public bool DocumentoCaricato { get; set; }

    // Appunto libero per la segreteria quando il documento manca ("cartacea nel
    // raccoglitore", "esenzione da verificare col medico") — viaggia con l'avviso ovunque compaia.
    public string? AppuntoSegreteria { get; set; }

    // Data in cui la ricetta è entrata nello stato "IntegrazioneRichiesta": usata per
    // calcolare la priorità in coda (punto aperto chiuso il 12/09/2026, vedi requisiti.md).
    public DateTime? IntegrazioneRichiestaIl { get; set; }

    public ICollection<Appuntamento> Appuntamenti { get; set; } = [];
    public AcquistoPacchetto? CicloGenerato { get; set; }
}
