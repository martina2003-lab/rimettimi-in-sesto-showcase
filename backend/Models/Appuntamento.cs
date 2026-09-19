namespace RimettimiInSesto.Api.Models;

public class Appuntamento
{
    public int Id { get; set; }

    public int PazienteId { get; set; }
    public Paziente Paziente { get; set; } = null!;

    public int FisioterapistaId { get; set; }
    public Fisioterapista Fisioterapista { get; set; } = null!;

    public DateTime DataOra { get; set; }

    // Multipla di 30 minuti: privato = scelta dal paziente; SSN = 30 min x n. distretti
    // prescritti nella ricetta trattati in quella seduta (non un dato libero).
    public int DurataMinuti { get; set; }

    public StatoAppuntamento Stato { get; set; } = StatoAppuntamento.Richiesto;
    public PercorsoAppuntamento Percorso { get; set; }

    // Uno dei due, secondo il percorso — mai entrambi.
    public int? AcquistoPacchettoId { get; set; }
    public AcquistoPacchetto? AcquistoPacchetto { get; set; }
    public int? RicettaId { get; set; }
    public Ricetta? Ricetta { get; set; }

    // Annullamento per assenza del fisioterapista: motivo + se la notifica è già stata
    // data altrove o va inviata (sempre con approvazione esplicita della Coordinatrice).
    public string? MotivoAnnullamento { get; set; }
    public bool? NotificaGiaDataAltrove { get; set; }

    // Richiesta di spostamento in attesa di approvazione. Il punto non ovvio, già deciso
    // in mockup/paziente.html: chiedere una modifica NON fa perdere lo slot confermato —
    // l'appuntamento resta dov'è finché la segreteria non approva. Per questo la nuova
    // data vive in un campo a parte invece di sovrascrivere DataOra: finché la richiesta
    // è aperta risultano occupati entrambi gli orari, il vecchio e il nuovo.
    public DateTime? ModificaRichiestaDataOra { get; set; }
    public DateTime? ModificaRichiestaIl { get; set; }

    // Unico elemento per-singola-seduta della cartella clinica (tutto il resto è per ciclo).
    public NotaSeduta? RiepilogoSeduta { get; set; }

    public ICollection<PropostaSlotAlternativo> ProposteSlot { get; set; } = [];
}

// Riepilogo di seduta: il fisioterapista registra un breve resoconto al termine
// dell'appuntamento. Alimenta quanto il paziente vede nel proprio portale.
public class NotaSeduta
{
    public int Id { get; set; }

    public int AppuntamentoId { get; set; }
    public Appuntamento Appuntamento { get; set; } = null!;

    public string Testo { get; set; } = string.Empty;
    public DateTime RegistrataIl { get; set; }
}

// Assenza fisioterapista, in tutte e tre le varianti (pianificata / improvvisa / lunga durata).
public class AssenzaFisioterapista
{
    public int Id { get; set; }

    public int FisioterapistaId { get; set; }
    public Fisioterapista Fisioterapista { get; set; } = null!;

    public DateOnly DataInizio { get; set; }
    public DateOnly DataFine { get; set; }
    public TipoAssenza Tipo { get; set; }
    public StatoApprovazioneAssenza StatoApprovazione { get; set; } = StatoApprovazioneAssenza.InAttesa;

    public string? Motivo { get; set; }
}

// Chiusura studio (festività, chiusura estiva, imprevisti): blocca la prenotabilità
// per tutti i fisioterapisti nel periodo indicato.
public class ChiusuraStudio
{
    public int Id { get; set; }

    public DateOnly DataInizio { get; set; }
    public DateOnly DataFine { get; set; }
    public string Motivo { get; set; } = string.Empty;
}

// Fino a 2-3 slot alternativi proposti dalla Coordinatrice quando non può confermare
// lo slot richiesto. Gli slot proposti restano bloccati finché la proposta è aperta.
public class PropostaSlotAlternativo
{
    public int Id { get; set; }

    public int AppuntamentoId { get; set; }
    public Appuntamento Appuntamento { get; set; } = null!;

    public List<DateTime> SlotProposti { get; set; } = [];
    public DateTime Scadenza { get; set; }
    public EsitoPropostaSlot Esito { get; set; } = EsitoPropostaSlot.InAttesa;
    public DateTime? SlotScelto { get; set; }
}
