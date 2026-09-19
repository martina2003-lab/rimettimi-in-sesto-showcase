using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RimettimiInSesto.Api.Data;
using RimettimiInSesto.Api.Dtos;
using RimettimiInSesto.Api.Models;
using RimettimiInSesto.Api.Services;
using Xunit;

namespace RimettimiInSesto.Api.Tests;

// Nati da una revisione del backend (12 settembre 2026) che ha trovato quattro buchi reali,
// tutti verificati sfruttandoli davvero via HTTP prima di correggerli: si poteva prenotare
// addebitando il pacchetto di un altro paziente, usare la ricetta SSN di un altro paziente,
// prenotare nel passato e prenotare a anni di distanza ignorando la finestra massima.
// Il controllo di titolarità sul PazienteId non bastava: anche le risorse collegate
// (pacchetto, ricetta) hanno un proprietario, e vanno verificate a parte.
public class BookingServiceTests
{
    private static ApplicationDbContext NuovoDbContext() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static BookingService NuovoService(ApplicationDbContext db) =>
        new(db, new AvailabilityService(db), new NotificaService(db, NullLogger<NotificaService>.Instance));

    // Un lunedì futuro alle 10:00, dentro la finestra di prenotazione e in orario di apertura.
    private static DateTime ProssimoLunedi()
    {
        var giorno = DateTime.Today.AddDays(7);
        while (giorno.DayOfWeek != DayOfWeek.Monday)
        {
            giorno = giorno.AddDays(1);
        }
        return giorno.AddHours(10);
    }

    private static async Task<(ApplicationDbContext Db, Paziente Marco, Paziente Anna, Fisioterapista Fisio, string UtenteMarco)>
        ScenarioBase()
    {
        var db = NuovoDbContext();

        var utenteMarco = new ApplicationUser { Id = "utente-marco", Nome = "Marco", Cognome = "Bianchi", Ruolo = Ruolo.Paziente };
        var marco = new Paziente { Nome = "Marco", Cognome = "Bianchi", Stato = StatoPaziente.ConsensoRaccolto };
        var anna = new Paziente { Nome = "Anna", Cognome = "Colombo", Stato = StatoPaziente.ConsensoRaccolto };
        var fisio = new Fisioterapista { UtenteId = "utente-fisio", TipoContratto = TipoContratto.Dipendente };

        db.Users.Add(utenteMarco);
        db.Pazienti.AddRange(marco, anna);
        db.Fisioterapisti.Add(fisio);
        await db.SaveChangesAsync();

        db.UtentiPazienti.Add(new UtentePaziente
        {
            UtenteId = utenteMarco.Id, PazienteId = marco.Id, Titolo = TitoloRelazione.SeStesso,
        });
        await db.SaveChangesAsync();

        return (db, marco, anna, fisio, utenteMarco.Id);
    }

    [Fact]
    public async Task Rifiuta_una_prenotazione_che_addebita_il_pacchetto_di_un_altro_paziente()
    {
        var (db, marco, anna, fisio, utenteMarco) = await ScenarioBase();
        var pacchettoDiAnna = new AcquistoPacchetto
        {
            PazienteId = anna.Id, Tipo = TipoPacchetto.Privato,
            SeduteTotali = 10, SeduteResidue = 2, DataAcquisto = DateOnly.FromDateTime(DateTime.Today),
        };
        db.AcquistiPacchetto.Add(pacchettoDiAnna);
        await db.SaveChangesAsync();

        var service = NuovoService(db);
        var richiesta = new CreaRichiestaRequest(
            marco.Id, fisio.Id, ProssimoLunedi(), PercorsoAppuntamento.Privato, 30, pacchettoDiAnna.Id, null);

        var eccezione = await Assert.ThrowsAsync<BookingValidationException>(
            () => service.RichiediAsync(utenteMarco, richiesta));
        Assert.Contains("non appartiene a questo paziente", eccezione.Message);
        Assert.Empty(await db.Appuntamenti.ToListAsync());
    }

    [Fact]
    public async Task Rifiuta_una_prenotazione_SSN_sulla_ricetta_di_un_altro_paziente()
    {
        var (db, marco, anna, fisio, utenteMarco) = await ScenarioBase();
        var ricettaDiAnna = new Ricetta
        {
            PazienteId = anna.Id, DataEmissione = DateOnly.FromDateTime(DateTime.Today),
            NumeroSeduteProscritte = 10, DistrettiCorporei = "Arto superiore destro",
        };
        db.Ricette.Add(ricettaDiAnna);
        await db.SaveChangesAsync();

        var service = NuovoService(db);
        var richiesta = new CreaRichiestaRequest(
            marco.Id, fisio.Id, ProssimoLunedi(), PercorsoAppuntamento.Ssn, null, null, ricettaDiAnna.Id);

        var eccezione = await Assert.ThrowsAsync<BookingValidationException>(
            () => service.RichiediAsync(utenteMarco, richiesta));
        Assert.Contains("non appartiene a questo paziente", eccezione.Message);
        Assert.Empty(await db.Appuntamenti.ToListAsync());
    }

    [Fact]
    public async Task Rifiuta_una_prenotazione_nel_passato()
    {
        var (db, marco, _, fisio, utenteMarco) = await ScenarioBase();
        var service = NuovoService(db);
        var richiesta = new CreaRichiestaRequest(
            marco.Id, fisio.Id, new DateTime(2020, 1, 1, 9, 0, 0), PercorsoAppuntamento.Privato, 30, null, null);

        var eccezione = await Assert.ThrowsAsync<BookingValidationException>(
            () => service.RichiediAsync(utenteMarco, richiesta));
        Assert.Contains("nel passato", eccezione.Message);
    }

    [Fact]
    public async Task Rifiuta_una_prenotazione_oltre_la_finestra_massima_configurata()
    {
        var (db, marco, _, fisio, utenteMarco) = await ScenarioBase();
        db.ImpostazioniAgenda.Add(new ImpostazioniAgenda { FinestraPrenotazioneMassimaGiorni = 60 });
        await db.SaveChangesAsync();

        var service = NuovoService(db);
        var richiesta = new CreaRichiestaRequest(
            marco.Id, fisio.Id, DateTime.Today.AddYears(3).AddHours(10), PercorsoAppuntamento.Privato, 30, null, null);

        var eccezione = await Assert.ThrowsAsync<BookingValidationException>(
            () => service.RichiediAsync(utenteMarco, richiesta));
        Assert.Contains("60 giorni", eccezione.Message);
    }

    [Fact]
    public async Task Chiedere_uno_spostamento_non_fa_perdere_lo_slot_gia_confermato()
    {
        // Il comportamento distintivo deciso in mockup/paziente.html: finché la segreteria
        // non approva, il paziente tiene il suo orario. Facile da rompere "semplificando"
        // con un DataOra sovrascritto, per questo è coperto da un test.
        var (db, marco, _, fisio, utenteMarco) = await ScenarioBase();
        var service = NuovoService(db);
        var originale = ProssimoLunedi();

        var appuntamento = await service.RichiediAsync(utenteMarco, new CreaRichiestaRequest(
            marco.Id, fisio.Id, originale, PercorsoAppuntamento.Privato, 30, null, null));
        await service.ConfermaAsync(appuntamento.Id);

        await service.RichiediModificaAsync(utenteMarco, appuntamento.Id, originale.AddHours(2));

        var dopo = await db.Appuntamenti.FindAsync(appuntamento.Id);
        Assert.Equal(originale, dopo!.DataOra);
        Assert.Equal(StatoAppuntamento.Confermato, dopo.Stato);
        Assert.Equal(originale.AddHours(2), dopo.ModificaRichiestaDataOra);
    }

    [Fact]
    public async Task Rifiutare_lo_spostamento_lascia_in_piedi_lappuntamento_originale()
    {
        var (db, marco, _, fisio, utenteMarco) = await ScenarioBase();
        var service = NuovoService(db);
        var originale = ProssimoLunedi();

        var appuntamento = await service.RichiediAsync(utenteMarco, new CreaRichiestaRequest(
            marco.Id, fisio.Id, originale, PercorsoAppuntamento.Privato, 30, null, null));
        await service.ConfermaAsync(appuntamento.Id);
        await service.RichiediModificaAsync(utenteMarco, appuntamento.Id, originale.AddHours(2));

        await service.RifiutaModificaAsync(appuntamento.Id);

        var dopo = await db.Appuntamenti.FindAsync(appuntamento.Id);
        Assert.Equal(originale, dopo!.DataOra);
        Assert.Equal(StatoAppuntamento.Confermato, dopo.Stato);
        Assert.Null(dopo.ModificaRichiestaDataOra);
    }

    [Fact]
    public async Task Completare_una_seduta_scala_una_seduta_dal_pacchetto()
    {
        var (db, marco, _, fisio, utenteMarco) = await ScenarioBase();
        var pacchetto = new AcquistoPacchetto
        {
            PazienteId = marco.Id, Tipo = TipoPacchetto.Privato,
            SeduteTotali = 10, SeduteResidue = 6, DataAcquisto = DateOnly.FromDateTime(DateTime.Today),
        };
        db.AcquistiPacchetto.Add(pacchetto);
        await db.SaveChangesAsync();

        var service = NuovoService(db);
        var appuntamento = await service.RichiediAsync(utenteMarco, new CreaRichiestaRequest(
            marco.Id, fisio.Id, ProssimoLunedi(), PercorsoAppuntamento.Privato, 30, pacchetto.Id, null));
        await service.ConfermaAsync(appuntamento.Id);

        await service.CompletaSedutaAsync(appuntamento.Id);

        Assert.Equal(5, (await db.AcquistiPacchetto.FindAsync(pacchetto.Id))!.SeduteResidue);
    }

    [Fact]
    public async Task Un_no_show_non_scala_la_seduta()
    {
        // Decisione deliberata: se una seduta mancata si perda o si recuperi cambia tra
        // privato e SSN e non risulta da nessun documento raccolto. Da confermare con lo studio.
        var (db, marco, _, fisio, utenteMarco) = await ScenarioBase();
        var pacchetto = new AcquistoPacchetto
        {
            PazienteId = marco.Id, Tipo = TipoPacchetto.Privato,
            SeduteTotali = 10, SeduteResidue = 6, DataAcquisto = DateOnly.FromDateTime(DateTime.Today),
        };
        db.AcquistiPacchetto.Add(pacchetto);
        await db.SaveChangesAsync();

        var service = NuovoService(db);
        var appuntamento = await service.RichiediAsync(utenteMarco, new CreaRichiestaRequest(
            marco.Id, fisio.Id, ProssimoLunedi(), PercorsoAppuntamento.Privato, 30, pacchetto.Id, null));
        await service.ConfermaAsync(appuntamento.Id);

        await service.RegistraNoShowAsync(appuntamento.Id);

        Assert.Equal(6, (await db.AcquistiPacchetto.FindAsync(pacchetto.Id))!.SeduteResidue);
    }

    [Fact]
    public async Task Rifiuta_la_cancellazione_sotto_il_preavviso_minimo()
    {
        var (db, marco, _, fisio, utenteMarco) = await ScenarioBase();
        db.ImpostazioniAgenda.Add(new ImpostazioniAgenda { PreavvisoMinimoCancellazioneOre = 24 });
        await db.SaveChangesAsync();

        // Appuntamento creato direttamente: prenotarlo dal servizio richiederebbe uno slot
        // in orario di apertura, mentre qui serve proprio un orario a ridosso di adesso.
        var appuntamento = new Appuntamento
        {
            PazienteId = marco.Id, FisioterapistaId = fisio.Id,
            DataOra = DateTime.Now.AddHours(3), DurataMinuti = 30,
            Stato = StatoAppuntamento.Confermato, Percorso = PercorsoAppuntamento.Privato,
        };
        db.Appuntamenti.Add(appuntamento);
        await db.SaveChangesAsync();

        var service = NuovoService(db);
        var eccezione = await Assert.ThrowsAsync<BookingValidationException>(
            () => service.CancellaDaPazienteAsync(utenteMarco, appuntamento.Id));

        Assert.Contains("24 ore prima", eccezione.Message);
        Assert.Equal(StatoAppuntamento.Confermato, (await db.Appuntamenti.FindAsync(appuntamento.Id))!.Stato);
    }

    [Fact]
    public async Task Un_utente_non_puo_spostare_lappuntamento_di_un_altro_paziente()
    {
        var (db, _, anna, fisio, utenteMarco) = await ScenarioBase();
        var appuntamentoDiAnna = new Appuntamento
        {
            PazienteId = anna.Id, FisioterapistaId = fisio.Id,
            DataOra = ProssimoLunedi(), DurataMinuti = 30,
            Stato = StatoAppuntamento.Confermato, Percorso = PercorsoAppuntamento.Privato,
        };
        db.Appuntamenti.Add(appuntamentoDiAnna);
        await db.SaveChangesAsync();

        var service = NuovoService(db);
        var eccezione = await Assert.ThrowsAsync<BookingValidationException>(
            () => service.RichiediModificaAsync(utenteMarco, appuntamentoDiAnna.Id, ProssimoLunedi().AddHours(1)));

        Assert.Contains("non appartiene", eccezione.Message);
    }

    [Fact]
    public async Task Accetta_una_prenotazione_valida_con_il_proprio_pacchetto()
    {
        var (db, marco, _, fisio, utenteMarco) = await ScenarioBase();
        var pacchettoDiMarco = new AcquistoPacchetto
        {
            PazienteId = marco.Id, Tipo = TipoPacchetto.Privato,
            SeduteTotali = 10, SeduteResidue = 6, DataAcquisto = DateOnly.FromDateTime(DateTime.Today),
        };
        db.AcquistiPacchetto.Add(pacchettoDiMarco);
        await db.SaveChangesAsync();

        var service = NuovoService(db);
        var richiesta = new CreaRichiestaRequest(
            marco.Id, fisio.Id, ProssimoLunedi(), PercorsoAppuntamento.Privato, 30, pacchettoDiMarco.Id, null);

        var appuntamento = await service.RichiediAsync(utenteMarco, richiesta);

        // Nessuna auto-conferma: nasce sempre "Richiesto" (principio guida).
        Assert.Equal(StatoAppuntamento.Richiesto, appuntamento.Stato);
        Assert.Equal(pacchettoDiMarco.Id, appuntamento.AcquistoPacchettoId);
    }

    // La ricetta non si può fotografare durante una telefonata: pretenderla prima renderebbe
    // impossibile prenotare al telefono un percorso convenzionato (requisiti.md).
    [Fact]
    public async Task La_coordinatrice_puo_prenotare_un_SSN_al_telefono_senza_ricetta()
    {
        var (db, marco, _, fisio, _) = await ScenarioBase();
        var service = NuovoService(db);
        var richiesta = new CreaRichiestaRequest(
            marco.Id, fisio.Id, ProssimoLunedi(), PercorsoAppuntamento.Ssn, 60, null, null);

        var appuntamento = await service.PrenotaDirettoAsync(richiesta);

        Assert.Equal(StatoAppuntamento.Confermato, appuntamento.Stato);
        Assert.Null(appuntamento.RicettaId);
        // Senza distretti prescritti da cui ricavarla, vale la durata concordata a voce.
        Assert.Equal(60, appuntamento.DurataMinuti);
    }

    // Cambiato il 14/09/2026: il paziente può caricare online i dati/foto della propria
    // ricetta (requisiti.md) ma può anche scegliere esplicitamente di portarla alla prima
    // seduta, esattamente come al telefono — non è più un caso riservato alla Coordinatrice.
    [Fact]
    public async Task Il_paziente_puo_prenotare_un_SSN_senza_ricetta_dal_portale()
    {
        var (db, marco, _, fisio, utenteMarco) = await ScenarioBase();
        var service = NuovoService(db);
        var richiesta = new CreaRichiestaRequest(
            marco.Id, fisio.Id, ProssimoLunedi(), PercorsoAppuntamento.Ssn, 60, null, null);

        var appuntamento = await service.RichiediAsync(utenteMarco, richiesta);

        Assert.Null(appuntamento.RicettaId);
        Assert.Equal(StatoAppuntamento.Richiesto, appuntamento.Stato);
    }

    // Riassegnare un appuntamento a un collega non deve spostare il problema sull'agenda di
    // un altro: chi subentra dev'essere davvero libero in quell'orario.
    [Fact]
    public async Task Rifiuta_la_riassegnazione_a_un_collega_gia_occupato()
    {
        var (db, marco, anna, ricci, _) = await ScenarioBase();
        var conti = new Fisioterapista { UtenteId = "utente-conti", TipoContratto = TipoContratto.Dipendente };
        db.Fisioterapisti.Add(conti);
        await db.SaveChangesAsync();

        var quando = ProssimoLunedi();
        var daSpostare = new Appuntamento
        {
            PazienteId = marco.Id, FisioterapistaId = ricci.Id, DataOra = quando, DurataMinuti = 60,
            Stato = StatoAppuntamento.Confermato, Percorso = PercorsoAppuntamento.Privato,
        };
        var giaInAgendaDiConti = new Appuntamento
        {
            PazienteId = anna.Id, FisioterapistaId = conti.Id, DataOra = quando, DurataMinuti = 60,
            Stato = StatoAppuntamento.Confermato, Percorso = PercorsoAppuntamento.Privato,
        };
        db.Appuntamenti.AddRange(daSpostare, giaInAgendaDiConti);
        await db.SaveChangesAsync();

        var service = NuovoService(db);
        await Assert.ThrowsAsync<BookingValidationException>(
            () => service.RiassegnaAsync(daSpostare.Id, conti.Id));

        Assert.Equal(ricci.Id, daSpostare.FisioterapistaId);
    }

    // Un appuntamento annullato perché il terapista non c'è non deve costare una seduta al
    // paziente: solo il completamento scala il pacchetto.
    [Fact]
    public async Task Annullare_per_assenza_non_scala_il_pacchetto()
    {
        var (db, marco, _, ricci, _) = await ScenarioBase();
        var pacchetto = new AcquistoPacchetto
        {
            PazienteId = marco.Id, Tipo = TipoPacchetto.Privato,
            SeduteTotali = 10, SeduteResidue = 6, DataAcquisto = DateOnly.FromDateTime(DateTime.Today),
        };
        db.AcquistiPacchetto.Add(pacchetto);
        await db.SaveChangesAsync();

        var appuntamento = new Appuntamento
        {
            PazienteId = marco.Id, FisioterapistaId = ricci.Id,
            DataOra = ProssimoLunedi(), DurataMinuti = 60,
            Stato = StatoAppuntamento.Confermato, Percorso = PercorsoAppuntamento.Privato,
            AcquistoPacchettoId = pacchetto.Id,
        };
        db.Appuntamenti.Add(appuntamento);
        await db.SaveChangesAsync();

        var service = NuovoService(db);
        await service.AnnullaPerAssenzaAsync(appuntamento.Id, "Assenza improvvisa.", notificaGiaDataAltrove: true);

        Assert.Equal(StatoAppuntamento.Annullato, appuntamento.Stato);
        Assert.True(appuntamento.NotificaGiaDataAltrove);
        Assert.Equal(6, pacchetto.SeduteResidue);
        // Avvisata a voce: il sistema non deve mandare un secondo messaggio per conto suo.
        Assert.Empty(await db.Notifiche.ToListAsync());
    }
}
