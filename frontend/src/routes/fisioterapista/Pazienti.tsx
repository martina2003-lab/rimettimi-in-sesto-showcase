import { useEffect, useState } from 'react'
import { api } from '../../api/client'
import type { CicloCartella, PazienteDelFisioterapista } from '../../api/types'

const FORMATO_DATA = new Intl.DateTimeFormat('it-IT', { day: 'numeric', month: 'long', year: 'numeric' })

export default function PazientiFisioterapista() {
  const [pazienti, setPazienti] = useState<PazienteDelFisioterapista[] | null>(null)
  const [errore, setErrore] = useState<string | null>(null)
  const [aperto, setAperto] = useState<number | null>(null)
  const [cicli, setCicli] = useState<CicloCartella[] | null>(null)
  const [mostraAccesso, setMostraAccesso] = useState(false)

  useEffect(() => {
    api
      .get<PazienteDelFisioterapista[]>('/api/fisioterapisti/miei-pazienti')
      .then(setPazienti)
      .catch((e) => setErrore(e instanceof Error ? e.message : 'Elenco non caricato.'))
  }, [])

  async function apri(paziente: PazienteDelFisioterapista) {
    if (aperto === paziente.id) {
      setAperto(null)
      return
    }
    setAperto(paziente.id)
    setCicli(null)
    setMostraAccesso(false)
    setErrore(null)
    try {
      // Ogni apertura di cartella passa dal controllo lato server e lascia una riga
      // nell'audit log: non è una lista già scaricata e filtrata nel browser.
      setCicli(await api.get<CicloCartella[]>(`/api/pazienti/${paziente.id}/cartella-clinica`))
    } catch (e) {
      setErrore(e instanceof Error ? e.message : 'Cartella non accessibile.')
    }
  }

  if (!pazienti) {
    return <p style={{ color: errore ? 'var(--danger)' : 'var(--muted)' }}>{errore ?? 'Caricamento…'}</p>
  }

  const abituali = pazienti.filter((p) => p.abituale)
  const derivati = pazienti.filter((p) => !p.abituale)

  function scheda(paziente: PazienteDelFisioterapista, derivato: boolean) {
    return (
      <article key={paziente.id} className="card">
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div>
            <div style={{ fontWeight: 700 }}>
              {paziente.nome} {paziente.cognome}
            </div>
            <div className="text-[0.84rem]" style={{ color: 'var(--muted)' }}>
              {paziente.appuntamentiConMe === 1
                ? '1 appuntamento con te'
                : `${paziente.appuntamentiConMe} appuntamenti con te`}
              {paziente.ultimoAppuntamento
                ? ` · ultimo il ${FORMATO_DATA.format(new Date(paziente.ultimoAppuntamento))}`
                : ''}
            </div>
          </div>
          <button className="btn btn-ghost btn-sm" onClick={() => void apri(paziente)}>
            {aperto === paziente.id ? 'Chiudi' : 'Apri cartella'}
          </button>
        </div>

        {/* Il principio dell'accesso derivato si vede in UI, non solo nei requisiti: qui è
            detto a chiare lettere che la cartella è aperta per via di un appuntamento. */}
        {derivato && (
          <p className="mt-3 rounded-lg px-3 py-2 text-[0.84rem]" style={{ background: 'var(--warm-soft)' }}>
            Non è un tuo paziente abituale. Puoi vedere la sua cartella perché hai, o hai
            avuto, un appuntamento con lui — e ogni accesso resta tracciato.
          </p>
        )}

        {aperto === paziente.id && (
          <div className="mt-4">
            <button
              className="btn btn-ghost btn-sm mb-3"
              onClick={() => setMostraAccesso(!mostraAccesso)}
            >
              {mostraAccesso ? 'Nascondi' : 'Perché posso accedere'}
            </button>

            {mostraAccesso && (
              <ul className="mb-3 rounded-lg px-3 py-2 text-[0.84rem]" style={{ background: 'var(--surface-2)' }}>
                {paziente.appuntamentiGiustificativi.map((voce) => (
                  <li key={voce} className="mono">{voce}</li>
                ))}
              </ul>
            )}

            {!cicli && <p style={{ color: 'var(--muted)' }}>Apertura cartella…</p>}
            {cicli?.length === 0 && (
              <p style={{ color: 'var(--muted)' }}>Nessun ciclo di trattamento aperto per questo paziente.</p>
            )}
            {cicli?.map((ciclo) => (
              <div key={ciclo.id} className="rounded-xl p-3" style={{ background: 'var(--surface-2)' }}>
                <div className="eyebrow mb-2">
                  Ciclo
                  {ciclo.dataInizioTerapia
                    ? ` dal ${FORMATO_DATA.format(new Date(`${ciclo.dataInizioTerapia}T00:00:00`))}`
                    : ''}
                </div>
                <dl className="flex flex-col gap-2 text-[0.88rem]">
                  {[
                    ['Diagnosi', ciclo.diagnosi],
                    ['Anamnesi', ciclo.anamnesiPatologicaRemota],
                    ['Esame obiettivo', ciclo.esameObiettivo],
                    ['Programma', ciclo.programmaRiabilitativo],
                    ['Note per le sostituzioni', ciclo.noteSostituzione],
                    [
                      'VAS',
                      ciclo.vasIniziale !== null
                        ? `${ciclo.vasIniziale}/10 all'inizio${ciclo.vasFinale !== null ? `, ${ciclo.vasFinale}/10 alla fine` : ''}`
                        : null,
                    ],
                  ]
                    .filter(([, valore]) => valore)
                    .map(([etichetta, valore]) => (
                      <div key={etichetta as string}>
                        <dt className="eyebrow">{etichetta}</dt>
                        <dd>{valore}</dd>
                      </div>
                    ))}
                </dl>
              </div>
            ))}
          </div>
        )}
      </article>
    )
  }

  return (
    <>
      <header className="mb-7">
        <h1 className="view-title">I miei pazienti</h1>
        <p className="view-sub">
          Non esiste un elenco assegnato: vedi chi ha, o ha avuto, un appuntamento con te.
        </p>
      </header>

      {errore && (
        <p className="mb-4 rounded-lg px-3 py-2 text-sm" style={{ background: 'var(--danger-soft)', color: 'var(--danger)' }} role="alert">
          {errore}
        </p>
      )}

      <section className="mb-7">
        <h2 className="eyebrow mb-3">Attivi con te</h2>
        {abituali.length === 0 ? (
          <div className="card" style={{ color: 'var(--muted)' }}>Nessuno al momento.</div>
        ) : (
          <div className="flex flex-col gap-3">{abituali.map((p) => scheda(p, false))}</div>
        )}
      </section>

      <section>
        <h2 className="eyebrow mb-3">Accesso da sostituzione o appuntamento singolo</h2>
        {derivati.length === 0 ? (
          <div className="card" style={{ color: 'var(--muted)' }}>Nessuno al momento.</div>
        ) : (
          <div className="flex flex-col gap-3">{derivati.map((p) => scheda(p, true))}</div>
        )}
      </section>
    </>
  )
}
