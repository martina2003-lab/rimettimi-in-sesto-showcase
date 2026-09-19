import { useEffect, useState } from 'react'
import { api } from '../../api/client'
import type { Consenso, PazienteRicerca, Ricetta, SchedaPaziente } from '../../api/types'

const TAB = ['Anagrafica', 'Percorso attivo', 'Storico', 'Documenti', 'Note operative'] as const
type Tab = (typeof TAB)[number]

const FORMATO_DATA = new Intl.DateTimeFormat('it-IT', { day: 'numeric', month: 'long', year: 'numeric' })
const FORMATO_ORA = new Intl.DateTimeFormat('it-IT', { hour: '2-digit', minute: '2-digit' })
const EURO = new Intl.NumberFormat('it-IT', { style: 'currency', currency: 'EUR' })

function dataIta(iso: string | null) {
  if (!iso) return '—'
  return FORMATO_DATA.format(new Date(iso.length === 10 ? `${iso}T00:00:00` : iso))
}

/** La durata di una seduta SSN non si sceglie: 30 minuti per distretto prescritto. */
function durataDaRicetta(distretti: string | null) {
  const numero = distretti?.split(',').filter((d) => d.trim()).length ?? 0
  if (numero === 0) return '—'
  const minuti = numero * 30
  const testo = minuti < 60 ? '30 min' : `${Math.floor(minuti / 60)} h${minuti % 60 ? ' 30 min' : ''}`
  return `${testo} (${numero} distretto${numero > 1 ? 'i' : ''})`
}

function statoRicetta(stato: Ricetta['stato']) {
  if (stato === 'Validata') return { classe: 'stato-confermato', testo: 'Validata' }
  if (stato === 'Respinta') return { classe: 'stato-annullato', testo: 'Respinta' }
  if (stato === 'IntegrazioneRichiesta') return { classe: 'stato-richiesto', testo: 'Integrazione richiesta' }
  return { classe: 'stato-richiesto', testo: 'Non ancora validata' }
}

function Riga({ etichetta, children }: { etichetta: string; children: React.ReactNode }) {
  return (
    <div
      className="flex flex-wrap items-baseline justify-between gap-2 py-2"
      style={{ borderBottom: '1px solid var(--border)' }}
    >
      <span className="eyebrow">{etichetta}</span>
      <span style={{ textAlign: 'right' }}>{children}</span>
    </div>
  )
}

/**
 * La scheda del paziente come la vede la segreteria.
 *
 * Vale la pena dire cosa NON c'è: nessun dato clinico. Anamnesi, valutazioni, note di
 * seduta e diagnosi restano al solo fisioterapista (principio guida non negoziabile), e
 * lo storico qui dice quando e con chi, mai cosa è stato fatto. Le note operative sono
 * l'alternativa dichiarata: "spesso in ritardo" è una nota di segreteria, non una nota
 * clinica, ed è per questo che esistono come entità separata.
 */
export default function PazientiCoordinatrice() {
  const [ricerca, setRicerca] = useState('')
  const [risultati, setRisultati] = useState<PazienteRicerca[]>([])
  const [scheda, setScheda] = useState<SchedaPaziente | null>(null)
  const [tab, setTab] = useState<Tab>('Anagrafica')
  const [nuovaNota, setNuovaNota] = useState('')
  const [consensi, setConsensi] = useState<Consenso[]>([])
  const [firmatario, setFirmatario] = useState('')
  const [inCorso, setInCorso] = useState(false)
  const [errore, setErrore] = useState<string | null>(null)

  useEffect(() => {
    if (scheda) return
    let annullato = false
    const attesa = setTimeout(() => {
      api
        .get<PazienteRicerca[]>(`/api/pazienti/cerca?q=${encodeURIComponent(ricerca)}`)
        .then((elenco) => {
          if (!annullato) setRisultati(elenco)
        })
        .catch((e) => {
          if (!annullato) setErrore(e instanceof Error ? e.message : 'Ricerca non riuscita.')
        })
    }, 250)
    return () => {
      annullato = true
      clearTimeout(attesa)
    }
  }, [ricerca, scheda])

  async function apri(pazienteId: number) {
    setErrore(null)
    try {
      const [dettaglio, elenco] = await Promise.all([
        api.get<SchedaPaziente>(`/api/pazienti/${pazienteId}/scheda`),
        api.get<Consenso[]>(`/api/consensi?pazienteId=${pazienteId}`),
      ])
      setScheda(dettaglio)
      setConsensi(elenco)
      setFirmatario('')
      setTab('Anagrafica')
    } catch (e) {
      setErrore(e instanceof Error ? e.message : 'Scheda non caricata.')
    }
  }

  // È questo che fa smettere una scheda di essere provvisoria: il consenso raccolto di
  // persona alla prima seduta, prima che il fisioterapista scriva qualsiasi dato clinico.
  async function registraConsensi() {
    if (!scheda) return
    setInCorso(true)
    setErrore(null)
    try {
      await api.post('/api/consensi/raccolti-in-studio', {
        pazienteId: scheda.id,
        firmatario: firmatario.trim() || null,
      })
      await apri(scheda.id)
    } catch (e) {
      setErrore(e instanceof Error ? e.message : 'Registrazione non riuscita.')
    } finally {
      setInCorso(false)
    }
  }

  async function aggiungiNota() {
    if (!scheda || !nuovaNota.trim()) return
    setInCorso(true)
    setErrore(null)
    try {
      await api.post(`/api/pazienti/${scheda.id}/note`, { testo: nuovaNota.trim() })
      setNuovaNota('')
      setScheda(await api.get<SchedaPaziente>(`/api/pazienti/${scheda.id}/scheda`))
    } catch (e) {
      setErrore(e instanceof Error ? e.message : 'Nota non salvata.')
    } finally {
      setInCorso(false)
    }
  }

  return (
    <>
      <header className="mb-6">
        <h1 className="view-title">Pazienti</h1>
        <p className="view-sub">
          Anagrafica, percorso, storico e documenti. Il contenuto clinico delle sedute non
          compare qui: la cartella è visibile al solo fisioterapista.
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

      {!scheda ? (
        <>
          <div className="field">
            <label htmlFor="ricerca-paziente">Cerca</label>
            <input
              id="ricerca-paziente"
              value={ricerca}
              onChange={(e) => setRicerca(e.target.value)}
              placeholder="Nome, codice fiscale o telefono…"
            />
          </div>

          <div className="flex flex-col gap-2">
            {risultati.length === 0 && (
              <p style={{ color: 'var(--muted)' }}>Nessuna scheda corrisponde alla ricerca.</p>
            )}
            {risultati.map((paziente) => (
              <button
                key={paziente.id}
                className="card"
                style={{ cursor: 'pointer', textAlign: 'left' }}
                onClick={() => void apri(paziente.id)}
              >
                <div className="flex flex-wrap items-center justify-between gap-3">
                  <div>
                    <div style={{ fontWeight: 700 }}>
                      {paziente.nome} {paziente.cognome}
                    </div>
                    <div className="text-[0.86rem]" style={{ color: 'var(--muted)' }}>
                      {paziente.telefono ?? 'telefono non registrato'}
                      {paziente.codiceFiscale ? ` · ${paziente.codiceFiscale}` : ''}
                    </div>
                  </div>
                  <span
                    className={`stato ${paziente.stato === 'Provvisorio' ? 'stato-richiesto' : 'stato-confermato'}`}
                  >
                    {paziente.stato === 'Provvisorio' ? 'Scheda provvisoria' : 'Consenso raccolto'}
                  </span>
                </div>
              </button>
            ))}
          </div>
        </>
      ) : (
        <>
          <button className="btn btn-ghost btn-sm mb-4" onClick={() => setScheda(null)}>
            ← Torna all'elenco
          </button>

          <div className="card">
            <div className="flex flex-wrap items-start justify-between gap-3">
              <div>
                <h2 style={{ fontFamily: "'Petrona', serif", fontSize: '1.4rem' }}>
                  {scheda.nome} {scheda.cognome}
                </h2>
                <div className="text-[0.86rem]" style={{ color: 'var(--muted)' }}>
                  {scheda.telefono ?? 'telefono non registrato'}
                  {scheda.codiceFiscale ? ` · ${scheda.codiceFiscale}` : ''}
                </div>
              </div>
              <span
                className={`stato ${scheda.stato === 'Provvisorio' ? 'stato-richiesto' : 'stato-confermato'}`}
              >
                {scheda.stato === 'Provvisorio' ? 'Scheda provvisoria' : 'Consenso raccolto'}
              </span>
            </div>

            <div className="mt-4 mb-4 flex flex-wrap gap-2">
              {TAB.map((nome) => (
                <button
                  key={nome}
                  className={`btn btn-sm ${tab === nome ? 'btn-primary' : 'btn-ghost'}`}
                  onClick={() => setTab(nome)}
                >
                  {nome}
                </button>
              ))}
            </div>

            {tab === 'Anagrafica' && (
              <div>
                <Riga etichetta="Nome e cognome">
                  {scheda.nome} {scheda.cognome}
                </Riga>
                <Riga etichetta="Codice fiscale">
                  {scheda.codiceFiscale ?? (
                    <span className="stato stato-richiesto">Da raccogliere</span>
                  )}
                </Riga>
                <Riga etichetta="Telefono">{scheda.telefono ?? '—'}</Riga>
                <Riga etichetta="Email">{scheda.email ?? '—'}</Riga>
                <Riga etichetta="Data di nascita">{dataIta(scheda.dataNascita)}</Riga>
                <Riga etichetta="Stato scheda">
                  {scheda.stato === 'Provvisorio'
                    ? 'Provvisoria — consenso da raccogliere di persona'
                    : 'Consenso raccolto'}
                </Riga>

                <div className="mt-4">
                  <h3 className="eyebrow mb-2">Consensi</h3>
                  {consensi.length === 0 ? (
                    <p className="text-[0.86rem]" style={{ color: 'var(--muted)' }}>
                      Nessun consenso registrato.
                    </p>
                  ) : (
                    consensi.map((consenso) => (
                      <Riga key={consenso.id} etichetta={consenso.tipo}>
                        {consenso.stato === 'Prestato' ? (
                          <>
                            {FORMATO_DATA.format(new Date(consenso.data))} ·{' '}
                            {consenso.versioneInformativa} · {consenso.firmatario}
                          </>
                        ) : (
                          <span className="stato stato-annullato">
                            Revocato il {FORMATO_DATA.format(new Date(consenso.data))}
                          </span>
                        )}
                      </Riga>
                    ))
                  )}

                  {scheda.stato === 'Provvisorio' && (
                    <div className="mt-3">
                      <label htmlFor="firmatario">
                        Chi ha firmato (vuoto: il paziente stesso)
                      </label>
                      <input
                        id="firmatario"
                        value={firmatario}
                        onChange={(e) => setFirmatario(e.target.value)}
                        placeholder="Es. Marco Bianchi (genitore)"
                      />
                      <p className="mt-2 text-[0.8rem]" style={{ color: 'var(--muted)' }}>
                        Registra i moduli firmati in studio. Da quel momento la scheda non è
                        più provvisoria e il fisioterapista può scrivere in cartella.
                      </p>
                      <button
                        className="btn btn-primary btn-sm mt-2"
                        disabled={inCorso}
                        onClick={() => void registraConsensi()}
                      >
                        {inCorso ? 'Registrazione…' : 'Registra i consensi raccolti in studio'}
                      </button>
                    </div>
                  )}
                </div>
              </div>
            )}

            {tab === 'Percorso attivo' && (
              <div className="flex flex-col gap-4">
                {scheda.pacchettiPrivati.length === 0 && scheda.ricette.length === 0 && (
                  <p style={{ color: 'var(--muted)' }}>
                    Nessun percorso attivo — scheda in attesa del primo appuntamento.
                  </p>
                )}

                {/* Privato e SSN restano due cose distinte, mai una lista unica di
                    "cose da cui scalare una seduta" (principio guida). */}
                {scheda.pacchettiPrivati.map((pacchetto) => (
                  <div key={pacchetto.id}>
                    <h3 className="eyebrow mb-2">Privato</h3>
                    <Riga etichetta="Sedute residue">
                      {pacchetto.seduteResidue} / {pacchetto.seduteTotali}
                    </Riga>
                    <Riga etichetta="Scadenza pacchetto">{dataIta(pacchetto.scadenza)}</Riga>
                    {pacchetto.prezzo !== null && (
                      <Riga etichetta="Prezzo">{EURO.format(pacchetto.prezzo)}</Riga>
                    )}
                  </div>
                ))}

                {scheda.ricette.map((ricetta) => {
                  const stato = statoRicetta(ricetta.stato)
                  return (
                    <div key={ricetta.id}>
                      <h3 className="eyebrow mb-2">Convenzionato SSN</h3>
                      <Riga etichetta="Ricetta">
                        <span className={`stato ${stato.classe}`}>{stato.testo}</span>
                      </Riga>
                      <Riga etichetta="NRE">
                        <span className="mono">{ricetta.numeroONre ?? '—'}</span>
                      </Riga>
                      <Riga etichetta="Distretti prescritti">
                        {ricetta.distrettiCorporei ?? '—'}
                      </Riga>
                      <Riga etichetta="Durata seduta">
                        {durataDaRicetta(ricetta.distrettiCorporei)}
                      </Riga>
                      <Riga etichetta="Sedute prescritte">{ricetta.numeroSeduteProscritte}</Riga>
                      <Riga etichetta="Ticket">
                        {ricetta.esenzione
                          ? `Esente ${ricetta.codiceEsenzione ?? ''}`.trim()
                          : ricetta.importoTicket !== null
                            ? EURO.format(ricetta.importoTicket)
                            : '—'}
                      </Riga>
                      <Riga etichetta="Finestra di completamento">
                        {dataIta(ricetta.finestraCompletamento)}
                      </Riga>
                      {ricetta.appuntamentiBloccati > 0 && ricetta.stato !== 'Validata' && (
                        <p
                          className="mt-3 rounded-lg px-3 py-2 text-[0.86rem]"
                          style={{ background: 'var(--warm-soft)' }}
                        >
                          Sta tenendo fermi {ricetta.appuntamentiBloccati} appuntamenti: si
                          valida dalla coda Ricette SSN.
                        </p>
                      )}
                    </div>
                  )
                })}
              </div>
            )}

            {tab === 'Storico' && (
              <div>
                {scheda.storico.length === 0 && (
                  <p style={{ color: 'var(--muted)' }}>Nessun appuntamento in archivio.</p>
                )}
                {scheda.storico.map((voce, indice) => (
                  <Riga
                    key={indice}
                    etichetta={`${dataIta(voce.dataOra)} · ${FORMATO_ORA.format(new Date(voce.dataOra))}`}
                  >
                    {voce.fisioterapistaNome} · {voce.durataMinuti} min ·{' '}
                    {voce.percorso === 'Ssn' ? 'SSN' : 'Privato'} · {voce.stato}
                  </Riga>
                ))}
                <p className="mt-3 text-[0.8rem]" style={{ color: 'var(--muted)' }}>
                  Quando e con chi, non cosa: il resoconto delle sedute sta nella cartella
                  clinica, che vede il solo fisioterapista.
                </p>
              </div>
            )}

            {tab === 'Documenti' && (
              <div className="flex flex-col gap-3">
                {scheda.ricette.length === 0 && scheda.pagamenti.length === 0 && (
                  <p style={{ color: 'var(--muted)' }}>Nessun documento per questo paziente.</p>
                )}
                {scheda.ricette.map((ricetta) => {
                  const stato = statoRicetta(ricetta.stato)
                  return (
                    <div
                      key={ricetta.id}
                      className="flex flex-wrap items-center justify-between gap-3 rounded-lg px-3 py-2"
                      style={{ background: 'var(--surface-2)' }}
                    >
                      <div>
                        <strong>Ricetta / impegnativa</strong>
                        <div className="text-[0.82rem]" style={{ color: 'var(--muted)' }}>
                          <span className="mono">{ricetta.numeroONre ?? 'NRE non registrato'}</span>{' '}
                          · emessa il {dataIta(ricetta.dataEmissione)}
                          {ricetta.medicoPrescrittore ? ` · ${ricetta.medicoPrescrittore}` : ''}
                        </div>
                      </div>
                      <span className={`stato ${stato.classe}`}>{stato.testo}</span>
                    </div>
                  )
                })}
                {scheda.pagamenti
                  .filter((pagamento) => pagamento.ricevutaNumero !== null)
                  .map((pagamento) => (
                    <div
                      key={pagamento.id}
                      className="flex flex-wrap items-center justify-between gap-3 rounded-lg px-3 py-2"
                      style={{ background: 'var(--surface-2)' }}
                    >
                      <div>
                        <strong>
                          Ricevuta n. {pagamento.ricevutaNumero}/{pagamento.ricevutaAnno}
                        </strong>
                        <div className="text-[0.82rem]" style={{ color: 'var(--muted)' }}>
                          {dataIta(pagamento.dataIncasso)} · {pagamento.origine}
                        </div>
                      </div>
                      <span className={pagamento.stato === 'Stornato' ? 'stato stato-annullato' : ''}>
                        {pagamento.stato === 'Stornato' ? 'Stornata' : EURO.format(pagamento.importo)}
                      </span>
                    </div>
                  ))}
              </div>
            )}

            {tab === 'Note operative' && (
              <div>
                {scheda.note.length === 0 && (
                  <p style={{ color: 'var(--muted)' }}>Nessuna nota operativa.</p>
                )}
                {scheda.note.map((nota) => (
                  <div
                    key={nota.id}
                    className="mb-2 rounded-lg px-3 py-2"
                    style={{ background: 'var(--surface-2)' }}
                  >
                    {nota.testo}
                    <div className="text-[0.78rem]" style={{ color: 'var(--muted)' }}>
                      {nota.autore} · {dataIta(nota.data)}
                    </div>
                  </div>
                ))}

                <div className="field mt-4">
                  <label htmlFor="nuova-nota">Aggiungi nota operativa (non clinica)</label>
                  <textarea
                    id="nuova-nota"
                    rows={2}
                    value={nuovaNota}
                    onChange={(e) => setNuovaNota(e.target.value)}
                    placeholder="Es. spesso in ritardo, preferisce il mattino…"
                  />
                </div>
                <button
                  className="btn btn-ghost btn-sm"
                  disabled={inCorso || !nuovaNota.trim()}
                  onClick={() => void aggiungiNota()}
                >
                  {inCorso ? 'Salvataggio…' : 'Aggiungi nota'}
                </button>
              </div>
            )}
          </div>
        </>
      )}
    </>
  )
}
