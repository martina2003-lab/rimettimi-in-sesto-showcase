using RimettimiInSesto.Api.Models;

namespace RimettimiInSesto.Api.Dtos;

// Percorso privato: il paziente sceglie la durata prima di vedere gli slot (30/60/90).
// Percorso SSN: la durata NON si chiede al paziente — il sistema la calcola dai distretti
// prescritti nella Ricetta indicata (principio guida, requisiti.md). Per questo DurataMinuti
// è ignorata quando Percorso == Ssn. RicettaId può restare null: la ricetta arriva spesso
// alla prima seduta, sia al telefono sia prenotando da sé (BookingService).
public record CreaRichiestaRequest(
    int PazienteId,
    int FisioterapistaId,
    DateTime DataOra,
    PercorsoAppuntamento Percorso,
    int? DurataMinuti,
    int? AcquistoPacchettoId,
    int? RicettaId);

public record RifiutaRichiestaRequest(string Motivo);

public record RichiediModificaRequest(DateTime NuovaDataOra);

public record CreaPropostaSlotRequest(List<DateTime> SlotProposti);
public record AccettaPropostaRequest(DateTime SlotScelto);

public record AppuntamentoResponse(
    int Id,
    int PazienteId,
    string PazienteNome,
    int FisioterapistaId,
    string FisioterapistaNome,
    DateTime DataOra,
    int DurataMinuti,
    string Stato,
    string Percorso,
    int? AcquistoPacchettoId,
    int? RicettaId,
    string? MotivoAnnullamento,
    // Valorizzata quando c'è uno spostamento chiesto e non ancora deciso: il paziente
    // deve vedere che tiene ancora il vecchio orario mentre aspetta risposta.
    DateTime? ModificaRichiestaDataOra);

// Una voce della lista d'attesa: chi aspetta, su quale appuntamento già confermato, e
// da quanto. `GiorniDiAttesa` viaggia calcolato perché è il criterio d'ordine della coda,
// e mostrarlo spiega perché una voce viene prima di un'altra.
public record AvvisoDisponibilitaDto(
    int Id,
    int PazienteId,
    string PazienteNome,
    int AppuntamentoId,
    DateTime AppuntamentoDataOra,
    string FisioterapistaNome,
    string Percorso,
    DateTime CreatoIl,
    int GiorniDiAttesa,
    string Stato,
    DateTime? AvvisatoIl);

public record CreaAvvisoRequest(int AppuntamentoId);
// Lo slot che si è liberato non è un dettaglio facoltativo: un "avviso di disponibilità
// anticipata" senza una disponibilità non vuol dire niente, ed è il termine di paragone
// su cui si applica la soglia di ImpostazioniAgenda.
public record AvvisaDisponibilitaRequest(DateTime NuovoSlot, string? Testo);

// Un collega che potrebbe prendere l'appuntamento al posto di chi è assente. Il tipo di
// contratto c'è perché requisiti.md dà priorità *implicita* ai dipendenti liberi: un
// suggerimento a chi decide, non una regola che sceglie da sé. Per il paziente resta un
// dato interno, mai esposto.
public record CollegaDisponibileDto(int FisioterapistaId, string Nome, string TipoContratto);

// Una riga della lista di lavoro sugli appuntamenti che cadono in un'assenza. Porta con sé
// ciò che serve a decidere senza aprire altro: chi è il paziente, quanto manca alla fine
// della finestra SSN (il criterio d'urgenza) e chi potrebbe subentrare in quell'orario.
public record AppuntamentoImpattatoDto(
    int AppuntamentoId,
    int PazienteId,
    string PazienteNome,
    DateTime DataOra,
    int DurataMinuti,
    string Stato,
    string Percorso,
    DateOnly? FinestraCompletamentoSsn,
    int? SeduteResidueSsn,
    List<CollegaDisponibileDto> ColleghiDisponibili);

public record RiassegnaRequest(int FisioterapistaId);
public record AnnullaPerAssenzaRequest(string? Motivo, bool NotificaGiaDataAltrove);
