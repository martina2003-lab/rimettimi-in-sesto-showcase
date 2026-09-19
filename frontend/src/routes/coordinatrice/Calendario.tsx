import { useEffect, useState } from 'react'
import { api } from '../../api/client'
import GrigliaLiberoOccupato from '../../components/GrigliaLiberoOccupato'
import type { Appuntamento, DisponibilitaGiorno, FisioterapistaPubblico } from '../../api/types'

const DURATE = [30, 60, 90]

const FORMATO_GIORNO = new Intl.DateTimeFormat('it-IT', { weekday: 'short', day: 'numeric', month: 'short' })
const FORMATO_LUNGO = new Intl.DateTimeFormat('it-IT', {
  weekday: 'long', day: 'numeric', month: 'long', year: 'numeric',
})
const FORMATO_ORA = new Intl.DateTimeFormat('it-IT', { hour: '2-digit', minute: '2-digit' })

function aMinuti(orario: string) {
  const [ore, minuti] = orario.split(':').map(Number)
  return ore * 60 + minuti
}

function formattaMinuti(minuti: number) {
  return `${String(Math.floor(minuti / 60)).padStart(2, '0')}:${String(minuti % 60).padStart(2, '0')}`
}

function lunediDi(data: Date) {
  const risultato = new Date(data)
  const giorno = risultato.getDay()
  risultato.setDate(risultato.getDate() + (giorno === 0 ? -6 : 1 - giorno))
  risultato.setHours(0, 0, 0, 0)
  return risultato
}

function aIso(data: Date) {
  const p = (n: number) => String(n).padStart(2, '0')
  return `${data.getFullYear()}-${p(data.getMonth() + 1)}-${p(data.getDate())}`
}

/** Il colore dice lo stato, non il percorso: è quello che fa decidere. */
function classeStato(stato: Appuntamento['stato']) {
  if (stato === 'Richiesto') return 'stato-richiesto'
  if (stato === 'Annullato' || stato === 'NoShow') return 'stato-annullato'
  return 'stato-confermato'
}

function sfondoStato(stato: Appuntamento['stato']) {
  if (stato === 'Richiesto') return 'var(--warm-soft)'
  if (stato === 'Annullato' || stato === 'NoShow') return 'var(--danger-soft)'
  return 'var(--good-soft)'
}

/**
 * Vista d'occhio della settimana, un fisioterapista alla volta.
 *
 * Le colonne sono i giorni e non i terapisti: la scelta è già stata presa sui mockup
 * (9 settembre 2026), perché una settimana per persona risponde alla domanda che la
 * segreteria si fa davvero — "questa settimana lui com'è messo?" — mentre la giornata
 * di tutti nascondeva il resto della settimana.
 */
export default function Calendario() {
  const [fisioterapisti, setFisioterapisti] = useState<FisioterapistaPubblico[]>([])
  const [fisioterapistaId, setFisioterapistaId] = useState<number | null>(null)
  // "Tutti" è una vista diversa (libero/occupato incrociando i terapisti), non un altro
  // valore di fisioterapistaId: tenerle distinte evita di dover trattare null come "non
  // ancora caricato" e insieme come "vista tutti".
  const [tutti, setTutti] = useState(false)
  const [durataVista, setDurataVista] = useState(60)
  const [lunedi, setLunedi] = useState(() => lunediDi(new Date()))

  const [giorni, setGiorni] = useState<DisponibilitaGiorno[] | null>(null)
  const [appuntamenti, setAppuntamenti] = useState<Appuntamento[]>([])
  const [scelto, setScelto] = useState<Appuntamento | null>(null)
  const [errore, setErrore] = useState<string | null>(null)

  useEffect(() => {
    api
      .get<FisioterapistaPubblico[]>('/api/fisioterapisti')
      .then((elenco) => {
        setFisioterapisti(elenco)
        if (elenco.length > 0) setFisioterapistaId((corrente) => corrente ?? elenco[0].id)
      })
      .catch((e) => setErrore(e instanceof Error ? e.message : 'Fisioterapisti non caricati.'))
  }, [])

  useEffect(() => {
    if (tutti || fisioterapistaId === null) return
    let annullato = false
    setGiorni(null)
    setScelto(null)
    setErrore(null)

    const date = Array.from({ length: 5 }, (_, i) => {
      const giorno = new Date(lunedi)
      giorno.setDate(lunedi.getDate() + i)
      return giorno
    })

    Promise.all([
      Promise.all(
        date.map((data) =>
          api.get<DisponibilitaGiorno>(
            `/api/agenda/disponibilita?fisioterapistaId=${fisioterapistaId}&data=${aIso(data)}`,
          ),
        ),
      ),
      api.get<Appuntamento[]>(
        `/api/appuntamenti/calendario?fisioterapistaId=${fisioterapistaId}&da=${aIso(date[0])}&a=${aIso(date[4])}`,
      ),
    ])
      .then(([disponibilita, elenco]) => {
        if (annullato) return
        setGiorni(disponibilita)
        setAppuntamenti(elenco)
      })
      .catch((e) => {
        if (!annullato) setErrore(e instanceof Error ? e.message : 'Calendario non caricato.')
      })

    return () => {
      annullato = true
    }
  }, [tutti, fisioterapistaId, lunedi])

  // Un appuntamento occupa più blocchi da 30 minuti: il nome si scrive solo nel primo,
  // gli altri restano colorati. Evita di far dipendere la griglia da un rowSpan, che
  // andrebbe ricalcolato ogni volta che cambiano gli orari di apertura del giorno.
  function appuntamentoNelBlocco(giorno: string, orario: string) {
    const minuti = aMinuti(orario)
    for (const appuntamento of appuntamenti) {
      if (!appuntamento.dataOra.startsWith(giorno)) continue
      const inizio = new Date(appuntamento.dataOra)
      const minutiInizio = inizio.getHours() * 60 + inizio.getMinutes()
      if (minuti >= minutiInizio && minuti < minutiInizio + appuntamento.durataMinuti) {
        return { appuntamento, primo: minuti === minutiInizio }
      }
    }
    return null
  }

  const orari = Array.from(new Set((giorni ?? []).flatMap((g) => g.slots.map((s) => s.orario)))).sort()

  return (
    <>
      <header className="mb-6">
        <h1 className="view-title">Calendario</h1>
        <p className="view-sub">
          {tutti
            ? 'Chi è libero, incrociando tutti i fisioterapisti — utile per trovare un orario a chi non ha preferenze.'
            : 'Una settimana alla volta, un fisioterapista alla volta. Gli orari senza colore sono liberi; quelli in giallo sono richieste ancora da decidere.'}
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

      <div className="mb-4 flex flex-wrap gap-2">
        {fisioterapisti.map((f) => (
          <button
            key={f.id}
            className={`btn btn-sm ${!tutti && fisioterapistaId === f.id ? 'btn-primary' : 'btn-ghost'}`}
            onClick={() => {
              setTutti(false)
              setFisioterapistaId(f.id)
            }}
          >
            {f.nome}
          </button>
        ))}
        <button
          className={`btn btn-sm ${tutti ? 'btn-primary' : 'btn-ghost'}`}
          onClick={() => setTutti(true)}
        >
          Tutti
        </button>
      </div>

      {tutti ? (
        <div className="card">
          <div className="mb-3 field" style={{ maxWidth: '260px' }}>
            <label>Durata della seduta da cercare</label>
            <div className="flex flex-wrap gap-2">
              {DURATE.map((valore) => (
                <button
                  key={valore}
                  className={`btn btn-sm ${durataVista === valore ? 'btn-primary' : 'btn-ghost'}`}
                  onClick={() => setDurataVista(valore)}
                >
                  {valore === 90 ? '1 h 30' : valore === 60 ? '1 ora' : '30 min'}
                </button>
              ))}
            </div>
          </div>
          <GrigliaLiberoOccupato durataMinuti={durataVista} />
        </div>
      ) : (
      <>
      <div className="card">
        <div className="mb-3 flex items-center justify-between gap-3">
          <button
            className="btn btn-ghost btn-sm"
            onClick={() => setLunedi(new Date(lunedi.getTime() - 7 * 86_400_000))}
          >
            ← Settimana precedente
          </button>
          <span className="eyebrow">
            {FORMATO_GIORNO.format(lunedi)} —{' '}
            {FORMATO_GIORNO.format(new Date(lunedi.getTime() + 4 * 86_400_000))}
          </span>
          <button
            className="btn btn-ghost btn-sm"
            onClick={() => setLunedi(new Date(lunedi.getTime() + 7 * 86_400_000))}
          >
            Settimana successiva →
          </button>
        </div>

        {!giorni && !errore && <p style={{ color: 'var(--muted)' }}>Caricamento…</p>}

        {giorni && (
          <div className="overflow-x-auto">
            <table className="w-full border-collapse text-center" style={{ minWidth: '640px' }}>
              <thead>
                <tr>
                  <th className="eyebrow p-2 text-left" />
                  {giorni.map((giorno) => (
                    <th key={giorno.giorno} className="eyebrow p-2">
                      {FORMATO_GIORNO.format(new Date(`${giorno.giorno}T00:00:00`))}
                      {giorno.chiuso && (
                        // Fuori dal maiuscoletto spaziato dell'intestazione: è una frase,
                        // non un'etichetta, e in colonna stretta diventava illeggibile.
                        <div
                          style={{
                            color: 'var(--danger)',
                            fontWeight: 600,
                            textTransform: 'none',
                            letterSpacing: 'normal',
                            fontSize: '0.72rem',
                          }}
                        >
                          {giorno.motivoChiusura ?? 'Chiuso'}
                        </div>
                      )}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {orari.map((orario) => (
                  <tr key={orario}>
                    <td className="mono p-1 text-right text-[0.78rem]" style={{ color: 'var(--muted)' }}>
                      {formattaMinuti(aMinuti(orario))}
                    </td>
                    {giorni.map((giorno) => {
                      const apertoQui = giorno.slots.some((s) => s.orario === orario)
                      if (!apertoQui) {
                        return (
                          <td key={giorno.giorno} className="p-1">
                            <div
                              className="h-8 rounded"
                              style={{ background: 'var(--surface-2)', opacity: 0.35 }}
                              title={giorno.motivoChiusura ?? 'Fuori orario'}
                            />
                          </td>
                        )
                      }

                      const occupato = appuntamentoNelBlocco(giorno.giorno, orario)
                      if (!occupato) {
                        return (
                          <td key={giorno.giorno} className="p-1">
                            <div className="h-8 rounded" style={{ background: 'var(--surface-2)' }} />
                          </td>
                        )
                      }

                      return (
                        <td key={giorno.giorno} className="p-1">
                          <button
                            className="h-8 w-full overflow-hidden rounded px-1 text-[0.72rem] font-semibold"
                            style={{
                              background: sfondoStato(occupato.appuntamento.stato),
                              textAlign: 'left',
                              whiteSpace: 'nowrap',
                              textOverflow: 'ellipsis',
                            }}
                            onClick={() => setScelto(occupato.appuntamento)}
                          >
                            {occupato.primo ? occupato.appuntamento.pazienteNome : ''}
                          </button>
                        </td>
                      )
                    })}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {scelto && (
        <div className="card mt-4">
          <div className="flex flex-wrap items-start justify-between gap-3">
            <div>
              <div style={{ fontWeight: 700 }}>{scelto.pazienteNome}</div>
              <div className="text-[0.88rem]" style={{ color: 'var(--muted)' }}>
                {FORMATO_LUNGO.format(new Date(scelto.dataOra))} ·{' '}
                <span className="mono">{FORMATO_ORA.format(new Date(scelto.dataOra))}</span> ·{' '}
                {scelto.durataMinuti} min · {scelto.fisioterapistaNome} ·{' '}
                {scelto.percorso === 'Ssn' ? 'Convenzionato SSN' : 'Privato'}
              </div>
            </div>
            <span className={`stato ${classeStato(scelto.stato)}`}>{scelto.stato}</span>
          </div>

          {scelto.stato === 'Richiesto' && (
            <p
              className="mt-3 rounded-lg px-3 py-2 text-[0.86rem]"
              style={{ background: 'var(--warm-soft)' }}
            >
              Tiene bloccato questo orario ma non è ancora confermato: si decide dalla coda
              Richieste, dove ci sono anche il rifiuto e la proposta di un altro orario.
            </p>
          )}
          {scelto.modificaRichiestaDataOra && (
            <p
              className="mt-3 rounded-lg px-3 py-2 text-[0.86rem]"
              style={{ background: 'var(--warm-soft)' }}
            >
              Il paziente ha chiesto di spostarlo al{' '}
              {FORMATO_LUNGO.format(new Date(scelto.modificaRichiestaDataOra))}, ore{' '}
              {FORMATO_ORA.format(new Date(scelto.modificaRichiestaDataOra))}. Finché non decidi,
              questo orario resta suo.
            </p>
          )}
          {scelto.motivoAnnullamento && (
            <p className="mt-3 text-[0.86rem]" style={{ color: 'var(--muted)' }}>
              Motivo: {scelto.motivoAnnullamento}
            </p>
          )}

          <button className="btn btn-ghost btn-sm mt-3" onClick={() => setScelto(null)}>
            Chiudi
          </button>
        </div>
      )}
      </>
      )}
    </>
  )
}
