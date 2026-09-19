import { useEffect, useState } from 'react'
import { api } from '../../api/client'
import type { Configurazione as ConfigurazioneDto } from '../../api/types'

export default function Configurazione() {
  const [dati, setDati] = useState<ConfigurazioneDto | null>(null)
  const [errore, setErrore] = useState<string | null>(null)
  const [esito, setEsito] = useState<string | null>(null)
  const [inCorso, setInCorso] = useState(false)

  const [prezzoPacchetto, setPrezzoPacchetto] = useState(0)
  const [sedutePacchetto, setSedutePacchetto] = useState(0)
  const [mesiValidita, setMesiValidita] = useState(0)
  const [prezzoSeduta, setPrezzoSeduta] = useState(0)
  const [ticket, setTicket] = useState(0)
  const [orari, setOrari] = useState({
    mattinaInizio: '09:00', mattinaFine: '13:00',
    pomeriggioInizio: '15:00', pomeriggioFine: '19:00',
  })

  async function carica() {
    const risposta = await api.get<ConfigurazioneDto>('/api/admin/configurazione')
    setDati(risposta)
    setPrezzoPacchetto(risposta.prezzoPacchettoPrivato)
    setSedutePacchetto(risposta.sedutePacchettoPrivato)
    setMesiValidita(risposta.durataValiditaPacchettoMesi)
    setPrezzoSeduta(risposta.prezzoSedutaSingolaManuale)
    setTicket(risposta.quotaTicketRegionale)
    // Il backend manda "09:00:00": all'input time servono ore e minuti.
    const oraSecca = (valore: string | null, predefinito: string) =>
      valore ? valore.slice(0, 5) : predefinito
    setOrari({
      mattinaInizio: oraSecca(risposta.mattinaInizio, '09:00'),
      mattinaFine: oraSecca(risposta.mattinaFine, '13:00'),
      pomeriggioInizio: oraSecca(risposta.pomeriggioInizio, '15:00'),
      pomeriggioFine: oraSecca(risposta.pomeriggioFine, '19:00'),
    })
  }

  useEffect(() => {
    carica().catch((e) => setErrore(e instanceof Error ? e.message : 'Dati non caricati.'))
  }, [])

  async function salva() {
    setInCorso(true)
    setErrore(null)
    setEsito(null)
    try {
      await api.put('/api/admin/listino', {
        prezzoPacchettoPrivato: prezzoPacchetto,
        sedutePacchettoPrivato: sedutePacchetto,
        durataValiditaPacchettoMesi: mesiValidita,
        prezzoSedutaSingolaManuale: prezzoSeduta,
        quotaTicketRegionale: ticket,
      })
      await carica()
      setEsito('Listino aggiornato. Vale per i prossimi acquisti, non per i pacchetti già venduti.')
    } catch (e) {
      setErrore(e instanceof Error ? e.message : 'Salvataggio non riuscito.')
    } finally {
      setInCorso(false)
    }
  }

  async function salvaOrari() {
    setInCorso(true)
    setErrore(null)
    setEsito(null)
    try {
      await api.put('/api/admin/orari', {
        mattinaInizio: `${orari.mattinaInizio}:00`,
        mattinaFine: `${orari.mattinaFine}:00`,
        pomeriggioInizio: `${orari.pomeriggioInizio}:00`,
        pomeriggioFine: `${orari.pomeriggioFine}:00`,
      })
      await carica()
      setEsito('Orari aggiornati: da adesso guidano anche le disponibilità mostrate ai pazienti.')
    } catch (e) {
      setErrore(e instanceof Error ? e.message : 'Salvataggio non riuscito.')
    } finally {
      setInCorso(false)
    }
  }

  return (
    <>
      <header className="mb-6">
        <h1 className="view-title">Configurazione</h1>
        <p className="view-sub">Listino, orari e account del portale.</p>
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
      {!dati && !errore && <p style={{ color: 'var(--muted)' }}>Caricamento…</p>}

      {dati && (
        <>
          <section className="card mb-4">
            <h2 className="eyebrow mb-3">Listino</h2>
            <div className="grid gap-3 sm:grid-cols-2">
              <div className="field mb-0">
                <label htmlFor="prezzo-pacchetto">Prezzo del pacchetto privato</label>
                <input id="prezzo-pacchetto" type="number" step="0.01" value={prezzoPacchetto} onChange={(e) => setPrezzoPacchetto(Number(e.target.value))} />
              </div>
              <div className="field mb-0">
                <label htmlFor="sedute-pacchetto">Sedute comprese</label>
                <input id="sedute-pacchetto" type="number" value={sedutePacchetto} onChange={(e) => setSedutePacchetto(Number(e.target.value))} />
              </div>
              <div className="field mb-0">
                <label htmlFor="mesi">Validità in mesi</label>
                <input id="mesi" type="number" value={mesiValidita} onChange={(e) => setMesiValidita(Number(e.target.value))} />
              </div>
              <div className="field mb-0">
                <label htmlFor="prezzo-seduta">Seduta singola di terapia manuale</label>
                <input id="prezzo-seduta" type="number" step="0.01" value={prezzoSeduta} onChange={(e) => setPrezzoSeduta(Number(e.target.value))} />
              </div>
              <div className="field mb-0">
                <label htmlFor="ticket">Quota ticket regionale</label>
                <input id="ticket" type="number" step="0.01" value={ticket} onChange={(e) => setTicket(Number(e.target.value))} />
              </div>
            </div>

            {/* Il ticket è precompilato ma resta correggibile in validazione: la cifra vera
                la porta la ricetta (decisione del 12 settembre in requisiti.md). */}
            <p className="mt-3 text-[0.82rem]" style={{ color: 'var(--muted)' }}>
              La quota ticket è un valore di partenza: in fase di validazione la segreteria
              può sempre correggerla leggendola dalla ricetta.
            </p>

            <button className="btn btn-primary btn-sm mt-3" disabled={inCorso} onClick={() => void salva()}>
              {inCorso ? 'Salvataggio…' : 'Salva il listino'}
            </button>
          </section>

          <section className="card mb-4">
            <h2 className="eyebrow mb-2">Orari di apertura</h2>
            <p className="mb-3">{dati.orariApertura}</p>

            <div className="grid gap-x-4 sm:grid-cols-2">
              <div className="field">
                <label htmlFor="mattina-inizio">Mattina, apertura</label>
                <input
                  id="mattina-inizio"
                  type="time"
                  value={orari.mattinaInizio}
                  onChange={(e) => setOrari({ ...orari, mattinaInizio: e.target.value })}
                />
              </div>
              <div className="field">
                <label htmlFor="mattina-fine">Mattina, chiusura</label>
                <input
                  id="mattina-fine"
                  type="time"
                  value={orari.mattinaFine}
                  onChange={(e) => setOrari({ ...orari, mattinaFine: e.target.value })}
                />
              </div>
              <div className="field">
                <label htmlFor="pomeriggio-inizio">Pomeriggio, apertura</label>
                <input
                  id="pomeriggio-inizio"
                  type="time"
                  value={orari.pomeriggioInizio}
                  onChange={(e) => setOrari({ ...orari, pomeriggioInizio: e.target.value })}
                />
              </div>
              <div className="field">
                <label htmlFor="pomeriggio-fine">Pomeriggio, chiusura</label>
                <input
                  id="pomeriggio-fine"
                  type="time"
                  value={orari.pomeriggioFine}
                  onChange={(e) => setOrari({ ...orari, pomeriggioFine: e.target.value })}
                />
              </div>
            </div>

            <p className="text-[0.82rem]" style={{ color: 'var(--muted)' }}>
              Questi orari sono la disponibilità che i pazienti vedono: cambiarli cambia
              subito cosa è prenotabile. Gli appuntamenti già presi fuori dal nuovo orario
              restano dove sono — vanno rivisti a mano dalla segreteria.
            </p>
            <button className="btn btn-primary btn-sm mt-3" disabled={inCorso} onClick={() => void salvaOrari()}>
              {inCorso ? 'Salvataggio…' : 'Salva gli orari'}
            </button>
          </section>

          <section>
            <h2 className="eyebrow mb-3">Account</h2>
            <div className="card">
              <table className="w-full text-[0.88rem]">
                <thead>
                  <tr>
                    <th className="eyebrow p-2 text-left">Nome</th>
                    <th className="eyebrow p-2 text-left">Email</th>
                    <th className="eyebrow p-2 text-left">Ruolo</th>
                  </tr>
                </thead>
                <tbody>
                  {dati.account.map((account) => (
                    <tr key={account.email}>
                      <td className="p-2">{account.nome} {account.cognome}</td>
                      <td className="mono p-2">{account.email}</td>
                      <td className="p-2">{account.ruolo}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <p className="mt-3 text-[0.82rem]" style={{ color: 'var(--muted)' }}>
              Elenco in sola lettura: i tre stati dell'account previsti dal mockup
              (attivo, disattivato, invito in sospeso) non sono ancora nel modello dati.
            </p>
          </section>
        </>
      )}
    </>
  )
}
