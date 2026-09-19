import { useEffect, useState } from 'react'
import { api } from '../../api/client'
import type { ProfiloFisioterapista } from '../../api/types'

export default function Bio() {
  const [profilo, setProfilo] = useState<ProfiloFisioterapista | null>(null)
  const [testo, setTesto] = useState('')
  const [errore, setErrore] = useState<string | null>(null)
  const [esito, setEsito] = useState<string | null>(null)
  const [inCorso, setInCorso] = useState(false)

  useEffect(() => {
    api
      .get<ProfiloFisioterapista>('/api/fisioterapisti/me')
      .then((p) => {
        setProfilo(p)
        setTesto(p.bio ?? '')
      })
      .catch((e) => setErrore(e instanceof Error ? e.message : 'Profilo non caricato.'))
  }, [])

  async function salva() {
    setInCorso(true)
    setErrore(null)
    setEsito(null)
    try {
      await api.put('/api/fisioterapisti/me/bio', { bio: testo.trim() || null })
      setEsito('Bio aggiornata: è quello che i pazienti leggono mentre scelgono.')
    } catch (e) {
      setErrore(e instanceof Error ? e.message : 'Salvataggio non riuscito.')
    } finally {
      setInCorso(false)
    }
  }

  if (!profilo) {
    return <p style={{ color: errore ? 'var(--danger)' : 'var(--muted)' }}>{errore ?? 'Caricamento…'}</p>
  }

  const iniziali = `${profilo.nome[0] ?? ''}${profilo.cognome[0] ?? ''}`.toUpperCase()

  return (
    <>
      <header className="mb-6">
        <h1 className="view-title">La mia bio</h1>
        <p className="view-sub">
          La scrivi tu, e la leggono i pazienti mentre scelgono con chi prenotare.
        </p>
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

      <div className="card mb-4">
        <label htmlFor="bio">Testo</label>
        <textarea id="bio" rows={6} value={testo} onChange={(e) => setTesto(e.target.value)} />
        <button className="btn btn-primary btn-sm mt-3" disabled={inCorso} onClick={() => void salva()}>
          {inCorso ? 'Salvataggio…' : 'Salva'}
        </button>
      </div>

      {/* Anteprima "così apparirà una volta salvato", come nel mockup: la bio si scrive
          guardando il posto in cui il paziente la incontrerà, non a vuoto. */}
      <section>
        <h2 className="eyebrow mb-3">Così ti vede il paziente</h2>
        <div className="card">
          <div className="flex items-center gap-3">
            <div className="avatar">{iniziali}</div>
            <div style={{ fontWeight: 700 }}>
              {profilo.nome} {profilo.cognome}
            </div>
          </div>
          <p className="mt-3 text-[0.9rem]">
            {testo.trim() || 'Nessuna bio: al paziente comparirà soltanto il tuo nome.'}
          </p>
        </div>
        <p className="mt-3 text-[0.82rem]" style={{ color: 'var(--muted)' }}>
          Il tipo di contratto ({profilo.tipoContratto.toLowerCase()}) resta un dato interno e
          non viene mai mostrato ai pazienti.
        </p>
      </section>
    </>
  )
}
