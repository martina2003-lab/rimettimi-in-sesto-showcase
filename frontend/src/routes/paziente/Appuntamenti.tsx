import { useEffect, useState } from 'react'
import { api } from '../../api/client'
import type { Appuntamento, AvvisoDisponibilita } from '../../api/types'
import { usePazienteAttivo } from '../../paziente/PazienteAttivoContext'

const FORMATO_DATA = new Intl.DateTimeFormat('it-IT', {
  weekday: 'long', day: 'numeric', month: 'long', year: 'numeric',
})
const FORMATO_ORA = new Intl.DateTimeFormat('it-IT', { hour: '2-digit', minute: '2-digit' })

function classeStato(stato: Appuntamento['stato']) {
  if (stato === 'Confermato') return 'stato stato-confermato'
  if (stato === 'Richiesto') return 'stato stato-richiesto'
  if (stato === 'Annullato' || stato === 'NoShow') return 'stato stato-annullato'
  return 'stato stato-neutro'
}

export default function AppuntamentiPaziente() {
  const [appuntamenti, setAppuntamenti] = useState<Appuntamento[] | null>(null)
  const [errore, setErrore] = useState<string | null>(null)
  const [azioneInCorso, setAzioneInCorso] = useState<number | null>(null)
  const [avvisi, setAvvisi] = useState<AvvisoDisponibilita[]>([])
  const { pazienteId } = usePazienteAttivo()

  async function carica() {
    try {
      const [elenco, attesa] = await Promise.all([
        api.get<Appuntamento[]>('/api/appuntamenti/miei'),
        api.get<AvvisoDisponibilita[]>('/api/avvisi-disponibilita/miei'),
      ])
      setAppuntamenti(elenco)
      setAvvisi(attesa)
      setErrore(null)
    } catch (e) {
      setErrore(e instanceof Error ? e.message : 'Caricamento non riuscito.')
    }
  }

  useEffect(() => {
    void carica()
  }, [])

  async function cancella(appuntamento: Appuntamento) {
    setAzioneInCorso(appuntamento.id)
    setErrore(null)
    try {
      await api.put(`/api/appuntamenti/${appuntamento.id}/cancella`)
      await carica()
    } catch (e) {
      // Il messaggio del backend è già scritto per essere letto da una persona
      // (es. il limite delle 24 ore), quindi si mostra così com'è.
      setErrore(e instanceof Error ? e.message : 'Operazione non riuscita.')
    } finally {
      setAzioneInCorso(null)
    }
  }

  // Chiedere di essere spostati prima non fa perdere l'appuntamento che si ha già: è il
  // punto della funzione (requisiti.md), e per questo l'avviso è appeso a una prenotazione
  // confermata invece di essere una coda a sé.
  async function cambiaAvviso(appuntamento: Appuntamento, avvisoAttivo: AvvisoDisponibilita | undefined) {
    setAzioneInCorso(appuntamento.id)
    setErrore(null)
    try {
      if (avvisoAttivo) {
        await api.put(`/api/avvisi-disponibilita/${avvisoAttivo.id}/chiudi`)
      } else {
        await api.post('/api/avvisi-disponibilita', { appuntamentoId: appuntamento.id })
      }
      await carica()
    } catch (e) {
      setErrore(e instanceof Error ? e.message : 'Operazione non riuscita.')
    } finally {
      setAzioneInCorso(null)
    }
  }

  if (errore && !appuntamenti) {
    return <p style={{ color: 'var(--danger)' }}>{errore}</p>
  }

  if (!appuntamenti) {
    return <p style={{ color: 'var(--muted)' }}>Caricamento…</p>
  }

  const adesso = new Date()
  const appuntamentiDelProfilo = appuntamenti.filter((a) => a.pazienteId === pazienteId)
  const futuri = appuntamentiDelProfilo.filter(
    (a) => new Date(a.dataOra) >= adesso && a.stato !== 'Annullato',
  )
  const passati = appuntamentiDelProfilo.filter(
    (a) => new Date(a.dataOra) < adesso || a.stato === 'Annullato',
  )

  return (
    <>
      <header className="mb-7">
        <h1 className="view-title">I tuoi appuntamenti</h1>
        <p className="view-sub">
          Ogni richiesta viene confermata dalla segreteria: nessuna prenotazione si conferma da sola.
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

      <section className="mb-9">
        <h2 className="eyebrow mb-3">In programma</h2>

        {futuri.length === 0 && (
          <div className="card" style={{ color: 'var(--muted)' }}>
            Non hai appuntamenti in programma.
          </div>
        )}

        <div className="flex flex-col gap-3">
          {futuri.map((appuntamento) => {
            const data = new Date(appuntamento.dataOra)
            return (
              <article key={appuntamento.id} className="card">
                <div className="flex flex-wrap items-start justify-between gap-3">
                  <div>
                    <div className="mono text-[1.05rem]" style={{ fontWeight: 600 }}>
                      {FORMATO_ORA.format(data)}
                    </div>
                    <div style={{ fontWeight: 700 }}>{FORMATO_DATA.format(data)}</div>
                    <div className="text-[0.88rem]" style={{ color: 'var(--muted)' }}>
                      {appuntamento.fisioterapistaNome} · {appuntamento.durataMinuti} min ·{' '}
                      {appuntamento.percorso === 'Ssn' ? 'Convenzionato SSN' : 'Privato'}
                    </div>
                  </div>

                  <div className="flex flex-col items-end gap-2">
                    <span className={classeStato(appuntamento.stato)}>{appuntamento.stato}</span>
                    <button
                      className="btn btn-ghost btn-sm"
                      disabled={azioneInCorso === appuntamento.id}
                      onClick={() => void cancella(appuntamento)}
                    >
                      Cancella
                    </button>
                  </div>
                </div>

                {appuntamento.stato === 'Confermato' && (() => {
                  const avviso = avvisi.find((a) => a.appuntamentoId === appuntamento.id)
                  return (
                    <div className="mt-3">
                      <button
                        className={`btn btn-sm ${avviso ? 'btn-primary' : 'btn-ghost'}`}
                        disabled={azioneInCorso === appuntamento.id}
                        onClick={() => void cambiaAvviso(appuntamento, avviso)}
                      >
                        {avviso ? 'Non avvisarmi più' : 'Avvisami se si libera prima'}
                      </button>
                      {avviso && (
                        <p className="mt-2 text-[0.82rem]" style={{ color: 'var(--muted)' }}>
                          {avviso.stato === 'Avvisato'
                            ? "Lo studio ti ha già segnalato un posto libero: l'appuntamento qui sopra resta valido finché non decidi di spostarlo."
                            : 'Se si libera un posto prima, lo studio ti avvisa. Questo appuntamento resta tuo comunque.'}
                        </p>
                      )}
                    </div>
                  )
                })()}

                {appuntamento.modificaRichiestaDataOra && (
                  <p
                    className="mt-3 rounded-lg px-3 py-2 text-[0.84rem]"
                    style={{ background: 'var(--warm-soft)' }}
                  >
                    Hai chiesto di spostarlo al{' '}
                    <strong>
                      {FORMATO_DATA.format(new Date(appuntamento.modificaRichiestaDataOra))} alle{' '}
                      {FORMATO_ORA.format(new Date(appuntamento.modificaRichiestaDataOra))}
                    </strong>
                    . Finché la segreteria non risponde, questo orario resta tuo.
                  </p>
                )}
              </article>
            )
          })}
        </div>
      </section>

      <section>
        <h2 className="eyebrow mb-3">Storico</h2>
        {passati.length === 0 && (
          <div className="card" style={{ color: 'var(--muted)' }}>Nessuna seduta passata.</div>
        )}
        <div className="flex flex-col gap-2">
          {passati.map((appuntamento) => {
            const data = new Date(appuntamento.dataOra)
            return (
              <div
                key={appuntamento.id}
                className="card flex flex-wrap items-center justify-between gap-3 py-3"
              >
                <div>
                  <span className="mono">{FORMATO_ORA.format(data)}</span>{' '}
                  <span style={{ fontWeight: 600 }}>{FORMATO_DATA.format(data)}</span>
                  <div className="text-[0.84rem]" style={{ color: 'var(--muted)' }}>
                    {appuntamento.fisioterapistaNome}
                    {appuntamento.motivoAnnullamento ? ` · ${appuntamento.motivoAnnullamento}` : ''}
                  </div>
                </div>
                <span className={classeStato(appuntamento.stato)}>{appuntamento.stato}</span>
              </div>
            )
          })}
        </div>
      </section>
    </>
  )
}
