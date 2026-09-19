import { useEffect, useState } from 'react'
import { api } from '../../api/client'
import type { Assenza, ProfiloFisioterapista } from '../../api/types'

const FORMATO_DATA = new Intl.DateTimeFormat('it-IT', { day: 'numeric', month: 'long', year: 'numeric' })
const soloData = (iso: string) => FORMATO_DATA.format(new Date(`${iso}T00:00:00`))

const TIPI = [
  ['Pianificata', 'Permesso o ferie, con preavviso'],
  ['Improvvisa', 'Malattia o imprevisto, su giorni già prenotati'],
  ['LungaDurata', 'Maternità, infortunio, aspettativa'],
] as const

function classeStato(stato: Assenza['statoApprovazione']) {
  if (stato === 'Approvata') return 'stato stato-confermato'
  if (stato === 'InAttesa') return 'stato stato-richiesto'
  if (stato === 'Rifiutata') return 'stato stato-annullato'
  return 'stato stato-neutro'
}

export default function Assenze() {
  const [profilo, setProfilo] = useState<ProfiloFisioterapista | null>(null)
  const [assenze, setAssenze] = useState<Assenza[] | null>(null)
  const [errore, setErrore] = useState<string | null>(null)
  const [inCorso, setInCorso] = useState(false)

  const [tipo, setTipo] = useState<(typeof TIPI)[number][0]>('Pianificata')
  const [dataInizio, setDataInizio] = useState('')
  const [dataFine, setDataFine] = useState('')
  const [motivo, setMotivo] = useState('')

  async function carica() {
    const [p, a] = await Promise.all([
      api.get<ProfiloFisioterapista>('/api/fisioterapisti/me'),
      api.get<Assenza[]>('/api/assenze/mie'),
    ])
    setProfilo(p)
    setAssenze(a)
  }

  useEffect(() => {
    carica().catch((e) => setErrore(e instanceof Error ? e.message : 'Dati non caricati.'))
  }, [])

  async function invia() {
    setInCorso(true)
    setErrore(null)
    try {
      await api.post('/api/assenze', { dataInizio, dataFine, tipo, motivo: motivo.trim() || null })
      setDataInizio('')
      setDataFine('')
      setMotivo('')
      await carica()
    } catch (e) {
      setErrore(e instanceof Error ? e.message : 'Invio non riuscito.')
    } finally {
      setInCorso(false)
    }
  }

  if (!profilo || !assenze) {
    return <p style={{ color: errore ? 'var(--danger)' : 'var(--muted)' }}>{errore ?? 'Caricamento…'}</p>
  }

  const dipendente = profilo.tipoContratto === 'Dipendente'

  return (
    <>
      <header className="mb-6">
        <h1 className="view-title">Assenze</h1>
        <p className="view-sub">
          {dipendente
            ? 'Le assenze passano dall’approvazione della segreteria e incidono sul monte ore.'
            : 'Da collaboratore gestisci la tua disponibilità senza bisogno di approvazioni.'}
        </p>
      </header>

      {errore && (
        <p className="mb-4 rounded-lg px-3 py-2 text-sm" style={{ background: 'var(--danger-soft)', color: 'var(--danger)' }} role="alert">
          {errore}
        </p>
      )}

      {profilo.patternDisponibilita && (
        <section className="card mb-4">
          <h2 className="eyebrow mb-2">Il tuo pattern settimanale</h2>
          <p>{profilo.patternDisponibilita}</p>
          <p className="mt-2 text-[0.82rem]" style={{ color: 'var(--muted)' }}>
            In sola lettura: lo imposta la segreteria da contratto.
          </p>
        </section>
      )}

      <section className="card mb-6">
        <h2 className="eyebrow mb-3">Segnala un'assenza</h2>

        <div className="field">
          <label>Tipo</label>
          <div className="flex flex-col gap-2">
            {TIPI.map(([valore, descrizione]) => (
              <button
                key={valore}
                className={`btn btn-sm ${tipo === valore ? 'btn-primary' : 'btn-ghost'}`}
                onClick={() => setTipo(valore)}
              >
                {descrizione}
              </button>
            ))}
          </div>
        </div>

        <div className="grid gap-3 sm:grid-cols-2">
          <div className="field mb-0">
            <label htmlFor="inizio">Dal</label>
            <input id="inizio" type="date" value={dataInizio} onChange={(e) => setDataInizio(e.target.value)} />
          </div>
          <div className="field mb-0">
            <label htmlFor="fine">Al</label>
            <input id="fine" type="date" value={dataFine} onChange={(e) => setDataFine(e.target.value)} />
          </div>
        </div>

        <div className="field mt-3">
          <label htmlFor="motivo">Motivo (facoltativo)</label>
          <input id="motivo" value={motivo} onChange={(e) => setMotivo(e.target.value)} />
        </div>

        {/* Su un'assenza improvvisa o lunga gli appuntamenti già presi non si toccano da
            qui: li ridecide la Coordinatrice uno per uno, come vuole requisiti.md. */}
        {tipo !== 'Pianificata' && (
          <p className="rounded-lg px-3 py-2 text-[0.84rem]" style={{ background: 'var(--warm-soft)' }}>
            Gli appuntamenti già fissati in questo periodo non vengono annullati da qui: la
            segreteria li rivede uno per uno, dando precedenza ai cicli SSN in scadenza.
          </p>
        )}

        <button
          className="btn btn-primary btn-sm mt-3"
          disabled={inCorso || !dataInizio || !dataFine}
          onClick={() => void invia()}
        >
          {inCorso ? 'Invio…' : dipendente ? 'Invia la richiesta' : 'Registra l’assenza'}
        </button>
      </section>

      <section>
        <h2 className="eyebrow mb-3">Le tue assenze</h2>
        {assenze.length === 0 ? (
          <div className="card" style={{ color: 'var(--muted)' }}>Nessuna assenza registrata.</div>
        ) : (
          <div className="flex flex-col gap-2">
            {assenze.map((assenza) => (
              <div key={assenza.id} className="card flex flex-wrap items-center justify-between gap-3 py-3">
                <div>
                  <div style={{ fontWeight: 600 }}>
                    dal {soloData(assenza.dataInizio)} al {soloData(assenza.dataFine)}
                  </div>
                  <div className="text-[0.84rem]" style={{ color: 'var(--muted)' }}>
                    {assenza.tipo === 'LungaDurata' ? 'Lunga durata' : assenza.tipo}
                    {assenza.motivo ? ` · ${assenza.motivo}` : ''}
                  </div>
                </div>
                <span className={classeStato(assenza.statoApprovazione)}>
                  {assenza.statoApprovazione === 'NonRichiesta'
                    ? 'registrata'
                    : assenza.statoApprovazione === 'InAttesa'
                      ? 'in attesa di approvazione'
                      : assenza.statoApprovazione}
                </span>
              </div>
            ))}
          </div>
        )}
      </section>
    </>
  )
}
