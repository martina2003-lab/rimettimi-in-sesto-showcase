import { useEffect, useState } from 'react'
import { api } from '../../api/client'
import type { Pagamento } from '../../api/types'

const EURO = new Intl.NumberFormat('it-IT', { style: 'currency', currency: 'EUR' })
const FORMATO_DATA = new Intl.DateTimeFormat('it-IT', { day: 'numeric', month: 'long', year: 'numeric' })

type Vista = 'da-riscuotere' | 'giornata' | 'ricevute'

const METODI = ['InStudio', 'Online'] as const

export default function Cassa() {
  const [vista, setVista] = useState<Vista>('da-riscuotere')
  const [voci, setVoci] = useState<Pagamento[] | null>(null)
  const [errore, setErrore] = useState<string | null>(null)
  const [inCorso, setInCorso] = useState(false)
  const [incassoAperto, setIncassoAperto] = useState<number | null>(null)
  const [metodo, setMetodo] = useState<(typeof METODI)[number]>('InStudio')
  const [opposizione, setOpposizione] = useState(false)

  const oggi = new Date().toISOString().slice(0, 10)

  async function carica(quale: Vista) {
    const percorso =
      quale === 'giornata' ? `/api/cassa/giornata?data=${oggi}` : `/api/cassa/${quale}`
    setVoci(await api.get<Pagamento[]>(percorso))
  }

  useEffect(() => {
    setVoci(null)
    carica(vista).catch((e) => setErrore(e instanceof Error ? e.message : 'Cassa non caricata.'))
  }, [vista])

  async function esegui(chiamata: () => Promise<unknown>) {
    setInCorso(true)
    setErrore(null)
    try {
      await chiamata()
      setIncassoAperto(null)
      setOpposizione(false)
      await carica(vista)
    } catch (e) {
      setErrore(e instanceof Error ? e.message : 'Operazione non riuscita.')
    } finally {
      setInCorso(false)
    }
  }

  return (
    <>
      <header className="mb-6">
        <h1 className="view-title">Cassa</h1>
        <p className="view-sub">Ogni voce porta con sé da dove nasce, così si può sempre risalire a cosa si sta incassando.</p>
      </header>

      <div className="mb-5 flex flex-wrap gap-2">
        {(
          [
            ['da-riscuotere', 'Da riscuotere'],
            ['giornata', 'Giornata'],
            ['ricevute', 'Ricevute'],
          ] as const
        ).map(([valore, etichetta]) => (
          <button
            key={valore}
            className={`btn btn-sm ${vista === valore ? 'btn-primary' : 'btn-ghost'}`}
            onClick={() => setVista(valore)}
          >
            {etichetta}
          </button>
        ))}
      </div>

      {errore && (
        <p className="mb-4 rounded-lg px-3 py-2 text-sm" style={{ background: 'var(--danger-soft)', color: 'var(--danger)' }} role="alert">
          {errore}
        </p>
      )}

      {!voci && !errore && <p style={{ color: 'var(--muted)' }}>Caricamento…</p>}

      {voci && voci.length === 0 && (
        <div className="card" style={{ color: 'var(--muted)' }}>
          {vista === 'da-riscuotere'
            ? 'Nessun arretrato.'
            : vista === 'giornata'
              ? 'Nessun incasso registrato oggi.'
              : 'Nessuna ricevuta emessa.'}
        </div>
      )}

      {voci && voci.length > 0 && (
        <>
          {vista === 'giornata' && (
            <div className="card mb-4">
              <div className="eyebrow mb-1">Totale incassato oggi</div>
              <div style={{ fontFamily: 'Petrona, serif', fontSize: '1.6rem', fontWeight: 600 }}>
                {EURO.format(voci.reduce((somma, v) => somma + v.importo, 0))}
              </div>
            </div>
          )}

          <div className="flex flex-col gap-2">
            {voci.map((voce) => (
              <article key={voce.id} className="card py-3">
                <div className="flex flex-wrap items-start justify-between gap-3">
                  <div>
                    <span style={{ fontWeight: 700 }}>{EURO.format(voce.importo)}</span>{' '}
                    <span style={{ fontWeight: 600 }}>{voce.pazienteNome}</span>
                    <div className="text-[0.84rem]" style={{ color: 'var(--muted)' }}>{voce.origine}</div>
                    {voce.ricevutaNumero && (
                      <div className="mono text-[0.8rem]" style={{ color: 'var(--muted)' }}>
                        Ricevuta n. {voce.ricevutaNumero}/{voce.ricevutaAnno}
                        {voce.dataIncasso ? ` · ${FORMATO_DATA.format(new Date(voce.dataIncasso))}` : ''}
                      </div>
                    )}
                  </div>

                  <div className="flex flex-col items-end gap-2">
                    <span className={`stato ${voce.stato === 'DaSaldare' ? 'stato-richiesto' : voce.stato === 'Stornato' ? 'stato-annullato' : 'stato-confermato'}`}>
                      {voce.stato === 'DaSaldare' ? 'Da saldare' : voce.stato}
                    </span>

                    {voce.stato === 'DaSaldare' && (
                      <button
                        className="btn btn-ghost btn-sm"
                        disabled={inCorso}
                        onClick={() => setIncassoAperto(incassoAperto === voce.id ? null : voce.id)}
                      >
                        Incassa
                      </button>
                    )}
                    {voce.stato === 'Pagato' && (
                      // Storno, mai cancellazione: la numerazione progressiva deve restare
                      // integra (requisiti.md, Glossario).
                      <button
                        className="btn btn-danger btn-sm"
                        disabled={inCorso}
                        onClick={() => void esegui(() => api.put(`/api/cassa/pagamenti/${voce.id}/storna`))}
                      >
                        Storna
                      </button>
                    )}
                  </div>
                </div>

                {incassoAperto === voce.id && (
                  <div className="mt-3 rounded-xl p-3" style={{ background: 'var(--surface-2)' }}>
                    <div className="field mb-2 max-w-xs">
                      <label htmlFor={`metodo-${voce.id}`}>Metodo</label>
                      <select id={`metodo-${voce.id}`} value={metodo} onChange={(e) => setMetodo(e.target.value as typeof metodo)}>
                        <option value="InStudio">In studio</option>
                        <option value="Online">Online</option>
                      </select>
                    </div>

                    {/* L'opposizione all'invio al Sistema TS si raccoglie qui, al momento
                        dell'incasso, non in un passaggio successivo (requisiti.md). */}
                    <label className="flex items-center gap-2 text-[0.86rem]" style={{ fontWeight: 400, color: 'var(--ink)' }}>
                      <input type="checkbox" className="w-auto" checked={opposizione} onChange={(e) => setOpposizione(e.target.checked)} />
                      Il paziente si oppone all'invio al Sistema TS
                    </label>

                    <p className="mt-2 text-[0.82rem]" style={{ color: 'var(--muted)' }}>
                      {voce.importo > 77.47
                        ? 'Sopra € 77,47: sulla ricevuta verrà applicata l\'imposta di bollo.'
                        : 'Prestazione esente IVA art. 10 n. 18, sotto la soglia del bollo.'}
                    </p>

                    <button
                      className="btn btn-primary btn-sm mt-2"
                      disabled={inCorso}
                      onClick={() =>
                        void esegui(() =>
                          api.put(`/api/cassa/pagamenti/${voce.id}/incassa`, {
                            metodo,
                            opposizioneSistemaTs: opposizione,
                          }),
                        )
                      }
                    >
                      Registra l'incasso ed emetti ricevuta
                    </button>
                  </div>
                )}
              </article>
            ))}
          </div>
        </>
      )}

      {vista === 'ricevute' && (
        <p className="mt-5 text-[0.84rem]" style={{ color: 'var(--muted)' }}>
          L'invio al Sistema TS non è integrato: il portale produce il file da caricare altrove,
          e l'invio resta un adempimento dello studio.
        </p>
      )}
    </>
  )
}
