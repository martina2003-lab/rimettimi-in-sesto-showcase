import { Navigate, Outlet } from 'react-router-dom'
import { HOME_PER_RUOLO, useAuth } from './AuthContext'
import type { Ruolo } from '../api/types'

// Questo è un filtro di comodità per la navigazione, NON il controllo di sicurezza:
// quello vive lato server su ogni endpoint (principio guida di CLAUDE.md). Qui serve
// solo a non mostrare a un ruolo schermate che il backend gli rifiuterebbe comunque.
export default function RottaProtetta({ ruoloRichiesto }: { ruoloRichiesto?: Ruolo }) {
  const { utente, caricamentoIniziale, erroreConnessione, riprova } = useAuth()

  if (caricamentoIniziale) {
    return (
      <div className="flex min-h-svh items-center justify-center" style={{ color: 'var(--muted)' }}>
        Caricamento…
      </div>
    )
  }

  // Server irraggiungibile con una sessione ancora valida: non è un logout, quindi non si
  // rimanda al login facendo credere che le credenziali siano scadute. Si spiega e si riprova.
  if (!utente && erroreConnessione) {
    return (
      <div className="flex min-h-svh items-center justify-center px-6">
        <div className="card max-w-md text-center">
          <h1 className="view-title mb-2 text-2xl">Server non raggiungibile</h1>
          <p className="mb-4" style={{ color: 'var(--muted)' }}>{erroreConnessione}</p>
          <button className="btn btn-primary" onClick={riprova}>Riprova</button>
        </div>
      </div>
    )
  }

  if (!utente) return <Navigate to="/login" replace />

  if (ruoloRichiesto && utente.ruolo !== ruoloRichiesto) {
    return <Navigate to={HOME_PER_RUOLO[utente.ruolo]} replace />
  }

  return <Outlet />
}
