import { useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { api } from '../../api/client'
import type {
  FisioterapistaPubblico,
  PercorsiAttivi,
  PrimaDisponibilita,
  Ricetta,
} from '../../api/types'
import GrigliaDisponibilita, { type SelezioneSlot } from '../../components/GrigliaDisponibilita'
import { usePazienteAttivo } from '../../paziente/PazienteAttivoContext'

const PASSI = ['Percorso', 'Fisioterapista', 'Data e ora', 'Riepilogo'] as const

const DURATE = [30, 60, 90]

const FORMATO_LUNGO = new Intl.DateTimeFormat('it-IT', {
  weekday: 'long', day: 'numeric', month: 'long', year: 'numeric',
})
const FORMATO_ORA = new Intl.DateTimeFormat('it-IT', { hour: '2-digit', minute: '2-digit' })

export default function Prenota() {
  const navigate = useNavigate()

  const [passo, setPasso] = useState(0)
  const [errore, setErrore] = useState<string | null>(null)
  const [invioInCorso, setInvioInCorso] = useState(false)

  const { pazienti, pazienteId } = usePazienteAttivo()
  const [percorsi, setPercorsi] = useState<PercorsiAttivi | null>(null)
  const [fisioterapisti, setFisioterapisti] = useState<FisioterapistaPubblico[]>([])

  const [percorso, setPercorso] = useState<'Privato' | 'Ssn'>('Privato')
  const [durata, setDurata] = useState(60)
  const [ricettaId, setRicettaId] = useState<number | null>(null)
  const [pacchettoId, setPacchettoId] = useState<number | null>(null)
  // Tutte le ricette del paziente (validate comprese, a differenza di percorsi.ricetteSsn che
  // ne tiene solo le validate): serve a sapere se ce n'è già una in corso, per non far
  // ricompilare il modulo da capo riaprendo il wizard (il sistema "si ricorda").
  const [mieRicette, setMieRicette] = useState<Ricetta[]>([])
  // L'utente può scartare la proposta automatica (ricetta pendente preselezionata) e tornare
  // a vedere il modulo/le altre opzioni — senza, una volta mostrata la card "in attesa" non
  // c'era modo di tornare indietro a scegliere diversamente.
  const [pendenteIgnorata, setPendenteIgnorata] = useState(false)
  const [nuovaNre, setNuovaNre] = useState('')
  const [nuoviDistretti, setNuoviDistretti] = useState('')
  const [documentoSelezionato, setDocumentoSelezionato] = useState(false)
  const [caricamentoRicettaInCorso, setCaricamentoRicettaInCorso] = useState(false)
  // Scelta esplicita di prenotare SSN senza aver caricato/scelto alcuna ricetta: la porta
  // alla prima seduta (requisiti.md). Senza questo flag il wizard non saprebbe distinguere
  // "non ho ancora deciso" da "ho deciso di non caricarla ora".
  const [prenotaSenzaRicetta, setPrenotaSenzaRicetta] = useState(false)
  const [fisioterapistaId, setFisioterapistaId] = useState<number | null>(null)
  const [profiloAperto, setProfiloAperto] = useState<number | null>(null)
  const [selezione, setSelezione] = useState<SelezioneSlot | null>(null)
  // "Nessuna preferenza" (requisiti.md): il terapista non si sceglie prima, lo decide il
  // primo orario libero. Resta un percorso a sé e non un fisioterapista finto, perché la
  // scelta vera arriva dopo, quando si prende lo slot.
  const [nessunaPreferenza, setNessunaPreferenza] = useState(false)
  const [prime, setPrime] = useState<PrimaDisponibilita[] | null>(null)

  useEffect(() => {
    if (pazienteId === null) return
    setPercorsi(null)
    setRicettaId(null)
    setPacchettoId(null)
    setPrenotaSenzaRicetta(false)
    setPendenteIgnorata(false)
    setNuovaNre('')
    setNuoviDistretti('')
    setDocumentoSelezionato(false)
    api
      .get<PercorsiAttivi>(`/api/pazienti/${pazienteId}/percorsi-attivi`)
      .then(setPercorsi)
      .catch((e) => setErrore(e instanceof Error ? e.message : 'Percorsi non caricati.'))

    // Abituale/già visto sono relativi al paziente scelto (Marco o Giulia): rifatta a ogni
    // cambio di scheda, non una volta sola al montaggio.
    api
      .get<FisioterapistaPubblico[]>(`/api/fisioterapisti?pazienteId=${pazienteId}`)
      .then(setFisioterapisti)
      .catch((e) => setErrore(e instanceof Error ? e.message : 'Fisioterapisti non caricati.'))

    api
      .get<Ricetta[]>(`/api/pazienti/${pazienteId}/ricette`)
      .then(setMieRicette)
      .catch((e) => setErrore(e instanceof Error ? e.message : 'Ricette non caricate.'))
  }, [pazienteId])

  // La ricetta "in corso" — inviata ma non ancora validata dalla segreteria. Al più una alla
  // volta ha senso in un demo: se ce n'è più di una, la più recente è quella attiva.
  const ricettaPendente =
    mieRicette
      .filter((r) => r.stato === 'NonAncoraValidata' || r.stato === 'IntegrazioneRichiesta')
      .at(-1) ?? null

  // Preselezione calcolata, non con un effect: se non ha scelto nulla e non ha scartato la
  // proposta, la ricetta pendente vale come scelta di default — ma resta possibile tornare
  // indietro e vedere le altre opzioni (bottone "Cambia" più sotto), cosa che un effect che
  // la riscriveva ad ogni render non permetteva.
  const ricettaIdEffettivo =
    ricettaId ?? (!prenotaSenzaRicetta && !pendenteIgnorata ? (ricettaPendente?.id ?? null) : null)

  async function caricaRicetta() {
    if (pazienteId === null || !nuoviDistretti.trim()) return
    setCaricamentoRicettaInCorso(true)
    setErrore(null)
    try {
      const creata = await api.post<Ricetta>(`/api/pazienti/${pazienteId}/ricette`, {
        numeroONre: nuovaNre.trim() || null,
        distrettiCorporei: nuoviDistretti.trim(),
        documentoCaricato: documentoSelezionato,
      })
      setMieRicette((r) => [...r, creata])
      setRicettaId(creata.id)
      setPrenotaSenzaRicetta(false)
      setPendenteIgnorata(false)
      setNuovaNre('')
      setNuoviDistretti('')
      setDocumentoSelezionato(false)
    } catch (e) {
      setErrore(e instanceof Error ? e.message : 'Caricamento ricetta non riuscito.')
    } finally {
      setCaricamentoRicettaInCorso(false)
    }
  }

  // La durata di una seduta SSN non è una scelta del paziente: 30 minuti per ogni distretto
  // prescritto. Il backend la ricalcola comunque da sé — qui serve solo perché la griglia
  // sappia quanti blocchi evidenziare. La ricetta scelta può essere già validata (con ciclo,
  // in percorsi.ricetteSsn) oppure appena caricata e ancora da validare (solo in mieRicette).
  const ricettaScelta = percorsi?.ricetteSsn.find((r) => r.id === ricettaIdEffettivo) ?? null
  const ricettaPendenteScelta = mieRicette.find((r) => r.id === ricettaIdEffettivo) ?? null
  const distrettiRicettaScelta = ricettaScelta?.distrettiCorporei ?? ricettaPendenteScelta?.distrettiCorporei
  const durataEffettiva =
    percorso === 'Ssn'
      ? (distrettiRicettaScelta?.split(',').filter((d) => d.trim()).length ?? 1) * 30
      : durata

  const fisioterapistaScelto = fisioterapisti.find((f) => f.id === fisioterapistaId) ?? null

  function avanti() {
    setErrore(null)
    const prossimo = Math.min(passo + 1, PASSI.length - 1)
    if (prossimo === 2 && nessunaPreferenza) {
      setPrime(null)
      api
        .get<PrimaDisponibilita[]>(`/api/agenda/prima-disponibilita?durataMinuti=${durataEffettiva}`)
        .then(setPrime)
        .catch((e) => setErrore(e instanceof Error ? e.message : 'Disponibilità non caricata.'))
    }
    setPasso(prossimo)
  }

  function indietro() {
    setErrore(null)
    setPasso((p) => Math.max(p - 1, 0))
  }

  const puoAvanzare =
    (passo === 0 &&
      pazienteId !== null &&
      (percorso === 'Privato' || ricettaIdEffettivo !== null || prenotaSenzaRicetta)) ||
    (passo === 1 && (fisioterapistaId !== null || nessunaPreferenza)) ||
    (passo === 2 && selezione !== null && fisioterapistaId !== null)

  async function invia() {
    if (!selezione || pazienteId === null || fisioterapistaId === null) return
    setInvioInCorso(true)
    setErrore(null)
    try {
      // Data locale senza fuso: il backend ragiona sull'orario dello studio, non in UTC.
      const i = selezione.inizio
      const dataOra = `${i.getFullYear()}-${String(i.getMonth() + 1).padStart(2, '0')}-${String(i.getDate()).padStart(2, '0')}T${String(i.getHours()).padStart(2, '0')}:${String(i.getMinutes()).padStart(2, '0')}:00`

      await api.post('/api/appuntamenti/richieste', {
        pazienteId,
        fisioterapistaId,
        dataOra,
        percorso,
        // Null solo quando c'è una ricetta: lì la durata la ricalcola il backend dai distretti
        // prescritti. Senza ricetta ("la porto alla prima seduta") non c'è niente da cui
        // ricavarla, e il backend la pretende — mandare null faceva fallire ogni prenotazione
        // SSN senza ricetta.
        durataMinuti: percorso === 'Ssn' && ricettaIdEffettivo !== null ? null : durataEffettiva,
        acquistoPacchettoId: percorso === 'Privato' ? pacchettoId : ricettaScelta?.cicloId ?? null,
        ricettaId: percorso === 'Ssn' ? ricettaIdEffettivo : null,
      })
      navigate('/paziente')
    } catch (e) {
      setErrore(e instanceof Error ? e.message : 'Invio non riuscito.')
    } finally {
      setInvioInCorso(false)
    }
  }

  return (
    <>
      <header className="mb-6">
        <h1 className="view-title">Prenota una seduta</h1>
        <p className="view-sub">
          La richiesta blocca l'orario scelto fino alla decisione della segreteria.
        </p>
      </header>

      <ol className="mb-7 flex flex-wrap gap-2">
        {PASSI.map((nome, indice) => (
          <li
            key={nome}
            className="rounded-full px-3 py-1 text-[0.78rem] font-bold"
            style={
              indice === passo
                ? { background: 'var(--accent)', color: 'var(--accent-ink)' }
                : indice < passo
                  ? { background: 'var(--good-soft)', color: 'var(--good)' }
                  : { background: 'var(--surface-2)', color: 'var(--muted)' }
            }
          >
            {indice + 1}. {nome}
          </li>
        ))}
      </ol>

      {errore && (
        <p
          className="mb-4 rounded-lg px-3 py-2 text-sm"
          style={{ background: 'var(--danger-soft)', color: 'var(--danger)' }}
          role="alert"
        >
          {errore}
        </p>
      )}

      {/* Le therapist-card dello step 2 hanno già bordo e ombra proprie (come nel mockup):
          il .card esterno qui creerebbe una cornice attorno a delle cornici. */}
      <div className={passo === 1 ? '' : 'card'}>
        {passo === 0 && (
          <>

            <div className="field">
              <label>Percorso</label>
              <div className="flex flex-wrap gap-2">
                {(['Privato', 'Ssn'] as const).map((valore) => (
                  <button
                    key={valore}
                    className={`btn ${percorso === valore ? 'btn-primary' : 'btn-ghost'}`}
                    onClick={() => setPercorso(valore)}
                  >
                    {valore === 'Privato' ? 'Privato' : 'Convenzionato SSN'}
                  </button>
                ))}
              </div>
            </div>

            {percorso === 'Privato' ? (
              <>
                <div className="field">
                  <label>Durata della seduta</label>
                  <div className="flex flex-wrap gap-2">
                    {DURATE.map((valore) => (
                      <button
                        key={valore}
                        className={`btn ${durata === valore ? 'btn-primary' : 'btn-ghost'}`}
                        onClick={() => {
                          setDurata(valore)
                          setSelezione(null)
                        }}
                      >
                        {valore === 90 ? '1 h 30' : valore === 60 ? '1 ora' : '30 min'}
                      </button>
                    ))}
                  </div>
                </div>

                <div className="field">
                  <label htmlFor="pacchetto">Da scalare dal pacchetto</label>
                  <select
                    id="pacchetto"
                    value={pacchettoId ?? ''}
                    onChange={(e) => setPacchettoId(e.target.value ? Number(e.target.value) : null)}
                  >
                    <option value="">Nessun pacchetto — seduta da saldare a parte</option>
                    {percorsi?.pacchettiPrivati.map((p) => (
                      <option key={p.id} value={p.id}>
                        {p.seduteResidue} sedute residue su {p.seduteTotali}
                        {p.scadenza ? ` · scade il ${p.scadenza.split('-').reverse().join('/')}` : ''}
                      </option>
                    ))}
                  </select>
                </div>
              </>
            ) : (
              <div className="field">
                <label htmlFor="ricetta">Ricetta</label>

                {percorsi && percorsi.ricetteSsn.length > 0 && (
                  <select
                    id="ricetta"
                    value={ricettaId ?? ''}
                    onChange={(e) => {
                      setRicettaId(e.target.value ? Number(e.target.value) : null)
                      setPrenotaSenzaRicetta(false)
                      setPendenteIgnorata(false)
                      setSelezione(null)
                    }}
                  >
                    <option value="">Scegli una ricetta…</option>
                    {percorsi.ricetteSsn.map((r) => (
                      <option key={r.id} value={r.id}>
                        {r.numeroONre} · {r.distrettiCorporei}
                        {r.seduteResidue !== null ? ` · ${r.seduteResidue} sedute residue` : ''}
                      </option>
                    ))}
                  </select>
                )}

                {/* Una ricetta già inviata ma non ancora validata: il sistema se lo ricorda,
                    non fa ricompilare il modulo ogni volta che si riapre il wizard. "Cambia"
                    permette comunque di tornare a vedere le altre opzioni. */}
                {ricettaPendente && ricettaIdEffettivo === ricettaPendente.id && ricettaId === null && (
                  <div
                    className="mt-2 flex items-center justify-between gap-3 rounded-lg px-3 py-2"
                    style={{ background: 'var(--good-soft)' }}
                  >
                    <div className="flex items-center gap-3">
                      <i className="ti ti-circle-check" style={{ color: 'var(--good)' }} />
                      <div>
                        <strong className="block text-[0.88rem]">
                          Ricetta {ricettaPendente.stato === 'IntegrazioneRichiesta' ? '— integrazione richiesta' : 'caricata'} — in attesa di validazione
                        </strong>
                        <span className="text-[0.8rem]" style={{ color: 'var(--muted)' }}>
                          {ricettaPendente.distrettiCorporei}
                          {ricettaPendente.numeroONre ? ` · NRE ${ricettaPendente.numeroONre}` : ''}
                          {ricettaPendente.documentoCaricato ? ' · documento allegato' : ''}
                        </span>
                      </div>
                    </div>
                    <button
                      type="button"
                      className="btn btn-ghost btn-sm"
                      onClick={() => {
                        setPendenteIgnorata(true)
                        setSelezione(null)
                      }}
                    >
                      Cambia
                    </button>
                  </div>
                )}

                {prenotaSenzaRicetta && (
                  <div
                    className="mt-2 flex items-center justify-between gap-3 rounded-lg px-3 py-2"
                    style={{ background: 'var(--warm-soft)' }}
                  >
                    <div className="flex items-center gap-3">
                      <i className="ti ti-clock" style={{ color: 'var(--warm)' }} />
                      <div>
                        <strong className="block text-[0.88rem]">Ricetta non ancora caricata</strong>
                        <span className="text-[0.8rem]" style={{ color: 'var(--muted)' }}>
                          Portala alla prima seduta, o caricala da qui in un secondo momento.
                        </span>
                      </div>
                    </div>
                    <button
                      type="button"
                      className="btn btn-ghost btn-sm"
                      onClick={() => {
                        setPrenotaSenzaRicetta(false)
                        setSelezione(null)
                      }}
                    >
                      Cambia
                    </button>
                  </div>
                )}

                {/* Modulo di caricamento: sempre disponibile quando non c'è già una ricetta
                    pendente selezionata né la scelta esplicita di non caricarla — anche se
                    esistono ricette validate di un ciclo precedente, il paziente può avviarne
                    uno nuovo. */}
                {!(ricettaPendente && ricettaIdEffettivo === ricettaPendente.id && ricettaId === null) && !prenotaSenzaRicetta && (
                  <div
                    className="mt-2 rounded-lg p-3"
                    style={{ background: 'var(--warm-soft)', border: '1px solid var(--border)' }}
                  >
                    {percorsi && percorsi.ricetteSsn.length === 0 && (
                      <p className="mb-2 text-[0.86rem]">
                        <strong>Non risultano ricette già validate.</strong> Carica ora la foto/scansione della tua
                        ricetta: la segreteria verifica il documento prima di validarla.
                      </p>
                    )}
                    <button
                      type="button"
                      className={`btn btn-sm mb-2 ${documentoSelezionato ? 'btn-primary' : 'btn-ghost'}`}
                      onClick={() => setDocumentoSelezionato((v) => !v)}
                    >
                      <i className="ti ti-camera" /> {documentoSelezionato ? 'Documento selezionato' : 'Carica foto o scansione'}
                    </button>
                    <div className="grid grid-cols-2 gap-2 mb-2">
                      <input
                        placeholder="Numero / NRE"
                        value={nuovaNre}
                        onChange={(e) => setNuovaNre(e.target.value)}
                      />
                      <input
                        placeholder="Distretti corporei (es. colonna lombare, ginocchio dx)"
                        value={nuoviDistretti}
                        onChange={(e) => setNuoviDistretti(e.target.value)}
                      />
                    </div>
                    <button
                      type="button"
                      className="btn btn-primary btn-sm"
                      disabled={!nuoviDistretti.trim() || caricamentoRicettaInCorso}
                      onClick={caricaRicetta}
                    >
                      {caricamentoRicettaInCorso ? 'Invio…' : 'Invia ricetta'}
                    </button>

                    <div className="my-3 text-center text-[0.78rem] font-semibold" style={{ color: 'var(--muted)' }}>
                      oppure
                    </div>

                    <div className="flex items-center justify-between gap-3">
                      <div>
                        <strong className="block text-[0.84rem]">Non ce l'ho ora</strong>
                        <span className="text-[0.78rem]" style={{ color: 'var(--muted)' }}>
                          Prenoto comunque e la porto alla prima seduta
                        </span>
                      </div>
                      <button
                        type="button"
                        className="btn btn-ghost btn-sm"
                        onClick={() => {
                          setPrenotaSenzaRicetta(true)
                          setRicettaId(null)
                          setSelezione(null)
                        }}
                      >
                        Prenota senza caricarla
                      </button>
                    </div>
                  </div>
                )}

                <p className="mt-2 text-[0.8rem]" style={{ color: 'var(--muted)' }}>
                  La durata non si sceglie: è di 30 minuti per ogni distretto corporeo indicato
                  dal medico sulla ricetta.
                </p>
              </div>
            )}
          </>
        )}

        {passo === 1 && (
          <div className="flex flex-col gap-3">
            <button
              className="option-noPref"
              type="button"
              style={
                nessunaPreferenza
                  ? { borderStyle: 'solid', borderColor: 'var(--accent)', boxShadow: '0 0 0 3px var(--accent-soft)' }
                  : undefined
              }
              onClick={() => {
                setNessunaPreferenza(true)
                setFisioterapistaId(null)
                setSelezione(null)
              }}
            >
              <span className="icon-circle">
                <i className="ti ti-circle-minus" aria-hidden="true" />
              </span>
              <span>
                <strong>Nessuna preferenza</strong>
                <span className="sub">Prenota il primo fisioterapista disponibile</span>
              </span>
            </button>

            {fisioterapisti.map((fisioterapista) => {
              const iniziali = fisioterapista.nome
                .split(' ')
                .filter((p) => p && p !== 'Dott.' && p !== 'Dott.ssa')
                .map((p) => p[0])
                .join('')
                .slice(0, 2)
                .toUpperCase()
              const selezionato = fisioterapistaId === fisioterapista.id

              return (
                <div key={fisioterapista.id} className="therapist-card" style={selezionato ? { borderColor: 'var(--accent)', boxShadow: '0 0 0 3px var(--accent-soft)' } : undefined}>
                  <button
                    className="therapist-select"
                    onClick={() => {
                      setFisioterapistaId(fisioterapista.id)
                      setNessunaPreferenza(false)
                      setSelezione(null)
                    }}
                  >
                    <span className="avatar">{iniziali}</span>
                    <span className="therapist-body">
                      <span className="therapist-top">
                        <span className="therapist-name">{fisioterapista.nome}</span>
                        {fisioterapista.abituale && (
                          <span className="badge badge-usual">Il tuo fisioterapista abituale</span>
                        )}
                        {!fisioterapista.abituale && fisioterapista.giaVisto && (
                          <span className="badge badge-again">Prenota di nuovo</span>
                        )}
                      </span>
                      {fisioterapista.bio && <span className="therapist-bio">{fisioterapista.bio}</span>}
                    </span>
                  </button>
                  <button
                    className="therapist-viewprofile"
                    type="button"
                    onClick={() => setProfiloAperto(fisioterapista.id)}
                  >
                    Vedi profilo
                  </button>
                </div>
              )
            })}
            <p className="text-[0.8rem]" style={{ color: 'var(--muted)' }}>
              Puoi cambiare fisioterapista a ogni prenotazione: non esiste un legame fisso.
            </p>
          </div>
        )}

        {profiloAperto !== null &&
          (() => {
            const fisioterapista = fisioterapisti.find((f) => f.id === profiloAperto)
            if (!fisioterapista) return null
            const iniziali = fisioterapista.nome
              .split(' ')
              .filter((p) => p && p !== 'Dott.' && p !== 'Dott.ssa')
              .map((p) => p[0])
              .join('')
              .slice(0, 2)
              .toUpperCase()
            return (
              <div className="profile-overlay" onClick={() => setProfiloAperto(null)}>
                <div className="profile-panel" role="dialog" aria-modal="true" onClick={(e) => e.stopPropagation()}>
                  <div className="profile-panel-head">
                    <span className="avatar">{iniziali}</span>
                    <button className="profile-panel-close" aria-label="Chiudi" onClick={() => setProfiloAperto(null)}>
                      <i className="ti ti-x" aria-hidden="true" />
                    </button>
                  </div>
                  <p className="profile-panel-name">{fisioterapista.nome}</p>
                  {fisioterapista.bio && <p className="profile-panel-bio">{fisioterapista.bio}</p>}
                  <button
                    className="btn btn-primary"
                    style={{ width: '100%' }}
                    onClick={() => {
                      setFisioterapistaId(fisioterapista.id)
                      setNessunaPreferenza(false)
                      setSelezione(null)
                      setProfiloAperto(null)
                    }}
                  >
                    Seleziona questo fisioterapista
                  </button>
                </div>
              </div>
            )
          })()}

        {passo === 2 && nessunaPreferenza && (
          <div className="flex flex-col gap-2">
            {!prime && <p style={{ color: 'var(--muted)' }}>Cerco i primi orari liberi…</p>}
            {prime?.length === 0 && (
              <p style={{ color: 'var(--muted)' }}>
                Nessun orario libero nelle prossime settimane. Prova a scegliere un
                fisioterapista e a guardare più avanti nel calendario.
              </p>
            )}
            {prime?.map((opzione) => {
              const inizio = new Date(opzione.dataOra)
              const scelta =
                selezione !== null &&
                fisioterapistaId === opzione.fisioterapistaId &&
                selezione.inizio.getTime() === inizio.getTime()
              return (
                <button
                  key={`${opzione.fisioterapistaId}-${opzione.dataOra}`}
                  className={`btn ${scelta ? 'btn-primary' : 'btn-ghost'}`}
                  style={{ textAlign: 'left' }}
                  onClick={() => {
                    setFisioterapistaId(opzione.fisioterapistaId)
                    setSelezione({
                      inizio,
                      fine: new Date(inizio.getTime() + durataEffettiva * 60_000),
                    })
                  }}
                >
                  {FORMATO_LUNGO.format(inizio)}, {FORMATO_ORA.format(inizio)} ·{' '}
                  {opzione.fisioterapistaNome}
                </button>
              )
            })}
            <p className="text-[0.8rem]" style={{ color: 'var(--muted)' }}>
              I primi orari liberi con chiunque sia disponibile. Scegliendone uno scegli anche
              il fisioterapista: te lo diciamo qui, e lo rivedi nel riepilogo.
            </p>
          </div>
        )}

        {passo === 2 && !nessunaPreferenza && fisioterapistaId !== null && (
          <GrigliaDisponibilita
            fisioterapistaId={fisioterapistaId}
            durataMinuti={durataEffettiva}
            selezione={selezione}
            onSeleziona={setSelezione}
          />
        )}

        {passo === 3 && selezione && (
          <dl className="flex flex-col gap-3">
            <div>
              <dt className="eyebrow">Paziente</dt>
              <dd>
                {pazienti.find((p) => p.id === pazienteId)?.nome}{' '}
                {pazienti.find((p) => p.id === pazienteId)?.cognome}
              </dd>
            </div>
            <div>
              <dt className="eyebrow">Percorso</dt>
              <dd>{percorso === 'Ssn' ? 'Convenzionato SSN' : 'Privato'}</dd>
            </div>
            <div>
              <dt className="eyebrow">Fisioterapista</dt>
              <dd>{fisioterapistaScelto?.nome}</dd>
            </div>
            <div>
              <dt className="eyebrow">Quando</dt>
              <dd>
                {FORMATO_LUNGO.format(selezione.inizio)},{' '}
                <span className="mono">
                  {FORMATO_ORA.format(selezione.inizio)}–{FORMATO_ORA.format(selezione.fine)}
                </span>{' '}
                ({durataEffettiva} minuti)
              </dd>
            </div>

            <p
              className="rounded-lg px-3 py-2 text-[0.86rem]"
              style={{ background: 'var(--warm-soft)' }}
            >
              Inviando la richiesta, questo orario resta bloccato per te e nessun altro può
              prenotarlo. Diventa però un appuntamento confermato solo quando la segreteria lo
              approva: nessuna prenotazione si conferma da sola.
            </p>
          </dl>
        )}

        <div className="mt-6 flex flex-wrap justify-between gap-2">
          <button className="btn btn-ghost" onClick={indietro} disabled={passo === 0}>
            Indietro
          </button>
          {passo < PASSI.length - 1 ? (
            <button className="btn btn-primary" onClick={avanti} disabled={!puoAvanzare}>
              Avanti
            </button>
          ) : (
            <button className="btn btn-primary" onClick={() => void invia()} disabled={invioInCorso}>
              {invioInCorso ? 'Invio…' : 'Invia la richiesta'}
            </button>
          )}
        </div>
      </div>
    </>
  )
}
