import { useState, type ReactNode } from 'react'
import { NavLink, Outlet, useNavigate } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'
import logo from '../assets/logo.svg'

export interface VoceMenu {
  etichetta: string
  percorso: string
  icona: string
}

// Sidebar fissa su desktop per tutti i ruoli. Sotto i 900px diventa una testata + barra in
// basso (18 settembre 2026, per scelta esplicita di estendere ai quattro ruoli quello che
// prima valeva solo per il Paziente nei mockup) — non più una sidebar ristretta a icone,
// che sotto i 900px nascondeva anche l'avviso "Demo" (nessuno l'aveva notato perché nessuno
// aveva ancora aperto la demo da uno schermo così stretto).
// `menuProfilo`: contenuto opzionale che rende l'avatar cliccabile e apre un piccolo menu
// a comparsa — oggi solo lo switcher di profilo del Paziente, ma il layout resta generico
// per non legare LayoutRuolo a un ruolo specifico.
//
// MAX_VOCI_BARRA: una barra in basso non scala come una sidebar — oltre un certo numero di
// voci gli elementi non si stringono, escono dal contenitore e diventano davvero
// irraggiungibili (verificato: con le 8 voci della Coordinatrice l'ultima, "Cassa", spariva
// del tutto oltre il bordo, non solo visivamente compressa). Sopra la soglia si mostrano le
// prime quattro e un quinto tab "Altro" con le rimanenti in un pannello — stesso pattern
// della tab "More" di iOS quando le sezioni sono troppe per stare in una riga.
const MAX_VOCI_BARRA = 5

export default function LayoutRuolo({
  voci,
  titoloRuolo,
  menuProfilo,
}: {
  voci: VoceMenu[]
  titoloRuolo: string
  menuProfilo?: ReactNode
}) {
  const { utente, logout } = useAuth()
  const navigate = useNavigate()
  const [menuProfiloAperto, setMenuProfiloAperto] = useState(false)
  const [altroAperto, setAltroAperto] = useState(false)

  const iniziali = utente ? `${utente.nome[0] ?? ''}${utente.cognome[0] ?? ''}`.toUpperCase() : ''

  // Solo per la barra in basso: la sidebar desktop mostra sempre tutte le `voci`, non serve
  // dividerle lì (vedi MAX_VOCI_BARRA sopra per il perché qui sì).
  const vociBarraVisibili = voci.length > MAX_VOCI_BARRA ? voci.slice(0, MAX_VOCI_BARRA - 1) : voci
  const vociBarraNascoste = voci.length > MAX_VOCI_BARRA ? voci.slice(MAX_VOCI_BARRA - 1) : []

  return (
    <div className="app-shell">
      {/* Testata + barra in basso: solo sotto i 900px (vedi index.css), stessa `voci` e
          stesso `menuProfilo` della sidebar desktop qui sotto — due presentazioni dello
          stesso menu, non due menu diversi da tenere allineati a mano. */}
      <header className="mobile-topbar">
        <img className="brand-logo" src={logo} alt="Rimettimi in sesto" style={{ height: '1.6rem' }} />
        {/* Deve restare leggibile su ogni schermata (vedi il commento sulla stessa scritta
            più sotto nella sidebar): prima spariva già sotto i 900px insieme al resto del
            testo "solo desktop", tablet compreso — nessuno l'aveva notato perché nessuno
            aveva ancora aperto la demo da uno schermo così stretto. */}
        <span
          className="rounded-full px-2 py-[0.15rem] text-[0.68rem] font-bold"
          style={{ background: 'var(--warm-soft)', color: 'var(--warm)' }}
        >
          Demo
        </span>
        <div className="flex-1" />
        <div style={{ position: 'relative' }}>
          <button
            type="button"
            className="avatar"
            style={{ border: 'none', cursor: 'pointer' }}
            onClick={() => setMenuProfiloAperto((v) => !v)}
            aria-label="Profilo e uscita"
          >
            {iniziali}
          </button>
          {menuProfiloAperto && (
            <>
              <div
                onClick={() => setMenuProfiloAperto(false)}
                style={{ position: 'fixed', inset: 0, zIndex: 25 }}
              />
              {/* Si apre verso il basso, non verso l'alto come nella sidebar: qui il
                  bottone sta in cima allo schermo, non in fondo. */}
              <div
                className="card"
                style={{ position: 'absolute', top: '100%', right: 0, marginTop: '0.5rem', minWidth: '220px', zIndex: 26, padding: '0.6rem' }}
              >
                <div className="px-2 py-1 mb-1 text-[0.82rem]" style={{ borderBottom: '1px solid var(--border)', paddingBottom: '0.5rem' }}>
                  <strong className="block text-[0.86rem]">{utente?.nome} {utente?.cognome}</strong>
                  <span style={{ color: 'var(--muted)' }}>{titoloRuolo}</span>
                </div>
                {menuProfilo}
                <button
                  type="button"
                  className="menu-popover-item"
                  onClick={() => {
                    logout()
                    navigate('/login', { replace: true })
                  }}
                >
                  <i className="ti ti-logout" />
                  <span>Esci</span>
                </button>
              </div>
            </>
          )}
        </div>
      </header>

      <nav className="bottom-nav">
        {vociBarraVisibili.map((voce) => (
          <NavLink
            key={voce.percorso}
            to={voce.percorso}
            end
            onClick={() => setAltroAperto(false)}
            className={({ isActive }) => `bottom-nav-item${isActive ? ' attivo' : ''}`}
          >
            <i className={`ti ${voce.icona}`} />
            <span>{voce.etichetta}</span>
          </NavLink>
        ))}

        {/* position:relative solo su questo contenitore, non sull'intero <nav>: lì uno
            stile inline avrebbe sovrascritto il position:fixed della barra stessa (era
            successo, e la barra era tornata un elemento in flusso che schiacciava il
            contenuto della pagina invece di restare ancorata in fondo). */}
        {vociBarraNascoste.length > 0 && (
          <div style={{ position: 'relative', flex: 1, display: 'flex' }}>
            <button
              type="button"
              className={`bottom-nav-item${altroAperto ? ' attivo' : ''}`}
              onClick={() => setAltroAperto((v) => !v)}
            >
              <i className="ti ti-dots" />
              <span>Altro</span>
            </button>

            {altroAperto && (
              <>
                <div
                  onClick={() => setAltroAperto(false)}
                  style={{ position: 'fixed', inset: 0, zIndex: 25 }}
                />
                <div
                  className="card"
                  style={{
                    position: 'absolute',
                    bottom: '100%',
                    right: '0.4rem',
                    marginBottom: '0.5rem',
                    minWidth: '200px',
                    zIndex: 26,
                    padding: '0.6rem',
                  }}
                >
                  {vociBarraNascoste.map((voce) => (
                    <NavLink
                      key={voce.percorso}
                      to={voce.percorso}
                      end
                      onClick={() => setAltroAperto(false)}
                      className={({ isActive }) => `menu-popover-item${isActive ? ' attivo' : ''}`}
                    >
                      <i className={`ti ${voce.icona}`} />
                      <span>{voce.etichetta}</span>
                    </NavLink>
                  ))}
                </div>
              </>
            )}
          </div>
        )}
      </nav>

      <aside className="sidebar">
        <div className="mb-3 flex items-center gap-2 px-1">
          <img className="brand-logo" src={logo} alt="Rimettimi in sesto" />
          <span
            className="solo-desktop"
            style={{ fontFamily: 'Montserrat, sans-serif', fontWeight: 700, fontSize: '0.86rem' }}
          >
            Rimettimi in sesto
          </span>
        </div>

        {/* Resta visibile su ogni schermata, non solo all'ingresso: chi arriva qui da un
            link diretto deve capire subito che è una dimostrazione con dati inventati. */}
        <div
          className="solo-desktop mb-6 rounded-lg px-2 py-1 text-[0.72rem]"
          style={{ background: 'var(--warm-soft)', color: 'var(--warm)' }}
        >
          Demo — dati inventati
        </div>

        <div className="eyebrow solo-desktop mb-2 px-2">{titoloRuolo}</div>

        <nav className="flex flex-col gap-1">
          {voci.map((voce) => (
            <NavLink
              key={voce.percorso}
              to={voce.percorso}
              end
              className={({ isActive }) => `nav-item${isActive ? ' attivo' : ''}`}
            >
              <i className={`ti ${voce.icona}`} />
              <span>{voce.etichetta}</span>
            </NavLink>
          ))}
        </nav>

        <div className="flex-1" />

        <div className="pt-3" style={{ position: 'relative', borderTop: '1px solid var(--border)' }}>
          {menuProfilo ? (
            <button
              type="button"
              className="flex items-center gap-2 px-1 py-1"
              style={{ width: '100%', textAlign: 'left', background: 'none', border: 'none', cursor: 'pointer' }}
              onClick={() => setMenuProfiloAperto((v) => !v)}
            >
              <div className="avatar">{iniziali}</div>
              <div className="solo-desktop text-[0.82rem] leading-tight">
                <strong className="block text-[0.86rem]">
                  {utente?.nome} {utente?.cognome}
                </strong>
                <span style={{ color: 'var(--muted)' }}>{titoloRuolo}</span>
              </div>
            </button>
          ) : (
            <div className="flex items-center gap-2 px-1 py-1">
              <div className="avatar">{iniziali}</div>
              <div className="solo-desktop text-[0.82rem] leading-tight">
                <strong className="block text-[0.86rem]">
                  {utente?.nome} {utente?.cognome}
                </strong>
                <span style={{ color: 'var(--muted)' }}>{titoloRuolo}</span>
              </div>
            </div>
          )}

          {menuProfilo && menuProfiloAperto && (
            <>
              {/* Overlay invisibile per chiudere il menu cliccando fuori, senza librerie
                  esterne per un solo popover. */}
              <div
                onClick={() => setMenuProfiloAperto(false)}
                style={{ position: 'fixed', inset: 0, zIndex: 25 }}
              />
              <div
                className="card"
                style={{
                  position: 'absolute',
                  bottom: '100%',
                  left: 0,
                  marginBottom: '0.5rem',
                  minWidth: '220px',
                  zIndex: 26,
                  padding: '0.6rem',
                }}
              >
                {menuProfilo}
              </div>
            </>
          )}

          <button
            className="nav-item mt-1"
            onClick={() => {
              logout()
              navigate('/login', { replace: true })
            }}
          >
            <i className="ti ti-logout" />
            <span>Esci</span>
          </button>
        </div>
      </aside>

      <main className="main">
        <Outlet />
      </main>
    </div>
  )
}
