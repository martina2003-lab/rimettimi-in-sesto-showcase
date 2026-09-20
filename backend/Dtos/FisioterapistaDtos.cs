using RimettimiInSesto.Api.Models;

namespace RimettimiInSesto.Api.Dtos;

// I pazienti a cui questo fisioterapista può accedere, divisi in due gruppi — ma qui non
// è un'etichetta scritta a mano: "abituale" è **calcolato** dallo storico appuntamenti,
// cioè esattamente il valore che il progetto dice di non memorizzare mai come legame fisso.
public record PazienteDelFisioterapistaDto(
    int Id, string Nome, string Cognome,
    bool Abituale,
    int AppuntamentiConMe,
    DateTime? UltimoAppuntamento,
    /** Gli appuntamenti che giustificano l'accesso: è ciò che rende verificabile la regola. */
    List<string> AppuntamentiGiustificativi);

public record ProfiloFisioterapistaDto(
    int Id, string Nome, string Cognome, string? Bio, string? FotoProfiloUrl,
    string TipoContratto, string? PatternDisponibilita);

public record AggiornaBioRequest(string? Bio);

public record AssenzaDto(
    int Id, int FisioterapistaId, string FisioterapistaNome,
    DateOnly DataInizio, DateOnly DataFine, string Tipo, string StatoApprovazione, string? Motivo);

public record RichiediAssenzaRequest(DateOnly DataInizio, DateOnly DataFine, TipoAssenza Tipo, string? Motivo);
