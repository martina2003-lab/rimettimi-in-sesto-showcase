using Microsoft.EntityFrameworkCore;
using RimettimiInSesto.Api.Data;
using RimettimiInSesto.Api.Models;

namespace RimettimiInSesto.Api.Services;

// Quando la Coordinatrice non può confermare lo slot richiesto, propone 2-3 alternative:
// il paziente ne sceglie una o rifiuta, e nel frattempo gli slot proposti restano bloccati
// (AvailabilityService li conta già occupati). Se non risponde entro la scadenza gli slot
// si liberano e la richiesta torna in carico alla Coordinatrice (requisiti.md).
//
// La scadenza è valutata alla lettura, non da un processo schedulato: in versione demo non
// esistono job in background (ARCHITETTURA.md, note demo), e una proposta scaduta non ha
// comunque effetti finché qualcuno non la guarda o non prova ad accettarla.
public class PropostaSlotService(ApplicationDbContext db, NotificaService notificaService)
{
    public async Task<PropostaSlotAlternativo> CreaAsync(int appuntamentoId, List<DateTime> slotProposti)
    {
        if (slotProposti.Count is < 1 or > 3)
        {
            throw new BookingValidationException("Si possono proporre da 1 a 3 slot alternativi.");
        }

        var appuntamento = await db.Appuntamenti.Include(a => a.Paziente)
            .FirstOrDefaultAsync(a => a.Id == appuntamentoId)
            ?? throw new BookingValidationException("Appuntamento non trovato.");
        if (appuntamento.Stato != StatoAppuntamento.Richiesto)
        {
            throw new BookingValidationException(
                $"Si propone un'alternativa solo su una richiesta ancora da decidere (questa è '{appuntamento.Stato}').");
        }

        var giaAperta = await db.ProposteSlotAlternativo
            .AnyAsync(p => p.AppuntamentoId == appuntamentoId && p.Esito == EsitoPropostaSlot.InAttesa);
        if (giaAperta)
        {
            throw new BookingValidationException("C'è già una proposta aperta per questa richiesta.");
        }

        var impostazioni = await db.ImpostazioniAgenda.FirstOrDefaultAsync() ?? new ImpostazioniAgenda();

        var proposta = new PropostaSlotAlternativo
        {
            AppuntamentoId = appuntamentoId,
            SlotProposti = slotProposti,
            Scadenza = DateTime.Now.AddHours(impostazioni.ValiditaPropostaSlotOre),
            Esito = EsitoPropostaSlot.InAttesa,
        };
        db.ProposteSlotAlternativo.Add(proposta);
        await db.SaveChangesAsync();

        var destinatario = appuntamento.Paziente.Email ?? appuntamento.Paziente.Telefono;
        if (!string.IsNullOrWhiteSpace(destinatario))
        {
            var elenco = string.Join(", ", slotProposti.Select(s => s.ToString("dd/MM/yyyy HH:mm")));
            await notificaService.NotificaAsync(TipoNotifica.Conferma, destinatario,
                $"Lo slot richiesto non è disponibile. Alternative proposte: {elenco}. " +
                $"Valide fino al {proposta.Scadenza:dd/MM/yyyy HH:mm}.",
                appuntamento.PazienteId, appuntamento.Id);
        }

        return proposta;
    }

    public async Task<PropostaSlotAlternativo> AccettaAsync(string utenteId, int propostaId, DateTime slotScelto)
    {
        var proposta = await CaricaPropostaDelPazienteAsync(utenteId, propostaId);

        if (!proposta.SlotProposti.Contains(slotScelto))
        {
            throw new BookingValidationException("Lo slot scelto non è tra quelli proposti.");
        }

        proposta.Esito = EsitoPropostaSlot.Accettata;
        proposta.SlotScelto = slotScelto;

        // Accettare un'alternativa conferma l'appuntamento: la decisione della Coordinatrice
        // è già stata presa quando ha proposto quegli orari, non serve una seconda conferma.
        proposta.Appuntamento.DataOra = slotScelto;
        proposta.Appuntamento.Stato = StatoAppuntamento.Confermato;
        await db.SaveChangesAsync();

        return proposta;
    }

    public async Task<PropostaSlotAlternativo> RifiutaAsync(string utenteId, int propostaId)
    {
        var proposta = await CaricaPropostaDelPazienteAsync(utenteId, propostaId);

        proposta.Esito = EsitoPropostaSlot.Rifiutata;
        // La richiesta originale decade con il rifiuto: gli slot proposti si liberano e
        // il paziente riparte da una nuova prenotazione, senza restare appeso a uno slot
        // che la Coordinatrice ha già detto di non poter dare.
        proposta.Appuntamento.Stato = StatoAppuntamento.Annullato;
        proposta.Appuntamento.MotivoAnnullamento = "Alternative proposte rifiutate dal paziente.";
        await db.SaveChangesAsync();

        return proposta;
    }

    private async Task<PropostaSlotAlternativo> CaricaPropostaDelPazienteAsync(string utenteId, int propostaId)
    {
        var proposta = await db.ProposteSlotAlternativo
            .Include(p => p.Appuntamento)
            .FirstOrDefaultAsync(p => p.Id == propostaId)
            ?? throw new BookingValidationException("Proposta non trovata.");

        var collegato = await db.UtentiPazienti.AnyAsync(up =>
            up.UtenteId == utenteId && up.PazienteId == proposta.Appuntamento.PazienteId);
        if (!collegato)
        {
            throw new BookingValidationException("Questa proposta non riguarda un paziente collegato all'utente.");
        }

        await ScadiSeNecessarioAsync(proposta);

        if (proposta.Esito != EsitoPropostaSlot.InAttesa)
        {
            throw new BookingValidationException($"Questa proposta non è più aperta (stato: {proposta.Esito}).");
        }

        return proposta;
    }

    // Marca scaduta una proposta che ha superato la sua validità, liberando di fatto gli
    // slot: da quel momento AvailabilityService non li conta più occupati.
    private async Task ScadiSeNecessarioAsync(PropostaSlotAlternativo proposta)
    {
        if (proposta.Esito == EsitoPropostaSlot.InAttesa && DateTime.Now > proposta.Scadenza)
        {
            proposta.Esito = EsitoPropostaSlot.Scaduta;
            await db.SaveChangesAsync();
        }
    }
}
