import { useEffect, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { api } from '../../api/client'
import type { Ricetta } from '../../api/types'

const EURO = new Intl.NumberFormat('it-IT', { style: 'currency', currency: 'EUR' })
const FORMATO_DATA = new Intl.DateTimeFormat('it-IT', { day: 'numeric', month: 'long', year: 'numeric' })

const soloData = (iso: string) => FORMATO_DATA.format(new Date(`${iso}T00:00:00`))

export default function RicetteSsn() {
  // Arriva dal collegamento "Vedi ricetta" nella coda Richieste: dice quale ricetta
  // evidenziare, dato che la coda qui può averne più d'una (CLAUDE.md, 15 settembre 2026).
  const [searchParams] = useSearchParams()
  const evidenziata = Number(searchParams.get('ricettaId')) || null

  const [ricette, setRicette] = useState<Ricetta[] | null>(null)
  const [errore, setErrore] = useState<string | null>(null)
  const [inCorso, setInCorso] = useState(false)
  const [aperta, setAperta] = useState<number | null>(null)
  const [azione, setAzione] = useState<'valida' | 'integrazione' | 'rifiuta' | null>(null)

  // Campi che la Coordinatrice conferma o corregge in validazione — "quel che il sistema
  // non può sapere" (requisiti.md): li legge dal documento, non arrivano già giusti.
  const [sedute, setSedute] = useState(10)
  const [ticket, setTicket] = useState(0)
  // Quanto proporre quando la ricetta non porta un importo suo: il listino lo dice, e
  // prima non lo leggeva nessuno — in validazione compariva uno zero da riscrivere.
  const [quotaPredefinita, setQuotaPredefinita] = useState(0)
  const [distretti, setDistretti] = useState('')
  const [finestra, setFinestra] = useState('')
  const [esenzione, setEsenzione] = useState(false)
  const [codiceEsenzione, setCodiceEsenzione] = useState('')
  const [motivo, setMotivo] = useState('')

  async function carica() {
    try {
      const [coda, quota] = await Promise.all([
        api.get<Ricetta[]>('/api/ricette'),
        api.get<{ importo: number }>('/api/ricette/quota-ticket-predefinita'),
      ])
      setRicette(coda)
      setQuotaPredefinita(quota.importo)
      setErrore(null)
    } catch (e) {
      setErrore(e instanceof Error ? e.message : 'Coda non caricata.')
    }
  }

  useEffect(() => {
    void carica()
  }, [])

  // Lo scroll aspetta che la coda sia arrivata: prima che ricette esista l'elemento non è
  // ancora nel DOM.
  useEffect(() => {
    if (!ricette || evidenziata === null) return
    document.getElementById(`ricetta-${evidenziata}`)?.scrollIntoView({ behavior: 'smooth', block: 'center' })
  }, [ricette, evidenziata])

  function apri(ricetta: Ricetta, quale: 'valida' | 'integrazione' | 'rifiuta') {
    setAperta(ricetta.id)
    setAzione(quale)
    setSedute(ricetta.numeroSeduteProscritte)
    setTicket(ricetta.importoTicket ?? quotaPredefinita)
    setDistretti(ricetta.distrettiCorporei ?? '')
    setFinestra(ricetta.finestraCompletamento ?? '')
    setEsenzione(ricetta.esenzione)
    setCodiceEsenzione(ricetta.codiceEsenzione ?? '')
    setMotivo('')
  }

  async function esegui(chiamata: () => Promise<unknown>) {
    setInCorso(true)
    setErrore(null)
    try {
      await chiamata()
      setAperta(null)
      setAzione(null)
      await carica()
    } catch (e) {
      setErrore(e instanceof Error ? e.message : 'Operazione non riuscita.')
    } finally {
      setInCorso(false)
    }
  }

  if (!ricette) {
    return <p style={{ color: errore ? 'var(--danger)' : 'var(--muted)' }}>{errore ?? 'Caricamento…'}</p>
  }

  return (
    <>
      <header className="mb-7">
        <h1 className="view-title">Ricette SSN</h1>
        <p className="view-sub">
          In ordine di urgenza d'agenda, non di anzianità del documento: prima le ricette che
          stanno tenendo fermi degli appuntamenti.
        </p>
      </header>

      {errore && (
        <p className="mb-4 rounded-lg px-3 py-2 text-sm" style={{ background: 'var(--danger-soft)', color: 'var(--danger)' }} role="alert">
          {errore}
        </p>
      )}

      {ricette.length === 0 && <div className="card" style={{ color: 'var(--muted)' }}>Nessuna ricetta da lavorare.</div>}

      <div className="flex flex-col gap-3">
        {ricette.map((ricetta) => (
          <article
            key={ricetta.id}
            id={`ricetta-${ricetta.id}`}
            className="card"
            style={
              ricetta.id === evidenziata
                ? { outline: '2px solid var(--accent)', outlineOffset: '2px' }
                : undefined
            }
          >
            <div className="flex flex-wrap items-start justify-between gap-3">
              <div>
                <div style={{ fontWeight: 700 }}>{ricetta.pazienteNome}</div>
                <div className="mono text-[0.84rem]" style={{ color: 'var(--muted)' }}>
                  {ricetta.numeroONre}
                </div>
                <div className="mt-1 text-[0.86rem]" style={{ color: 'var(--muted)' }}>
                  {ricetta.medicoPrescrittore} · emessa il {soloData(ricetta.dataEmissione)}
                  {ricetta.scadenza ? ` · valida fino al ${soloData(ricetta.scadenza)}` : ''}
                </div>
                <div className="mt-1 text-[0.86rem]">{ricetta.quesitoDiagnostico}</div>
              </div>

              <div className="flex flex-col items-end gap-1">
                <span className={`stato ${ricetta.stato === 'IntegrazioneRichiesta' ? 'stato-neutro' : 'stato-richiesto'}`}>
                  {ricetta.stato === 'IntegrazioneRichiesta' ? 'In attesa di integrazione' : 'Da validare'}
                </span>
                {/* Quanti slot sta tenendo fermi è il criterio d'ordine della coda:
                    mostrarlo spiega perché questa ricetta viene prima di un'altra. */}
                {ricetta.appuntamentiBloccati > 0 && (
                  <span className="text-[0.8rem]" style={{ color: 'var(--warm)' }}>
                    {ricetta.appuntamentiBloccati === 1
                      ? 'tiene fermo 1 appuntamento'
                      : `tiene fermi ${ricetta.appuntamentiBloccati} appuntamenti`}
                  </span>
                )}
                {ricetta.priorizzataPerAttesa && (
                  <span className="text-[0.8rem]" style={{ color: 'var(--danger)' }}>
                    attesa oltre la soglia
                  </span>
                )}
              </div>
            </div>

            {ricetta.appuntoSegreteria && (
              <p className="mt-3 rounded-lg px-3 py-2 text-[0.84rem]" style={{ background: 'var(--surface-2)' }}>
                {ricetta.appuntoSegreteria}
              </p>
            )}

            <div className="mt-3 flex flex-wrap gap-2">
              <button className="btn btn-primary btn-sm" disabled={inCorso} onClick={() => apri(ricetta, 'valida')}>
                Valida
              </button>
              <button className="btn btn-ghost btn-sm" disabled={inCorso} onClick={() => apri(ricetta, 'integrazione')}>
                Chiedi un'integrazione
              </button>
              <button className="btn btn-danger btn-sm" disabled={inCorso} onClick={() => apri(ricetta, 'rifiuta')}>
                Respingi
              </button>
            </div>

            {aperta === ricetta.id && azione === 'valida' && (
              <div className="mt-4 rounded-xl p-3" style={{ background: 'var(--surface-2)' }}>
                <div className="grid gap-3 sm:grid-cols-2">
                  <div className="field mb-0">
                    <label htmlFor={`sedute-${ricetta.id}`}>Sedute da autorizzare</label>
                    <input id={`sedute-${ricetta.id}`} type="number" min={1} value={sedute} onChange={(e) => setSedute(Number(e.target.value))} />
                  </div>
                  <div className="field mb-0">
                    <label htmlFor={`distretti-${ricetta.id}`}>Distretti da trattare</label>
                    <input id={`distretti-${ricetta.id}`} value={distretti} onChange={(e) => setDistretti(e.target.value)} />
                  </div>
                  <div className="field mb-0">
                    <label htmlFor={`finestra-${ricetta.id}`}>Da completare entro</label>
                    <input id={`finestra-${ricetta.id}`} type="date" value={finestra} onChange={(e) => setFinestra(e.target.value)} />
                  </div>
                  <div className="field mb-0">
                    <label htmlFor={`ticket-${ricetta.id}`}>Ticket dovuto</label>
                    <input id={`ticket-${ricetta.id}`} type="number" step="0.01" value={ticket} onChange={(e) => setTicket(Number(e.target.value))} />
                    {ricetta.importoTicket === null && !esenzione && (
                      <p className="mt-1 text-[0.78rem]" style={{ color: 'var(--muted)' }}>
                        Proposto dal listino (quota provvisoria): controllalo sulla ricetta.
                      </p>
                    )}
                  </div>
                </div>

                <label className="mt-3 flex items-center gap-2 text-[0.86rem]" style={{ fontWeight: 400, color: 'var(--ink)' }}>
                  <input type="checkbox" className="w-auto" checked={esenzione} onChange={(e) => setEsenzione(e.target.checked)} />
                  Esente dal ticket
                </label>
                {esenzione && (
                  <div className="field mt-2 max-w-xs">
                    <label htmlFor={`codice-${ricetta.id}`}>Codice di esenzione</label>
                    <input id={`codice-${ricetta.id}`} value={codiceEsenzione} onChange={(e) => setCodiceEsenzione(e.target.value)} />
                  </div>
                )}

                {/* Cosa produce la validazione, detto prima di confermarla: è il punto in cui
                    la ricetta smette di essere un documento e diventa un ciclo con effetti
                    economici e di agenda (requisiti.md). */}
                <p className="mt-3 rounded-lg px-3 py-2 text-[0.84rem]" style={{ background: 'var(--warm-soft)' }}>
                  Validando apri un ciclo di <strong>{sedute} sedute</strong>
                  {distretti ? ` da ${distretti.split(',').filter((d) => d.trim()).length * 30} minuti l'una` : ''}
                  {finestra ? `, da completare entro il ${soloData(finestra)}` : ''}. In cassa comparirà{' '}
                  {esenzione ? 'un ticket a zero con l\'esenzione registrata' : `un ticket di ${EURO.format(ticket)}`}.
                </p>

                <button
                  className="btn btn-primary btn-sm mt-3"
                  disabled={inCorso || !finestra}
                  onClick={() =>
                    void esegui(() =>
                      api.put(`/api/ricette/${ricetta.id}/valida`, {
                        numeroSedute: sedute,
                        importoTicket: esenzione ? 0 : ticket,
                        esenzione,
                        codiceEsenzione: esenzione ? codiceEsenzione : null,
                        distrettiCorporei: distretti,
                        finestraCompletamento: finestra,
                      }),
                    )
                  }
                >
                  Conferma la validazione
                </button>
              </div>
            )}

            {aperta === ricetta.id && (azione === 'integrazione' || azione === 'rifiuta') && (
              <div className="mt-4">
                <label htmlFor={`motivo-${ricetta.id}`}>
                  {azione === 'integrazione' ? 'Cosa manca' : 'Perché viene respinta'}
                </label>
                <input id={`motivo-${ricetta.id}`} value={motivo} onChange={(e) => setMotivo(e.target.value)} />

                {azione === 'integrazione' ? (
                  <>
                    <p className="mt-2 text-[0.82rem]" style={{ color: 'var(--muted)' }}>
                      Gli appuntamenti già fissati non si toccano: il paziente può ancora portare
                      il documento in studio.
                    </p>
                    <button
                      className="btn btn-primary btn-sm mt-2"
                      disabled={inCorso || !motivo.trim()}
                      onClick={() => void esegui(() => api.put(`/api/ricette/${ricetta.id}/integrazione`, { motivo: motivo.trim() }))}
                    >
                      Chiedi l'integrazione
                    </button>
                  </>
                ) : (
                  <>
                    {/* Respingere non basta: va deciso cosa ne è degli appuntamenti in
                        agenda, e nessuna di queste scelte è automatica (requisiti.md). */}
                    <p className="mt-2 text-[0.82rem]" style={{ color: 'var(--muted)' }}>
                      Decidi anche che fine fanno gli appuntamenti collegati.
                    </p>
                    <div className="mt-2 flex flex-col gap-2">
                      {(
                        [
                          ['Mantieni', 'Mantieni', 'Restano in agenda, segnalati senza ricetta valida'],
                          ['Mantieni', 'ConvertiPrivato', 'Converti i confermati a percorso privato'],
                          ['Annulla', 'Annulla', 'Annulla tutto e libera gli slot'],
                        ] as const
                      ).map(([richiesti, confermati, descrizione]) => (
                        <button
                          key={confermati}
                          className="btn btn-ghost btn-sm"
                          disabled={inCorso || !motivo.trim()}
                          onClick={() =>
                            void esegui(() =>
                              api.put(`/api/ricette/${ricetta.id}/rifiuta`, {
                                motivo: motivo.trim(),
                                esitoRichiesti: richiesti,
                                esitoConfermati: confermati,
                              }),
                            )
                          }
                        >
                          {descrizione}
                        </button>
                      ))}
                    </div>
                  </>
                )}
              </div>
            )}
          </article>
        ))}
      </div>
    </>
  )
}
