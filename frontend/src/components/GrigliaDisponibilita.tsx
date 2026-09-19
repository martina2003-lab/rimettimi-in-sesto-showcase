import { useEffect, useState } from 'react'
import { api } from '../api/client'
import type { DisponibilitaGiorno } from '../api/types'

const PASSO_MINUTI = 30

export interface SelezioneSlot {
  /** Inizio effettivo della seduta, che può essere prima dell'orario cliccato. */
  inizio: Date
  fine: Date
}

const FORMATO_GIORNO = new Intl.DateTimeFormat('it-IT', { weekday: 'short', day: 'numeric', month: 'short' })

function aMinuti(orario: string) {
  const [ore, minuti] = orario.split(':').map(Number)
  return ore * 60 + minuti
}

function formattaMinuti(minuti: number) {
  const ore = Math.floor(minuti / 60)
  return `${String(ore).padStart(2, '0')}:${String(minuti % 60).padStart(2, '0')}`
}

/** Lunedì della settimana che contiene la data indicata. */
function lunediDi(data: Date) {
  const risultato = new Date(data)
  const giorno = risultato.getDay()
  const scarto = giorno === 0 ? -6 : 1 - giorno
  risultato.setDate(risultato.getDate() + scarto)
  risultato.setHours(0, 0, 0, 0)
  return risultato
}

/**
 * Da quale settimana conviene partire. Nel fine settimana lo studio è chiuso, quindi
 * mostrare la settimana in corso significherebbe aprire il wizard su cinque giorni già
 * passati: si salta direttamente a quella successiva.
 */
function lunediIniziale() {
  const oggi = new Date()
  const giorno = oggi.getDay()
  if (giorno === 0) return lunediDi(new Date(oggi.getTime() + 86_400_000))
  if (giorno === 6) return lunediDi(new Date(oggi.getTime() + 2 * 86_400_000))
  return lunediDi(oggi)
}

function aIso(data: Date) {
  return `${data.getFullYear()}-${String(data.getMonth() + 1).padStart(2, '0')}-${String(data.getDate()).padStart(2, '0')}`
}

/**
 * Dove far partire davvero la seduta, dato l'orario cliccato.
 *
 * La griglia dice soltanto quando lo studio è libero — un fatto della giornata, stabile
 * fra le durate — e la durata vive qui, nell'interazione: si prova a partire dall'orario
 * cliccato e, se in avanti la seduta non ci sta, la si **aggancia indietro** dentro la
 * stessa finestra libera, coprendo comunque l'orario cliccato. È la decisione presa il
 * 9 settembre 2026 sui mockup: colorare le celle in base alla durata bucava la griglia
 * a caso e la faceva cambiare forma a ogni cambio di durata.
 *
 * I blocchi devono essere contigui **nel tempo**, non solo adiacenti nell'array: fra le
 * 12:30 e le 15:00 c'è la pausa, e una seduta non può scavalcarla.
 */
export function calcolaInizio(
  slots: { orario: string; libero: boolean }[],
  indiceCliccato: number,
  blocchiNecessari: number,
): number | null {
  const staInPiedi = (partenza: number) => {
    if (partenza < 0 || partenza + blocchiNecessari > slots.length) return false
    for (let i = 0; i < blocchiNecessari; i++) {
      if (!slots[partenza + i].libero) return false
      if (i > 0) {
        const precedente = aMinuti(slots[partenza + i - 1].orario)
        const corrente = aMinuti(slots[partenza + i].orario)
        if (corrente - precedente !== PASSO_MINUTI) return false
      }
    }
    return true
  }

  if (staInPiedi(indiceCliccato)) return indiceCliccato

  for (let partenza = indiceCliccato - 1; partenza > indiceCliccato - blocchiNecessari; partenza--) {
    if (staInPiedi(partenza)) return partenza
  }

  return null
}

interface Props {
  fisioterapistaId: number
  durataMinuti: number
  selezione: SelezioneSlot | null
  onSeleziona: (selezione: SelezioneSlot | null) => void
}

export default function GrigliaDisponibilita({ fisioterapistaId, durataMinuti, selezione, onSeleziona }: Props) {
  const [lunedi, setLunedi] = useState(lunediIniziale)
  const [giorni, setGiorni] = useState<DisponibilitaGiorno[] | null>(null)
  const [errore, setErrore] = useState<string | null>(null)

  useEffect(() => {
    let annullato = false
    setGiorni(null)
    setErrore(null)

    const date = Array.from({ length: 5 }, (_, i) => {
      const giorno = new Date(lunedi)
      giorno.setDate(lunedi.getDate() + i)
      return giorno
    })

    Promise.all(
      date.map((data) =>
        api.get<DisponibilitaGiorno>(
          `/api/agenda/disponibilita?fisioterapistaId=${fisioterapistaId}&data=${aIso(data)}`,
        ),
      ),
    )
      .then((risultati) => {
        if (!annullato) setGiorni(risultati)
      })
      .catch((e) => {
        if (!annullato) setErrore(e instanceof Error ? e.message : 'Disponibilità non caricata.')
      })

    return () => {
      annullato = true
    }
  }, [fisioterapistaId, lunedi])

  const blocchiNecessari = durataMinuti / PASSO_MINUTI

  // Le righe della griglia sono l'unione degli orari di apertura visti nei vari giorni:
  // così una giornata chiusa non fa collassare la tabella.
  const orari = Array.from(
    new Set((giorni ?? []).flatMap((g) => g.slots.map((s) => s.orario))),
  ).sort()

  function seleziona(giorno: DisponibilitaGiorno, indice: number) {
    const partenza = calcolaInizio(giorno.slots, indice, blocchiNecessari)
    if (partenza === null) return

    const minutiInizio = aMinuti(giorno.slots[partenza].orario)
    const [anno, mese, giornoDelMese] = giorno.giorno.split('-').map(Number)
    const inizio = new Date(anno, mese - 1, giornoDelMese, Math.floor(minutiInizio / 60), minutiInizio % 60)

    // Ricliccare la stessa cella annulla la selezione, invece di riselezionare lo stesso
    // orario: un modo rapido per cambiare idea senza dover cercare un'altra casella.
    if (selezione && selezione.inizio.getTime() === inizio.getTime()) {
      onSeleziona(null)
      return
    }

    const fine = new Date(inizio.getTime() + durataMinuti * 60_000)
    onSeleziona({ inizio, fine })
  }

  function statoCella(giorno: DisponibilitaGiorno, indice: number) {
    const slot = giorno.slots[indice]
    if (!slot.libero) return 'occupato'

    if (selezione) {
      const [anno, mese, giornoDelMese] = giorno.giorno.split('-').map(Number)
      const minuti = aMinuti(slot.orario)
      const istante = new Date(anno, mese - 1, giornoDelMese, Math.floor(minuti / 60), minuti % 60)
      if (istante >= selezione.inizio && istante < selezione.fine) return 'scelto'
    }

    return 'libero'
  }

  return (
    <div>
      <div className="mb-3 flex items-center justify-between gap-3">
        <button
          className="btn btn-ghost btn-sm"
          onClick={() => setLunedi(new Date(lunedi.getTime() - 7 * 86_400_000))}
          disabled={lunedi <= lunediIniziale()}
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

      {errore && <p style={{ color: 'var(--danger)' }}>{errore}</p>}
      {!giorni && !errore && <p style={{ color: 'var(--muted)' }}>Caricamento disponibilità…</p>}

      {giorni && (
        <div className="overflow-x-auto">
          <table className="w-full border-collapse text-center" style={{ minWidth: '520px' }}>
            <thead>
              <tr>
                <th className="eyebrow p-2 text-left" />
                {giorni.map((giorno) => (
                  <th key={giorno.giorno} className="eyebrow p-2">
                    {FORMATO_GIORNO.format(new Date(`${giorno.giorno}T00:00:00`))}
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
                    const indice = giorno.slots.findIndex((s) => s.orario === orario)
                    if (indice === -1) {
                      return (
                        <td key={giorno.giorno} className="p-1">
                          <div
                            className="h-7 rounded"
                            style={{ background: 'var(--surface-2)', opacity: 0.35 }}
                            title={giorno.motivoChiusura ?? 'Fuori orario'}
                          />
                        </td>
                      )
                    }

                    const stato = statoCella(giorno, indice)
                    const stile =
                      stato === 'scelto'
                        ? { background: 'var(--accent)', color: 'var(--accent-ink)' }
                        : stato === 'libero'
                          ? { background: 'var(--good-soft)', color: 'var(--good)' }
                          : { background: 'var(--surface-2)', color: 'var(--muted)' }

                    return (
                      <td key={giorno.giorno} className="p-1">
                        <button
                          className="h-7 w-full rounded text-[0.72rem] font-semibold"
                          style={{ ...stile, cursor: stato === 'occupato' ? 'default' : 'pointer' }}
                          disabled={stato === 'occupato'}
                          onClick={() => seleziona(giorno, indice)}
                          aria-label={`${giorno.giorno} ${orario} ${stato}`}
                        >
                          {stato === 'scelto' ? '●' : ''}
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

      <p className="mt-3 text-[0.8rem]" style={{ color: 'var(--muted)' }}>
        Le celle verdi sono gli orari in cui lo studio è libero. Scegliendone una, la seduta di{' '}
        {durataMinuti} minuti parte da lì; se non ci sta, si sposta indietro quel tanto che basta a
        comprendere comunque l'orario scelto.
      </p>
    </div>
  )
}
