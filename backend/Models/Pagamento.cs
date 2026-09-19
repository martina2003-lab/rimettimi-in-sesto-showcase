namespace RimettimiInSesto.Api.Models;

// Due tipi con regole proprie — mai collassati in un unico concetto generico
// di "pacchetto" (principio guida di CLAUDE.md).
public class AcquistoPacchetto
{
    public int Id { get; set; }

    public int PazienteId { get; set; }
    public Paziente Paziente { get; set; } = null!;

    public TipoPacchetto Tipo { get; set; }

    // Privato: da ImpostazioniListino. SSN: ereditati dalla Ricetta collegata.
    public int SeduteTotali { get; set; }
    public int SeduteResidue { get; set; }
    public decimal? Prezzo { get; set; }
    public DateOnly DataAcquisto { get; set; }
    public DateOnly? Scadenza { get; set; }

    // Valorizzato solo per Tipo == Ssn: la ricetta che ha generato questo ciclo.
    public int? RicettaId { get; set; }
    public Ricetta? Ricetta { get; set; }

    public ICollection<Appuntamento> Appuntamenti { get; set; } = [];
    public ICollection<Pagamento> Pagamenti { get; set; } = [];
}

public class Pagamento
{
    public int Id { get; set; }

    public int PazienteId { get; set; }
    public Paziente Paziente { get; set; } = null!;

    public decimal Importo { get; set; }
    public MetodoPagamento Metodo { get; set; }
    public StatoPagamento Stato { get; set; } = StatoPagamento.DaSaldare;

    // Origine tracciata: da cosa nasce questa voce (coerente con la Cassa della Coordinatrice).
    public string Origine { get; set; } = string.Empty;

    // Seconda dimensione della vista Entrate dell'Admin, incrociata col percorso
    // privato/SSN — mai tre categorie parallele (ARCHITETTURA.md). Sta sul Pagamento e non
    // sull'Appuntamento perché una terapia strumentale può essere venduta a ciclo di
    // applicazioni, senza un appuntamento proprio: è il caso della tecarterapia in cassa.
    public TipoPrestazione TipoPrestazione { get; set; } = TipoPrestazione.Manuale;

    public int? AcquistoPacchettoId { get; set; }
    public AcquistoPacchetto? AcquistoPacchetto { get; set; }
    public int? AppuntamentoId { get; set; }
    public Appuntamento? Appuntamento { get; set; }

    // In modalità test: nessuna chiave Stripe live collegata (vedi ARCHITETTURA.md, note demo).
    public string? RiferimentoStripe { get; set; }

    public DateTime? DataIncasso { get; set; }
}

// Ricevuta fiscale — numerazione progressiva annuale unica (chiuso il 12/09/2026,
// vedi requisiti.md), non per sezionale privato/SSN.
public class Ricevuta
{
    public int Id { get; set; }

    public int NumeroProgressivo { get; set; }
    public int Anno { get; set; }

    public int PagamentoId { get; set; }
    public Pagamento Pagamento { get; set; } = null!;

    public decimal Importo { get; set; }
    public bool ImpostaBolloApplicata { get; set; } // sopra € 77,47, esenzione IVA art. 10 n. 18
    public bool Stornata { get; set; }

    // Stato dell'export verso il Sistema TS — solo file da caricare altrove, nessuna
    // integrazione diretta (chiuso il 12/09/2026, vedi requisiti.md).
    public bool InviataSistemaTs { get; set; }
    public bool OpposizioneSistemaTs { get; set; }
}
