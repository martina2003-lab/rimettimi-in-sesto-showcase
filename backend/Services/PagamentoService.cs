using Microsoft.EntityFrameworkCore;
using RimettimiInSesto.Api.Data;
using RimettimiInSesto.Api.Dtos;
using RimettimiInSesto.Api.Models;

namespace RimettimiInSesto.Api.Services;

public class PagamentoValidationException(string messaggio) : Exception(messaggio);

// Stripe in modalità test soltanto (ARCHITETTURA.md, "Note per il deploy demo"): "Online"
// qui è sempre un esito positivo simulato, mai una vera chiamata a Stripe — coerente con
// il checkout inerte già mostrato in mockup/paziente.html ("Simulazione" ben visibile).
public class PagamentoService(ApplicationDbContext db)
{
    public async Task<AcquistoPacchetto> AcquistaPacchettoAsync(int pazienteId, AcquistaPacchettoRequest request)
    {
        var listino = await db.ImpostazioniListino.FirstOrDefaultAsync() ?? new ImpostazioniListino();

        var pacchetto = new AcquistoPacchetto
        {
            PazienteId = pazienteId, Tipo = TipoPacchetto.Privato,
            SeduteTotali = listino.SedutePacchettoPrivato, SeduteResidue = listino.SedutePacchettoPrivato,
            Prezzo = listino.PrezzoPacchettoPrivato,
            DataAcquisto = DateOnly.FromDateTime(DateTime.UtcNow),
            Scadenza = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(listino.DurataValiditaPacchettoMesi)),
        };
        db.AcquistiPacchetto.Add(pacchetto);
        await db.SaveChangesAsync();

        var pagamento = new Pagamento
        {
            PazienteId = pazienteId, Importo = listino.PrezzoPacchettoPrivato,
            Metodo = request.Metodo, Origine = $"Acquisto pacchetto {listino.SedutePacchettoPrivato} sedute",
            AcquistoPacchettoId = pacchetto.Id,
            // "Paga in studio" lascia il pacchetto in attesa di attivazione finché non
            // viene saldato — nessun campo dedicato: lo stato deriva dal Pagamento collegato,
            // stesso pattern del "fisioterapista abituale" calcolato, non memorizzato.
            Stato = request.Metodo == MetodoPagamento.Online ? StatoPagamento.Pagato : StatoPagamento.DaSaldare,
            DataIncasso = request.Metodo == MetodoPagamento.Online ? DateTime.UtcNow : null,
        };
        db.Pagamenti.Add(pagamento);
        await db.SaveChangesAsync();

        return pacchetto;
    }

    public async Task<Pagamento> AcquistaSedutaSingolaAsync(int pazienteId, AcquistaSedutaSingolaRequest request)
    {
        var listino = await db.ImpostazioniListino.FirstOrDefaultAsync() ?? new ImpostazioniListino();

        var pagamento = new Pagamento
        {
            PazienteId = pazienteId, Importo = listino.PrezzoSedutaSingolaManuale,
            Metodo = request.Metodo, Origine = "Seduta singola (terapia manuale)",
            Stato = request.Metodo == MetodoPagamento.Online ? StatoPagamento.Pagato : StatoPagamento.DaSaldare,
            DataIncasso = request.Metodo == MetodoPagamento.Online ? DateTime.UtcNow : null,
        };
        db.Pagamenti.Add(pagamento);
        await db.SaveChangesAsync();
        return pagamento;
    }

    // Numerazione progressiva annuale unica — chiuso il 12/09/2026, vedi requisiti.md.
    public async Task<Ricevuta> IncassaAsync(int pagamentoId, IncassaRequest request)
    {
        var pagamento = await db.Pagamenti.FindAsync(pagamentoId)
            ?? throw new PagamentoValidationException("Pagamento non trovato.");
        if (pagamento.Stato != StatoPagamento.DaSaldare)
        {
            throw new PagamentoValidationException($"Non si può incassare un pagamento in stato '{pagamento.Stato}'.");
        }

        pagamento.Stato = StatoPagamento.Pagato;
        pagamento.Metodo = request.Metodo;
        pagamento.DataIncasso = DateTime.UtcNow;

        var anno = DateTime.UtcNow.Year;
        var ultimoNumero = await db.Ricevute.Where(r => r.Anno == anno)
            .Select(r => (int?)r.NumeroProgressivo).MaxAsync() ?? 0;

        var ricevuta = new Ricevuta
        {
            NumeroProgressivo = ultimoNumero + 1, Anno = anno,
            PagamentoId = pagamento.Id, Importo = pagamento.Importo,
            // Imposta di bollo sopra € 77,47, esenzione IVA art. 10 n. 18 (requisiti.md/ARCHITETTURA.md).
            ImpostaBolloApplicata = pagamento.Importo > 77.47m,
            OpposizioneSistemaTs = request.OpposizioneSistemaTs,
        };
        db.Ricevute.Add(ricevuta);

        await db.SaveChangesAsync();
        return ricevuta;
    }

    // Storno al posto della cancellazione — una ricevuta non si cancella mai, la
    // numerazione progressiva deve restare integra (requisiti.md, Glossario).
    public async Task StornaAsync(int pagamentoId)
    {
        var pagamento = await db.Pagamenti.FindAsync(pagamentoId)
            ?? throw new PagamentoValidationException("Pagamento non trovato.");
        if (pagamento.Stato != StatoPagamento.Pagato)
        {
            throw new PagamentoValidationException("Si può stornare solo un pagamento già incassato.");
        }

        pagamento.Stato = StatoPagamento.Stornato;

        var ricevuta = await db.Ricevute.FirstOrDefaultAsync(r => r.PagamentoId == pagamentoId);
        if (ricevuta is not null)
        {
            ricevuta.Stornata = true;
        }

        await db.SaveChangesAsync();
    }

    public static PagamentoDto ToDto(Pagamento p, string pazienteNome, Ricevuta? ricevuta) => new(
        p.Id, p.PazienteId, pazienteNome, p.Importo, p.Metodo.ToString(), p.Stato.ToString(),
        p.Origine, p.DataIncasso, ricevuta?.NumeroProgressivo, ricevuta?.Anno);
}
