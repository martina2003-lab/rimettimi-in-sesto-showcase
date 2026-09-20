import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { api } from '../../api/client'
import type { Appuntamento, Ricetta } from '../../api/types'

const FORMATO_LUNGO = new Intl.DateTimeFormat('it-IT', {
  weekday: 'long', day: 'numeric', month: 'long', year: 'numeric',
})
const FORMATO_ORA = new Intl.DateTimeFormat('it-IT', { hour: '2-digit', minute: '2-digit' })

/** Valore per un input datetime-local a partire da una data. */
function perInput(data: Date) {
  const p = (n: number) => String(n).padStart(2, '0')
  return `${data.getFullYear()}-${p(data.getMonth() + 1)}-${p(data.getDate())}T${p(data.getHours())}:${p(data.getMinutes())}`
}

export default function RichiesteCoordinatrice() {
  const [richieste, setRichieste] = useState<Appuntamento[] | null>(null)
  // Non per decidere qui: solo per sapere se la ricetta di una richiesta SSN è ancora in
  // coda di validazione, e mostrare il collegamento solo quando ha senso — se è già
  // validata, la coda Ricette non la contiene più.
  const [ricetteInCoda, setRicetteInCoda] = useState<Ricetta[]>([])
  const [errore, setErrore] = useState<string | null>(null)
  const [inCorso, setInCorso] = useState(false)
  const [rifiutoAperto, setRifiutoAperto] = useState<number | null>(null)
  const [motivo, setMotivo] = useState('')
  const [propostaAperta, setPropostaAperta] = useState<number | null>(null)
  const [slot, setSlot] = useState<string[]>(['', '', ''])

  async function carica() {
    try {
      const [daDecidere, ricette] = await Promise.all([
        api.get<Appuntamento[]>('/api/appuntamenti/da-decidere'),
        api.get<Ricetta[]>('/api/ricette'),
      ])
      setRichieste(daDecidere)
      setRicetteInCoda(ricette)
      setErrore(null)
    } catch (e) {
      setErrore(e instanceof Error ? e.message : 'Coda non caricata.')
    }
  }

  useEffect(() => {
    void carica()
  }, [])

  async function esegui(azione: () => Promise<unknown>) {
    setInCorso(true)
    setErrore(null)
    try {
      await azione()
      setRifiutoAperto(null)
      setPropostaAperta(null)
      setMotivo('')
      setSlot(['', '', ''])
      await carica()
    } catch (e) {
      setErrore(e instanceof Error ? e.message : 'Operazione non riuscita.')
    } finally {
      setInCorso(false)
    }
  }

  if (!richieste) {
    return <p style={{ color: errore ? 'var(--danger)' : 'var(--muted)' }}>{errore ?? 'Caricamento…'}</p>
  }

  return (
    <>
      <header className="mb-7">
        <h1 className="view-title">Richieste da decidere</h1>
        <p className="view-sub">
          Ogni slot qui è già bloccato per il paziente che l'ha chiesto: finché resta in coda,
          nessun altro può prenotarlo.
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

      {richieste.length === 0 && (
        <div className="card" style={{ color: 'var(--muted)' }}>Nessuna richiesta in attesa.</div>
      )}

      <div className="flex flex-col gap-3">
        {richieste.map((richiesta) => {
          const spostamento = richiesta.modificaRichiestaDataOra
          const data = new Date(richiesta.dataOra)

          return (
            <article key={richiesta.id} className="card">
              <div className="flex flex-wrap items-start justify-between gap-3">
                <div>
                  <div style={{ fontWeight: 700 }}>{richiesta.pazienteNome}</div>
                  <div className="text-[0.88rem]" style={{ color: 'var(--muted)' }}>
                    {FORMATO_LUNGO.format(data)} · <span className="mono">{FORMATO_ORA.format(data)}</span> ·{' '}
                    {richiesta.fisioterapistaNome} · {richiesta.durataMinuti} min ·{' '}
                    {richiesta.percorso === 'Ssn' ? 'Convenzionato SSN' : 'Privato'}
                  </div>
                </div>
                <span className={`stato ${spostamento ? 'stato-neutro' : 'stato-richiesto'}`}>
                  {spostamento ? 'Spostamento chiesto' : 'Da confermare'}
                </span>
              </div>

              {/* Le due code (Richieste e Ricette SSN) mostrano lo stesso paziente da due
                  punti di vista diversi: qui si dice solo se la ricetta è già in coda o manca
                  ancora, senza duplicare la validazione. */}
              {richiesta.percorso === 'Ssn' && richiesta.ricettaId === null && (
                <p
                  className="mt-3 rounded-lg px-3 py-2 text-[0.86rem]"
                  style={{ background: 'var(--warm-soft)' }}
                >
                  Ricetta non ancora consegnata: va richiesta al paziente prima della seduta.
                </p>
              )}
              {richiesta.percorso === 'Ssn' && richiesta.ricettaId !== null && (() => {
                const ricetta = ricetteInCoda.find((r) => r.id === richiesta.ricettaId)
                if (!ricetta) return null
                return (
                  <p
                    className="mt-3 rounded-lg px-3 py-2 text-[0.86rem]"
                    style={{ background: 'var(--warm-soft)' }}
                  >
                    La ricetta è già in coda di validazione.{' '}
                    <Link to={`/coordinatrice/ricette?ricettaId=${ricetta.id}`} style={{ fontWeight: 600 }}>
                      Vedi ricetta →
                    </Link>
                  </p>
                )
              })()}

              {/* Due decisioni diverse: confermare una richiesta nuova, oppure approvare
                  uno spostamento su un appuntamento che il paziente ha già confermato. */}
              {spostamento ? (
                <>
                  <p
                    className="mt-3 rounded-lg px-3 py-2 text-[0.86rem]"
                    style={{ background: 'var(--warm-soft)' }}
                  >
                    Chiede di spostarlo a{' '}
                    <strong>
                      {FORMATO_LUNGO.format(new Date(spostamento))}, {FORMATO_ORA.format(new Date(spostamento))}
                    </strong>
                    . Finché non decidi, tiene il posto di prima e blocca anche il nuovo.
                  </p>
                  <div className="mt-3 flex flex-wrap gap-2">
                    <button
                      className="btn btn-primary btn-sm"
                      disabled={inCorso}
                      onClick={() => void esegui(() => api.put(`/api/appuntamenti/${richiesta.id}/approva-modifica`))}
                    >
                      Approva lo spostamento
                    </button>
                    <button
                      className="btn btn-ghost btn-sm"
                      disabled={inCorso}
                      onClick={() => void esegui(() => api.put(`/api/appuntamenti/${richiesta.id}/rifiuta-modifica`))}
                    >
                      Rifiuta, resta com'era
                    </button>
                  </div>
                </>
              ) : (
                <>
                  <div className="mt-3 flex flex-wrap gap-2">
                    <button
                      className="btn btn-primary btn-sm"
                      disabled={inCorso}
                      onClick={() => void esegui(() => api.put(`/api/appuntamenti/${richiesta.id}/conferma`))}
                    >
                      Conferma
                    </button>
                    <button
                      className="btn btn-ghost btn-sm"
                      disabled={inCorso}
                      onClick={() => {
                        setPropostaAperta(propostaAperta === richiesta.id ? null : richiesta.id)
                        setRifiutoAperto(null)
                        const base = new Date(richiesta.dataOra)
                        setSlot([perInput(base), '', ''])
                      }}
                    >
                      Proponi un altro orario
                    </button>
                    <button
                      className="btn btn-danger btn-sm"
                      disabled={inCorso}
                      onClick={() => {
                        setRifiutoAperto(rifiutoAperto === richiesta.id ? null : richiesta.id)
                        setPropostaAperta(null)
                      }}
                    >
                      Rifiuta
                    </button>
                  </div>

                  {rifiutoAperto === richiesta.id && (
                    <div className="mt-3">
                      <label htmlFor={`motivo-${richiesta.id}`}>Motivo del rifiuto</label>
                      <input
                        id={`motivo-${richiesta.id}`}
                        value={motivo}
                        onChange={(e) => setMotivo(e.target.value)}
                        placeholder="Lo vede il paziente nella notifica."
                      />
                      <button
                        className="btn btn-danger btn-sm mt-2"
                        disabled={inCorso || !motivo.trim()}
                        onClick={() =>
                          void esegui(() =>
                            api.put(`/api/appuntamenti/${richiesta.id}/rifiuta`, { motivo: motivo.trim() }),
                          )
                        }
                      >
                        Conferma il rifiuto
                      </button>
                    </div>
                  )}

                  {propostaAperta === richiesta.id && (
                    <div className="mt-3">
                      <label>Fino a tre alternative</label>
                      <div className="flex flex-col gap-2">
                        {slot.map((valore, indice) => (
                          <input
                            key={indice}
                            type="datetime-local"
                            value={valore}
                            onChange={(e) =>
                              setSlot(slot.map((v, i) => (i === indice ? e.target.value : v)))
                            }
                          />
                        ))}
                      </div>
                      <p className="mt-2 text-[0.8rem]" style={{ color: 'var(--muted)' }}>
                        Gli orari proposti restano bloccati finché il paziente non risponde o la
                        proposta scade.
                      </p>
                      <button
                        className="btn btn-primary btn-sm mt-2"
                        disabled={inCorso || slot.every((s) => !s)}
                        onClick={() =>
                          void esegui(() =>
                            api.post(`/api/appuntamenti/${richiesta.id}/proposte-slot`, {
                              slotProposti: slot.filter(Boolean).map((s) => `${s}:00`),
                            }),
                          )
                        }
                      >
                        Invia le alternative
                      </button>
                    </div>
                  )}
                </>
              )}
            </article>
          )
        })}
      </div>
    </>
  )
}
