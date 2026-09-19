import { useEffect, useState } from 'react'
import { api } from '../../api/client'
import type { Assenza, FisioterapistaPubblico } from '../../api/types'
import AppuntamentiImpattati from './AppuntamentiImpattati'

const FORMATO_DATA = new Intl.DateTimeFormat('it-IT', { day: 'numeric', month: 'long', year: 'numeric' })
const soloData = (iso: string) => FORMATO_DATA.format(new Date(`${iso}T00:00:00`))

export default function FisioterapistiCoordinatrice() {
  const [fisioterapisti, setFisioterapisti] = useState<FisioterapistaPubblico[] | null>(null)
  const [assenze, setAssenze] = useState<Assenza[] | null>(null)
  const [errore, setErrore] = useState<string | null>(null)
  const [inCorso, setInCorso] = useState(false)
  const [worklistAperta, setWorklistAperta] = useState<number | null>(null)

  async function carica() {
    const [elenco, tutte] = await Promise.all([
      api.get<FisioterapistaPubblico[]>('/api/fisioterapisti'),
      api.get<Assenza[]>('/api/assenze'),
    ])
    setFisioterapisti(elenco)
    setAssenze(tutte)
  }

  useEffect(() => {
    carica().catch((e) => setErrore(e instanceof Error ? e.message : 'Dati non caricati.'))
  }, [])

  async function decidi(id: number, esito: 'approva' | 'rifiuta') {
    setInCorso(true)
    setErrore(null)
    try {
      await api.put(`/api/assenze/${id}/${esito}`)
      await carica()
    } catch (e) {
      setErrore(e instanceof Error ? e.message : 'Operazione non riuscita.')
    } finally {
      setInCorso(false)
    }
  }

  if (!fisioterapisti || !assenze) {
    return <p style={{ color: errore ? 'var(--danger)' : 'var(--muted)' }}>{errore ?? 'Caricamento…'}</p>
  }

  const daApprovare = assenze.filter((a) => a.statoApprovazione === 'InAttesa')
  const altre = assenze.filter((a) => a.statoApprovazione !== 'InAttesa')

  return (
    <>
      <header className="mb-7">
        <h1 className="view-title">Fisioterapisti</h1>
        <p className="view-sub">Chi c'è, e chi manca — con le richieste di assenza da decidere.</p>
      </header>

      {errore && (
        <p className="mb-4 rounded-lg px-3 py-2 text-sm" style={{ background: 'var(--danger-soft)', color: 'var(--danger)' }} role="alert">
          {errore}
        </p>
      )}

      <section className="mb-7">
        <h2 className="eyebrow mb-3">Richieste da approvare</h2>
        {daApprovare.length === 0 ? (
          <div className="card" style={{ color: 'var(--muted)' }}>Nessuna richiesta in attesa.</div>
        ) : (
          <div className="flex flex-col gap-3">
            {daApprovare.map((assenza) => (
              <article key={assenza.id} className="card">
                <div className="flex flex-wrap items-start justify-between gap-3">
                  <div>
                    <div style={{ fontWeight: 700 }}>{assenza.fisioterapistaNome}</div>
                    <div className="text-[0.86rem]" style={{ color: 'var(--muted)' }}>
                      dal {soloData(assenza.dataInizio)} al {soloData(assenza.dataFine)} ·{' '}
                      {assenza.tipo === 'LungaDurata' ? 'lunga durata' : assenza.tipo.toLowerCase()}
                      {assenza.motivo ? ` · ${assenza.motivo}` : ''}
                    </div>
                  </div>
                  <div className="flex flex-wrap gap-2">
                    <button className="btn btn-primary btn-sm" disabled={inCorso} onClick={() => void decidi(assenza.id, 'approva')}>
                      Approva
                    </button>
                    <button className="btn btn-ghost btn-sm" disabled={inCorso} onClick={() => void decidi(assenza.id, 'rifiuta')}>
                      Rifiuta
                    </button>
                  </div>
                </div>

                {/* Approvare un'assenza non annulla nulla: gli appuntamenti che cadono in
                    quel periodo restano da rivedere a mano, per ordine di urgenza. */}
                <p className="mt-3 text-[0.82rem]" style={{ color: 'var(--muted)' }}>
                  Approvando, gli orari smettono di essere prenotabili. Gli appuntamenti già
                  presi in quei giorni restano e vanno rivisti uno per uno.
                </p>
                <button
                  className="btn btn-ghost btn-sm mt-2"
                  onClick={() => setWorklistAperta(worklistAperta === assenza.id ? null : assenza.id)}
                >
                  {worklistAperta === assenza.id ? 'Chiudi' : 'Rivedi gli appuntamenti'}
                </button>
                {worklistAperta === assenza.id && <AppuntamentiImpattati assenzaId={assenza.id} />}
              </article>
            ))}
          </div>
        )}
      </section>

      <section className="mb-7">
        <h2 className="eyebrow mb-3">Lo staff</h2>
        <div className="flex flex-col gap-2">
          {fisioterapisti.map((fisioterapista) => {
            const oggi = new Date().toISOString().slice(0, 10)
            const assente = assenze.find(
              (a) =>
                a.fisioterapistaId === fisioterapista.id &&
                a.statoApprovazione !== 'Rifiutata' &&
                a.dataInizio <= oggi &&
                a.dataFine >= oggi,
            )

            return (
              <div key={fisioterapista.id} className="card flex flex-wrap items-center justify-between gap-3 py-3">
                <div style={{ fontWeight: 600 }}>{fisioterapista.nome}</div>
                {assente ? (
                  <span className="stato stato-annullato">
                    assente fino al {soloData(assente.dataFine)}
                  </span>
                ) : (
                  <span className="stato stato-confermato">in studio</span>
                )}
              </div>
            )
          })}
        </div>
      </section>

      <section>
        <h2 className="eyebrow mb-3">Assenze registrate</h2>
        {altre.length === 0 ? (
          <div className="card" style={{ color: 'var(--muted)' }}>Nessuna.</div>
        ) : (
          <div className="flex flex-col gap-2">
            {altre.map((assenza) => (
              <div key={assenza.id} className="card py-3">
                <div className="flex flex-wrap items-center justify-between gap-3">
                  <div>
                    <span style={{ fontWeight: 600 }}>{assenza.fisioterapistaNome}</span>
                    <div className="text-[0.84rem]" style={{ color: 'var(--muted)' }}>
                      dal {soloData(assenza.dataInizio)} al {soloData(assenza.dataFine)}
                      {assenza.motivo ? ` · ${assenza.motivo}` : ''}
                    </div>
                  </div>
                  <div className="flex flex-wrap items-center gap-3">
                    <span className={`stato ${assenza.statoApprovazione === 'Rifiutata' ? 'stato-annullato' : 'stato-neutro'}`}>
                      {assenza.statoApprovazione === 'NonRichiesta' ? 'collaboratore' : assenza.statoApprovazione}
                    </span>
                    {assenza.statoApprovazione !== 'Rifiutata' && (
                      <button
                        className="btn btn-ghost btn-sm"
                        onClick={() => setWorklistAperta(worklistAperta === assenza.id ? null : assenza.id)}
                      >
                        {worklistAperta === assenza.id ? 'Chiudi' : 'Rivedi gli appuntamenti'}
                      </button>
                    )}
                  </div>
                </div>
                {worklistAperta === assenza.id && <AppuntamentiImpattati assenzaId={assenza.id} />}
              </div>
            ))}
          </div>
        )}
      </section>
    </>
  )
}
