import { useEffect, useState } from 'react'
import { api } from '../../api/client'
import GrigliaDisponibilita, { type SelezioneSlot } from '../../components/GrigliaDisponibilita'
import GrigliaLiberoOccupato from '../../components/GrigliaLiberoOccupato'
import type {
  Appuntamento,
  FisioterapistaPubblico,
  PazienteRicerca,
  PercorsiAttivi,
} from '../../api/types'

const DURATE = [30, 60, 90]

const FORMATO_LUNGO = new Intl.DateTimeFormat('it-IT', {
  weekday: 'long', day: 'numeric', month: 'long', year: 'numeric',
})
const FORMATO_ORA = new Intl.DateTimeFormat('it-IT', { hour: '2-digit', minute: '2-digit' })

/** Data locale senza fuso: il backend ragiona sull'orario dello studio, non in UTC. */
function perApi(data: Date) {
  const p = (n: number) => String(n).padStart(2, '0')
  return `${data.getFullYear()}-${p(data.getMonth() + 1)}-${p(data.getDate())}T${p(data.getHours())}:${p(data.getMinutes())}:00`
}

/**
 * Prenotazione per conto del paziente: chi telefona o si presenta allo sportello.
 *
 * È l'unica prenotazione che nasce già confermata — eccezione dichiarata al principio
 * "nessuna prenotazione si auto-conferma", perché qui il confermatore è la Coordinatrice
 * stessa, che sta decidendo mentre parla col paziente.
 */
export default function NuovoAppuntamento() {
  const [modalita, setModalita] = useState<'esistente' | 'nuovo'>('esistente')

  const [ricerca, setRicerca] = useState('')
  const [risultati, setRisultati] = useState<PazienteRicerca[]>([])
  const [pazienteScelto, setPazienteScelto] = useState<PazienteRicerca | null>(null)

  const [nuovo, setNuovo] = useState({ nome: '', cognome: '', telefono: '', codiceFiscale: '', email: '' })
  const [possibiliDoppioni, setPossibiliDoppioni] = useState<PazienteRicerca[]>([])
  // Se la prenotazione fallisce dopo aver creato la scheda (slot soffiato nel frattempo),
  // la scheda esiste già: si riusa invece di crearne una seconda al secondo tentativo.
  const [schedaCreata, setSchedaCreata] = useState<PazienteRicerca | null>(null)

  const [percorsi, setPercorsi] = useState<PercorsiAttivi | null>(null)
  const [percorso, setPercorso] = useState<'Privato' | 'Ssn'>('Privato')
  const [durata, setDurata] = useState(60)
  const [pacchettoId, setPacchettoId] = useState<number | null>(null)
  const [ricettaId, setRicettaId] = useState<number | null>(null)

  const [fisioterapisti, setFisioterapisti] = useState<FisioterapistaPubblico[]>([])
  const [fisioterapistaId, setFisioterapistaId] = useState<number | null>(null)
  const [selezione, setSelezione] = useState<SelezioneSlot | null>(null)
  // Due modi di cercare l'orario: partendo dal fisioterapista (come oggi) o partendo
  // dall'orario che il paziente ha chiesto al telefono, senza preferenze di terapista
  // (CLAUDE.md, 15 settembre 2026) — stessa griglia libero/occupato del Calendario.
  const [cercaPer, setCercaPer] = useState<'fisioterapista' | 'orario'>('fisioterapista')

  const [errore, setErrore] = useState<string | null>(null)
  const [inCorso, setInCorso] = useState(false)
  const [esito, setEsito] = useState<Appuntamento | null>(null)

  useEffect(() => {
    api
      .get<FisioterapistaPubblico[]>('/api/fisioterapisti')
      .then((elenco) => {
        setFisioterapisti(elenco)
        if (elenco.length > 0) setFisioterapistaId((corrente) => corrente ?? elenco[0].id)
      })
      .catch((e) => setErrore(e instanceof Error ? e.message : 'Fisioterapisti non caricati.'))
  }, [])

  // La ricerca parte da sola mentre si scrive: al telefono si ha in mano un nome
  // approssimativo o un numero, e fermarsi a premere "cerca" è un passaggio in più.
  useEffect(() => {
    if (modalita !== 'esistente') return
    let annullato = false
    const attesa = setTimeout(() => {
      api
        .get<PazienteRicerca[]>(`/api/pazienti/cerca?q=${encodeURIComponent(ricerca)}`)
        .then((elenco) => {
          if (!annullato) setRisultati(elenco)
        })
        .catch((e) => {
          if (!annullato) setErrore(e instanceof Error ? e.message : 'Ricerca non riuscita.')
        })
    }, 250)
    return () => {
      annullato = true
      clearTimeout(attesa)
    }
  }, [ricerca, modalita])

  // Scrivendo i dati di una scheda nuova, si controlla comunque che non esista già:
  // il codice fiscale lo rifiuta il backend, ma al telefono spesso non si ha, e
  // requisiti.md tratta nome+cognome+telefono come indizio da confermare a mano —
  // quindi qui si mostra e si lascia decidere, invece di bloccare o di ignorare.
  useEffect(() => {
    if (modalita !== 'nuovo') {
      setPossibiliDoppioni([])
      return
    }
    const termine = nuovo.telefono.trim().length >= 4 ? nuovo.telefono.trim() : nuovo.cognome.trim()
    if (termine.length < 3) {
      setPossibiliDoppioni([])
      return
    }
    let annullato = false
    const attesa = setTimeout(() => {
      api
        .get<PazienteRicerca[]>(`/api/pazienti/cerca?q=${encodeURIComponent(termine)}`)
        .then((elenco) => {
          if (!annullato) setPossibiliDoppioni(elenco)
        })
        .catch(() => {
          // Un controllo di cortesia: se fallisce non deve impedire di prenotare.
        })
    }, 400)
    return () => {
      annullato = true
      clearTimeout(attesa)
    }
  }, [modalita, nuovo.telefono, nuovo.cognome])

  // Da cosa scalare la seduta: pacchetto privato o ciclo SSN già aperto. Senza questo,
  // una prenotazione telefonica nascerebbe sempre slegata dal percorso del paziente.
  useEffect(() => {
    setPercorsi(null)
    setPacchettoId(null)
    setRicettaId(null)
    if (pazienteScelto === null) return
    api
      .get<PercorsiAttivi>(`/api/pazienti/${pazienteScelto.id}/percorsi-attivi`)
      .then(setPercorsi)
      .catch((e) => setErrore(e instanceof Error ? e.message : 'Percorsi non caricati.'))
  }, [pazienteScelto])

  const ricettaScelta = percorsi?.ricetteSsn.find((r) => r.id === ricettaId) ?? null

  // Con la ricetta in mano la durata la detta lei: 30 minuti per distretto prescritto.
  // Senza — il caso normale al telefono — la sceglie la Coordinatrice su quel che le
  // riferisce il paziente, e la ricetta la correggerà quando arriverà in studio.
  const durataEffettiva =
    percorso === 'Ssn' && ricettaScelta
      ? (ricettaScelta.distrettiCorporei?.split(',').filter((d) => d.trim()).length ?? 1) * 30
      : durata

  const datiNuovoCompleti =
    nuovo.nome.trim() !== '' && nuovo.cognome.trim() !== '' && nuovo.telefono.trim() !== ''

  const pazientePronto = modalita === 'esistente' ? pazienteScelto !== null : datiNuovoCompleti

  const puoConfermare = pazientePronto && fisioterapistaId !== null && selezione !== null && !inCorso

  function ricomincia() {
    setModalita('esistente')
    setRicerca('')
    setPazienteScelto(null)
    setNuovo({ nome: '', cognome: '', telefono: '', codiceFiscale: '', email: '' })
    setPossibiliDoppioni([])
    setSchedaCreata(null)
    setPercorso('Privato')
    setDurata(60)
    setPacchettoId(null)
    setRicettaId(null)
    setCercaPer('fisioterapista')
    setSelezione(null)
    setEsito(null)
    setErrore(null)
  }

  async function conferma() {
    if (!selezione || fisioterapistaId === null) return
    setInCorso(true)
    setErrore(null)
    try {
      let pazienteId: number
      if (modalita === 'esistente') {
        pazienteId = pazienteScelto!.id
      } else {
        const scheda =
          schedaCreata ??
          (await api.post<PazienteRicerca>('/api/pazienti/provvisorio', {
            nome: nuovo.nome.trim(),
            cognome: nuovo.cognome.trim(),
            telefono: nuovo.telefono.trim(),
            codiceFiscale: nuovo.codiceFiscale.trim() || null,
            email: nuovo.email.trim() || null,
          }))
        setSchedaCreata(scheda)
        pazienteId = scheda.id
      }

      const creato = await api.post<Appuntamento>('/api/appuntamenti/prenota-diretto', {
        pazienteId,
        fisioterapistaId,
        dataOra: perApi(selezione.inizio),
        percorso,
        durataMinuti: percorso === 'Ssn' && ricettaScelta ? null : durata,
        acquistoPacchettoId:
          percorso === 'Privato' ? pacchettoId : (ricettaScelta?.cicloId ?? null),
        ricettaId: percorso === 'Ssn' ? ricettaId : null,
      })
      setEsito(creato)
    } catch (e) {
      setErrore(e instanceof Error ? e.message : 'Prenotazione non riuscita.')
    } finally {
      setInCorso(false)
    }
  }

  if (esito) {
    const quando = new Date(esito.dataOra)
    return (
      <>
        <header className="mb-7">
          <h1 className="view-title">Appuntamento confermato</h1>
          <p className="view-sub">Nessuna conferma da attendere: l'hai preso tu.</p>
        </header>
        <div className="card">
          <span className="stato stato-confermato">Confermato</span>
          <p className="mt-3">
            <strong>{esito.pazienteNome}</strong> — {FORMATO_LUNGO.format(quando)},{' '}
            <span className="mono">{FORMATO_ORA.format(quando)}</span> con {esito.fisioterapistaNome} ·{' '}
            {esito.durataMinuti} min · {esito.percorso === 'Ssn' ? 'Convenzionato SSN' : 'Privato'}
          </p>
          {esito.percorso === 'Ssn' && esito.ricettaId === null && (
            <p
              className="mt-3 rounded-lg px-3 py-2 text-[0.86rem]"
              style={{ background: 'var(--warm-soft)' }}
            >
              Ricetta ancora da consegnare: ricordati di farla portare alla prima seduta. Finché
              non è validata, il ciclo non è aperto e la durata resta quella che hai indicato tu.
            </p>
          )}
          {schedaCreata && (
            <p
              className="mt-3 rounded-lg px-3 py-2 text-[0.86rem]"
              style={{ background: 'var(--surface-2)' }}
            >
              La scheda di {schedaCreata.nome} {schedaCreata.cognome} è provvisoria: il consenso
              privacy e quello ai dati sanitari vanno raccolti di persona alla prima seduta,
              prima che il fisioterapista scriva qualsiasi dato clinico.
            </p>
          )}
          <button className="btn btn-primary mt-4" onClick={ricomincia}>
            Prenota un altro appuntamento
          </button>
        </div>
      </>
    )
  }

  return (
    <>
      <header className="mb-7">
        <h1 className="view-title">Nuovo appuntamento</h1>
        <p className="view-sub">
          Prenotazione per conto del paziente — al telefono o allo sportello. Nasce già
          confermata, perché il confermatore sei tu.
        </p>
      </header>

      {errore && (
        <p
          className="mb-4 rounded-lg px-3 py-2 text-sm"
          style={{ background: 'var(--danger-soft)', color: 'var(--danger)' }}
          role="alert"
        >
          {errore}
        </p>
      )}

      <div className="flex flex-col gap-4">
        <section className="card">
          <h2 className="eyebrow mb-3">Chi</h2>
          <div className="mb-4 flex flex-wrap gap-2">
            <button
              className={`btn btn-sm ${modalita === 'esistente' ? 'btn-primary' : 'btn-ghost'}`}
              onClick={() => {
                setModalita('esistente')
                setErrore(null)
              }}
            >
              Paziente esistente
            </button>
            <button
              className={`btn btn-sm ${modalita === 'nuovo' ? 'btn-primary' : 'btn-ghost'}`}
              onClick={() => {
                setModalita('nuovo')
                setPazienteScelto(null)
                setErrore(null)
              }}
            >
              Nuovo (telefono)
            </button>
          </div>

          {modalita === 'esistente' ? (
            <>
              <div className="field">
                <label htmlFor="ricerca">Cerca paziente</label>
                <input
                  id="ricerca"
                  value={ricerca}
                  onChange={(e) => setRicerca(e.target.value)}
                  placeholder="Nome, codice fiscale o telefono…"
                />
              </div>
              <div className="flex flex-col gap-2">
                {risultati.length === 0 && (
                  <p className="text-[0.86rem]" style={{ color: 'var(--muted)' }}>
                    Nessuna scheda trovata. Se non è mai stato in studio, crea una scheda nuova.
                  </p>
                )}
                {risultati.map((paziente) => {
                  const scelto = pazienteScelto?.id === paziente.id
                  return (
                    <button
                      key={paziente.id}
                      className={`btn ${scelto ? 'btn-primary' : 'btn-ghost'}`}
                      style={{ textAlign: 'left' }}
                      onClick={() => {
                        setPazienteScelto(scelto ? null : paziente)
                        setSelezione(null)
                      }}
                    >
                      {paziente.nome} {paziente.cognome}
                      <span style={{ fontWeight: 400, opacity: 0.8 }}>
                        {paziente.telefono ? ` · ${paziente.telefono}` : ''}
                        {paziente.codiceFiscale ? ` · ${paziente.codiceFiscale}` : ''}
                        {paziente.stato === 'Provvisorio' ? ' · scheda provvisoria' : ''}
                      </span>
                    </button>
                  )
                })}
              </div>
            </>
          ) : (
            <>
              <div className="grid gap-x-4 sm:grid-cols-2">
                <div className="field">
                  <label htmlFor="na-nome">Nome *</label>
                  <input
                    id="na-nome"
                    value={nuovo.nome}
                    onChange={(e) => setNuovo({ ...nuovo, nome: e.target.value })}
                  />
                </div>
                <div className="field">
                  <label htmlFor="na-cognome">Cognome *</label>
                  <input
                    id="na-cognome"
                    value={nuovo.cognome}
                    onChange={(e) => setNuovo({ ...nuovo, cognome: e.target.value })}
                  />
                </div>
                <div className="field">
                  <label htmlFor="na-telefono">Telefono *</label>
                  <input
                    id="na-telefono"
                    value={nuovo.telefono}
                    onChange={(e) => setNuovo({ ...nuovo, telefono: e.target.value })}
                  />
                </div>
                <div className="field">
                  <label htmlFor="na-cf">Codice fiscale (opzionale)</label>
                  <input
                    id="na-cf"
                    value={nuovo.codiceFiscale}
                    onChange={(e) => setNuovo({ ...nuovo, codiceFiscale: e.target.value })}
                  />
                </div>
                <div className="field">
                  <label htmlFor="na-email">Email (opzionale)</label>
                  <input
                    id="na-email"
                    type="email"
                    value={nuovo.email}
                    onChange={(e) => setNuovo({ ...nuovo, email: e.target.value })}
                  />
                </div>
              </div>
              {possibiliDoppioni.length > 0 && !schedaCreata && (
                <div
                  className="mb-3 rounded-lg px-3 py-2 text-[0.86rem]"
                  style={{ background: 'var(--warm-soft)' }}
                >
                  Esiste già una scheda che somiglia a questa. Se è la stessa persona, usala:
                  una seconda scheda spezzerebbe il suo storico in due.
                  <div className="mt-2 flex flex-col gap-2">
                    {possibiliDoppioni.map((paziente) => (
                      <button
                        key={paziente.id}
                        className="btn btn-ghost btn-sm"
                        style={{ textAlign: 'left' }}
                        onClick={() => {
                          setModalita('esistente')
                          setPazienteScelto(paziente)
                          setRicerca(paziente.cognome)
                          setSelezione(null)
                        }}
                      >
                        Usa la scheda di {paziente.nome} {paziente.cognome}
                        {paziente.telefono ? ` · ${paziente.telefono}` : ''}
                      </button>
                    ))}
                  </div>
                </div>
              )}

              <p className="text-[0.8rem]" style={{ color: 'var(--muted)' }}>
                Il consenso privacy e quello ai dati sanitari non si raccolgono per telefono:
                si firmano di persona alla prima seduta. Questa scheda serve solo a bloccare
                lo slot.
              </p>
              {schedaCreata && (
                <p className="mt-2 text-[0.8rem]" style={{ color: 'var(--muted)' }}>
                  Scheda già creata per {schedaCreata.nome} {schedaCreata.cognome}: riprovando
                  non ne nasce una seconda.
                </p>
              )}
            </>
          )}
        </section>

        <section className="card">
          <h2 className="eyebrow mb-3">Percorso</h2>
          <div className="mb-4 flex flex-wrap gap-2">
            {(['Privato', 'Ssn'] as const).map((valore) => (
              <button
                key={valore}
                className={`btn btn-sm ${percorso === valore ? 'btn-primary' : 'btn-ghost'}`}
                onClick={() => {
                  setPercorso(valore)
                  setSelezione(null)
                }}
              >
                {valore === 'Privato' ? 'Privato' : 'Convenzionato SSN'}
              </button>
            ))}
          </div>

          {percorso === 'Ssn' && (
            <div className="field">
              <label htmlFor="na-ricetta">Ricetta</label>
              <select
                id="na-ricetta"
                value={ricettaId ?? ''}
                onChange={(e) => {
                  setRicettaId(e.target.value ? Number(e.target.value) : null)
                  setSelezione(null)
                }}
              >
                <option value="">La ricetta non è ancora stata consegnata</option>
                {percorsi?.ricetteSsn.map((r) => (
                  <option key={r.id} value={r.id}>
                    {r.numeroONre} · {r.distrettiCorporei}
                    {r.seduteResidue !== null ? ` · ${r.seduteResidue} sedute residue` : ''}
                  </option>
                ))}
              </select>
              {ricettaScelta === null && (
                <p
                  className="mt-2 rounded-lg px-3 py-2 text-[0.86rem]"
                  style={{ background: 'var(--warm-soft)' }}
                >
                  Al telefono la ricetta non si può fotografare: prenota lo stesso e falla
                  portare alla prima seduta, la validi quando arriva. La durata qui sotto è
                  quella che concordi a voce.
                </p>
              )}
            </div>
          )}

          {(percorso === 'Privato' || ricettaScelta === null) && (
            <div className="field">
              <label>Durata della seduta</label>
              <div className="flex flex-wrap gap-2">
                {DURATE.map((valore) => (
                  <button
                    key={valore}
                    className={`btn btn-sm ${durata === valore ? 'btn-primary' : 'btn-ghost'}`}
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
          )}

          {percorso === 'Privato' && modalita === 'esistente' && (
            <div className="field">
              <label htmlFor="na-pacchetto">Da scalare dal pacchetto</label>
              <select
                id="na-pacchetto"
                value={pacchettoId ?? ''}
                onChange={(e) => setPacchettoId(e.target.value ? Number(e.target.value) : null)}
                disabled={pazienteScelto === null}
              >
                <option value="">Nessun pacchetto — seduta da saldare a parte</option>
                {percorsi?.pacchettiPrivati.map((p) => (
                  <option key={p.id} value={p.id}>
                    {p.seduteResidue} sedute residue su {p.seduteTotali}
                  </option>
                ))}
              </select>
            </div>
          )}
        </section>

        <section className="card">
          <h2 className="eyebrow mb-3">Quando</h2>
          <div className="mb-4 flex flex-wrap gap-2">
            <button
              className={`btn btn-sm ${cercaPer === 'fisioterapista' ? 'btn-primary' : 'btn-ghost'}`}
              onClick={() => {
                setCercaPer('fisioterapista')
                setSelezione(null)
              }}
            >
              Cerca per fisioterapista
            </button>
            <button
              className={`btn btn-sm ${cercaPer === 'orario' ? 'btn-primary' : 'btn-ghost'}`}
              onClick={() => {
                setCercaPer('orario')
                setSelezione(null)
              }}
            >
              Cerca per orario
            </button>
          </div>

          {cercaPer === 'fisioterapista' ? (
            <>
              <div className="field">
                <label htmlFor="na-terapista">Fisioterapista</label>
                <select
                  id="na-terapista"
                  value={fisioterapistaId ?? ''}
                  onChange={(e) => {
                    setFisioterapistaId(Number(e.target.value))
                    setSelezione(null)
                  }}
                >
                  {fisioterapisti.map((f) => (
                    <option key={f.id} value={f.id}>
                      {f.nome}
                    </option>
                  ))}
                </select>
              </div>

              {fisioterapistaId !== null && (
                <GrigliaDisponibilita
                  fisioterapistaId={fisioterapistaId}
                  durataMinuti={durataEffettiva}
                  selezione={selezione}
                  onSeleziona={setSelezione}
                />
              )}
            </>
          ) : selezione && fisioterapistaId !== null ? (
            <div className="flex flex-wrap items-center justify-between gap-3">
              <span className="text-[0.9rem]">
                <strong>{fisioterapisti.find((f) => f.id === fisioterapistaId)?.nome}</strong> —{' '}
                {FORMATO_LUNGO.format(selezione.inizio)},{' '}
                <span className="mono">
                  {FORMATO_ORA.format(selezione.inizio)}–{FORMATO_ORA.format(selezione.fine)}
                </span>
              </span>
              <button className="btn btn-ghost btn-sm" onClick={() => setSelezione(null)}>
                Cambia
              </button>
            </div>
          ) : (
            <GrigliaLiberoOccupato
              durataMinuti={durataEffettiva}
              onScegli={(idScelto, orario) => {
                setFisioterapistaId(idScelto)
                setSelezione({ inizio: orario, fine: new Date(orario.getTime() + durataEffettiva * 60_000) })
              }}
            />
          )}
        </section>

        <div className="flex flex-wrap items-center gap-3">
          <button className="btn btn-primary" disabled={!puoConfermare} onClick={() => void conferma()}>
            {inCorso ? 'Salvataggio…' : 'Conferma appuntamento'}
          </button>
          {selezione && (
            <span className="text-[0.86rem]" style={{ color: 'var(--muted)' }}>
              {FORMATO_LUNGO.format(selezione.inizio)},{' '}
              <span className="mono">
                {FORMATO_ORA.format(selezione.inizio)}–{FORMATO_ORA.format(selezione.fine)}
              </span>
            </span>
          )}
        </div>
      </div>
    </>
  )
}
