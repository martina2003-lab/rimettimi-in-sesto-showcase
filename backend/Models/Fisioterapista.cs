namespace RimettimiInSesto.Api.Models;

public class Fisioterapista
{
    public int Id { get; set; }

    public string UtenteId { get; set; } = string.Empty;
    public ApplicationUser Utente { get; set; } = null!;

    // Testo libero, gestito dal fisioterapista stesso, mostrato al paziente in fase di scelta.
    public string? Bio { get; set; }
    public string? FotoProfiloUrl { get; set; }

    // Dato interno, mai esposto al paziente.
    public TipoContratto TipoContratto { get; set; }

    // Monte ore settimanale da contratto (CCNL Sanità Privata per i dipendenti). Serve al
    // tasso di saturazione nella vista Admin: senza, le ore erogate non hanno un termine
    // di paragone. Per i collaboratori è un riferimento indicativo, non un obbligo.
    public decimal OreSettimanaliContratto { get; set; }

    // Testo libero per i pattern di disponibilità/orari (dipendenti: da contratto + permessi
    // approvati; collaboratori: gestita autonomamente) — niente modello a slot fisso qui,
    // la disponibilità reale emerge dal confronto con Appuntamento/AssenzaFisioterapista/ChiusuraStudio.
    public string? PatternDisponibilita { get; set; }

    public ICollection<Appuntamento> Appuntamenti { get; set; } = [];
    public ICollection<AssenzaFisioterapista> Assenze { get; set; } = [];
}
