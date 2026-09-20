using Microsoft.EntityFrameworkCore;
using RimettimiInSesto.Api.Data;
using RimettimiInSesto.Api.Models;

namespace RimettimiInSesto.Api.Services;

public record SlotDisponibilita(TimeOnly Orario, bool Libero);

public record DisponibilitaGiornoResult(
    DateOnly Giorno, int FisioterapistaId, bool Chiuso, string? MotivoChiusura, List<SlotDisponibilita> Slots);

public record PrimaDisponibilita(int FisioterapistaId, string FisioterapistaNome, DateTime DataOra);

public record FisioterapistaBreve(int Id, string Nome);
public record SlotLiberoOccupato(TimeOnly Orario, List<FisioterapistaBreve> Liberi);
public record GiornoLiberoOccupato(DateOnly Giorno, List<SlotLiberoOccupato> Slots);

// Calcola SOLO quando lo studio è libero per un fisioterapista in un giorno — un fatto
// della giornata, stabile fra le durate. La scelta di "quanti slot da 30 min servono e da
// dove si aggancia la seduta" resta client-side, esattamente come deciso il 9 settembre 2026
// per la griglia settimanale del Paziente: qui non si ragiona per durata.
//
// Occupato = appuntamento Richiesto o Confermato (una richiesta blocca lo slot dall'invio,
// altrimenti la conferma manuale diventerebbe una corsa tra pazienti sullo stesso slot)
// + slot inclusi in una proposta alternativa ancora aperta.
public class AvailabilityService(ApplicationDbContext db)
{
    private static readonly DayOfWeek[] GiorniApertura =
        [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday];

    private const int PassoMinuti = 30;

    /// <summary>
    /// I primi orari liberi con chiunque, per chi non ha preferenze di terapista.
    /// </summary>
    /// <remarks>
    /// È l'unico punto in cui questo servizio ragiona per durata, e la ragione è che il
    /// confronto va fatto *fra* i terapisti: senza, il client dovrebbe interrogarli tutti,
    /// per ogni giorno, e fare qui il lavoro che il server fa una volta sola. Restituisce
    /// una proposta per terapista al giorno — il primo orario utile — perché la domanda di
    /// chi non ha preferenze è "quando posso venire", non "com'è messa l'agenda".
    /// </remarks>
    public async Task<List<PrimaDisponibilita>> PrimeDisponibilitaAsync(
        int durataMinuti, DateOnly da, int giorniDaGuardare, int massimo)
    {
        // Poche proposte per giornata, non tutta l'agenda: la domanda di chi non ha
        // preferenze è "quando posso venire", e un elenco di ogni mezz'ora libera
        // sarebbe di nuovo la griglia, cioè la schermata che ha scelto di saltare.
        const int PropostePerGiorno = 3;

        var fisioterapisti = await db.Fisioterapisti
            .Include(f => f.Utente)
            .Select(f => new { f.Id, Nome = f.Utente.Nome + " " + f.Utente.Cognome })
            .ToListAsync();

        var blocchiNecessari = durataMinuti / PassoMinuti;
        var risultato = new List<PrimaDisponibilita>();

        for (var scarto = 0; scarto < giorniDaGuardare && risultato.Count < massimo; scarto++)
        {
            var giorno = da.AddDays(scarto);
            if (!GiorniApertura.Contains(giorno.DayOfWeek)) continue;

            // Un orario compare una volta sola, col primo terapista che ce l'ha libero:
            // chi ha detto "nessuna preferenza" non vuole quattro righe uguali con quattro
            // nomi diversi — sarebbe di nuovo la scelta del terapista, quella a cui ha
            // appena rinunciato.
            var delGiorno = new SortedDictionary<TimeOnly, PrimaDisponibilita>();

            foreach (var fisioterapista in fisioterapisti)
            {
                var disponibilita = await CalcolaDisponibilitaGiornoAsync(fisioterapista.Id, giorno);
                if (disponibilita.Chiuso) continue;

                foreach (var orario in IniziUtili(disponibilita.Slots, blocchiNecessari))
                {
                    if (!delGiorno.ContainsKey(orario))
                    {
                        delGiorno[orario] = new PrimaDisponibilita(
                            fisioterapista.Id, fisioterapista.Nome, giorno.ToDateTime(orario));
                    }
                }
            }

            risultato.AddRange(delGiorno.Values.Take(PropostePerGiorno));
        }

        return risultato.OrderBy(r => r.DataOra).Take(massimo).ToList();
    }

    /// <summary>
    /// Vista d'occhio libero/occupato su una settimana, incrociando tutti i fisioterapisti:
    /// per ogni casella, chi ha quella durata libera lì. Serve alla Coordinatrice per cercare
    /// un buco senza dover controllare un fisioterapista alla volta (Calendario "Tutti" e
    /// Nuovo Appuntamento "Cerca per orario") — stessa logica di aggancio contiguo di
    /// <see cref="PrimeDisponibilitaAsync"/>, ma qui si vuole la griglia intera, non le prime
    /// proposte: chi cerca un orario preciso deve vedere anche le caselle vuote.
    /// </summary>
    public async Task<List<GiornoLiberoOccupato>> GrigliaLiberoOccupatoAsync(
        DateOnly da, DateOnly a, int durataMinuti)
    {
        var fisioterapisti = await db.Fisioterapisti
            .Include(f => f.Utente)
            .Select(f => new FisioterapistaBreve(f.Id, f.Utente.Nome + " " + f.Utente.Cognome))
            .ToListAsync();

        var blocchiNecessari = durataMinuti / PassoMinuti;
        var risultato = new List<GiornoLiberoOccupato>();

        for (var giorno = da; giorno <= a; giorno = giorno.AddDays(1))
        {
            if (!GiorniApertura.Contains(giorno.DayOfWeek)) continue;

            // Gli orari di riga vengono dall'unione dei fisioterapisti aperti quel giorno:
            // se uno è assente le sue caselle restano vuote, ma la griglia non deve accorciarsi.
            var orariBase = new SortedSet<TimeOnly>();
            var liberiPerOrario = new Dictionary<TimeOnly, List<FisioterapistaBreve>>();

            foreach (var fisioterapista in fisioterapisti)
            {
                var disponibilita = await CalcolaDisponibilitaGiornoAsync(fisioterapista.Id, giorno);
                if (disponibilita.Chiuso) continue;

                foreach (var slot in disponibilita.Slots)
                {
                    orariBase.Add(slot.Orario);
                }

                foreach (var orario in IniziUtili(disponibilita.Slots, blocchiNecessari))
                {
                    if (!liberiPerOrario.TryGetValue(orario, out var lista))
                    {
                        lista = [];
                        liberiPerOrario[orario] = lista;
                    }
                    lista.Add(fisioterapista);
                }
            }

            var slots = orariBase
                .Select(orario => new SlotLiberoOccupato(
                    orario, liberiPerOrario.TryGetValue(orario, out var liberi) ? liberi : []))
                .ToList();

            risultato.Add(new GiornoLiberoOccupato(giorno, slots));
        }

        return risultato;
    }

    // Tutti gli orari in cui una seduta di quella durata ci sta. I blocchi devono essere
    // contigui nel tempo, non solo adiacenti nell'array: fra le 12:30 e le 15:00 c'è la
    // pausa pranzo e una seduta non può scavalcarla (stessa regola dell'aggancio nella
    // griglia settimanale).
    private static IEnumerable<TimeOnly> IniziUtili(List<SlotDisponibilita> slots, int blocchiNecessari)
    {
        for (var partenza = 0; partenza + blocchiNecessari <= slots.Count; partenza++)
        {
            var staInPiedi = true;
            for (var i = 0; i < blocchiNecessari && staInPiedi; i++)
            {
                if (!slots[partenza + i].Libero) staInPiedi = false;
                else if (i > 0 && (slots[partenza + i].Orario - slots[partenza + i - 1].Orario).TotalMinutes != PassoMinuti)
                {
                    staInPiedi = false;
                }
            }
            if (staInPiedi) yield return slots[partenza].Orario;
        }
    }

    public async Task<DisponibilitaGiornoResult> CalcolaDisponibilitaGiornoAsync(int fisioterapistaId, DateOnly giorno)
    {
        if (!GiorniApertura.Contains(giorno.DayOfWeek))
        {
            return new DisponibilitaGiornoResult(giorno, fisioterapistaId, true, "Lo studio è chiuso il sabato e la domenica.", []);
        }

        var chiuso = await db.ChiusureStudio
            .AnyAsync(c => giorno >= c.DataInizio && giorno <= c.DataFine);
        if (chiuso)
        {
            return new DisponibilitaGiornoResult(giorno, fisioterapistaId, true, "Studio chiuso in questa data.", []);
        }

        var assente = await db.AssenzeFisioterapisti.AnyAsync(a =>
            a.FisioterapistaId == fisioterapistaId
            && giorno >= a.DataInizio && giorno <= a.DataFine
            && a.StatoApprovazione != StatoApprovazioneAssenza.Rifiutata);
        if (assente)
        {
            return new DisponibilitaGiornoResult(giorno, fisioterapistaId, true, "Fisioterapista assente in questa data.", []);
        }

        var impostazioni = await db.ImpostazioniListino.FirstOrDefaultAsync() ?? new ImpostazioniListino();

        var inizioGiorno = giorno.ToDateTime(TimeOnly.MinValue);
        var fineGiorno = inizioGiorno.AddDays(1);

        var appuntamentiOccupati = await db.Appuntamenti
            .Where(a => a.FisioterapistaId == fisioterapistaId
                && a.DataOra >= inizioGiorno && a.DataOra < fineGiorno
                && (a.Stato == StatoAppuntamento.Richiesto || a.Stato == StatoAppuntamento.Confermato))
            .Select(a => new { a.DataOra, a.DurataMinuti })
            .ToListAsync();

        var intervalliOccupati = appuntamentiOccupati
            .Select(a => (Inizio: a.DataOra, Fine: a.DataOra.AddMinutes(a.DurataMinuti)))
            .ToList();

        // Anche lo slot chiesto in una modifica ancora da approvare è occupato: il paziente
        // tiene il vecchio finché non gli danno il nuovo, quindi per quel po' di tempo ne
        // blocca due. Senza, la segreteria potrebbe approvare uno spostamento su un orario
        // nel frattempo dato a qualcun altro.
        var modificheInAttesa = await db.Appuntamenti
            .Where(a => a.FisioterapistaId == fisioterapistaId
                && a.ModificaRichiestaDataOra != null
                && a.ModificaRichiestaDataOra >= inizioGiorno && a.ModificaRichiestaDataOra < fineGiorno
                && (a.Stato == StatoAppuntamento.Richiesto || a.Stato == StatoAppuntamento.Confermato))
            .Select(a => new { Nuova = a.ModificaRichiestaDataOra!.Value, a.DurataMinuti })
            .ToListAsync();

        intervalliOccupati.AddRange(modificheInAttesa
            .Select(m => (Inizio: m.Nuova, Fine: m.Nuova.AddMinutes(m.DurataMinuti))));

        // Slot inclusi in una proposta alternativa ancora aperta: restano bloccati finché
        // il paziente non risponde o la proposta scade.
        var proposteAperte = await db.ProposteSlotAlternativo
            .Include(p => p.Appuntamento)
            .Where(p => p.Esito == EsitoPropostaSlot.InAttesa && p.Appuntamento.FisioterapistaId == fisioterapistaId)
            .ToListAsync();

        foreach (var proposta in proposteAperte)
        {
            foreach (var slot in proposta.SlotProposti.Where(s => s.Date == giorno.ToDateTime(TimeOnly.MinValue).Date))
            {
                intervalliOccupati.Add((slot, slot.AddMinutes(proposta.Appuntamento.DurataMinuti)));
            }
        }

        var fasce = new List<(TimeOnly Inizio, TimeOnly Fine)>();
        if (impostazioni.MattinaInizio is { } mattinaInizio && impostazioni.MattinaFine is { } mattinaFine)
        {
            fasce.Add((mattinaInizio, mattinaFine));
        }
        if (impostazioni.PomeriggioInizio is { } pomeriggioInizio && impostazioni.PomeriggioFine is { } pomeriggioFine)
        {
            fasce.Add((pomeriggioInizio, pomeriggioFine));
        }

        var slots = new List<SlotDisponibilita>();
        foreach (var (fasciaInizio, fasciaFine) in fasce)
        {
            for (var orario = fasciaInizio; orario < fasciaFine; orario = orario.AddMinutes(PassoMinuti))
            {
                var inizioSlot = giorno.ToDateTime(orario);
                var fineSlot = inizioSlot.AddMinutes(PassoMinuti);

                // Uno slot già passato non è "libero": non lo è per nessun client, e mostrarlo
                // tale porterebbe a proporre un orario che la prenotazione rifiuta comunque.
                var libero = inizioSlot > DateTime.Now
                    && !intervalliOccupati.Any(o => inizioSlot < o.Fine && fineSlot > o.Inizio);

                slots.Add(new SlotDisponibilita(orario, libero));
            }
        }

        return new DisponibilitaGiornoResult(giorno, fisioterapistaId, false, null, slots);
    }
}
