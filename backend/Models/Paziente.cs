namespace RimettimiInSesto.Api.Models;

// La scheda paziente non presuppone un account (principio guida di CLAUDE.md):
// UtenteId è nullable perché la Coordinatrice crea schede per chi prenota al telefono
// prima che il paziente si registri. Il collegamento avviene poi per codice fiscale.
public class Paziente
{
    public int Id { get; set; }

    public string Nome { get; set; } = string.Empty;
    public string Cognome { get; set; } = string.Empty;

    // Nullable: una scheda provvisoria può nascere senza CF ancora confermato.
    public string? CodiceFiscale { get; set; }
    public string? Telefono { get; set; }
    public string? Email { get; set; }
    public DateOnly? DataNascita { get; set; }

    public StatoPaziente Stato { get; set; } = StatoPaziente.Provvisorio;

    // NIENTE FisioterapistaDiRiferimento qui — principio guida non negoziabile.
    // Il "fisioterapista abituale" è calcolato a runtime dallo storico Appuntamento.

    public ICollection<UtentePaziente> UtentiCollegati { get; set; } = [];
    public ICollection<Appuntamento> Appuntamenti { get; set; } = [];
    public ICollection<NotaOperativa> NoteOperative { get; set; } = [];
    public ICollection<Consenso> Consensi { get; set; } = [];
    public ICollection<CartellaClinica> Cicli { get; set; } = [];
    public ICollection<Ricetta> Ricette { get; set; } = [];
    public ICollection<AcquistoPacchetto> Pacchetti { get; set; } = [];
    public Controindicazioni? Controindicazioni { get; set; }
}

// Tabella di collegamento Utente <-> Paziente, con il titolo con cui l'utente agisce
// (sé stesso / genitore / tutore) — richiesto dai moduli di consenso cartacei.
public class UtentePaziente
{
    public int Id { get; set; }

    public string UtenteId { get; set; } = string.Empty;
    public ApplicationUser Utente { get; set; } = null!;

    public int PazienteId { get; set; }
    public Paziente Paziente { get; set; } = null!;

    public TitoloRelazione Titolo { get; set; }
}

// Note della Coordinatrice sul paziente (es. "spesso in ritardo") — separate dalla
// cartella clinica per non violare la regola di accesso ai dati sanitari (mai clinico).
public class NotaOperativa
{
    public int Id { get; set; }

    public int PazienteId { get; set; }
    public Paziente Paziente { get; set; } = null!;

    public string Testo { get; set; } = string.Empty;
    public string Autore { get; set; } = string.Empty;
    public DateTime Data { get; set; }
}
