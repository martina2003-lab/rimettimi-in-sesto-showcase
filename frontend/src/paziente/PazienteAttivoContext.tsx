import { createContext, useContext, useEffect, useState, type ReactNode } from 'react'
import { api } from '../api/client'
import type { PazienteCollegato } from '../api/types'

// Un solo switcher, non uno per pagina (come nel mockup): scegli il profilo una volta,
// e tutte le pagine dell'area Paziente (Appuntamenti, Prenota, Cartella, Pagamenti) restano
// coerenti su quel profilo finché non lo cambi di nuovo. La prima versione aveva un
// useState locale identico ripetuto in quattro file: cambiare pagina faceva perdere la
// scelta e tornare al default, perché ogni pagina aveva il proprio stato indipendente.
interface PazienteAttivoValue {
  pazienti: PazienteCollegato[]
  pazienteId: number | null
  setPazienteId: (id: number) => void
  caricamento: boolean
}

const PazienteAttivoContext = createContext<PazienteAttivoValue | null>(null)

export function PazienteAttivoProvider({ children }: { children: ReactNode }) {
  const [pazienti, setPazienti] = useState<PazienteCollegato[]>([])
  const [pazienteId, setPazienteId] = useState<number | null>(null)
  const [caricamento, setCaricamento] = useState(true)

  useEffect(() => {
    api
      .get<PazienteCollegato[]>('/api/pazienti/miei')
      .then((collegati) => {
        setPazienti(collegati)
        if (collegati.length > 0) setPazienteId(collegati[0].id)
      })
      .finally(() => setCaricamento(false))
  }, [])

  return (
    <PazienteAttivoContext.Provider value={{ pazienti, pazienteId, setPazienteId, caricamento }}>
      {children}
    </PazienteAttivoContext.Provider>
  )
}

export function usePazienteAttivo() {
  const ctx = useContext(PazienteAttivoContext)
  if (!ctx) throw new Error('usePazienteAttivo va chiamato dentro PazienteAttivoProvider')
  return ctx
}
