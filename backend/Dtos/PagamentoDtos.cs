using RimettimiInSesto.Api.Models;

namespace RimettimiInSesto.Api.Dtos;

public record PagamentoDto(
    int Id, int PazienteId, string PazienteNome, decimal Importo,
    string Metodo, string Stato, string Origine, DateTime? DataIncasso,
    int? RicevutaNumero, int? RicevutaAnno);

public record PagamentiPazienteResponse(List<PagamentoDto> DaSaldare, List<PagamentoDto> Storico);

// Checkout simulato in modalità test (ARCHITETTURA.md, note demo): "Online" è sempre esito
// positivo qui, nessun dato carta transita mai dal codice del portale — coerente con Stripe
// Checkout scelto in ARCHITETTURA.md. "InStudio" lascia la voce da saldare, la incassa poi
// la Coordinatrice.
public record AcquistaPacchettoRequest(MetodoPagamento Metodo);
public record AcquistaSedutaSingolaRequest(MetodoPagamento Metodo);

// L'opposizione all'invio al Sistema TS va raccolta e registrata al momento dell'incasso
// (requisiti.md, Glossario) — non un passaggio successivo separato.
public record IncassaRequest(MetodoPagamento Metodo, bool OpposizioneSistemaTs);
