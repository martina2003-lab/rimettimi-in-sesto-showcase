using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using RimettimiInSesto.Api.Models;

namespace RimettimiInSesto.Api.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Paziente> Pazienti => Set<Paziente>();
    public DbSet<UtentePaziente> UtentiPazienti => Set<UtentePaziente>();
    public DbSet<NotaOperativa> NoteOperative => Set<NotaOperativa>();
    public DbSet<Fisioterapista> Fisioterapisti => Set<Fisioterapista>();
    public DbSet<Appuntamento> Appuntamenti => Set<Appuntamento>();
    public DbSet<NotaSeduta> NoteSeduta => Set<NotaSeduta>();
    public DbSet<AssenzaFisioterapista> AssenzeFisioterapisti => Set<AssenzaFisioterapista>();
    public DbSet<ChiusuraStudio> ChiusureStudio => Set<ChiusuraStudio>();
    public DbSet<PropostaSlotAlternativo> ProposteSlotAlternativo => Set<PropostaSlotAlternativo>();
    public DbSet<CartellaClinica> CartelleCliniche => Set<CartellaClinica>();
    public DbSet<Controindicazioni> Controindicazioni => Set<Controindicazioni>();
    public DbSet<Consenso> Consensi => Set<Consenso>();
    public DbSet<AuditLogAccessoClinico> AuditLogAccessiClinici => Set<AuditLogAccessoClinico>();
    public DbSet<Ricetta> Ricette => Set<Ricetta>();
    public DbSet<AcquistoPacchetto> AcquistiPacchetto => Set<AcquistoPacchetto>();
    public DbSet<Pagamento> Pagamenti => Set<Pagamento>();
    public DbSet<Ricevuta> Ricevute => Set<Ricevuta>();
    public DbSet<Notifica> Notifiche => Set<Notifica>();
    public DbSet<AvvisoDisponibilita> AvvisiDisponibilita => Set<AvvisoDisponibilita>();
    public DbSet<ImpostazioniAgenda> ImpostazioniAgenda => Set<ImpostazioniAgenda>();
    public DbSet<ImpostazioniListino> ImpostazioniListino => Set<ImpostazioniListino>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Utente <-> Paziente: many-to-many esplicita, con il titolo della relazione.
        builder.Entity<UtentePaziente>()
            .HasIndex(up => new { up.UtenteId, up.PazienteId })
            .IsUnique();

        // Un fisioterapista ha un solo profilo; un utente ha al più un profilo fisioterapista.
        builder.Entity<Fisioterapista>()
            .HasIndex(f => f.UtenteId)
            .IsUnique();

        // Codice fiscale unico quando presente (match per collegare un Utente che si registra).
        builder.Entity<Paziente>()
            .HasIndex(p => p.CodiceFiscale)
            .IsUnique()
            .HasFilter("[CodiceFiscale] IS NOT NULL");

        // Due prenotazioni simultanee sullo stesso slot passavano entrambe il controllo di
        // disponibilità: fra la lettura e la scrittura non c'era niente che le serializzasse.
        // Questo indice è la rete di sicurezza a livello di database — copre la collisione
        // esatta (stesso terapista, stessa ora d'inizio) anche se il controllo applicativo
        // venisse aggirato. Non copre le sovrapposizioni parziali (le 9:00 da un'ora contro
        // le 9:30): per quelle serve la sezione critica in BookingService, perché un vincolo
        // di esclusione su intervalli SQLite non ce l'ha.
        // Gli stati 0 e 1 sono Richiesto e Confermato: un appuntamento annullato ha liberato
        // il suo orario e non deve impedire a nessuno di riprenderlo.
        builder.Entity<Appuntamento>()
            .HasIndex(a => new { a.FisioterapistaId, a.DataOra })
            .IsUnique()
            .HasFilter("[Stato] IN (0, 1)");

        // Appuntamento -> AcquistoPacchetto / Ricetta: FK opzionali, niente cascade multiplo
        // per evitare cicli di eliminazione (path multipli verso Paziente).
        builder.Entity<Appuntamento>()
            .HasOne(a => a.AcquistoPacchetto)
            .WithMany(p => p.Appuntamenti)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Appuntamento>()
            .HasOne(a => a.Ricetta)
            .WithMany(r => r.Appuntamenti)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Appuntamento>()
            .HasOne(a => a.Paziente)
            .WithMany(p => p.Appuntamenti)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Appuntamento>()
            .HasOne(a => a.Fisioterapista)
            .WithMany(f => f.Appuntamenti)
            .OnDelete(DeleteBehavior.Restrict);

        // Ricevuta: numerazione progressiva annuale unica (chiuso il 12/09/2026).
        builder.Entity<Ricevuta>()
            .HasIndex(r => new { r.Anno, r.NumeroProgressivo })
            .IsUnique();

        // Enum salvati come stringa: leggibili direttamente aprendo la tabella SQLite
        // durante la demo, niente valori numerici opachi da decifrare.
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.ClrType.GetProperties())
            {
                var propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
                if (propertyType.IsEnum)
                {
                    builder.Entity(entityType.ClrType).Property(property.Name).HasConversion<string>();
                }
            }
        }
    }
}
