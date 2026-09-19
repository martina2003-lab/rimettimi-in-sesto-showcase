// In sviluppo il backend sta su un'altra porta; una volta compilato, frontend e API sono
// serviti dallo stesso App Service, quindi l'origine è la stessa e basta un percorso
// relativo. `VITE_API_URL` resta per puntare altrove senza ricompilare le convinzioni.
//
// L'host non è mai scritto come "localhost" fisso: se il portale viene aperto da un altro
// dispositivo sulla stessa rete (es. il telefono, puntando all'indirizzo IP del computer),
// "localhost" per quel dispositivo indicherebbe se stesso, non il computer con l'API.
// Usando lo stesso host con cui è stata aperta la pagina, funziona in entrambi i casi.
const BASE_URL = import.meta.env.VITE_API_URL ?? (import.meta.env.DEV ? `http://${window.location.hostname}:5131` : '')

const CHIAVE_TOKEN = 'rimettimi-token'

export function leggiToken(): string | null {
  return localStorage.getItem(CHIAVE_TOKEN)
}

export function salvaToken(token: string) {
  localStorage.setItem(CHIAVE_TOKEN, token)
}

export function rimuoviToken() {
  localStorage.removeItem(CHIAVE_TOKEN)
}

export class ApiError extends Error {
  // Campo dichiarato e assegnato a mano invece che come parametro del costruttore:
  // le proprietà-parametro non sono sintassi cancellabile, e il progetto compila con
  // `erasableSyntaxOnly` attivo.
  readonly status: number

  constructor(message: string, status: number) {
    super(message)
    this.status = status
  }
}

// Il backend risponde agli errori di dominio con { messaggio: "..." }: sono messaggi
// già scritti per essere letti da una persona (es. "Si può cancellare un appuntamento
// fino a 24 ore prima"), quindi si mostrano così come sono invece di tradurli qui.
async function richiesta<T>(metodo: string, percorso: string, corpo?: unknown): Promise<T> {
  const token = leggiToken()

  let risposta: Response
  try {
    risposta = await fetch(`${BASE_URL}${percorso}`, {
      method: metodo,
      headers: {
        ...(corpo !== undefined ? { 'Content-Type': 'application/json' } : {}),
        ...(token ? { Authorization: `Bearer ${token}` } : {}),
      },
      body: corpo !== undefined ? JSON.stringify(corpo) : undefined,
    })
  } catch {
    // Server irraggiungibile o rete assente: fetch fallisce senza risposta HTTP. Senza
    // questo blocco arriverebbe a schermo il "Failed to fetch" del browser, in inglese
    // e incomprensibile per chi sta usando il portale.
    throw new ApiError(
      'Non riesco a contattare il server. Controlla la connessione e riprova fra un momento.',
      0,
    )
  }

  if (!risposta.ok) {
    let messaggio = `Errore ${risposta.status}`
    try {
      const dati = await risposta.json()
      if (dati?.messaggio) messaggio = dati.messaggio
    } catch {
      // risposta senza corpo JSON (401/403 tipicamente): resta il messaggio generico
    }
    if (risposta.status === 401) messaggio = 'Sessione scaduta, accedi di nuovo.'
    if (risposta.status === 403) messaggio = 'Non hai i permessi per questa operazione.'
    throw new ApiError(messaggio, risposta.status)
  }

  if (risposta.status === 204) return undefined as T
  const testo = await risposta.text()
  return (testo ? JSON.parse(testo) : undefined) as T
}

export const api = {
  get: <T>(percorso: string) => richiesta<T>('GET', percorso),
  post: <T>(percorso: string, corpo?: unknown) => richiesta<T>('POST', percorso, corpo ?? {}),
  put: <T>(percorso: string, corpo?: unknown) => richiesta<T>('PUT', percorso, corpo ?? {}),
}
