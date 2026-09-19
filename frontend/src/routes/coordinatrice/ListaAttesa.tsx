import { useEffect, useState } from 'react'
import { api } from '../../api/client'
import type { AvvisoDisponibilita } from '../../api/types'

const FORMATO_LUNGO = new Intl.DateTimeFormat('it-IT', {
  weekday: 'long', day: 'numeric', month: 'long', year: 'numeric',
})
const FORMATO_ORA = new Intl.DateTimeFormat('it-IT', { hour: '2-digit', minute: '2-digit' })
const FORMATO_BREVE = new Intl.DateTimeFormat('it-IT', { day: 'numeric', month: 'long' })

/**
 * La lista d'attesa: chi ha chiesto di essere spostato prima, se si libera qualcosa.
 *
 * Ogni voce porta l'appuntamento che il paziente ha già in mano — nessuno è qui *invece*
 * di avere un appuntamento (requisiti.md). L'ordine è per anzianità di attesa, che è
 * l'unico criterio equo quando lo slot che si libera è uno solo, e il numero di giorni
 * è mostrato apposta: spiega perché una voce viene prima di un'altra.
 */
export default function ListaAttesa() {
  const [voci, setVoci] = useState<AvvisoDisponibilita[] | null>(null)
  const [errore, setErrore] = useState<string | null>(null)
  const [inCorso, setInCorso] = useState(false)
  const [apertoId, setApertoId] = useState<number | null>(null)
  const [testo, setTesto] = useState('')
  const [nuovoSlot, setNuovoSlot] = useState('')

  async function carica() {
    try {
      setVoci(await api.get<AvvisoDisponibilita[]>('/api/avvisi-disponibilita'))
      setErrore(null)
    } catch (e) {
      setErrore(e instanceof Error ? e.message : 'Lista non caricata.')
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
      setApertoId(null)
      setTesto('')
      setNuovoSlot('')
      await carica()
    } catch (e) {
      setErrore(e instanceof Error ? e.message : 'Operazione non riuscita.')
    } finally {
      setInCorso(false)
    }
  }

  if (!voci) {
    return <p style={{ color: errore ? 'var(--danger)' : 'var(--muted)' }}>{errore ?? 'Caricamento…'}</p>
  }

  return (
    <>
      <header className="mb-7">
        <h1 className="view-title">Lista d'attesa</h1>
        <p className="view-sub">
          Chi vuole essere spostato prima se si libera un posto. Ognuno ha già il suo
          appuntamento: qui si chiede solo di anticiparlo.
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

      {voci.length === 0 && (
        <div className="card" style={{ color: 'var(--muted)' }}>
          Nessuno in lista d'attesa.
        </div>
      )}

      <div className="flex flex-col gap-3">
        {voci.map((voce) => {
          const quando = new Date(voce.appuntamentoDataOra)
          return (
            <article key={voce.id} className="card">
              <div className="flex flex-wrap items-start justify-between gap-3">
                <div>
                  <div style={{ fontWeight: 700 }}>{voce.pazienteNome}</div>
                  <div className="text-[0.88rem]" style={{ color: 'var(--muted)' }}>
                    Ha {FORMATO_LUNGO.format(quando)},{' '}
                    <span className="mono">{FORMATO_ORA.format(quando)}</span> con{' '}
                    {voce.fisioterapistaNome} ·{' '}
                    {voce.percorso === 'Ssn' ? 'Convenzionato SSN' : 'Privato'}
                  </div>
                </div>
                <span className={`stato ${voce.stato === 'Avvisato' ? 'stato-confermato' : 'stato-richiesto'}`}>
                  {voce.stato === 'Avvisato'
                    ? `Avvisato${voce.avvisatoIl ? ` il ${FORMATO_BREVE.format(new Date(voce.avvisatoIl))}` : ''}`
                    : `In attesa da ${voce.giorniDiAttesa} giorn${voce.giorniDiAttesa === 1 ? 'o' : 'i'}`}
                </span>
              </div>

              <div className="mt-3 flex flex-wrap gap-2">
                <button
                  className="btn btn-primary btn-sm"
                  disabled={inCorso}
                  onClick={() => {
                    setApertoId(apertoId === voce.id ? null : voce.id)
                    setTesto('')
                    setNuovoSlot('')
                  }}
                >
                  {voce.stato === 'Avvisato' ? 'Avvisa di nuovo' : 'Avvisa che si è liberato un posto'}
                </button>
                <button
                  className="btn btn-ghost btn-sm"
                  disabled={inCorso}
                  onClick={() => void esegui(() => api.put(`/api/avvisi-disponibilita/${voce.id}/chiudi`))}
                >
                  Togli dalla lista
                </button>
              </div>

              {apertoId === voce.id && (
                <div className="mt-3">
                  {/* Lo slot non è un dettaglio del messaggio: è il termine di paragone su
                      cui si misura se vale la pena disturbare il paziente. */}
                  <label htmlFor={`slot-${voce.id}`}>Che posto si è liberato</label>
                  <input
                    id={`slot-${voce.id}`}
                    type="datetime-local"
                    value={nuovoSlot}
                    onChange={(e) => setNuovoSlot(e.target.value)}
                  />
                  <label className="mt-2 block" htmlFor={`testo-${voce.id}`}>
                    Da aggiungere al messaggio (facoltativo)
                  </label>
                  <input
                    id={`testo-${voce.id}`}
                    value={testo}
                    onChange={(e) => setTesto(e.target.value)}
                    placeholder="Es. la richiamiamo noi in mattinata."
                  />
                  <p className="mt-2 text-[0.8rem]" style={{ color: 'var(--muted)' }}>
                    Lo spostamento non è automatico: l'avviso dice che c'è un posto, poi
                    l'appuntamento lo sposti tu quando il paziente accetta.
                  </p>
                  <button
                    className="btn btn-primary btn-sm mt-2"
                    disabled={inCorso || !nuovoSlot}
                    onClick={() =>
                      void esegui(() =>
                        api.put(`/api/avvisi-disponibilita/${voce.id}/avvisa`, {
                          nuovoSlot: `${nuovoSlot}:00`,
                          testo: testo.trim() || null,
                        }),
                      )
                    }
                  >
                    {inCorso ? 'Invio…' : 'Invia avviso'}
                  </button>
                </div>
              )}
            </article>
          )
        })}
      </div>

      <p className="mt-4 text-[0.8rem]" style={{ color: 'var(--muted)' }}>
        In questa versione dimostrativa l'avviso non parte davvero: resta registrato fra le
        notifiche, senza email né SMS.
      </p>
    </>
  )
}
