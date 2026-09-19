namespace RimettimiInSesto.Api.Models;

// Organizzata per CICLO di trattamento, non per singola seduta — struttura ricavata
// dai moduli cartacei reali (scheda-cartacea.md). Le singole sedute vivono in NotaSeduta,
// agganciate all'Appuntamento, non qui.
public class CartellaClinica
{
    public int Id { get; set; }

    public int PazienteId { get; set; }
    public Paziente Paziente { get; set; } = null!;

    // Tutti a testo libero, come sul modulo cartaceo — nessuna tassonomia imposta.
    public string? Provenienza { get; set; }
    public string? AnamnesiPatologicaRemota { get; set; }
    public string? EsameObiettivo { get; set; }
    public string? EsamiSpecialistici { get; set; }
    public string? Diagnosi { get; set; }
    public string? ProgrammaRiabilitativo { get; set; }
    public string? IndicazioniPaziente { get; set; }

    // Campo già presente sulla carta: conferma che il passaggio di consegne tra colleghi
    // è prassi corrente, non una funzionalità nuova da inventare.
    public string? NoteSostituzione { get; set; }

    // Unico dato clinico strutturato e misurabile — 0-10, a inizio e fine ciclo.
    public int? VasIniziale { get; set; }
    public int? VasFinale { get; set; }

    public DateOnly? DataInizioTerapia { get; set; }
    public DateOnly? DataFineTerapia { get; set; }

    // Firmatari testuali — "Medico responsabile" non ha un account nel sistema.
    public string? FirmaFisioterapista { get; set; }
    public string? FirmaMedicoResponsabile { get; set; }
}

// Dodici controindicazioni sì/no, dichiarate dal paziente. Dato consultabile,
// SENZA alcun alert automatico quando si prenota una strumentale — scelta deliberata,
// non va reintrodotta senza una decisione esplicita (vedi requisiti.md).
public class Controindicazioni
{
    public int Id { get; set; }

    public int PazienteId { get; set; }
    public Paziente Paziente { get; set; } = null!;

    public bool Pacemaker { get; set; }
    public bool Gravidanza { get; set; }
    public bool NeoplasiaInAttoOPregressa { get; set; }
    public bool Epilessia { get; set; }
    public bool LesioniCutaneeOFratture { get; set; }
    public bool StatoInfiammatorioAcuto { get; set; }
    public bool DisturbiCardiocircolatori { get; set; }
    public bool MezziDiSintesiOProtesi { get; set; }
    public bool GraveOsteoporosi { get; set; }
    public bool TendenzaEmorragie { get; set; }
    public bool ProtesiAcustiche { get; set; }
    public bool AllergiaFans { get; set; }

    public string? InterventiChirurgici { get; set; }
    public string? TerapieFarmacologicheInAtto { get; set; }
}

// Due consensi distinti (dati identificativi / dati sensibili) più il consenso informato
// al trattamento sanitario — mai una spunta unica: la granularità è un requisito GDPR.
public class Consenso
{
    public int Id { get; set; }

    public int PazienteId { get; set; }
    public Paziente Paziente { get; set; } = null!;

    public TipoConsenso Tipo { get; set; }
    public DateTime Data { get; set; }
    public string VersioneInformativa { get; set; } = string.Empty;
    public StatoConsenso Stato { get; set; } = StatoConsenso.Prestato;

    // Il paziente stesso, oppure il genitore/tutore che firma per lui.
    public string Firmatario { get; set; } = string.Empty;
}

// Audit log degli accessi clinici — NON opzionale (principio guida di CLAUDE.md):
// unico meccanismo di controllo sui sostituti, dato che l'autorizzazione è derivata
// dall'appuntamento e mai concessa esplicitamente caso per caso.
public class AuditLogAccessoClinico
{
    public int Id { get; set; }

    public string UtenteId { get; set; } = string.Empty;
    public ApplicationUser Utente { get; set; } = null!;

    public int PazienteId { get; set; }
    public Paziente Paziente { get; set; } = null!;

    public TipoAccessoClinico TipoAccesso { get; set; }
    public DateTime DataOra { get; set; }

    // L'appuntamento che giustifica l'accesso (regola: ha, o ha avuto, un appuntamento
    // assegnato con quel paziente) — tracciato per rendere l'audit verificabile, non solo dichiarato.
    public int? AppuntamentoGiustificativoId { get; set; }
}
