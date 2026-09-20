using Microsoft.EntityFrameworkCore;
using RimettimiInSesto.Api.Data;
using RimettimiInSesto.Api.Models;
using RimettimiInSesto.Api.Services;
using Xunit;

namespace RimettimiInSesto.Api.Tests;

// Copre il principio guida non negoziabile del progetto: "l'accesso clinico deriva
// dall'appuntamento, non da un legame statico" + "l'audit log non è opzionale".
// È il pezzo più delicato del sistema (dati sanitari, categoria particolare GDPR),
// merita una verifica automatica invece di fidarsi a vista del codice.
public class ClinicalAccessServiceTests
{
    private static ApplicationDbContext NuovoDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static async Task<(ApplicationDbContext Db, Fisioterapista Fisio, Paziente Paziente)> ScenarioBase()
    {
        var db = NuovoDbContext();

        var fisio = new Fisioterapista { UtenteId = "utente-fisio-1", TipoContratto = TipoContratto.Dipendente };
        var paziente = new Paziente { Nome = "Marco", Cognome = "Bianchi", Stato = StatoPaziente.ConsensoRaccolto };
        db.Fisioterapisti.Add(fisio);
        db.Pazienti.Add(paziente);
        await db.SaveChangesAsync();

        return (db, fisio, paziente);
    }

    [Fact]
    public async Task Autorizza_se_esiste_un_appuntamento_tra_fisioterapista_e_paziente()
    {
        var (db, fisio, paziente) = await ScenarioBase();
        db.Appuntamenti.Add(new Appuntamento
        {
            FisioterapistaId = fisio.Id, PazienteId = paziente.Id,
            DataOra = DateTime.UtcNow.AddDays(-7), DurataMinuti = 60,
            Stato = StatoAppuntamento.Completato, Percorso = PercorsoAppuntamento.Privato,
        });
        await db.SaveChangesAsync();

        var service = new ClinicalAccessService(db);
        var risultato = await service.AutorizzaAsync(fisio.UtenteId, paziente.Id, TipoAccessoClinico.Lettura);

        Assert.True(risultato.Autorizzato);
        Assert.NotNull(risultato.AppuntamentoGiustificativoId);
    }

    [Fact]
    public async Task Rifiuta_se_non_esiste_alcun_appuntamento_tra_fisioterapista_e_paziente()
    {
        // Caso chiave: nessuna FK fisioterapistaDiRiferimento da aggirare — un fisioterapista
        // che non ha mai visto questo paziente non deve poter leggere la sua cartella.
        var (db, fisio, paziente) = await ScenarioBase();

        var service = new ClinicalAccessService(db);
        var risultato = await service.AutorizzaAsync(fisio.UtenteId, paziente.Id, TipoAccessoClinico.Lettura);

        Assert.False(risultato.Autorizzato);
        Assert.Null(risultato.AppuntamentoGiustificativoId);
    }

    [Fact]
    public async Task Autorizza_anche_se_lappuntamento_e_passato_o_annullato()
    {
        // "ha, O HA AVUTO, un appuntamento assegnato" — copre le sostituzioni per assenza:
        // un appuntamento annullato/completato giustifica comunque la consultazione.
        var (db, fisio, paziente) = await ScenarioBase();
        db.Appuntamenti.Add(new Appuntamento
        {
            FisioterapistaId = fisio.Id, PazienteId = paziente.Id,
            DataOra = DateTime.UtcNow.AddMonths(-6), DurataMinuti = 30,
            Stato = StatoAppuntamento.Annullato, Percorso = PercorsoAppuntamento.Ssn,
        });
        await db.SaveChangesAsync();

        var service = new ClinicalAccessService(db);
        var risultato = await service.AutorizzaAsync(fisio.UtenteId, paziente.Id, TipoAccessoClinico.Lettura);

        Assert.True(risultato.Autorizzato);
    }

    [Fact]
    public async Task Un_accesso_autorizzato_scrive_sempre_una_riga_di_audit_log()
    {
        var (db, fisio, paziente) = await ScenarioBase();
        var appuntamento = new Appuntamento
        {
            FisioterapistaId = fisio.Id, PazienteId = paziente.Id,
            DataOra = DateTime.UtcNow.AddDays(-1), DurataMinuti = 60,
            Stato = StatoAppuntamento.Completato, Percorso = PercorsoAppuntamento.Privato,
        };
        db.Appuntamenti.Add(appuntamento);
        await db.SaveChangesAsync();

        var service = new ClinicalAccessService(db);
        await service.AutorizzaAsync(fisio.UtenteId, paziente.Id, TipoAccessoClinico.Scrittura);

        var righeAudit = await db.AuditLogAccessiClinici.ToListAsync();
        var riga = Assert.Single(righeAudit);
        Assert.Equal(fisio.UtenteId, riga.UtenteId);
        Assert.Equal(paziente.Id, riga.PazienteId);
        Assert.Equal(TipoAccessoClinico.Scrittura, riga.TipoAccesso);
        Assert.Equal(appuntamento.Id, riga.AppuntamentoGiustificativoId);
    }

    [Fact]
    public async Task Un_accesso_rifiutato_non_scrive_alcuna_riga_di_audit_log()
    {
        var (db, fisio, paziente) = await ScenarioBase();

        var service = new ClinicalAccessService(db);
        await service.AutorizzaAsync(fisio.UtenteId, paziente.Id, TipoAccessoClinico.Lettura);

        Assert.Empty(await db.AuditLogAccessiClinici.ToListAsync());
    }

    [Fact]
    public async Task Rifiuta_se_lutente_non_e_un_fisioterapista()
    {
        // Coordinatrice e Admin non devono MAI poter passare da qui: "solo il fisioterapista
        // accede alla cartella clinica" (principio guida). Se l'utente non ha un profilo
        // Fisioterapista collegato, l'accesso è negato a prescindere da tutto il resto.
        var (db, _, paziente) = await ScenarioBase();

        var service = new ClinicalAccessService(db);
        var risultato = await service.AutorizzaAsync("utente-coordinatrice-1", paziente.Id, TipoAccessoClinico.Lettura);

        Assert.False(risultato.Autorizzato);
        Assert.Empty(await db.AuditLogAccessiClinici.ToListAsync());
    }

    [Fact]
    public async Task Isola_correttamente_due_pazienti_diversi()
    {
        var (db, fisio, pazienteA) = await ScenarioBase();
        var pazienteB = new Paziente { Nome = "Anna", Cognome = "Colombo", Stato = StatoPaziente.ConsensoRaccolto };
        db.Pazienti.Add(pazienteB);
        db.Appuntamenti.Add(new Appuntamento
        {
            FisioterapistaId = fisio.Id, PazienteId = pazienteA.Id,
            DataOra = DateTime.UtcNow.AddDays(-1), DurataMinuti = 60,
            Stato = StatoAppuntamento.Completato, Percorso = PercorsoAppuntamento.Privato,
        });
        await db.SaveChangesAsync();

        var service = new ClinicalAccessService(db);
        var risultatoA = await service.AutorizzaAsync(fisio.UtenteId, pazienteA.Id, TipoAccessoClinico.Lettura);
        var risultatoB = await service.AutorizzaAsync(fisio.UtenteId, pazienteB.Id, TipoAccessoClinico.Lettura);

        Assert.True(risultatoA.Autorizzato);
        Assert.False(risultatoB.Autorizzato);
    }
}
