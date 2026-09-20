using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RimettimiInSesto.Api.Data;
using RimettimiInSesto.Api.Dtos;
using RimettimiInSesto.Api.Models;

namespace RimettimiInSesto.Api.Controllers;

// I consensi privacy e al trattamento sanitario. L'entità esisteva dal primo giorno e non
// la leggeva né scriveva nessuno: lo stato "consenso raccolto" della scheda era un'etichetta
// che niente poteva far cambiare, e nessuno poteva revocare alcunché.
[ApiController]
[Route("api/consensi")]
[Authorize]
public class ConsensiController(ApplicationDbContext db) : ControllerBase
{
    // Versione dell'informativa in corso. Ogni consenso la porta con sé, perché requisiti.md
    // chiede di poter dimostrare *a cosa* il paziente avesse aderito, non solo che avesse
    // detto di sì: cambiata l'informativa, i consensi vecchi restano legati alla loro.
    private const string VersioneInformativaCorrente = "v2.1";

    [HttpGet]
    public async Task<ActionResult<List<ConsensoDto>>> Elenco([FromQuery] int pazienteId)
    {
        if (!await PuoVedereAsync(pazienteId)) return Forbid();

        var consensi = await db.Consensi
            .Where(c => c.PazienteId == pazienteId)
            .OrderBy(c => c.Tipo)
            .Select(c => new ConsensoDto(
                c.Id, c.PazienteId, c.Tipo.ToString(), c.Data, c.VersioneInformativa,
                c.Stato.ToString(), c.Firmatario))
            .ToListAsync();

        return Ok(consensi);
    }

    // Prestare (o ri-prestare) un consenso dal portale. Nasce un fatto nuovo, con data e
    // versione di oggi: un consenso revocato e poi ridato non è lo stesso consenso di prima.
    [HttpPost]
    [Authorize(Roles = "Paziente")]
    public async Task<ActionResult<ConsensoDto>> Presta(PrestaConsensoRequest request)
    {
        var utenteId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var collegamento = await db.UtentiPazienti
            .Include(up => up.Utente)
            .FirstOrDefaultAsync(up => up.UtenteId == utenteId && up.PazienteId == request.PazienteId);
        if (collegamento is null) return Forbid();

        if (request.Tipo == TipoConsenso.TrattamentoSanitario)
        {
            return BadRequest(new
            {
                messaggio = "Il consenso informato al trattamento si raccoglie di persona in studio, non dal portale.",
            });
        }

        // Chi firma: sé stesso, oppure il genitore/tutore che agisce per un minore. È il
        // dato che i moduli cartacei reali dello studio richiedono espressamente.
        var firmatario = collegamento.Titolo == TitoloRelazione.SeStesso
            ? $"{collegamento.Utente.Nome} {collegamento.Utente.Cognome}"
            : $"{collegamento.Utente.Nome} {collegamento.Utente.Cognome} ({collegamento.Titolo.ToString().ToLowerInvariant()})";

        var consenso = await db.Consensi
            .FirstOrDefaultAsync(c => c.PazienteId == request.PazienteId && c.Tipo == request.Tipo);

        if (consenso is null)
        {
            consenso = new Consenso { PazienteId = request.PazienteId, Tipo = request.Tipo };
            db.Consensi.Add(consenso);
        }

        consenso.Stato = StatoConsenso.Prestato;
        consenso.Data = DateTime.Now;
        consenso.VersioneInformativa = VersioneInformativaCorrente;
        consenso.Firmatario = firmatario;

        await db.SaveChangesAsync();

        return Ok(new ConsensoDto(
            consenso.Id, consenso.PazienteId, consenso.Tipo.ToString(), consenso.Data,
            consenso.VersioneInformativa, consenso.Stato.ToString(), consenso.Firmatario));
    }

    // "La revoca deve essere facile quanto la concessione" (obbligo GDPR, requisiti.md): se
    // il consenso si presta con una spunta, non si può pretendere una lettera per ritirarlo.
    [HttpPut("{id:int}/revoca")]
    [Authorize(Roles = "Paziente")]
    public async Task<ActionResult> Revoca(int id)
    {
        var consenso = await db.Consensi.Include(c => c.Paziente).FirstOrDefaultAsync(c => c.Id == id);
        if (consenso is null) return NotFound();
        if (!await PuoVedereAsync(consenso.PazienteId)) return Forbid();

        if (consenso.Tipo == TipoConsenso.TrattamentoSanitario)
        {
            return BadRequest(new
            {
                messaggio = "Il consenso informato al trattamento si revoca in studio, per comunicazione alla direzione sanitaria: non con uno switch.",
            });
        }

        consenso.Stato = StatoConsenso.Revocato;
        consenso.Data = DateTime.Now;

        // La comunicazione alla direzione sanitaria la genera il sistema (requisiti.md).
        // Qui prende la forma di una nota in segreteria invece di una mail a un indirizzo
        // inventato: la direzione sanitaria non ha un account nel portale, e una nota sulla
        // scheda è un posto che esiste davvero e che qualcuno guarda.
        db.NoteOperative.Add(new NotaOperativa
        {
            PazienteId = consenso.PazienteId,
            Testo = $"Consenso «{Etichetta(consenso.Tipo)}» revocato dal portale. Da comunicare alla direzione sanitaria.",
            Autore = "Sistema",
            Data = DateTime.Now,
        });

        await db.SaveChangesAsync();
        return NoContent();
    }

    // Il consenso raccolto di persona alla prima seduta: è questo che fa smettere una scheda
    // di essere provvisoria. Prima non esisteva niente che potesse cambiare quello stato,
    // e la Coordinatrice guardava un'etichetta che non poteva muovere.
    [HttpPost("raccolti-in-studio")]
    [Authorize(Roles = "Coordinatrice")]
    public async Task<ActionResult> RegistraRaccoltiInStudio(RegistraConsensiRequest request)
    {
        var paziente = await db.Pazienti.FirstOrDefaultAsync(p => p.Id == request.PazienteId);
        if (paziente is null) return NotFound();

        var firmatario = string.IsNullOrWhiteSpace(request.Firmatario)
            ? $"{paziente.Nome} {paziente.Cognome}"
            : request.Firmatario.Trim();

        foreach (var tipo in Enum.GetValues<TipoConsenso>())
        {
            var consenso = await db.Consensi
                .FirstOrDefaultAsync(c => c.PazienteId == paziente.Id && c.Tipo == tipo);
            if (consenso is null)
            {
                consenso = new Consenso { PazienteId = paziente.Id, Tipo = tipo };
                db.Consensi.Add(consenso);
            }

            consenso.Stato = StatoConsenso.Prestato;
            consenso.Data = DateTime.Now;
            consenso.VersioneInformativa = VersioneInformativaCorrente;
            consenso.Firmatario = firmatario;
        }

        paziente.Stato = StatoPaziente.ConsensoRaccolto;
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static string Etichetta(TipoConsenso tipo) => tipo switch
    {
        TipoConsenso.DatiIdentificativi => "Dati personali identificativi",
        TipoConsenso.DatiSensibili => "Dati sensibili",
        _ => "Consenso informato al trattamento sanitario",
    };

    private async Task<bool> PuoVedereAsync(int pazienteId)
    {
        if (User.IsInRole("Coordinatrice")) return true;
        if (!User.IsInRole("Paziente")) return false;

        var utenteId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        return await db.UtentiPazienti.AnyAsync(up => up.UtenteId == utenteId && up.PazienteId == pazienteId);
    }
}
