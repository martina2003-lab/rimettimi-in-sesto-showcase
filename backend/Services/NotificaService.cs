using Microsoft.Extensions.Logging;
using RimettimiInSesto.Api.Data;
using RimettimiInSesto.Api.Models;

namespace RimettimiInSesto.Api.Services;

// MAI un invio reale — nessun collegamento a Communication Services/Twilio/SendGrid
// (ARCHITETTURA.md, "Note per il deploy demo"). Scrivere qui è l'intero ciclo di vita
// della notifica in questa fase: una riga in tabella + un log, non un provider esterno.
// Quando la fase demo finirà, questo è il punto in cui inserire un provider vero,
// senza toccare i chiamanti (BookingService, ecc.).
public class NotificaService(ApplicationDbContext db, ILogger<NotificaService> logger)
{
    public async Task NotificaAsync(
        TipoNotifica tipo, string destinatario, string testo, int? pazienteId = null, int? appuntamentoId = null)
    {
        var canale = destinatario.Contains('@') ? CanaleNotifica.Email : CanaleNotifica.Sms;

        var notifica = new Notifica
        {
            Tipo = tipo, Canale = canale, Destinatario = destinatario, Testo = testo,
            CreataIl = DateTime.UtcNow, PazienteId = pazienteId, AppuntamentoId = appuntamentoId,
            // "Inviata" qui significa solo "registrata nel log demo" — l'intero ciclo di vita
            // della notifica in questa fase, non una conferma di recapito reale.
            Stato = StatoNotifica.Inviata,
        };
        db.Notifiche.Add(notifica);
        await db.SaveChangesAsync();

        logger.LogInformation(
            "[NOTIFICA SIMULATA] {Canale} a {Destinatario} — {Tipo}: {Testo}",
            canale, destinatario, tipo, testo);
    }
}
