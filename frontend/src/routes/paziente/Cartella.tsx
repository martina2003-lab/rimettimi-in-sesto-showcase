import { useEffect, useState } from 'react'
import { api } from '../../api/client'
import type {
  CartellaCompleta,
  Consenso,
  RiepilogoPaziente,
  TipoConsenso,
} from '../../api/types'
import { usePazienteAttivo } from '../../paziente/PazienteAttivoContext'

const ETICHETTE: Record<TipoConsenso, { titolo: string; sotto: string }> = {
  DatiIdentificativi: {
    titolo: 'Dati personali identificativi',
    sotto: 'Nome, contatti, codice fiscale',
  },
  DatiSensibili: {
    titolo: 'Dati sensibili',
    sotto: 'Informazioni sulla tua salute',
  },
  TrattamentoSanitario: {
    titolo: 'Consenso informato al trattamento sanitario',
    sotto: 'Rischi, controindicazioni, adesione al piano di trattamento',
  },
}

const FORMATO_DATA = new Intl.DateTimeFormat('it-IT', { day: 'numeric', month: 'long', year: 'numeric' })

export default function Cartella() {
  const { pazienteId } = usePazienteAttivo()
  const [riepilogo, setRiepilogo] = useState<RiepilogoPaziente | null>(null)
  const [completa, setCompleta] = useState<CartellaCompleta | null>(null)
  const [errore, setErrore] = useState<string | null>(null)
  const [inCorso, setInCorso] = useState(false)
  const [consensi, setConsensi] = useState<Consenso[]>([])

  useEffect(() => {
    if (pazienteId === null) return
    setRiepilogo(null)
    setCompleta(null)
    api
      .get<RiepilogoPaziente>(`/api/pazienti/${pazienteId}/riepilogo`)
      .then(setRiepilogo)
      .catch((e) => setErrore(e instanceof Error ? e.message : 'Cartella non caricata.'))
    void caricaConsensi(pazienteId)
  }, [pazienteId])

  async function caricaConsensi(id: number) {
    try {
      setConsensi(await api.get<Consenso[]>(`/api/consensi?pazienteId=${id}`))
    } catch (e) {
      setErrore(e instanceof Error ? e.message : 'Consensi non caricati.')
    }
  }

  // "La revoca deve essere facile quanto la concessione" (obbligo GDPR): stesso gesto,
  // stesso posto, nessun modulo da scrivere.
  async function cambiaConsenso(consenso: Consenso) {
    if (pazienteId === null) return
    setInCorso(true)
    setErrore(null)
    try {
      if (consenso.stato === 'Prestato') {
        await api.put(`/api/consensi/${consenso.id}/revoca`)
      } else {
        await api.post('/api/consensi', { pazienteId, tipo: consenso.tipo })
      }
      await caricaConsensi(pazienteId)
    } catch (e) {
      setErrore(e instanceof Error ? e.message : 'Operazione non riuscita.')
    } finally {
      setInCorso(false)
    }
  }

  // Diritto di accesso GDPR art. 15: è un'azione esplicita, non la vista di default.
  // Di norma il paziente vede un riepilogo; le note tecniche interne del fisioterapista
  // arrivano solo se le chiede (requisiti.md).
  async function richiediCopiaCompleta() {
    if (pazienteId === null) return
    setInCorso(true)
    setErrore(null)
    try {
      setCompleta(await api.get<CartellaCompleta>(`/api/pazienti/${pazienteId}/cartella-completa`))
    } catch (e) {
      setErrore(e instanceof Error ? e.message : 'Richiesta non riuscita.')
    } finally {
      setInCorso(false)
    }
  }

  return (
    <>
      <header className="mb-6">
        <h1 className="view-title">Cartella clinica</h1>
        <p className="view-sub">
          Quello che vedi qui è il riepilogo del tuo percorso. La scheda clinica completa la
          tiene il fisioterapista, e puoi chiederne copia quando vuoi.
        </p>
      </header>

      {errore && (
        <p
          className="mb-4 rounded-lg px-3 py-2 text-sm"
          style={{ background: 'var(--danger-soft)', color: 'var(--danger)' }}
          role="alert"
        >
          {errore}
        </p>
      )}

      {!riepilogo && !errore && <p style={{ color: 'var(--muted)' }}>Caricamento…</p>}

      {riepilogo && (
        <>
          <section className="card mb-4">
            <h2 className="eyebrow mb-2">Diagnosi</h2>
            <p>{riepilogo.diagnosi ?? 'Nessun ciclo di trattamento aperto al momento.'}</p>

            {riepilogo.programmaRiabilitativo && (
              <>
                <h2 className="eyebrow mb-2 mt-4">Programma riabilitativo</h2>
                <p>{riepilogo.programmaRiabilitativo}</p>
              </>
            )}
          </section>

          <section className="mb-6">
            <h2 className="eyebrow mb-3">Riepilogo sedute</h2>
            {riepilogo.sedute.length === 0 ? (
              <div className="card" style={{ color: 'var(--muted)' }}>
                Nessuna seduta con un resoconto registrato.
              </div>
            ) : (
              <div className="flex flex-col gap-2">
                {riepilogo.sedute.map((seduta) => (
                  <div key={seduta.dataOra} className="card py-3">
                    <div className="text-[0.88rem]" style={{ fontWeight: 700 }}>
                      {FORMATO_DATA.format(new Date(seduta.dataOra))} · {seduta.fisioterapistaNome}
                    </div>
                    <p className="mt-1 text-[0.9rem]">{seduta.testo}</p>
                  </div>
                ))}
              </div>
            )}
          </section>

          <section className="card">
            <h2 className="eyebrow mb-2">Copia completa</h2>
            <p className="mb-3 text-[0.9rem]">
              Hai diritto a ricevere copia integrale della tua cartella, note del fisioterapista
              comprese (art. 15 GDPR).
            </p>
            <button className="btn btn-ghost" disabled={inCorso} onClick={() => void richiediCopiaCompleta()}>
              {inCorso ? 'Richiesta in corso…' : 'Richiedi copia completa'}
            </button>

            {completa && (
              <div className="mt-4 flex flex-col gap-3">
                {completa.cicli.length === 0 && (
                  <p style={{ color: 'var(--muted)' }}>Non risultano cicli di trattamento registrati.</p>
                )}
                {completa.cicli.map((ciclo) => (
                  <div key={ciclo.id} className="rounded-xl p-3" style={{ background: 'var(--surface-2)' }}>
                    <div className="eyebrow mb-2">
                      Ciclo
                      {ciclo.dataInizioTerapia
                        ? ` dal ${FORMATO_DATA.format(new Date(`${ciclo.dataInizioTerapia}T00:00:00`))}`
                        : ''}
                    </div>
                    <dl className="flex flex-col gap-2 text-[0.88rem]">
                      {[
                        ['Provenienza', ciclo.provenienza],
                        ['Anamnesi', ciclo.anamnesiPatologicaRemota],
                        ['Esame obiettivo', ciclo.esameObiettivo],
                        ['Esami specialistici', ciclo.esamiSpecialistici],
                        ['Diagnosi', ciclo.diagnosi],
                        ['Programma riabilitativo', ciclo.programmaRiabilitativo],
                        ['Indicazioni', ciclo.indicazioniPaziente],
                        ['Note per le sostituzioni', ciclo.noteSostituzione],
                        [
                          'Dolore riferito (VAS)',
                          ciclo.vasIniziale !== null
                            ? `${ciclo.vasIniziale}/10 all'inizio${ciclo.vasFinale !== null ? `, ${ciclo.vasFinale}/10 alla fine` : ''}`
                            : null,
                        ],
                      ]
                        .filter(([, valore]) => valore)
                        .map(([etichetta, valore]) => (
                          <div key={etichetta as string}>
                            <dt className="eyebrow">{etichetta}</dt>
                            <dd>{valore}</dd>
                          </div>
                        ))}
                    </dl>
                  </div>
                ))}
              </div>
            )}
          </section>
        </>
      )}

      <section className="mt-9">
        <h2 className="eyebrow mb-3">I tuoi consensi</h2>
        <div className="card flex flex-col gap-3">
          {consensi.length === 0 && (
            <p style={{ color: 'var(--muted)' }}>
              Nessun consenso registrato: si raccolgono in studio, alla prima seduta.
            </p>
          )}

          {consensi.map((consenso) => {
            const etichetta = ETICHETTE[consenso.tipo]
            const sanitario = consenso.tipo === 'TrattamentoSanitario'
            return (
              <div
                key={consenso.id}
                className="flex flex-wrap items-start justify-between gap-3 pb-3"
                style={{ borderBottom: '1px solid var(--border)' }}
              >
                <div>
                  <div style={{ fontWeight: 600 }}>{etichetta.titolo}</div>
                  <div className="text-[0.82rem]" style={{ color: 'var(--muted)' }}>
                    {etichetta.sotto}
                  </div>
                  <div className="mt-1 text-[0.82rem]" style={{ color: 'var(--muted)' }}>
                    {consenso.stato === 'Prestato' ? (
                      <>
                        {sanitario ? 'Raccolto in studio il ' : 'Prestato il '}
                        {FORMATO_DATA.format(new Date(consenso.data))}
                        {!sanitario ? ` · Informativa ${consenso.versioneInformativa}` : ''} ·{' '}
                        {consenso.firmatario}
                      </>
                    ) : (
                      <span style={{ color: 'var(--danger)', fontWeight: 700 }}>
                        Revocato il {FORMATO_DATA.format(new Date(consenso.data))}
                      </span>
                    )}
                  </div>
                </div>

                {/* Il consenso al trattamento si firma di persona e si revoca per
                    comunicazione alla direzione sanitaria: qui si legge e basta. */}
                {sanitario ? (
                  <span className="stato stato-confermato">Raccolto in studio</span>
                ) : (
                  <button
                    className={`btn btn-sm ${consenso.stato === 'Prestato' ? 'btn-ghost' : 'btn-primary'}`}
                    disabled={inCorso}
                    onClick={() => void cambiaConsenso(consenso)}
                  >
                    {consenso.stato === 'Prestato' ? 'Revoca' : 'Presta di nuovo'}
                  </button>
                )}
              </div>
            )
          })}

          <p className="text-[0.8rem]" style={{ color: 'var(--muted)' }}>
            La revoca dei consensi privacy è immediata quanto la concessione. Il consenso
            informato al trattamento si raccoglie — e si revoca — di persona in studio, per
            comunicazione alla direzione sanitaria: non con uno switch.
          </p>
        </div>
      </section>
    </>
  )
}
