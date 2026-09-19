using Microsoft.AspNetCore.Identity;

namespace RimettimiInSesto.Api.Models;

// Estende IdentityUser invece di un Utente su misura: RBAC nativo, integrazione diretta
// con ASP.NET Core Identity (vedi ARCHITETTURA.md, stack tecnico).
public class ApplicationUser : IdentityUser
{
    public string Nome { get; set; } = string.Empty;
    public string Cognome { get; set; } = string.Empty;
    public Ruolo Ruolo { get; set; }

    // Un Utente-Paziente può gestire più schede Paziente collegate (sé stesso, un figlio, ecc.).
    public ICollection<UtentePaziente> PazientiCollegati { get; set; } = [];

    // Popolato solo se Ruolo == Fisioterapista.
    public Fisioterapista? ProfiloFisioterapista { get; set; }
}
