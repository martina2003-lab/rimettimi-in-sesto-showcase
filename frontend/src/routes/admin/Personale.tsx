import { useEffect, useState } from 'react'
import { api } from '../../api/client'
import type { RigaPersonale } from '../../api/types'

/**
 * Il benchmark di settore dichiarato in requisiti.md: 75–85% è sano, sotto è
 * sotto-utilizzo, sopra il 90% è sovraccarico — **non un traguardo**. Per questo il
 * verde si ferma prima del massimo: una saturazione altissima è un problema, non un premio.
 */
function letturaSaturazione(valore: number): { testo: string; colore: string } {
  if (valore > 90) return { testo: 'sovraccarico', colore: 'var(--danger)' }
  if (valore >= 75) return { testo: 'equilibrato', colore: 'var(--good)' }
  if (valore >= 50) return { testo: 'sotto-utilizzo', colore: 'var(--warm)' }
  return { testo: 'molto sotto la soglia', colore: 'var(--muted)' }
}

function inizioMese() {
  const oggi = new Date()
  return new Date(oggi.getFullYear(), oggi.getMonth(), 1).toISOString().slice(0, 10)
}

export default function Personale() {
  const [da, setDa] = useState(inizioMese)
  const [a, setA] = useState(() => new Date().toISOString().slice(0, 10))
  const [righe, setRighe] = useState<RigaPersonale[] | null>(null)
  const [errore, setErrore] = useState<string | null>(null)

  useEffect(() => {
    setRighe(null)
    api
      .get<RigaPersonale[]>(`/api/admin/personale?da=${da}&a=${a}`)
      .then(setRighe)
      .catch((e) => setErrore(e instanceof Error ? e.message : 'Dati non caricati.'))
  }, [da, a])

  return (
    <>
      <header className="mb-6">
        <h1 className="view-title">Personale</h1>
        <p className="view-sub">
          Ore erogate a confronto col monte ore da contratto, nel periodo scelto.
        </p>
      </header>

      <div className="mb-5 flex flex-wrap gap-3">
        <div className="field mb-0">
          <label htmlFor="da">Dal</label>
          <input id="da" type="date" value={da} onChange={(e) => setDa(e.target.value)} />
        </div>
        <div className="field mb-0">
          <label htmlFor="a">Al</label>
          <input id="a" type="date" value={a} onChange={(e) => setA(e.target.value)} />
        </div>
      </div>

      {errore && (
        <p className="mb-4 rounded-lg px-3 py-2 text-sm" style={{ background: 'var(--danger-soft)', color: 'var(--danger)' }} role="alert">
          {errore}
        </p>
      )}
      {!righe && !errore && <p style={{ color: 'var(--muted)' }}>Caricamento…</p>}

      {righe && (
        <>
          <div className="flex flex-col gap-3">
            {righe.map((riga) => {
              const lettura = riga.saturazione !== null ? letturaSaturazione(riga.saturazione) : null
              return (
                <article key={riga.fisioterapistaId} className="card">
                  <div className="flex flex-wrap items-start justify-between gap-3">
                    <div>
                      <div style={{ fontWeight: 700 }}>{riga.nome}</div>
                      <div className="text-[0.84rem]" style={{ color: 'var(--muted)' }}>
                        {riga.contratto === 'Dipendente' ? 'Dipendente' : 'Collaboratore'}
                      </div>
                    </div>
                    {lettura && (
                      <div className="text-right">
                        <div style={{ fontFamily: 'Petrona, serif', fontSize: '1.5rem', fontWeight: 600, color: lettura.colore }}>
                          {riga.saturazione}%
                        </div>
                        <div className="text-[0.78rem]" style={{ color: lettura.colore }}>{lettura.testo}</div>
                      </div>
                    )}
                  </div>

                  <div className="mt-3 text-[0.86rem]" style={{ color: 'var(--muted)' }}>
                    <span className="mono">{riga.oreErogate}</span>{' '}
                    {riga.oreErogate === 1 ? 'ora erogata' : 'ore erogate'} su{' '}
                    <span className="mono">{riga.oreContratto}</span> da contratto nel periodo
                  </div>
                </article>
              )
            })}
          </div>

          {/* Il senso del benchmark va detto qui, non lasciato interpretare: un numero alto
              non è un risultato migliore. */}
          <p className="mt-4 rounded-lg px-3 py-2 text-[0.82rem]" style={{ background: 'var(--surface-2)' }}>
            Fra il 75% e l'85% la saturazione è considerata sana; sopra il 90% segnala
            sovraccarico, non un traguardo raggiunto. Il dato è aggregato sul periodo e non
            traccia la singola giornata di lavoro.
          </p>
        </>
      )}
    </>
  )
}
