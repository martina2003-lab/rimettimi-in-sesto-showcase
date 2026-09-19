import { useEffect, useState } from 'react'
import { api } from '../../api/client'
import type { AppuntamentoImpattato } from '../../api/types'

const FORMATO_LUNGO = new Intl.DateTimeFormat('it-IT', {
  weekday: 'long', day: 'numeric', month: 'long', year: 'numeric',
})
const FORMATO_ORA = new Intl.DateTimeFormat('it-IT', { hour: '2-digit', minute: '2-digit' })
const FORMATO_DATA = new Intl.DateTimeFormat('it-IT', { day: 'numeric', month: 'long', year: 'numeric' })

/**
 * Cosa ne è degli appuntamenti già presi quando un fisioterapista manca.
 *
 * Due strade, mai una automatica: passarlo a un collega libero in quello stesso orario —
 * il paziente non perde il posto e non ricomincia da capo — oppure annullarlo. In entrambi
 * i casi decide lei: requisiti.md dà priorità *implicita* ai dipendenti liberi, cioè un
 * suggerimento a chi guarda, non una regola che sceglie da sé.
 *
 * L'ordine è per urgenza: prima i cicli SSN con la finestra di completamento più vicina,
 * perché è l'unica scadenza che non si può spostare.
 */
export default function AppuntamentiImpattati({ assenzaId }: { assenzaId: number }) {
  const [voci, setVoci] = useState<AppuntamentoImpattato[] | null>(null)
  const [errore, setErrore] = useState<string | null>(null)
  const [inCorso, setInCorso] = useState(false)
  const [annullaId, setAnnullaId] = useState<number | null>(null)
  const [motivo, setMotivo] = useState('')
  const [giaAvvisato, setGiaAvvisato] = useState(true)

  async function carica() {
    try {
      setVoci(await api.get<AppuntamentoImpattato[]>(`/api/assenze/${assenzaId}/appuntamenti-impattati`))
      setErrore(null)
    } catch (e) {
      setErrore(e instanceof Error ? e.message : 'Elenco non caricato.')
    }
  }

  useEffect(() => {
    void carica()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [assenzaId])

  async function esegui(azione: () => Promise<unknown>) {
    setInCorso(true)
    setErrore(null)
    try {
      await azione()
      setAnnullaId(null)
      setMotivo('')
      setGiaAvvisato(true)
      await carica()
    } catch (e) {
      setErrore(e instanceof Error ? e.message : 'Operazione non riuscita.')
    } finally {
      setInCorso(false)
    }
  }

  if (!voci) {
    return <p className="mt-3 text-[0.86rem]" style={{ color: 'var(--muted)' }}>Caricamento…</p>
  }

  return (
    <div className="mt-3">
      {errore && (
        <p
          className="mb-3 rounded-lg px-3 py-2 text-[0.86rem]"
          style={{ background: 'var(--danger-soft)', color: 'var(--danger)' }}
          role="alert"
        >
          {errore}
        </p>
      )}

      {voci.length === 0 ? (
        <p className="text-[0.86rem]" style={{ color: 'var(--muted)' }}>
          Nessun appuntamento cade in questo periodo: non c'è niente da rivedere.
        </p>
      ) : (
        <div className="flex flex-col gap-2">
          {voci.map((voce) => {
            const quando = new Date(voce.dataOra)
            return (
              <div
                key={voce.appuntamentoId}
                className="rounded-lg px-3 py-3"
                style={{ background: 'var(--surface-2)' }}
              >
                <div className="flex flex-wrap items-start justify-between gap-2">
                  <div>
                    <div style={{ fontWeight: 700 }}>{voce.pazienteNome}</div>
                    <div className="text-[0.84rem]" style={{ color: 'var(--muted)' }}>
                      {FORMATO_LUNGO.format(quando)} ·{' '}
                      <span className="mono">{FORMATO_ORA.format(quando)}</span> ·{' '}
                      {voce.durataMinuti} min ·{' '}
                      {voce.percorso === 'Ssn' ? 'Convenzionato SSN' : 'Privato'}
                    </div>
                  </div>
                  {/* Il motivo per cui questa riga viene prima delle altre, scritto. */}
                  {voce.finestraCompletamentoSsn && (
                    <span className="stato stato-richiesto">
                      ciclo da chiudere entro il{' '}
                      {FORMATO_DATA.format(new Date(`${voce.finestraCompletamentoSsn}T00:00:00`))}
                      {voce.seduteResidueSsn !== null ? ` · ${voce.seduteResidueSsn} sedute` : ''}
                    </span>
                  )}
                </div>

                <div className="mt-3 flex flex-wrap items-center gap-2">
                  {voce.colleghiDisponibili.length === 0 ? (
                    <span className="text-[0.82rem]" style={{ color: 'var(--muted)' }}>
                      Nessun collega libero in questo orario.
                    </span>
                  ) : (
                    voce.colleghiDisponibili.map((collega) => (
                      <button
                        key={collega.fisioterapistaId}
                        className="btn btn-ghost btn-sm"
                        disabled={inCorso}
                        onClick={() =>
                          void esegui(() =>
                            api.put(`/api/appuntamenti/${voce.appuntamentoId}/riassegna`, {
                              fisioterapistaId: collega.fisioterapistaId,
                            }),
                          )
                        }
                      >
                        Passa a {collega.nome}
                        {collega.tipoContratto === 'Dipendente' ? ' · dipendente' : ''}
                      </button>
                    ))
                  )}
                  <button
                    className="btn btn-danger btn-sm"
                    disabled={inCorso}
                    onClick={() => {
                      setAnnullaId(annullaId === voce.appuntamentoId ? null : voce.appuntamentoId)
                      setMotivo('')
                      setGiaAvvisato(true)
                    }}
                  >
                    Annulla
                  </button>
                </div>

                {annullaId === voce.appuntamentoId && (
                  <div className="mt-3">
                    <label htmlFor={`motivo-${voce.appuntamentoId}`}>
                      Motivo (lo vede il paziente)
                    </label>
                    <input
                      id={`motivo-${voce.appuntamentoId}`}
                      value={motivo}
                      onChange={(e) => setMotivo(e.target.value)}
                      placeholder="Es. assenza improvvisa della Dott.ssa Ricci."
                    />

                    {/* Nessun invio automatico: per una seduta di domani la cosa seria è
                        una telefonata, e il sistema non deve mandare un secondo messaggio
                        che contraddice quanto già detto a voce. */}
                    <label
                      className="mt-2 flex items-center gap-2"
                      style={{ textTransform: 'none', letterSpacing: 'normal', fontWeight: 400 }}
                    >
                      <input
                        type="checkbox"
                        checked={giaAvvisato}
                        onChange={(e) => setGiaAvvisato(e.target.checked)}
                        style={{ width: 'auto' }}
                      />
                      Ho già avvisato il paziente a voce — non mandare nessuna notifica
                    </label>

                    <button
                      className="btn btn-danger btn-sm mt-2"
                      disabled={inCorso}
                      onClick={() =>
                        void esegui(() =>
                          api.put(`/api/appuntamenti/${voce.appuntamentoId}/annulla-per-assenza`, {
                            motivo: motivo.trim() || null,
                            notificaGiaDataAltrove: giaAvvisato,
                          }),
                        )
                      }
                    >
                      {inCorso ? 'Annullamento…' : 'Conferma annullamento'}
                    </button>
                  </div>
                )}
              </div>
            )
          })}
        </div>
      )}
    </div>
  )
}
