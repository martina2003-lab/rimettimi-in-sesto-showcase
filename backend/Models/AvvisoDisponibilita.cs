namespace RimettimiInSesto.Api.Models;

/// <summary>
/// Richiesta di essere spostato prima, se si libera qualcosa.
/// </summary>
/// <remarks>
/// Non è una coda di gente senza appuntamento, ed è la ragione per cui
/// <see cref="AppuntamentoId"/> è obbligatorio e non nullable: requisiti.md dice che il
/// paziente <em>prenota comunque</em> il primo slot disponibile e non resta mai senza un
/// appuntamento confermato — l'avviso è un desiderio in più, appeso a una prenotazione
/// che esiste già. Modellarla come una lista di attesa autonoma avrebbe reso possibile
/// esattamente la situazione che il progetto esclude.
/// </remarks>
public class AvvisoDisponibilita
{
    public int Id { get; set; }

    public int PazienteId { get; set; }
    public Paziente Paziente { get; set; } = null!;

    // L'appuntamento già confermato che il paziente vorrebbe anticipare.
    public int AppuntamentoId { get; set; }
    public Appuntamento Appuntamento { get; set; } = null!;

    public DateTime CreatoIl { get; set; }

    public StatoAvvisoDisponibilita Stato { get; set; } = StatoAvvisoDisponibilita.Attivo;

    // Quando la segreteria ha segnalato uno slot libero: serve a non avvisare due volte
    // per la stessa occasione e a far vedere in coda chi è già stato contattato.
    public DateTime? AvvisatoIl { get; set; }
}
