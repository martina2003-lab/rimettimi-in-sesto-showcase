import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import { AuthProvider, HOME_PER_RUOLO, useAuth } from './auth/AuthContext'
import RottaProtetta from './auth/RottaProtetta'
import LayoutRuolo, { type VoceMenu } from './components/LayoutRuolo'
import Login from './routes/Login'
import AppuntamentiPaziente from './routes/paziente/Appuntamenti'
import Prenota from './routes/paziente/Prenota'
import AgendaFisioterapista from './routes/fisioterapista/Agenda'
import RichiesteCoordinatrice from './routes/coordinatrice/Richieste'
import CalendarioCoordinatrice from './routes/coordinatrice/Calendario'
import PazientiCoordinatrice from './routes/coordinatrice/Pazienti'
import ListaAttesa from './routes/coordinatrice/ListaAttesa'
import RicetteSsn from './routes/coordinatrice/Ricette'
import NuovoAppuntamento from './routes/coordinatrice/NuovoAppuntamento'
import Cassa from './routes/coordinatrice/Cassa'
import Andamento from './routes/admin/Andamento'
import Personale from './routes/admin/Personale'
import ConfigurazioneAdmin from './routes/admin/Configurazione'
import PazientiFisioterapista from './routes/fisioterapista/Pazienti'
import Bio from './routes/fisioterapista/Bio'
import Assenze from './routes/fisioterapista/Assenze'
import FisioterapistiCoordinatrice from './routes/coordinatrice/Fisioterapisti'
import Cartella from './routes/paziente/Cartella'
import PagamentiPaziente from './routes/paziente/Pagamenti'
import { PazienteAttivoProvider } from './paziente/PazienteAttivoContext'
import SwitcherPaziente from './paziente/SwitcherPaziente'

// Le voci di menu rispecchiano le sezioni già disegnate nei mockup, una per ruolo.
const MENU_PAZIENTE: VoceMenu[] = [
  { etichetta: 'Appuntamenti', percorso: '/paziente', icona: 'ti-calendar-event' },
  { etichetta: 'Prenota', percorso: '/paziente/prenota', icona: 'ti-calendar-plus' },
  { etichetta: 'Cartella', percorso: '/paziente/cartella', icona: 'ti-report-medical' },
  { etichetta: 'Pagamenti', percorso: '/paziente/pagamenti', icona: 'ti-receipt' },
]

const MENU_FISIOTERAPISTA: VoceMenu[] = [
  { etichetta: 'Agenda', percorso: '/fisioterapista', icona: 'ti-calendar-event' },
  { etichetta: 'Pazienti', percorso: '/fisioterapista/pazienti', icona: 'ti-users' },
  { etichetta: 'Bio', percorso: '/fisioterapista/bio', icona: 'ti-id-badge' },
  { etichetta: 'Assenze', percorso: '/fisioterapista/assenze', icona: 'ti-calendar-off' },
]

const MENU_COORDINATRICE: VoceMenu[] = [
  { etichetta: 'Calendario', percorso: '/coordinatrice/calendario', icona: 'ti-calendar-event' },
  { etichetta: 'Richieste', percorso: '/coordinatrice', icona: 'ti-inbox' },
  { etichetta: 'Nuovo appuntamento', percorso: '/coordinatrice/nuovo', icona: 'ti-calendar-plus' },
  { etichetta: 'Ricette SSN', percorso: '/coordinatrice/ricette', icona: 'ti-file-certificate' },
  { etichetta: 'Pazienti', percorso: '/coordinatrice/pazienti', icona: 'ti-users' },
  { etichetta: "Lista d'attesa", percorso: '/coordinatrice/attesa', icona: 'ti-hourglass' },
  { etichetta: 'Fisioterapisti', percorso: '/coordinatrice/fisioterapisti', icona: 'ti-stethoscope' },
  { etichetta: 'Cassa', percorso: '/coordinatrice/cassa', icona: 'ti-cash-register' },
]

// "Entrate" e "Andamento" erano due voci separate che rispondevano alla stessa domanda con
// pezzi diversi: la prima un totale + l'incrocio percorso/prestazione per un anno, la
// seconda il trend nel tempo. Fuse in una sola voce — l'incrocio ora vive dentro Andamento,
// visibile solo a grana annuale — per non avere due schermate che si contraddicono quando
// i dati divergono.
const MENU_ADMIN: VoceMenu[] = [
  { etichetta: 'Andamento', percorso: '/admin', icona: 'ti-trending-up' },
  { etichetta: 'Personale', percorso: '/admin/personale', icona: 'ti-users-group' },
  { etichetta: 'Configurazione', percorso: '/admin/configurazione', icona: 'ti-settings' },
]

// Mandare "/" all'area del proprio ruolo, o al login se non si è autenticati.
function Ingresso() {
  const { utente, caricamentoIniziale } = useAuth()
  if (caricamentoIniziale) return null
  return <Navigate to={utente ? HOME_PER_RUOLO[utente.ruolo] : '/login'} replace />
}

export default function App() {
  return (
    <AuthProvider>
      <BrowserRouter>
        <Routes>
          <Route path="/" element={<Ingresso />} />
          <Route path="/login" element={<Login />} />

          <Route element={<RottaProtetta ruoloRichiesto="Paziente" />}>
            <Route
              path="/paziente"
              element={
                <PazienteAttivoProvider>
                  <LayoutRuolo voci={MENU_PAZIENTE} titoloRuolo="Paziente" menuProfilo={<SwitcherPaziente />} />
                </PazienteAttivoProvider>
              }
            >
              <Route index element={<AppuntamentiPaziente />} />
              <Route path="prenota" element={<Prenota />} />
              <Route path="cartella" element={<Cartella />} />
              <Route path="pagamenti" element={<PagamentiPaziente />} />
            </Route>
          </Route>

          <Route element={<RottaProtetta ruoloRichiesto="Fisioterapista" />}>
            <Route path="/fisioterapista" element={<LayoutRuolo voci={MENU_FISIOTERAPISTA} titoloRuolo="Fisioterapista" />}>
              <Route index element={<AgendaFisioterapista />} />
              <Route path="pazienti" element={<PazientiFisioterapista />} />
              <Route path="bio" element={<Bio />} />
              <Route path="assenze" element={<Assenze />} />
            </Route>
          </Route>

          <Route element={<RottaProtetta ruoloRichiesto="Coordinatrice" />}>
            <Route path="/coordinatrice" element={<LayoutRuolo voci={MENU_COORDINATRICE} titoloRuolo="Coordinatrice" />}>
              <Route index element={<RichiesteCoordinatrice />} />
              <Route path="fisioterapisti" element={<FisioterapistiCoordinatrice />} />
              <Route path="cassa" element={<Cassa />} />
              <Route path="ricette" element={<RicetteSsn />} />
              <Route path="nuovo" element={<NuovoAppuntamento />} />
              <Route path="calendario" element={<CalendarioCoordinatrice />} />
              <Route path="pazienti" element={<PazientiCoordinatrice />} />
              <Route path="attesa" element={<ListaAttesa />} />
            </Route>
          </Route>

          <Route element={<RottaProtetta ruoloRichiesto="Admin" />}>
            <Route path="/admin" element={<LayoutRuolo voci={MENU_ADMIN} titoloRuolo="Admin" />}>
              <Route index element={<Andamento />} />
              <Route path="personale" element={<Personale />} />
              {/* Redirect per un eventuale link vecchio già in giro (es. salvato nei
                  preferiti): la voce di menu "Andamento" ora è l'indice stesso. */}
              <Route path="andamento" element={<Navigate to="/admin" replace />} />
              <Route path="configurazione" element={<ConfigurazioneAdmin />} />
            </Route>
          </Route>

          <Route path="*" element={<Ingresso />} />
        </Routes>
      </BrowserRouter>
    </AuthProvider>
  )
}
