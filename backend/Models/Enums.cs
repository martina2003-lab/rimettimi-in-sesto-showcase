namespace RimettimiInSesto.Api.Models;

// I 4 ruoli del portale — RBAC applicato lato server su questi valori, mai solo in UI.
public enum Ruolo
{
    Paziente,
    Fisioterapista,
    Coordinatrice,
    Admin
}

// Titolo con cui un Utente agisce su un Paziente collegato (sé stesso / genitore / tutore).
public enum TitoloRelazione
{
    SeStesso,
    Genitore,
    Tutore
}

public enum StatoPaziente
{
    Provvisorio,
    ConsensoRaccolto
}

public enum TipoContratto
{
    Dipendente,
    Collaboratore
}

// Nessuna auto-conferma: ogni richiesta parte "Richiesto", salvo le prenotazioni
// create direttamente dalla Coordinatrice (telefono/sportello), che nascono già "Confermato".
public enum StatoAppuntamento
{
    Richiesto,
    Confermato,
    Completato,
    Annullato,
    NoShow
}

public enum PercorsoAppuntamento
{
    Privato,
    Ssn
}

public enum TipoAssenza
{
    Pianificata,
    Improvvisa,
    LungaDurata
}

public enum StatoApprovazioneAssenza
{
    InAttesa,
    Approvata,
    Rifiutata,
    NonRichiesta // collaboratori: nessuna approvazione necessaria
}

public enum TipoPrestazione
{
    Manuale,
    Strumentale
}

// I tre esiti della validazione ricetta SSN — requisiti.md, "Validazione delle ricette SSN".
public enum StatoRicetta
{
    NonAncoraValidata,
    IntegrazioneRichiesta,
    Validata,
    Respinta
}

public enum TipoPacchetto
{
    Privato,
    Ssn
}

public enum MetodoPagamento
{
    Online,
    InStudio,
    TicketSsn
}

public enum StatoPagamento
{
    DaSaldare,
    Pagato,
    Stornato
}

// Canale sempre "loggato", mai inviato davvero — vedi ARCHITETTURA.md, "Note per il deploy demo".
public enum CanaleNotifica
{
    Email,
    Sms
}

public enum TipoNotifica
{
    Conferma,
    Promemoria,
    Cancellazione,
    AvvisoDisponibilitaAnticipata
}

// L'avviso si chiude quando non ha più senso: il paziente è stato spostato, oppure ci ha
// ripensato. Non si cancella la riga, perché "è stato avvisato e ha detto no" è un fatto
// che la segreteria ha bisogno di ricordare la volta dopo.
public enum StatoAvvisoDisponibilita
{
    Attivo,
    Avvisato,
    Chiuso
}

public enum StatoNotifica
{
    DaInviare,
    Inviata // in demo: "scritta nel log", mai un invio reale
}

public enum EsitoPropostaSlot
{
    InAttesa,
    Accettata,
    Rifiutata,
    Scaduta
}

public enum TipoConsenso
{
    DatiIdentificativi,
    DatiSensibili,
    TrattamentoSanitario
}

public enum StatoConsenso
{
    Prestato,
    Revocato
}

// Lettura/scrittura sulla cartella clinica — ogni accesso va tracciato, non è opzionale.
public enum TipoAccessoClinico
{
    Lettura,
    Scrittura
}

// Destino degli appuntamenti ancora "Richiesto" quando la loro ricetta viene respinta —
// requisiti.md: "mantieni" qui significa lasciarlo lì segnalato come senza ricetta valida,
// non semplicemente non toccarlo (il segnale è derivato da Ricetta.Stato, non un campo a parte).
public enum EsitoAppuntamentoRichiesto
{
    Mantieni,
    Annulla
}

// Destino degli appuntamenti già "Confermato" quando la loro ricetta viene respinta —
// nessuna di queste è automatica (requisiti.md).
public enum EsitoAppuntamentoConfermato
{
    Mantieni,
    ConvertiPrivato,
    Annulla
}
