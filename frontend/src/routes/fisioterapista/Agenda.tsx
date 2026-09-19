import { useEffect, useState } from 'react'
import { api } from '../../api/client'
import type { Appuntamento, DisponibilitaGiorno, ProfiloFisioterapista } from '../../api/types'

const GIORNI_LABEL = ['Lun', 'Mar', 'Mer', 'Gio', 'Ven']
const FORMATO_BREVE = new Intl.DateTimeFormat('it-IT', { day: '2-digit', month: '2-digit' })
const FORMATO_LUNGO = new Intl.DateTimeFormat('it-IT', { weekday: 'long', day: 'numeric', month: 'long' })
const FORMATO_ORA = new Intl.DateTimeFormat('it-IT', { hour: '2-digit', minute: '2-digit' })

function isoData(d: Date) {
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
}

function lunediDi(d: Date) {
  const giorno = d.getDay()
  const l = new Date(d)
  l.setDate(d.getDate() - (giorno === 0 ? 6 : giorno - 1))
  l.setHours(0, 0, 0, 0)
  return l
}

function aMinuti(orario: string) {
  const [ore, minuti] = orario.split(':').map(Number)
  return ore * 60 + minuti
}

function formattaMinuti(minuti: number) {
  return `${String(Math.floor(minuti / 60)).padStart(2, '0')}:${String(minuti % 60).padStart(2, '0')}`
}

function classeStato(stato: Appuntamento['stato']) {
  if (stato === 'Confermato') return 'stato stato-confermato'
  if (stato === 'Richiesto') return 'stato stato-richiesto'
  if (stato === 'Annullato' || stato === 'NoShow') return 'stato stato-annullato'
  return 'stato stato-neutro'
}

// Il "Completato" non può essere lo stesso grigio delle celle vuote della griglia: si
// confondono, ed è esattamente il difetto trovato provando il mockup. Bianco con bordo
// resta neutro ma si vede.
function sfondoBlocco(stato: Appuntamento['stato']) {
  if (stato === 'Confermato') return 'var(--good-soft)'
  if (stato === 'Richiesto') return 'var(--warm-soft)'
  if (stato === 'Annullato' || stato === 'NoShow') return 'var(--danger-soft)'
  return 'var(--surface)'
}

function testoBlocco(stato: Appuntamento['stato']) {
  if (stato === 'Confermato') return 'var(--good)'
  if (stato === 'Richiesto') return 'var(--warm)'
  if (stato === 'Annullato' || stato === 'NoShow') return 'var(--danger)'
  return 'var(--muted)'
}

/**
 * Griglia settimanale, non lista: con molti appuntamenti in un giorno la lista non fa
 * vedere a colpo d'occhio i buchi fra una seduta e l'altra. Stesso pattern del Calendario
 * della Coordinatrice, qui applicato automaticamente al fisioterapista che ha fatto login
 * (mai ai colleghi) e con i comandi di completamento/no-show nel pannello di dettaglio.
 */
export default function AgendaFisioterapista() {
  const [profiloId, setProfiloId] = useState<number | null>(null)
  const [inizioSettimana, setInizioSettimana] = useState(() => lunediDi(new Date()))
  const [giorni, setGiorni] = useState<DisponibilitaGiorno[] | null>(null)
  const [appuntamenti, setAppuntamenti] = useState<Appuntamento[]>([])
  const [scelto, setScelto] = useState<Appuntamento | null>(null)
  const [errore, setErrore] = useState<string | null>(null)
  const [inCorso, setInCorso] = useState(false)
  /** true quando nel pannello di dettaglio è aperto il campo del riepilogo di seduta. */
  const [inChiusura, setInChiusura] = useState(false)
  const [riepilogo, setRiepilogo] = useState('')

  useEffect(() => {
    api
      .get<ProfiloFisioterapista>('/api/fisioterapisti/me')
      .then((p) => setProfiloId(p.id))
      .catch((e) => setErrore(e instanceof Error ? e.message : 'Profilo non caricato.'))
  }, [])

  const date = Array.from({ length: 5 }, (_, i) => {
    const g = new Date(inizioSettimana)
    g.setDate(inizioSettimana.getDate() + i)
    return g
  })
  const domenica = new Date(inizioSettimana)
  domenica.setDate(inizioSettimana.getDate() + 6)

  async function carica() {
    if (profiloId === null) return
    try {
      const [disponibilita, elenco] = await Promise.all([
        Promise.all(
          date.map((d) =>
            api.get<DisponibilitaGiorno>(
              `/api/agenda/disponibilita?fisioterapistaId=${profiloId}&data=${isoData(d)}`,
            ),
          ),
        ),
        api.get<Appuntamento[]>(
          `/api/appuntamenti/agenda?da=${isoData(date[0])}&a=${isoData(date[4])}`,
        ),
      ])
      setGiorni(disponibilita)
      setAppuntamenti(elenco)
      setErrore(null)
    } catch (e) {
      setErrore(e instanceof Error ? e.message : 'Agenda non caricata.')
    }
  }

  useEffect(() => {
    setGiorni(null)
    setScelto(null)
    void carica()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [profiloId, inizioSettimana])

  const oggi = isoData(new Date())
  const inQuestaSettimana = isoData(inizioSettimana) <= oggi && oggi <= isoData(domenica)

  // Un appuntamento occupa più blocchi da 30 minuti: il nome si scrive solo nel primo, gli
  // altri restano colorati — evita di far dipendere la griglia da un rowSpan (stesso motivo
  // del Calendario della Coordinatrice).
  function blocco(giorno: string, orario: string) {
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

  // Il riepilogo di seduta e il completamento sono un gesto solo, anche se lato API sono
  // due chiamate: si scrive cosa si è fatto e con ciò la seduta è chiusa. Il riepilogo va
  // salvato per primo — se fallisse dopo il completamento, resterebbe una seduta chiusa
  // senza il resoconto che il paziente poi legge.
  async function completa(appuntamento: Appuntamento) {
    setInCorso(true)
    setErrore(null)
    try {
      if (riepilogo.trim()) {
        await api.post(`/api/appuntamenti/${appuntamento.id}/riepilogo-seduta`, { testo: riepilogo.trim() })
      }
      await api.put(`/api/appuntamenti/${appuntamento.id}/completa`)
      setInChiusura(false)
      setRiepilogo('')
      setScelto(null)
      await carica()
    } catch (e) {
      setErrore(e instanceof Error ? e.message : 'Operazione non riuscita.')
    } finally {
      setInCorso(false)
    }
  }

  async function segnaNoShow(appuntamento: Appuntamento) {
    setInCorso(true)
    setErrore(null)
    try {
      await api.put(`/api/appuntamenti/${appuntamento.id}/no-show`)
      setScelto(null)
      await carica()
    } catch (e) {
      setErrore(e instanceof Error ? e.message : 'Operazione non riuscita.')
    } finally {
      setInCorso(false)
    }
  }

  return (
    <>
      <header className="mb-6">
        <h1 className="view-title">La mia agenda</h1>
        <p className="view-sub">Un colpo d'occhio sulla settimana. Clicca un appuntamento per i dettagli e i comandi.</p>
      </header>

      <div className="flex items-center justify-center gap-4 mb-5">
        <button
          type="button"
          className="btn btn-ghost btn-sm"
          aria-label="Settimana precedente"
          onClick={() => setInizioSettimana((prec) => { const d = new Date(prec); d.setDate(d.getDate() - 7); return d })}
        >
          ←
        </button>
        <div className="text-center" style={{ minWidth: '13ch' }}>
          <div style={{ fontWeight: 700, fontSize: '.95rem' }}>
            {FORMATO_BREVE.format(date[0])} – {FORMATO_BREVE.format(date[4])}
          </div>
          {inQuestaSettimana && (
            <div className="text-[0.72rem]" style={{ color: 'var(--accent)', fontWeight: 600 }}>Questa settimana</div>
          )}
        </div>
        <button
          type="button"
          className="btn btn-ghost btn-sm"
          aria-label="Settimana successiva"
          onClick={() => setInizioSettimana((prec) => { const d = new Date(prec); d.setDate(d.getDate() + 7); return d })}
        >
          →
        </button>
      </div>

      {errore && (
        <p
          className="mb-4 rounded-lg px-3 py-2 text-sm"
          style={{ background: 'var(--danger-soft)', color: 'var(--danger)' }}
          role="alert"
        >
          {errore}
        </p>
      )}

      <div className="card">
        {!giorni && !errore && <p style={{ color: 'var(--muted)' }}>Caricamento…</p>}

        {giorni && (
          <div className="overflow-x-auto">
            <table className="w-full border-collapse text-center" style={{ minWidth: '640px' }}>
              <thead>
                <tr>
                  <th className="eyebrow p-2 text-left" />
                  {giorni.map((giorno, i) => (
                    <th
                      key={giorno.giorno}
                      className="eyebrow p-2"
                      style={giorno.giorno === oggi ? { color: 'var(--accent)' } : undefined}
                    >
                      {GIORNI_LABEL[i]} {FORMATO_BREVE.format(new Date(`${giorno.giorno}T00:00:00`))}
                      {giorno.chiuso && (
                        <div
                          style={{
                            color: 'var(--danger)', fontWeight: 600, textTransform: 'none',
                            letterSpacing: 'normal', fontSize: '0.72rem',
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
                            <div className="h-8 rounded" style={{ background: 'var(--surface-2)', opacity: 0.35 }} />
                          </td>
                        )
                      }

                      const occupato = blocco(giorno.giorno, orario)
                      if (!occupato) {
                        return (
                          <td key={giorno.giorno} className="p-1">
                            <div className="h-8 rounded" style={{ background: 'var(--surface-2)' }} />
                          </td>
                        )
                      }

                      const selezionato = scelto?.id === occupato.appuntamento.id
                      return (
                        <td key={giorno.giorno} className="p-1">
                          <button
                            className="h-8 w-full overflow-hidden rounded px-1 text-[0.72rem] font-semibold"
                            style={{
                              background: sfondoBlocco(occupato.appuntamento.stato),
                              color: testoBlocco(occupato.appuntamento.stato),
                              border: selezionato
                                ? '2px solid var(--ink)'
                                : occupato.appuntamento.stato === 'Completato'
                                  ? '1px solid var(--border)'
                                  : '1px solid transparent',
                              textAlign: 'left', whiteSpace: 'nowrap', textOverflow: 'ellipsis',
                            }}
                            onClick={() => {
                              setScelto(occupato.appuntamento)
                              setInChiusura(false)
                              setRiepilogo('')
                            }}
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
                {scelto.durataMinuti} min · {scelto.percorso === 'Ssn' ? 'Convenzionato SSN' : 'Privato'}
              </div>
            </div>
            <span className={classeStato(scelto.stato)}>{scelto.stato}</span>
          </div>

          {scelto.stato === 'Richiesto' && (
            <p className="mt-3 rounded-lg px-3 py-2 text-[0.86rem]" style={{ background: 'var(--warm-soft)' }}>
              Tiene bloccato questo orario ma non è ancora confermato: lo decide la segreteria dalla coda Richieste.
            </p>
          )}
          {scelto.motivoAnnullamento && (
            <p className="mt-3 text-[0.86rem]" style={{ color: 'var(--muted)' }}>Motivo: {scelto.motivoAnnullamento}</p>
          )}

          {scelto.stato === 'Confermato' && !inChiusura && (
            <div className="mt-3 flex flex-wrap gap-2">
              <button className="btn btn-primary btn-sm" disabled={inCorso} onClick={() => { setInChiusura(true); setRiepilogo('') }}>
                Completa seduta
              </button>
              <button className="btn btn-ghost btn-sm" disabled={inCorso} onClick={() => void segnaNoShow(scelto)}>
                Non presentato
              </button>
            </div>
          )}

          {scelto.stato === 'Confermato' && inChiusura && (
            <div className="mt-3">
              <label htmlFor="riepilogo-scelto">Riepilogo della seduta</label>
              <textarea
                id="riepilogo-scelto"
                rows={3}
                value={riepilogo}
                onChange={(e) => setRiepilogo(e.target.value)}
                placeholder="Che cosa è stato svolto, in breve. È quello che il paziente leggerà nel proprio portale."
              />
              <div className="mt-2 flex flex-wrap items-center gap-2">
                <button className="btn btn-primary btn-sm" disabled={inCorso} onClick={() => void completa(scelto)}>
                  {inCorso ? 'Salvataggio…' : 'Chiudi la seduta'}
                </button>
                <button className="btn btn-ghost btn-sm" disabled={inCorso} onClick={() => setInChiusura(false)}>
                  Annulla
                </button>
                <span className="text-[0.8rem]" style={{ color: 'var(--muted)' }}>
                  Completare la seduta scala una seduta dal pacchetto o dal ciclo.
                </span>
              </div>
            </div>
          )}

          <button className="btn btn-ghost btn-sm mt-3" onClick={() => setScelto(null)}>Chiudi</button>
        </div>
      )}
    </>
  )
}
