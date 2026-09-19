import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { HOME_PER_RUOLO, useAuth } from '../auth/AuthContext'
import type { Ruolo } from '../api/types'
import logo from '../assets/logo.svg'

// Si entra scegliendo un ruolo, non digitando credenziali: il modulo email/password è stato
// tolto il 15 settembre 2026 perché un form di login su un indirizzo condiviso e senza
// reputazione (azurewebsites.net) è il segnale che fa classificare una pagina come phishing
// — ed è quello che era successo. Gli account restano quelli del seed, ma non si scrivono:
// li usa direttamente il pulsante.
const ACCOUNT_DEMO: { ruolo: Ruolo; chi: string; cosaVede: string; email: string; icona: string }[] = [
  {
    ruolo: 'Paziente',
    chi: 'Marco Bianchi',
    cosaVede: 'Prenota una seduta, consulta la propria cartella e i pagamenti.',
    email: 'marco.bianchi@paziente.example',
    icona: 'ti-user-heart',
  },
  {
    ruolo: 'Fisioterapista',
    chi: 'Elena Ricci',
    cosaVede: "Agenda della giornata, cartella clinica dei propri pazienti, assenze.",
    email: 'elena.ricci@studio.example',
    icona: 'ti-stethoscope',
  },
  {
    ruolo: 'Coordinatrice',
    chi: 'Francesca',
    cosaVede: 'Calendario, richieste da confermare, ricette, cassa.',
    email: 'francesca@studio.example',
    icona: 'ti-headset',
  },
  {
    ruolo: 'Admin',
    chi: 'Fabrizio Rinaldi',
    cosaVede: 'Andamento delle entrate e ore del personale, senza accesso clinico.',
    email: 'fabrizio.rinaldi@studio.example',
    icona: 'ti-chart-infographic',
  },
]

const PASSWORD_DEMO = 'demo1234'

export default function Login() {
  const { login } = useAuth()
  const navigate = useNavigate()
  const [errore, setErrore] = useState<string | null>(null)
  const [inCorso, setInCorso] = useState(false)

  async function entra(emailScelta: string, passwordScelta: string) {
    setErrore(null)
    setInCorso(true)
    try {
      const utente = await login(emailScelta, passwordScelta)
      navigate(HOME_PER_RUOLO[utente.ruolo], { replace: true })
    } catch (e) {
      setErrore(e instanceof Error ? e.message : 'Accesso non riuscito.')
    } finally {
      setInCorso(false)
    }
  }

  return (
    <div className="mx-auto max-w-[920px] px-6 pb-16 pt-12">
      <div className="mb-7 flex flex-col items-center gap-3">
        <img className="brand-logo" style={{ height: '3rem' }} src={logo} alt="Rimettimi in sesto" />
        <h1 className="view-title text-center">Rimettimi in sesto</h1>
      </div>

      <p className="view-sub mb-7 text-center">
        Prototipo di portale per la gestione di uno studio di fisioterapia convenzionato:
        prenotazioni, agenda condivisa, cartella clinica e cassa.
      </p>

      <div
        className="mx-auto mb-10 flex max-w-[640px] items-start gap-3 rounded-xl px-4 py-3 text-[0.86rem]"
        style={{ background: 'var(--warm-soft)' }}
      >
        <i className="ti ti-info-circle text-lg" style={{ color: 'var(--warm)' }} />
        <span>
          <strong>Progetto dimostrativo.</strong> Non è il sito di uno studio reale e non
          appartiene a nessuna attività esistente: persone, pazienti, diagnosi e importi sono
          inventati a scopo di esempio. Non ci sono account personali e non viene chiesta
          nessuna credenziale — si entra scegliendo uno dei quattro ruoli qui sotto. Nessuna
          email o SMS viene realmente inviato e nessun pagamento è reale.
        </span>
      </div>

      {errore && (
        <p
          className="mx-auto mb-6 max-w-[640px] rounded-lg px-3 py-2 text-sm"
          style={{ background: 'var(--danger-soft)', color: 'var(--danger)' }}
          role="alert"
        >
          {errore}
        </p>
      )}

      <div className="eyebrow mb-5 flex items-center gap-3">
        <span className="h-px flex-1" style={{ background: 'var(--border)' }} />
        Scegli un ruolo per entrare
        <span className="h-px flex-1" style={{ background: 'var(--border)' }} />
      </div>

      <div className="grid gap-4 sm:grid-cols-2">
        {ACCOUNT_DEMO.map((account) => (
          <div key={account.ruolo} className="card flex flex-col gap-3">
            <div className="flex items-center gap-3">
              <div
                className="flex h-10 w-10 flex-shrink-0 items-center justify-center rounded-[10px] text-xl"
                style={{ background: 'var(--accent-soft)', color: 'var(--accent)' }}
              >
                <i className={`ti ${account.icona}`} />
              </div>
              <div>
                <div style={{ fontFamily: 'Montserrat, sans-serif', fontWeight: 700, fontSize: '0.92rem' }}>
                  {account.ruolo}
                </div>
                <div className="text-[0.78rem]" style={{ color: 'var(--muted)' }}>
                  {account.chi} — personaggio di esempio
                </div>
              </div>
            </div>

            {/* flex-1: le descrizioni hanno lunghezze diverse, senza questo i pulsanti
                delle quattro schede finivano ad altezze diverse. */}
            <p className="flex-1 text-[0.82rem]" style={{ color: 'var(--muted)' }}>
              {account.cosaVede}
            </p>

            <button
              className="btn btn-primary"
              disabled={inCorso}
              onClick={() => void entra(account.email, PASSWORD_DEMO)}
            >
              {inCorso ? 'Accesso in corso…' : `Entra come ${account.ruolo}`}
            </button>
          </div>
        ))}
      </div>

      <p className="mt-9 text-center text-[0.8rem]" style={{ color: 'var(--muted)' }}>
        I permessi sono comunque applicati sul server, ruolo per ruolo: ogni profilo vede
        soltanto ciò che gli compete, e la cartella clinica solo il fisioterapista.
      </p>
    </div>
  )
}
