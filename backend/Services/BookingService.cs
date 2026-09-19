using Microsoft.EntityFrameworkCore;
using RimettimiInSesto.Api.Data;
using RimettimiInSesto.Api.Dtos;
using RimettimiInSesto.Api.Models;

namespace RimettimiInSesto.Api.Services;

public class BookingValidationException(string messaggio) : Exception(messaggio);

// Nessuna prenotazione si auto-conferma, e ogni richiesta blocca il suo slot (principio
// guida di CLAUDE.md). Unica eccezione: le prenotazioni create dalla Coordinatrice stessa
// (telefono/sportello), che nascono già "Confermato" — è lei stessa il confermatore.
public class BookingService(ApplicationDbContext db, AvailabilityService availabilityService, NotificaService notificaService)
{
    private const int PassoMinuti = 30;

    // Fra "lo slot è libero?" e "scrivo l'appuntamento" non c'era niente: due richieste
    // simultanee sullo stesso orario passavano entrambe il controllo e finivano tutte e due
    // in agenda. Qui il controllo e la scrittura diventano un gesto solo.
    //
    // È un lucchetto **di processo**: basta finché l'API gira su una sola istanza, che è la
    // forma del deploy previsto (un solo App Service, vedi ARCHITETTURA.md). Con più
    // istanze non varrebbe più, e servirebbe un lucchetto sul database — l'indice unico in
    // ApplicationDbContext resta comunque la rete di sicurezza per la collisione esatta.
    private static readonly SemaphoreSlim LucchettoPrenotazioni = new(1, 1);

    // Percorso Paziente: la richiesta nasce "Richiesto", mai auto-confermata. Verifica che
    // l'utente che chiama sia davvero collegato al Paziente per cui sta prenotando.
    // SSN senza ricetta è ammesso anche qui, non solo al telefono: requisiti.md tratta "la
    // porto alla prima seduta" come normale anche per chi prenota da sé online, non solo per
    // chi chiama. È il wizard (scelta esplicita "Prenota senza caricarla") a decidere se
    // RicettaId resta null, non questo servizio.
    public async Task<Appuntamento> RichiediAsync(string utenteId, CreaRichiestaRequest request)
    {
        var collegato = await db.UtentiPazienti
            .AnyAsync(up => up.UtenteId == utenteId && up.PazienteId == request.PazienteId);
        if (!collegato)
        {
            throw new BookingValidationException("Questo utente non è collegato al paziente indicato.");
        }

        return await CreaAppuntamentoAsync(request, StatoAppuntamento.Richiesto, ssnSenzaRicettaAmmesso: true);
    }

    // Percorso Coordinatrice: prenotazione telefonica/sportello, nasce già confermata.
    // È anche l'unico percorso in cui un SSN può nascere senza ricetta: al telefono la
    // ricetta non si può fotografare, arriva alla prima seduta (requisiti.md). Pretenderla
    // prima renderebbe impossibile prenotare al telefono un percorso convenzionato.
    public async Task<Appuntamento> PrenotaDirettoAsync(CreaRichiestaRequest request)
    {
        return await CreaAppuntamentoAsync(request, StatoAppuntamento.Confermato, ssnSenzaRicettaAmmesso: true);
    }

    public async Task<Appuntamento> ConfermaAsync(int appuntamentoId)
    {
        var appuntamento = await db.Appuntamenti.Include(a => a.Paziente).FirstOrDefaultAsync(a => a.Id == appuntamentoId)
            ?? throw new BookingValidationException("Appuntamento non trovato.");
        if (appuntamento.Stato != StatoAppuntamento.Richiesto)
        {
            throw new BookingValidationException($"Non si può confermare un appuntamento in stato '{appuntamento.Stato}'.");
        }

        appuntamento.Stato = StatoAppuntamento.Confermato;
        await db.SaveChangesAsync();

        await NotificaSePossibileAsync(appuntamento, TipoNotifica.Conferma,
            $"Appuntamento confermato per il {appuntamento.DataOra:dd/MM/yyyy HH:mm}.");

        return appuntamento;
    }

    public async Task<Appuntamento> RifiutaAsync(int appuntamentoId, string motivo)
    {
        var appuntamento = await db.Appuntamenti.Include(a => a.Paziente).FirstOrDefaultAsync(a => a.Id == appuntamentoId)
            ?? throw new BookingValidationException("Appuntamento non trovato.");
        if (appuntamento.Stato != StatoAppuntamento.Richiesto)
        {
            throw new BookingValidationException($"Non si può rifiutare un appuntamento in stato '{appuntamento.Stato}'.");
        }

        appuntamento.Stato = StatoAppuntamento.Annullato;
        appuntamento.MotivoAnnullamento = motivo;
        await db.SaveChangesAsync();

        await NotificaSePossibileAsync(appuntamento, TipoNotifica.Cancellazione,
            $"Richiesta del {appuntamento.DataOra:dd/MM/yyyy HH:mm} non confermata: {motivo}");

        return appuntamento;
    }

    // --- Appuntamenti impattati da un'assenza (Coordinatrice) ------------------------

    // Riassegnare a un collega invece di cancellare: il paziente tiene il suo orario e non
    // deve ricominciare da capo. Non c'è nessuna priorità automatica al dipendente libero —
    // requisiti.md la chiama "implicita", cioè un suggerimento a chi decide, non una regola
    // che sceglie da sé. Chi riceve l'appuntamento deve comunque essere davvero libero:
    // senza questo controllo si sposterebbe il problema sull'agenda di un altro.
    public async Task<Appuntamento> RiassegnaAsync(int appuntamentoId, int nuovoFisioterapistaId)
    {
        var appuntamento = await db.Appuntamenti.Include(a => a.Paziente)
            .FirstOrDefaultAsync(a => a.Id == appuntamentoId)
            ?? throw new BookingValidationException("Appuntamento non trovato.");

        if (appuntamento.Stato is not (StatoAppuntamento.Richiesto or StatoAppuntamento.Confermato))
        {
            throw new BookingValidationException($"Non si può riassegnare un appuntamento in stato '{appuntamento.Stato}'.");
        }

        if (appuntamento.FisioterapistaId == nuovoFisioterapistaId)
        {
            throw new BookingValidationException("L'appuntamento è già assegnato a questo fisioterapista.");
        }

        _ = await db.Fisioterapisti.FindAsync(nuovoFisioterapistaId)
            ?? throw new BookingValidationException("Fisioterapista non trovato.");

        await VerificaSlotLiberoAsync(nuovoFisioterapistaId, appuntamento.DataOra, appuntamento.DurataMinuti);

        appuntamento.FisioterapistaId = nuovoFisioterapistaId;
        await db.SaveChangesAsync();

        return appuntamento;
    }

    // La notifica NON parte da sé: la Coordinatrice dichiara se ha già avvisato il paziente
    // altrove — al telefono, tipicamente, che per una seduta di domani è l'unica cosa seria
    // da fare. È il senso del campo NotificaGiaDataAltrove, e il motivo per cui requisiti.md
    // vuole un'approvazione esplicita prima di ogni invio.
    public async Task<Appuntamento> AnnullaPerAssenzaAsync(
        int appuntamentoId, string motivo, bool notificaGiaDataAltrove)
    {
        var appuntamento = await db.Appuntamenti.Include(a => a.Paziente)
            .FirstOrDefaultAsync(a => a.Id == appuntamentoId)
            ?? throw new BookingValidationException("Appuntamento non trovato.");

        if (appuntamento.Stato is not (StatoAppuntamento.Richiesto or StatoAppuntamento.Confermato))
        {
            throw new BookingValidationException($"Non si può annullare un appuntamento in stato '{appuntamento.Stato}'.");
        }

        appuntamento.Stato = StatoAppuntamento.Annullato;
        appuntamento.MotivoAnnullamento = string.IsNullOrWhiteSpace(motivo)
            ? "Annullato per assenza del fisioterapista."
            : motivo.Trim();
        appuntamento.NotificaGiaDataAltrove = notificaGiaDataAltrove;

        // Una seduta annullata non consuma il pacchetto: solo il completamento lo scala.
        await db.SaveChangesAsync();

        if (!notificaGiaDataAltrove)
        {
            await NotificaSePossibileAsync(appuntamento, TipoNotifica.Cancellazione,
                $"Appuntamento del {appuntamento.DataOra:dd/MM/yyyy HH:mm} annullato: {appuntamento.MotivoAnnullamento}");
        }

        return appuntamento;
    }

    // --- Modifica e cancellazione lato Paziente -------------------------------------

    // Chiedere lo spostamento NON fa perdere lo slot già confermato: l'appuntamento resta
    // dov'è e la nuova data resta "richiesta" finché la segreteria non approva
    // (comportamento già deciso in mockup/paziente.html).
    public async Task<Appuntamento> RichiediModificaAsync(string utenteId, int appuntamentoId, DateTime nuovaDataOra)
    {
        var appuntamento = await CaricaAppuntamentoDelPazienteAsync(utenteId, appuntamentoId);

        if (appuntamento.Stato is not (StatoAppuntamento.Richiesto or StatoAppuntamento.Confermato))
        {
            throw new BookingValidationException($"Non si può spostare un appuntamento in stato '{appuntamento.Stato}'.");
        }

        await VerificaPreavvisoAsync(appuntamento.DataOra, "spostare");
        await VerificaFinestraTemporaleAsync(nuovaDataOra);
        await VerificaSlotLiberoAsync(appuntamento.FisioterapistaId, nuovaDataOra, appuntamento.DurataMinuti);

        appuntamento.ModificaRichiestaDataOra = nuovaDataOra;
        appuntamento.ModificaRichiestaIl = DateTime.Now;
        await db.SaveChangesAsync();

        return appuntamento;
    }

    public async Task<Appuntamento> ApprovaModificaAsync(int appuntamentoId)
    {
        var appuntamento = await db.Appuntamenti.Include(a => a.Paziente)
            .FirstOrDefaultAsync(a => a.Id == appuntamentoId)
            ?? throw new BookingValidationException("Appuntamento non trovato.");
        if (appuntamento.ModificaRichiestaDataOra is not { } nuovaDataOra)
        {
            throw new BookingValidationException("Non c'è nessuna richiesta di spostamento da approvare.");
        }

        appuntamento.DataOra = nuovaDataOra;
        appuntamento.ModificaRichiestaDataOra = null;
        appuntamento.ModificaRichiestaIl = null;
        appuntamento.Stato = StatoAppuntamento.Confermato;
        await db.SaveChangesAsync();

        await NotificaSePossibileAsync(appuntamento, TipoNotifica.Conferma,
            $"Spostamento approvato: nuovo appuntamento il {nuovaDataOra:dd/MM/yyyy HH:mm}.");

        return appuntamento;
    }

    // Il rifiuto lascia in piedi l'appuntamento originale: è tutto il senso di non
    // sovrascrivere DataOra quando la modifica viene chiesta.
    public async Task<Appuntamento> RifiutaModificaAsync(int appuntamentoId)
    {
        var appuntamento = await db.Appuntamenti.Include(a => a.Paziente)
            .FirstOrDefaultAsync(a => a.Id == appuntamentoId)
            ?? throw new BookingValidationException("Appuntamento non trovato.");
        if (appuntamento.ModificaRichiestaDataOra is null)
        {
            throw new BookingValidationException("Non c'è nessuna richiesta di spostamento da rifiutare.");
        }

        appuntamento.ModificaRichiestaDataOra = null;
        appuntamento.ModificaRichiestaIl = null;
        await db.SaveChangesAsync();

        await NotificaSePossibileAsync(appuntamento, TipoNotifica.Cancellazione,
            $"Spostamento non approvato: resta valido l'appuntamento del {appuntamento.DataOra:dd/MM/yyyy HH:mm}.");

        return appuntamento;
    }

    public async Task<Appuntamento> CancellaDaPazienteAsync(string utenteId, int appuntamentoId)
    {
        var appuntamento = await CaricaAppuntamentoDelPazienteAsync(utenteId, appuntamentoId);

        if (appuntamento.Stato is not (StatoAppuntamento.Richiesto or StatoAppuntamento.Confermato))
        {
            throw new BookingValidationException($"Non si può cancellare un appuntamento in stato '{appuntamento.Stato}'.");
        }

        await VerificaPreavvisoAsync(appuntamento.DataOra, "cancellare");

        appuntamento.Stato = StatoAppuntamento.Annullato;
        appuntamento.MotivoAnnullamento = "Cancellato dal paziente.";
        appuntamento.ModificaRichiestaDataOra = null;
        appuntamento.ModificaRichiestaIl = null;
        await db.SaveChangesAsync();

        return appuntamento;
    }

    // --- Chiusura della seduta lato Fisioterapista -----------------------------------

    // Una seduta completata è l'unico momento in cui il pacchetto (o il ciclo SSN) cala:
    // non alla prenotazione, che può ancora essere annullata, e non alla conferma.
    public async Task<Appuntamento> CompletaSedutaAsync(int appuntamentoId)
    {
        var appuntamento = await db.Appuntamenti.FindAsync(appuntamentoId)
            ?? throw new BookingValidationException("Appuntamento non trovato.");
        if (appuntamento.Stato != StatoAppuntamento.Confermato)
        {
            throw new BookingValidationException($"Si può completare solo un appuntamento confermato (questo è '{appuntamento.Stato}').");
        }

        appuntamento.Stato = StatoAppuntamento.Completato;
        await ScalaSedutaDalPacchettoAsync(appuntamento);
        await db.SaveChangesAsync();

        return appuntamento;
    }

    // Il no-show NON scala la seduta: se la seduta mancata si perda o si recuperi è una
    // decisione dello studio, diversa tra privato e SSN, e non risulta da nessun documento
    // raccolto finora. Meglio lasciarla fuori che inventarla — da confermare con lo studio.
    public async Task<Appuntamento> RegistraNoShowAsync(int appuntamentoId)
    {
        var appuntamento = await db.Appuntamenti.FindAsync(appuntamentoId)
            ?? throw new BookingValidationException("Appuntamento non trovato.");
        if (appuntamento.Stato != StatoAppuntamento.Confermato)
        {
            throw new BookingValidationException($"Si può segnare come no-show solo un appuntamento confermato (questo è '{appuntamento.Stato}').");
        }

        appuntamento.Stato = StatoAppuntamento.NoShow;
        await db.SaveChangesAsync();

        return appuntamento;
    }

    private async Task ScalaSedutaDalPacchettoAsync(Appuntamento appuntamento)
    {
        if (appuntamento.AcquistoPacchettoId is not { } pacchettoId)
        {
            return; // seduta singola fuori pacchetto: nulla da scalare
        }

        var pacchetto = await db.AcquistiPacchetto.FindAsync(pacchettoId);
        if (pacchetto is not null && pacchetto.SeduteResidue > 0)
        {
            pacchetto.SeduteResidue--;
        }
    }

    // "Modifica/cancellazione consentita entro un preavviso minimo configurabile dalla
    // Coordinatrice; sotto quella soglia il paziente deve contattare lo studio" (requisiti.md).
    private async Task VerificaPreavvisoAsync(DateTime dataOraAppuntamento, string azione)
    {
        var impostazioni = await db.ImpostazioniAgenda.FirstOrDefaultAsync() ?? new ImpostazioniAgenda();
        var limite = dataOraAppuntamento.AddHours(-impostazioni.PreavvisoMinimoCancellazioneOre);

        if (DateTime.Now > limite)
        {
            throw new BookingValidationException(
                $"Si può {azione} un appuntamento fino a {impostazioni.PreavvisoMinimoCancellazioneOre} ore prima. " +
                "Oltre quella soglia occorre contattare direttamente lo studio.");
        }
    }

    private async Task<Appuntamento> CaricaAppuntamentoDelPazienteAsync(string utenteId, int appuntamentoId)
    {
        var appuntamento = await db.Appuntamenti.Include(a => a.Paziente)
            .FirstOrDefaultAsync(a => a.Id == appuntamentoId)
            ?? throw new BookingValidationException("Appuntamento non trovato.");

        var collegato = await db.UtentiPazienti
            .AnyAsync(up => up.UtenteId == utenteId && up.PazienteId == appuntamento.PazienteId);
        if (!collegato)
        {
            throw new BookingValidationException("Questo appuntamento non appartiene a un paziente collegato all'utente.");
        }

        return appuntamento;
    }

    // "Sempre con approvazione esplicita della Coordinatrice prima dell'invio" per le
    // notifiche di annullamento (principio guida) — qui l'approvazione È l'azione stessa
    // della Coordinatrice (conferma/rifiuta), non un passaggio ulteriore da inventare.
    private async Task NotificaSePossibileAsync(Appuntamento appuntamento, TipoNotifica tipo, string testo)
    {
        var destinatario = appuntamento.Paziente.Email ?? appuntamento.Paziente.Telefono;
        if (!string.IsNullOrWhiteSpace(destinatario))
        {
            await notificaService.NotificaAsync(tipo, destinatario, testo, appuntamento.PazienteId, appuntamento.Id);
        }
    }

    private async Task<Appuntamento> CreaAppuntamentoAsync(
        CreaRichiestaRequest request, StatoAppuntamento statoIniziale, bool ssnSenzaRicettaAmmesso = false)
    {
        var paziente = await db.Pazienti.FindAsync(request.PazienteId)
            ?? throw new BookingValidationException("Paziente non trovato.");
        var fisioterapista = await db.Fisioterapisti.FindAsync(request.FisioterapistaId)
            ?? throw new BookingValidationException("Fisioterapista non trovato.");

        await VerificaFinestraTemporaleAsync(request.DataOra);

        int durataMinuti;
        Ricetta? ricetta = null;

        if (request.Percorso == PercorsoAppuntamento.Ssn && request.RicettaId is null && ssnSenzaRicettaAmmesso)
        {
            // Ricetta ancora da consegnare: nessun distretto da cui ricavare la durata,
            // quindi la sceglie la Coordinatrice su quel che le dice il paziente. Alla
            // validazione la ricetta vera porterà con sé la durata corretta.
            durataMinuti = DurataPrivataValidata(request.DurataMinuti);
        }
        else if (request.Percorso == PercorsoAppuntamento.Ssn)
        {
            if (request.RicettaId is null)
            {
                throw new BookingValidationException("Il percorso SSN richiede una ricetta.");
            }
            ricetta = await db.Ricette.FindAsync(request.RicettaId)
                ?? throw new BookingValidationException("Ricetta non trovata.");

            // La ricetta deve essere DI QUESTO paziente: senza questo controllo si potrebbero
            // scaricare sedute sul ciclo SSN di un'altra persona (il controllo di titolarità
            // sul PazienteId non basta, perché le risorse collegate hanno un proprietario proprio).
            if (ricetta.PazienteId != request.PazienteId)
            {
                throw new BookingValidationException("La ricetta indicata non appartiene a questo paziente.");
            }

            // La durata NON è una scelta del paziente: 30 minuti per ogni distretto prescritto.
            var numeroDistretti = string.IsNullOrWhiteSpace(ricetta.DistrettiCorporei)
                ? 1
                : ricetta.DistrettiCorporei.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length;
            durataMinuti = numeroDistretti * PassoMinuti;
        }
        else
        {
            durataMinuti = DurataPrivataValidata(request.DurataMinuti);
        }

        // Stessa ragione della ricetta: un pacchetto ha un proprietario, e senza questo
        // controllo si potrebbero consumare le sedute residue di un altro paziente.
        if (request.AcquistoPacchettoId is { } pacchettoId)
        {
            var pacchetto = await db.AcquistiPacchetto.FindAsync(pacchettoId)
                ?? throw new BookingValidationException("Pacchetto non trovato.");
            if (pacchetto.PazienteId != request.PazienteId)
            {
                throw new BookingValidationException("Il pacchetto indicato non appartiene a questo paziente.");
            }
        }

        var appuntamento = new Appuntamento
        {
            PazienteId = request.PazienteId,
            FisioterapistaId = request.FisioterapistaId,
            DataOra = request.DataOra,
            DurataMinuti = durataMinuti,
            Stato = statoIniziale,
            Percorso = request.Percorso,
            AcquistoPacchettoId = request.AcquistoPacchettoId,
            RicettaId = ricetta?.Id,
        };

        await LucchettoPrenotazioni.WaitAsync();
        try
        {
            await VerificaSlotLiberoAsync(request.FisioterapistaId, request.DataOra, durataMinuti);

            db.Appuntamenti.Add(appuntamento);
            try
            {
                await db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // L'indice unico ha parato una collisione esatta che il controllo non aveva
                // visto: succede solo se la scrittura arriva da un'altra istanza.
                db.Entry(appuntamento).State = EntityState.Detached;
                throw new BookingValidationException(
                    "Quell'orario è stato appena preso da qualcun altro: scegline un altro.");
            }
        }
        finally
        {
            LucchettoPrenotazioni.Release();
        }

        return appuntamento;
    }

    private static int DurataPrivataValidata(int? durataMinuti)
    {
        if (durataMinuti is not (30 or 60 or 90))
        {
            throw new BookingValidationException("La durata deve essere 30, 60 o 90 minuti.");
        }
        return durataMinuti.Value;
    }

    // "Prenotabile entro una finestra massima configurabile dalla Coordinatrice" (requisiti.md):
    // il parametro esisteva in ImpostazioniAgenda ma non era applicato da nessuna parte, quindi
    // si poteva prenotare nel passato o a anni di distanza.
    private async Task VerificaFinestraTemporaleAsync(DateTime dataOra)
    {
        if (dataOra < DateTime.Now)
        {
            throw new BookingValidationException("Non si può prenotare un appuntamento nel passato.");
        }

        var impostazioni = await db.ImpostazioniAgenda.FirstOrDefaultAsync() ?? new ImpostazioniAgenda();
        var limiteMassimo = DateTime.Now.AddDays(impostazioni.FinestraPrenotazioneMassimaGiorni);
        if (dataOra > limiteMassimo)
        {
            throw new BookingValidationException(
                $"Si può prenotare al massimo con {impostazioni.FinestraPrenotazioneMassimaGiorni} giorni di anticipo.");
        }
    }

    // Ricalcola la disponibilità reale al momento della prenotazione (non fidarsi di uno
    // stato letto in precedenza dal client): copre sia i giorni chiusi/assenze sia gli slot
    // già occupati da un'altra richiesta/proposta nel frattempo.
    private async Task VerificaSlotLiberoAsync(int fisioterapistaId, DateTime dataOra, int durataMinuti)
    {
        var giorno = DateOnly.FromDateTime(dataOra);
        var disponibilita = await availabilityService.CalcolaDisponibilitaGiornoAsync(fisioterapistaId, giorno);

        if (disponibilita.Chiuso)
        {
            throw new BookingValidationException(disponibilita.MotivoChiusura ?? "Il fisioterapista non è disponibile in questa data.");
        }

        var orarioRichiesto = TimeOnly.FromDateTime(dataOra);
        var numeroSlotNecessari = durataMinuti / PassoMinuti;

        for (var i = 0; i < numeroSlotNecessari; i++)
        {
            var orarioSlot = orarioRichiesto.AddMinutes(i * PassoMinuti);
            var slot = disponibilita.Slots.FirstOrDefault(s => s.Orario == orarioSlot);
            if (slot is null || !slot.Libero)
            {
                throw new BookingValidationException($"Lo slot delle {orarioSlot:HH:mm} non è disponibile.");
            }
        }
    }
}
