using Microsoft.EntityFrameworkCore;
using RimettimiInSesto.Api.Data;
using RimettimiInSesto.Api.Models;

namespace RimettimiInSesto.Api.Services;

public record ClinicalAccessResult(bool Autorizzato, int? AppuntamentoGiustificativoId);

// Il cuore del principio guida "l'accesso clinico deriva dall'appuntamento, non da un
// legame statico" (CLAUDE.md). Nessuna FK Paziente.fisioterapistaDiRiferimento: un
// fisioterapista accede alla cartella di un paziente se ha, o ha avuto, un appuntamento
// assegnato con lui — qualunque stato, perché anche un appuntamento annullato o passato
// giustifica la consultazione (es. per capire perché fu annullato, o note di sostituzione).
//
// Ogni endpoint che tocca CartellaClinica/NotaSeduta DEVE passare da qui prima di leggere
// o scrivere — mai un controllo "a vista" in UI. L'audit log non è opzionale: è l'unico
// meccanismo di controllo sugli accessi dei sostituti, dato che l'autorizzazione è derivata
// e non concessa esplicitamente caso per caso.
public class ClinicalAccessService(ApplicationDbContext db)
{
    public async Task<ClinicalAccessResult> AutorizzaAsync(
        string fisioterapistaUtenteId, int pazienteId, TipoAccessoClinico tipoAccesso)
    {
        var fisioterapista = await db.Fisioterapisti
            .FirstOrDefaultAsync(f => f.UtenteId == fisioterapistaUtenteId);

        if (fisioterapista is null)
        {
            // Non è nemmeno un fisioterapista: nessun accesso, nessun log (non è stato
            // acceduto nulla). Coordinatrice e Admin non devono mai arrivare qui —
            // "solo il fisioterapista accede alla cartella clinica" (principio guida).
            return new ClinicalAccessResult(false, null);
        }

        var appuntamentoGiustificativo = await db.Appuntamenti
            .Where(a => a.FisioterapistaId == fisioterapista.Id && a.PazienteId == pazienteId)
            .OrderByDescending(a => a.DataOra)
            .Select(a => (int?)a.Id)
            .FirstOrDefaultAsync();

        if (appuntamentoGiustificativo is null)
        {
            return new ClinicalAccessResult(false, null);
        }

        db.AuditLogAccessiClinici.Add(new AuditLogAccessoClinico
        {
            UtenteId = fisioterapistaUtenteId,
            PazienteId = pazienteId,
            TipoAccesso = tipoAccesso,
            DataOra = DateTime.UtcNow,
            AppuntamentoGiustificativoId = appuntamentoGiustificativo,
        });
        await db.SaveChangesAsync();

        return new ClinicalAccessResult(true, appuntamentoGiustificativo);
    }
}
