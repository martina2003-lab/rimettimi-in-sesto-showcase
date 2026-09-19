namespace RimettimiInSesto.Api.Dtos;

public record CartellaClinicaDto(
    int Id, int PazienteId,
    string? Provenienza, string? AnamnesiPatologicaRemota, string? EsameObiettivo,
    string? EsamiSpecialistici, string? Diagnosi, string? ProgrammaRiabilitativo,
    string? IndicazioniPaziente, string? NoteSostituzione,
    int? VasIniziale, int? VasFinale,
    DateOnly? DataInizioTerapia, DateOnly? DataFineTerapia,
    string? FirmaFisioterapista, string? FirmaMedicoResponsabile);

// Solo i campi che il fisioterapista scrive all'apertura/durante il ciclo — niente Id qui,
// lo assegna il server.
public record SalvaCicloRequest(
    string? Provenienza, string? AnamnesiPatologicaRemota, string? EsameObiettivo,
    string? EsamiSpecialistici, string? Diagnosi, string? ProgrammaRiabilitativo,
    string? IndicazioniPaziente, string? NoteSostituzione,
    int? VasIniziale, int? VasFinale,
    DateOnly? DataInizioTerapia, DateOnly? DataFineTerapia,
    string? FirmaFisioterapista, string? FirmaMedicoResponsabile);

public record ControindicazioniDto(
    bool Pacemaker, bool Gravidanza, bool NeoplasiaInAttoOPregressa, bool Epilessia,
    bool LesioniCutaneeOFratture, bool StatoInfiammatorioAcuto, bool DisturbiCardiocircolatori,
    bool MezziDiSintesiOProtesi, bool GraveOsteoporosi, bool TendenzaEmorragie,
    bool ProtesiAcustiche, bool AllergiaFans,
    string? InterventiChirurgici, string? TerapieFarmacologicheInAtto);

public record RiepilogoSedutaRequest(string Testo);

// Vista Paziente di default: diagnosi + resoconto sedute, MAI le note tecniche interne
// (anamnesi, esame obiettivo) — requisiti.md, "Cartella clinica" lato Paziente.
public record RiepilogoSedutaVoce(DateTime DataOra, string FisioterapistaNome, string? Testo);
public record RiepilogoPazienteResponse(string? Diagnosi, string? ProgrammaRiabilitativo, List<RiepilogoSedutaVoce> Sedute);

// Copia integrale su richiesta esplicita — diritto di accesso GDPR art. 15 (requisiti.md).
public record CartellaCompletaResponse(List<CartellaClinicaDto> Cicli, List<RiepilogoSedutaVoce> Sedute);
