import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from 'react'
import { ApiError, api, leggiToken, rimuoviToken, salvaToken } from '../api/client'
import type { LoginResponse, Ruolo } from '../api/types'

interface UtenteCorrente {
  utenteId: string
  email: string
  nome: string
  cognome: string
  ruolo: Ruolo
}

interface AuthContextValue {
  utente: UtenteCorrente | null
  caricamentoIniziale: boolean
  /** Valorizzato quando la sessione c'è ma il server non risponde: è diverso da "non autenticato". */
  erroreConnessione: string | null
  riprova: () => void
  login: (email: string, password: string) => Promise<UtenteCorrente>
  logout: () => void
}

const AuthContext = createContext<AuthContextValue | null>(null)

const CHIAVE_UTENTE = 'rimettimi-utente'

export function AuthProvider({ children }: { children: ReactNode }) {
  const [utente, setUtente] = useState<UtenteCorrente | null>(null)
  const [caricamentoIniziale, setCaricamentoIniziale] = useState(true)
  const [erroreConnessione, setErroreConnessione] = useState<string | null>(null)
  const [tentativo, setTentativo] = useState(0)

  // Al ricaricamento della pagina il token è ancora in localStorage: si verifica che sia
  // valido chiedendolo al backend, invece di fidarsi di quanto c'è scritto nel browser.
  useEffect(() => {
    if (!leggiToken()) {
      setCaricamentoIniziale(false)
      return
    }

    setCaricamentoIniziale(true)
    setErroreConnessione(null)

    api
      .get<UtenteCorrente>('/api/auth/me')
      .then((dati) => setUtente(dati))
      .catch((errore) => {
        // Due situazioni diverse che sarebbe sbagliato trattare allo stesso modo.
        // Sessione davvero non più valida (401): si esce e si rifà l'accesso.
        // Server irraggiungibile: la sessione è ancora buona e il token va tenuto —
        // altrimenti un calo di rete in studio costringerebbe a riautenticarsi.
        if (errore instanceof ApiError && errore.status === 401) {
          rimuoviToken()
          localStorage.removeItem(CHIAVE_UTENTE)
          return
        }
        setErroreConnessione(
          errore instanceof Error ? errore.message : 'Non riesco a contattare il server.',
        )
      })
      .finally(() => setCaricamentoIniziale(false))
  }, [tentativo])

  const valore = useMemo<AuthContextValue>(
    () => ({
      utente,
      caricamentoIniziale,
      erroreConnessione,
      riprova: () => setTentativo((n) => n + 1),
      login: async (email, password) => {
        const risposta = await api.post<LoginResponse>('/api/auth/login', { email, password })
        salvaToken(risposta.token)
        const corrente: UtenteCorrente = {
          utenteId: risposta.utenteId,
          email: risposta.email,
          nome: risposta.nome,
          cognome: risposta.cognome,
          ruolo: risposta.ruolo,
        }
        localStorage.setItem(CHIAVE_UTENTE, JSON.stringify(corrente))
        setUtente(corrente)
        return corrente
      },
      logout: () => {
        rimuoviToken()
        localStorage.removeItem(CHIAVE_UTENTE)
        setUtente(null)
      },
    }),
    [utente, caricamentoIniziale, erroreConnessione],
  )

  return <AuthContext.Provider value={valore}>{children}</AuthContext.Provider>
}

export function useAuth() {
  const contesto = useContext(AuthContext)
  if (!contesto) throw new Error('useAuth va usato dentro AuthProvider')
  return contesto
}

// Dove atterra ciascun ruolo dopo il login: quattro aree separate, una per ruolo.
export const HOME_PER_RUOLO: Record<Ruolo, string> = {
  Paziente: '/paziente',
  Fisioterapista: '/fisioterapista',
  Coordinatrice: '/coordinatrice',
  Admin: '/admin',
}
