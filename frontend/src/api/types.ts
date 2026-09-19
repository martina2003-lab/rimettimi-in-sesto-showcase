// Specchio dei DTO del backend (backend/Dtos/). Tenuti in italiano come il dominio:
// stessa parola in requisiti.md, nel database, nell'API e qui — nessun glossario in mezzo.

export type Ruolo = 'Paziente' | 'Fisioterapista' | 'Coordinatrice' | 'Admin'

export interface LoginResponse {
  token: string
  scadeIl: string
  utenteId: string
  email: string
  nome: string
  cognome: string
  ruolo: Ruolo
}

export interface SlotDisponibilita {
  orario: string // "09:00:00"
  libero: boolean
}

export interface DisponibilitaGiorno {
  giorno: string // "2026-09-21"
  fisioterapistaId: number
  chiuso: boolean
  motivoChiusura: string | null
  slots: SlotDisponibilita[]
}

export type StatoAppuntamento =
  | 'Richiesto' | 'Confermato' | 'Completato' | 'Annullato' | 'NoShow'

export interface Appuntamento {
  id: number
  pazienteId: number
  pazienteNome: string
  fisioterapistaId: number
  fisioterapistaNome: string
  dataOra: string
  durataMinuti: number
  stato: StatoAppuntamento
  percorso: 'Privato' | 'Ssn'
  acquistoPacchettoId: number | null
  ricettaId: number | null
  motivoAnnullamento: string | null
  /** Spostamento chiesto e non ancora deciso: l'orario originale resta valido. */
  modificaRichiestaDataOra: string | null
}

export interface Pagamento {
  id: number
  pazienteId: number
  pazienteNome: string
  importo: number
  metodo: string
  stato: 'DaSaldare' | 'Pagato' | 'Stornato'
  origine: string
  dataIncasso: string | null
  ricevutaNumero: number | null
  ricevutaAnno: number | null
}

export interface PagamentiPaziente {
  daSaldare: Pagamento[]
  storico: Pagamento[]
}

export interface RiepilogoSeduta {
  dataOra: string
  fisioterapistaNome: string
  testo: string | null
}

export interface FisioterapistaPubblico {
  id: number
  nome: string
  bio: string | null
  fotoProfiloUrl: string | null
  abituale: boolean
  giaVisto: boolean
}

export interface PazienteCollegato {
  id: number
  nome: string
  cognome: string
  titolo: 'SeStesso' | 'Genitore' | 'Tutore'
}

export interface PacchettoAttivo {
  id: number
  seduteTotali: number
  seduteResidue: number
  scadenza: string | null
  prezzo: number | null
}

export interface RicettaAttiva {
  id: number
  numeroONre: string | null
  distrettiCorporei: string | null
  numeroSedute: number
  finestraCompletamento: string | null
  cicloId: number | null
  seduteResidue: number | null
}

export interface PercorsiAttivi {
  pacchettiPrivati: PacchettoAttivo[]
  ricetteSsn: RicettaAttiva[]
}

export type StatoRicetta = 'NonAncoraValidata' | 'IntegrazioneRichiesta' | 'Validata' | 'Respinta'

// Mirror di RicettaDto: usato dal wizard di prenotazione per sapere, riaprendolo, se il
// paziente ha già una ricetta in corso invece di fargli ricompilare il modulo da capo.
export interface Ricetta {
  id: number
  pazienteId: number
  stato: StatoRicetta
  numeroONre: string | null
  distrettiCorporei: string | null
  documentoCaricato: boolean
}

export interface RiepilogoPaziente {
  diagnosi: string | null
  programmaRiabilitativo: string | null
  sedute: RiepilogoSeduta[]
}

export interface CicloCartella {
  id: number
  pazienteId: number
  provenienza: string | null
  anamnesiPatologicaRemota: string | null
  esameObiettivo: string | null
  esamiSpecialistici: string | null
  diagnosi: string | null
  programmaRiabilitativo: string | null
  indicazioniPaziente: string | null
  noteSostituzione: string | null
  vasIniziale: number | null
  vasFinale: number | null
  dataInizioTerapia: string | null
  dataFineTerapia: string | null
  firmaFisioterapista: string | null
  firmaMedicoResponsabile: string | null
}

export interface CartellaCompleta {
  cicli: CicloCartella[]
  sedute: RiepilogoSeduta[]
}

export interface Ricetta {
  id: number
  pazienteId: number
  pazienteNome: string
  stato: 'NonAncoraValidata' | 'IntegrazioneRichiesta' | 'Validata' | 'Respinta'
  numeroONre: string | null
  medicoPrescrittore: string | null
  dataEmissione: string
  scadenza: string | null
  finestraCompletamento: string | null
  distrettiCorporei: string | null
  quesitoDiagnostico: string | null
  esenzione: boolean
  codiceEsenzione: string | null
  numeroSeduteProscritte: number
  importoTicket: number | null
  appuntoSegreteria: string | null
  /** Quanti appuntamenti questa ricetta sta tenendo fermi: è il criterio d'ordine della coda. */
  appuntamentiBloccati: number
  priorizzataPerAttesa: boolean
}

export interface VoceEntrate {
  percorso: string
  tipoPrestazione: 'Manuale' | 'Strumentale'
  importo: number
}

export type PeriodoAndamento = 'settimana' | 'mese' | 'anno'

export interface BarraAndamento {
  da: string
  a: string
  etichetta: string
  importo: number
  /** Stesso importo diviso sulle due dimensioni, per colorare la barra in due segmenti. */
  importoPrivato: number
  importoSsn: number
  /** Incassato nello stesso intervallo, un anno prima. */
  importoAnnoPrecedente: number
  /**
   * Valorizzato solo per una barra interamente nel futuro: quanto è già in agenda
   * (appuntamenti confermati), non una previsione. Zero anche per una barra futura non
   * significa "vuota": può solo dire che nessuno ha ancora potuto prenotarci nulla, se la
   * barra è oltre `finestraPrenotazioneGiorni`.
   */
  importoPrenotato: number
  seduteErogate: number
}

export interface Andamento {
  periodo: PeriodoAndamento
  da: string
  a: string
  etichetta: string
  totale: number
  totaleAnnoPrecedente: number
  daRiscuotere: number
  numeroVociDaRiscuotere: number
  seduteErogate: number
  finestraPrenotazioneGiorni: number
  perCategoria: VoceEntrate[]
  barre: BarraAndamento[]
}

export interface RigaPersonale {
  fisioterapistaId: number
  nome: string
  contratto: 'Dipendente' | 'Collaboratore'
  oreErogate: number
  oreContratto: number
  saturazione: number | null
}

export interface AccountAdmin {
  email: string
  nome: string
  cognome: string
  ruolo: Ruolo
}

export interface Configurazione {
  prezzoPacchettoPrivato: number
  sedutePacchettoPrivato: number
  durataValiditaPacchettoMesi: number
  prezzoSedutaSingolaManuale: number
  quotaTicketRegionale: number
  orariApertura: string
  mattinaInizio: string | null
  mattinaFine: string | null
  pomeriggioInizio: string | null
  pomeriggioFine: string | null
  account: AccountAdmin[]
}

export interface PazienteDelFisioterapista {
  id: number
  nome: string
  cognome: string
  /** Calcolato dallo storico appuntamenti, mai un legame memorizzato. */
  abituale: boolean
  appuntamentiConMe: number
  ultimoAppuntamento: string | null
  appuntamentiGiustificativi: string[]
}

export interface ProfiloFisioterapista {
  id: number
  nome: string
  cognome: string
  bio: string | null
  fotoProfiloUrl: string | null
  tipoContratto: 'Dipendente' | 'Collaboratore'
  patternDisponibilita: string | null
}

export interface Assenza {
  id: number
  fisioterapistaId: number
  fisioterapistaNome: string
  dataInizio: string
  dataFine: string
  tipo: 'Pianificata' | 'Improvvisa' | 'LungaDurata'
  statoApprovazione: 'InAttesa' | 'Approvata' | 'Rifiutata' | 'NonRichiesta'
  motivo: string | null
}

export interface PazienteRicerca {
  id: number
  nome: string
  cognome: string
  codiceFiscale: string | null
  telefono: string | null
  stato: 'Provvisorio' | 'ConsensoRaccolto'
}

export interface NotaOperativa {
  id: number
  testo: string
  autore: string
  data: string
}

export interface VoceStorico {
  dataOra: string
  fisioterapistaNome: string
  durataMinuti: number
  stato: StatoAppuntamento
  percorso: 'Privato' | 'Ssn'
}

/** La scheda come la vede la segreteria: nessun dato clinico, per principio. */
export interface SchedaPaziente {
  id: number
  nome: string
  cognome: string
  codiceFiscale: string | null
  telefono: string | null
  email: string | null
  dataNascita: string | null
  stato: 'Provvisorio' | 'ConsensoRaccolto'
  pacchettiPrivati: PacchettoAttivo[]
  ricette: Ricetta[]
  storico: VoceStorico[]
  pagamenti: Pagamento[]
  note: NotaOperativa[]
}

/** Una voce della lista d'attesa: sempre appesa a un appuntamento già confermato. */
export interface AvvisoDisponibilita {
  id: number
  pazienteId: number
  pazienteNome: string
  appuntamentoId: number
  appuntamentoDataOra: string
  fisioterapistaNome: string
  percorso: 'Privato' | 'Ssn'
  creatoIl: string
  giorniDiAttesa: number
  stato: 'Attivo' | 'Avvisato' | 'Chiuso'
  avvisatoIl: string | null
}

export interface CollegaDisponibile {
  fisioterapistaId: number
  nome: string
  tipoContratto: 'Dipendente' | 'Collaboratore'
}

/** Un appuntamento che cade dentro un'assenza, con quel che serve per decidere. */
export interface AppuntamentoImpattato {
  appuntamentoId: number
  pazienteId: number
  pazienteNome: string
  dataOra: string
  durataMinuti: number
  stato: StatoAppuntamento
  percorso: 'Privato' | 'Ssn'
  finestraCompletamentoSsn: string | null
  seduteResidueSsn: number | null
  colleghiDisponibili: CollegaDisponibile[]
}

/** Un primo orario utile per chi non ha preferenze di terapista. */
export interface PrimaDisponibilita {
  fisioterapistaId: number
  fisioterapistaNome: string
  dataOra: string
}

export interface FisioterapistaBreve {
  id: number
  nome: string
}

export interface SlotLiberoOccupato {
  orario: string // "09:00:00"
  liberi: FisioterapistaBreve[]
}

/** Vista d'occhio libero/occupato di una settimana, tutti i fisioterapisti insieme. */
export interface GiornoLiberoOccupato {
  giorno: string // "2026-09-21"
  slots: SlotLiberoOccupato[]
}

export type TipoConsenso = 'DatiIdentificativi' | 'DatiSensibili' | 'TrattamentoSanitario'

/** Un consenso con quel che serve a dimostrarlo: quando, quale informativa, chi ha firmato. */
export interface Consenso {
  id: number
  pazienteId: number
  tipo: TipoConsenso
  data: string
  versioneInformativa: string
  stato: 'Prestato' | 'Revocato'
  firmatario: string
}
