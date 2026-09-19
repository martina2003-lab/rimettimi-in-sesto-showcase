namespace RimettimiInSesto.Api.Models;

// Versione demo: MAI inviata davvero (nessun collegamento a Communication Services/Twilio/
// SendGrid — vedi ARCHITETTURA.md, "Note per il deploy demo"). Riga scritta qui = log,
// non un invio reale. Stato resta "DaInviare" finché nessun processo di demo la marca "Inviata".
public class Notifica
{
    public int Id { get; set; }

    public TipoNotifica Tipo { get; set; }
    public CanaleNotifica Canale { get; set; }
    public StatoNotifica Stato { get; set; } = StatoNotifica.DaInviare;

    public string Destinatario { get; set; } = string.Empty;
    public string Testo { get; set; } = string.Empty;
    public DateTime CreataIl { get; set; }

    public int? PazienteId { get; set; }
    public Paziente? Paziente { get; set; }
    public int? AppuntamentoId { get; set; }
    public Appuntamento? Appuntamento { get; set; }
}
