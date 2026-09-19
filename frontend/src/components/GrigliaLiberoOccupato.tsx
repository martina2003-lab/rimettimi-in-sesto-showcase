import { useEffect, useState } from 'react'
import { api } from '../api/client'
import type { GiornoLiberoOccupato } from '../api/types'

const FORMATO_GIORNO = new Intl.DateTimeFormat('it-IT', { weekday: 'short', day: 'numeric', month: 'short' })
const FORMATO_LUNGO = new Intl.DateTimeFormat('it-IT', { weekday: 'long', day: 'numeric', month: 'long' })

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

/** Nel fine settimana lo studio è chiuso: si parte dalla settimana successiva. */
function lunediIniziale() {
  const oggi = new Date()
  const giorno = oggi.getDay()
  if (giorno === 0) return lunediDi(new Date(oggi.getTime() + 86_400_000))
  if (giorno === 6) return lunediDi(new Date(oggi.getTime() + 2 * 86_400_000))
  return lunediDi(oggi)
}

function aIso(data: Date) {
  const p = (n: number) => String(n).padStart(2, '0')
  return `${data.getFullYear()}-${p(data.getMonth() + 1)}-${p(data.getDate())}`
}

interface CasellaAperta {
  giorno: string
  orario: string
  liberi: { id: number; nome: string }[]
}

interface Props {
  durataMinuti: number
  /**
   * Se presente, scegliere un nome nel popup imposta la scelta (Nuovo Appuntamento).
   * Se assente, il popup resta di sola consultazione: nel Calendario non c'è nessuna
   * prenotazione in corso, solo il colpo d'occhio su chi è libero.
   */
  onScegli?: (fisioterapistaId: number, orario: Date) => void
}

/**
 * Vista libero/occupato di una settimana, tutti i fisioterapisti insieme: dice quanti sono
 * liberi in ogni casella, non chi è occupato con chi. Serve a chi cerca un orario preciso
 * senza preferenze di terapista (CLAUDE.md, 15 settembre 2026) — usata sia dal Calendario
 * della Coordinatrice ("Tutti") sia da Nuovo Appuntamento ("Cerca per orario").
 */
export default function GrigliaLiberoOccupato({ durataMinuti, onScegli }: Props) {
  const [lunedi, setLunedi] = useState(lunediIniziale)
  const [giorni, setGiorni] = useState<GiornoLiberoOccupato[] | null>(null)
  const [errore, setErrore] = useState<string | null>(null)
  const [aperta, setAperta] = useState<CasellaAperta | null>(null)

  useEffect(() => {
    let annullato = false
    setGiorni(null)
    setErrore(null)
    setAperta(null)

    const fine = new Date(lunedi)
    fine.setDate(lunedi.getDate() + 4)

    api
      .get<GiornoLiberoOccupato[]>(
        `/api/agenda/libero-occupato?da=${aIso(lunedi)}&a=${aIso(fine)}&durataMinuti=${durataMinuti}`,
      )
      .then((risultato) => {
        if (!annullato) setGiorni(risultato)
      })
      .catch((e) => {
        if (!annullato) setErrore(e instanceof Error ? e.message : 'Disponibilità non caricata.')
      })

    return () => {
      annullato = true
    }
  }, [lunedi, durataMinuti])

  const orari = Array.from(new Set((giorni ?? []).flatMap((g) => g.slots.map((s) => s.orario)))).sort()

  function scegli(fisioterapistaId: number) {
    if (!aperta || !onScegli) return
    const [anno, mese, giornoDelMese] = aperta.giorno.split('-').map(Number)
    const minuti = aMinuti(aperta.orario)
    onScegli(
      fisioterapistaId,
      new Date(anno, mese - 1, giornoDelMese, Math.floor(minuti / 60), minuti % 60),
    )
    setAperta(null)
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
      {!giorni && !errore && <p style={{ color: 'var(--muted)' }}>Caricamento…</p>}

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
                    const slot = giorno.slots.find((s) => s.orario === orario)
                    if (!slot) {
                      return (
                        <td key={giorno.giorno} className="p-1">
                          <div
                            className="h-7 rounded"
                            style={{ background: 'var(--surface-2)', opacity: 0.35 }}
                          />
                        </td>
                      )
                    }

                    const libero = slot.liberi.length > 0
                    const casellaAperta =
                      aperta && aperta.giorno === giorno.giorno && aperta.orario === orario

                    return (
                      <td key={giorno.giorno} className="p-1" style={{ position: 'relative' }}>
                        <button
                          className="h-7 w-full rounded text-[0.72rem] font-semibold"
                          style={{
                            background: libero ? 'var(--good-soft)' : 'var(--surface-2)',
                            color: libero ? 'var(--good)' : 'var(--muted)',
                            cursor: libero ? 'pointer' : 'default',
                          }}
                          disabled={!libero}
                          onClick={() => setAperta({ giorno: giorno.giorno, orario, liberi: slot.liberi })}
                          aria-label={`${giorno.giorno} ${orario} — ${slot.liberi.length} liberi`}
                        >
                          {libero ? slot.liberi.length : ''}
                        </button>

                        {casellaAperta && (
                          <div
                            className="card"
                            style={{
                              position: 'absolute',
                              zIndex: 20,
                              top: '100%',
                              left: 0,
                              marginTop: 4,
                              minWidth: '190px',
                              textAlign: 'left',
                              boxShadow: '0 4px 16px rgba(0,0,0,0.18)',
                            }}
                          >
                            <div className="eyebrow mb-2">
                              {FORMATO_LUNGO.format(new Date(`${giorno.giorno}T00:00:00`))} ·{' '}
                              <span className="mono">{formattaMinuti(aMinuti(orario))}</span>
                            </div>
                            <div className="flex flex-col gap-1">
                              {aperta!.liberi.map((f) =>
                                onScegli ? (
                                  <button
                                    key={f.id}
                                    className="btn btn-ghost btn-sm"
                                    style={{ textAlign: 'left' }}
                                    onClick={() => scegli(f.id)}
                                  >
                                    {f.nome}
                                  </button>
                                ) : (
                                  <span key={f.id} className="text-[0.86rem]" style={{ padding: '2px 4px' }}>
                                    {f.nome}
                                  </span>
                                ),
                              )}
                            </div>
                            <button className="btn btn-ghost btn-sm mt-2" onClick={() => setAperta(null)}>
                              Chiudi
                            </button>
                          </div>
                        )}
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
        Il numero dice quanti fisioterapisti hanno {durataMinuti} minuti liberi lì. Clicca per
        vedere chi.
      </p>
    </div>
  )
}
