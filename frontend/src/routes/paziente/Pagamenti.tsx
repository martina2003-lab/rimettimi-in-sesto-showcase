import { useEffect, useState } from 'react'
import { api } from '../../api/client'
import type { PagamentiPaziente, PercorsiAttivi } from '../../api/types'
import { usePazienteAttivo } from '../../paziente/PazienteAttivoContext'

const EURO = new Intl.NumberFormat('it-IT', { style: 'currency', currency: 'EUR' })
const FORMATO_DATA = new Intl.DateTimeFormat('it-IT', { day: 'numeric', month: 'long', year: 'numeric' })

export default function Pagamenti() {
  const { pazienteId } = usePazienteAttivo()
  const [pagamenti, setPagamenti] = useState<PagamentiPaziente | null>(null)
  const [percorsi, setPercorsi] = useState<PercorsiAttivi | null>(null)
  const [errore, setErrore] = useState<string | null>(null)
  const [esito, setEsito] = useState<string | null>(null)
  const [inCorso, setInCorso] = useState(false)

  async function carica(id: number) {
    const [p, pe] = await Promise.all([
      api.get<PagamentiPaziente>(`/api/pazienti/${id}/pagamenti`),
      api.get<PercorsiAttivi>(`/api/pazienti/${id}/percorsi-attivi`),
    ])
    setPagamenti(p)
    setPercorsi(pe)
  }

  useEffect(() => {
    if (pazienteId === null) return
    setPagamenti(null)
    carica(pazienteId).catch((e) => setErrore(e instanceof Error ? e.message : 'Pagamenti non caricati.'))
  }, [pazienteId])

  // Pagamento simulato: nessun campo carta è digitabile e nessun dato di pagamento passa
  // dal codice del portale — coerente con la scelta di Stripe Checkout in ARCHITETTURA.md
  // e con il fatto che in versione demo Stripe è in modalità test.
  async function acquista(cosa: 'pacchetti' | 'sedute-singole', metodo: 'Online' | 'InStudio') {
    if (pazienteId === null) return
    setInCorso(true)
    setErrore(null)
    setEsito(null)
    try {
      await api.post(`/api/pazienti/${pazienteId}/${cosa}`, { metodo })
      await carica(pazienteId)
      setEsito(
        metodo === 'Online'
          ? 'Pagamento simulato riuscito. La ricevuta la emette la segreteria.'
          : 'Segnato come da saldare in studio: lo trovi qui sotto finché non viene incassato.',
      )
    } catch (e) {
      setErrore(e instanceof Error ? e.message : 'Operazione non riuscita.')
    } finally {
      setInCorso(false)
    }
  }

  return (
    <>
      <header className="mb-6">
        <h1 className="view-title">Pagamenti</h1>
        <p className="view-sub">Quello che c'è da saldare, il tuo percorso attivo e cosa puoi acquistare.</p>
      </header>

      {errore && (
        <p className="mb-4 rounded-lg px-3 py-2 text-sm" style={{ background: 'var(--danger-soft)', color: 'var(--danger)' }} role="alert">
          {errore}
        </p>
      )}
      {esito && (
        <p className="mb-4 rounded-lg px-3 py-2 text-sm" style={{ background: 'var(--good-soft)', color: 'var(--good)' }}>
          {esito}
        </p>
      )}

      {!pagamenti && !errore && <p style={{ color: 'var(--muted)' }}>Caricamento…</p>}

      {pagamenti && (
        <>
          <section className="mb-7">
            <h2 className="eyebrow mb-3">Da saldare</h2>
            {pagamenti.daSaldare.length === 0 ? (
              <div className="card" style={{ color: 'var(--muted)' }}>Non hai nulla in sospeso.</div>
            ) : (
              <div className="flex flex-col gap-2">
                {pagamenti.daSaldare.map((voce) => (
                  <div key={voce.id} className="card flex flex-wrap items-center justify-between gap-3 py-3">
                    <div>
                      <div style={{ fontWeight: 700 }}>{EURO.format(voce.importo)}</div>
                      {/* Ogni voce porta la propria origine, come nella cassa della segreteria:
                          si deve sempre poter risalire a cosa si sta pagando. */}
                      <div className="text-[0.84rem]" style={{ color: 'var(--muted)' }}>{voce.origine}</div>
                    </div>
                    <span className="stato stato-richiesto">Da saldare</span>
                  </div>
                ))}
                <p className="text-[0.82rem]" style={{ color: 'var(--muted)' }}>
                  Queste voci si saldano in studio: la ricevuta la emette la segreteria.
                </p>
              </div>
            )}
          </section>

          <section className="mb-7">
            <h2 className="eyebrow mb-3">Il tuo percorso</h2>
            {percorsi && percorsi.pacchettiPrivati.length === 0 && percorsi.ricetteSsn.length === 0 && (
              <div className="card" style={{ color: 'var(--muted)' }}>Nessun pacchetto o ciclo attivo.</div>
            )}
            <div className="flex flex-col gap-2">
              {percorsi?.pacchettiPrivati.map((pacchetto) => (
                <div key={pacchetto.id} className="card">
                  <div className="eyebrow mb-1">Pacchetto privato</div>
                  <div style={{ fontWeight: 700 }}>
                    {pacchetto.seduteResidue} sedute residue su {pacchetto.seduteTotali}
                  </div>
                  {pacchetto.scadenza && (
                    <div className="text-[0.84rem]" style={{ color: 'var(--muted)' }}>
                      Valido fino al {FORMATO_DATA.format(new Date(`${pacchetto.scadenza}T00:00:00`))}
                    </div>
                  )}
                </div>
              ))}
              {/* Per un percorso SSN si mostra il ciclo, non il pacchetto: sono due cose
                  diverse e mostrarle insieme confonderebbe (principio guida). */}
              {percorsi?.ricetteSsn.map((ricetta) => (
                <div key={ricetta.id} className="card">
                  <div className="eyebrow mb-1">Ciclo convenzionato SSN</div>
                  <div style={{ fontWeight: 700 }}>
                    {ricetta.seduteResidue ?? ricetta.numeroSedute} sedute residue su {ricetta.numeroSedute}
                  </div>
                  <div className="text-[0.84rem]" style={{ color: 'var(--muted)' }}>
                    Ricetta {ricetta.numeroONre}
                    {ricetta.finestraCompletamento
                      ? ` · da completare entro il ${FORMATO_DATA.format(new Date(`${ricetta.finestraCompletamento}T00:00:00`))}`
                      : ''}
                  </div>
                </div>
              ))}
            </div>
          </section>

          <section className="mb-7">
            <h2 className="eyebrow mb-3">Acquista</h2>
            <div className="card">
              <p className="mb-3 text-[0.9rem]">
                Puoi acquistare un pacchetto di sedute o una singola seduta di terapia manuale.
              </p>
              <div className="flex flex-wrap gap-2">
                <button className="btn btn-primary btn-sm" disabled={inCorso} onClick={() => void acquista('pacchetti', 'Online')}>
                  Pacchetto — paga ora
                </button>
                <button className="btn btn-ghost btn-sm" disabled={inCorso} onClick={() => void acquista('pacchetti', 'InStudio')}>
                  Pacchetto — pago in studio
                </button>
                <button className="btn btn-ghost btn-sm" disabled={inCorso} onClick={() => void acquista('sedute-singole', 'InStudio')}>
                  Seduta singola — pago in studio
                </button>
              </div>
              <p className="mt-3 rounded-lg px-3 py-2 text-[0.82rem]" style={{ background: 'var(--warm-soft)' }}>
                Pagamento simulato: nessun dato di carta viene chiesto né passa da questo portale.
                Nella versione reale il pagamento avverrebbe sulle pagine del gestore.
              </p>
            </div>
          </section>

          <section>
            <h2 className="eyebrow mb-3">Storico</h2>
            {pagamenti.storico.length === 0 ? (
              <div className="card" style={{ color: 'var(--muted)' }}>Nessun pagamento registrato.</div>
            ) : (
              <div className="flex flex-col gap-2">
                {pagamenti.storico.map((voce) => (
                  <div key={voce.id} className="card flex flex-wrap items-center justify-between gap-3 py-3">
                    <div>
                      <span style={{ fontWeight: 700 }}>{EURO.format(voce.importo)}</span>{' '}
                      <span className="text-[0.84rem]" style={{ color: 'var(--muted)' }}>
                        {voce.origine}
                      </span>
                      {voce.ricevutaNumero && (
                        <div className="mono text-[0.8rem]" style={{ color: 'var(--muted)' }}>
                          Ricevuta n. {voce.ricevutaNumero}/{voce.ricevutaAnno}
                        </div>
                      )}
                    </div>
                    <span className={`stato ${voce.stato === 'Stornato' ? 'stato-annullato' : 'stato-confermato'}`}>
                      {voce.stato}
                    </span>
                  </div>
                ))}
              </div>
            )}
          </section>
        </>
      )}
    </>
  )
}
