import { useEffect, useState } from 'react'
import { api } from '../../api/client'
import type { Andamento as AndamentoDto, BarraAndamento, PeriodoAndamento } from '../../api/types'

const EURO = new Intl.NumberFormat('it-IT', { style: 'currency', currency: 'EUR' })
// Solo per l'etichetta di variazione sopra ogni barra: con più barre consecutive che
// mostrano una differenza, i centesimi allargano il testo quanto basta a farlo toccare
// quello della barra vicina.
const EURO_TONDO = new Intl.NumberFormat('it-IT', { style: 'currency', currency: 'EUR', maximumFractionDigits: 0 })

const PERIODI: { valore: PeriodoAndamento; etichetta: string }[] = [
  { valore: 'settimana', etichetta: 'Settimana' },
  { valore: 'mese', etichetta: 'Mese' },
  { valore: 'anno', etichetta: 'Anno' },
]

function isoData(d: Date) {
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
}

// Righe della tabella di dettaglio. Non una per barra: una fila di mesi chiusi e davvero a
// zero (es. Gen-Giu prima che lo studio avesse incassi) non è dodici informazioni diverse,
// è la stessa informazione ripetuta — accorpata in una riga sola invece di farla scorrere
// dodici volte identica. Idem per una fila di mesi futuri con la stessa spiegazione (es. due
// mesi entrambi oltre la finestra di prenotazione).
type RigaTabella =
  | { tipo: 'zero'; chiave: string; daEtichetta: string; aEtichetta: string }
  | { tipo: 'vuoto-futuro'; chiave: string; daEtichetta: string; aEtichetta: string; ragione: string }
  | { tipo: 'prenotato'; barra: BarraAndamento }
  | { tipo: 'reale'; barra: BarraAndamento; delta: number | null; eCorrente: boolean }

function raggruppaRigheTabella(barre: BarraAndamento[], oggiIso: string, finestraGiorni: number): RigaTabella[] {
  const sogliaPrenotabile = isoData(new Date(new Date().setDate(new Date().getDate() + finestraGiorni)))
  const righe: RigaTabella[] = []

  barre.forEach((barra, indice) => {
    const precedente = indice > 0 ? barre[indice - 1].importo : null
    const nelFuturo = barra.da > oggiIso
    const eCorrente = barra.da <= oggiIso && oggiIso <= barra.a
    const haPrenotato = nelFuturo && barra.importoPrenotato > 0

    if (nelFuturo && !haPrenotato) {
      const ragione =
        barra.a < sogliaPrenotabile
          ? 'ancora nessuno ha prenotato in questo periodo'
          : `oltre i ${finestraGiorni} giorni della finestra massima di prenotazione`
      const ultima = righe[righe.length - 1]
      if (ultima?.tipo === 'vuoto-futuro' && ultima.ragione === ragione) {
        ultima.aEtichetta = barra.etichetta
      } else {
        righe.push({ tipo: 'vuoto-futuro', chiave: barra.da, daEtichetta: barra.etichetta, aEtichetta: barra.etichetta, ragione })
      }
      return
    }

    if (haPrenotato) {
      righe.push({ tipo: 'prenotato', barra })
      return
    }

    // Chiuso e senza nessuna attività: si accorpa solo se non è il periodo in corso, che
    // resta sempre la sua riga (è quella che cambia mentre la si guarda).
    if (!eCorrente && barra.importo === 0 && barra.seduteErogate === 0) {
      const ultima = righe[righe.length - 1]
      if (ultima?.tipo === 'zero') {
        ultima.aEtichetta = barra.etichetta
      } else {
        righe.push({ tipo: 'zero', chiave: barra.da, daEtichetta: barra.etichetta, aEtichetta: barra.etichetta })
      }
      return
    }

    const delta = !nelFuturo && precedente !== null ? barra.importo - precedente : null
    righe.push({ tipo: 'reale', barra, delta, eCorrente })
  })

  return righe
}

export default function Andamento() {
  const [periodo, setPeriodo] = useState<PeriodoAndamento>('anno')
  const [riferimento, setRiferimento] = useState(() => new Date())
  const [andamento, setAndamento] = useState<AndamentoDto | null>(null)
  const [errore, setErrore] = useState<string | null>(null)

  useEffect(() => {
    setAndamento(null)
    api
      .get<AndamentoDto>(`/api/admin/andamento?periodo=${periodo}&riferimento=${isoData(riferimento)}`)
      .then(setAndamento)
      .catch((e) => setErrore(e instanceof Error ? e.message : 'Dati non caricati.'))
  }, [periodo, riferimento])

  function cambiaPeriodo(nuovo: PeriodoAndamento) {
    setPeriodo(nuovo)
    // Passare da una grana all'altra con un riferimento rimasto indietro (es. l'anno scelto
    // nella vista precedente) confonderebbe più che aiutare: si riparte sempre da oggi.
    setRiferimento(new Date())
  }

  function sposta(direzione: 1 | -1) {
    setRiferimento((precedente) => {
      const d = new Date(precedente)
      if (periodo === 'settimana') d.setDate(d.getDate() + 7 * direzione)
      else if (periodo === 'mese') d.setMonth(d.getMonth() + direzione)
      else d.setFullYear(d.getFullYear() + direzione)
      return d
    })
  }

  // Il mix privato/SSN si legge nei due colori della barra (e, a grana anno, anche nella
  // griglia incrociata sotto): niente torta a parte per la stessa informazione.
  const privatoTotale = andamento?.perCategoria.filter((v) => v.percorso === 'Privato').reduce((s, v) => s + v.importo, 0) ?? 0
  const ssnTotale = andamento?.perCategoria.filter((v) => v.percorso === 'SSN').reduce((s, v) => s + v.importo, 0) ?? 0
  const totalePercorsi = privatoTotale + ssnTotale
  const privatoPct = totalePercorsi > 0 ? Math.round((privatoTotale / totalePercorsi) * 100) : 0
  const ssnPct = totalePercorsi > 0 ? 100 - privatoPct : 0

  // Griglia incrociata percorso × tipo prestazione — la sola cosa che l'ex schermata
  // "Entrate" offriva e che il resto di questa pagina non copre già. Ha senso solo
  // sull'intero anno: su una settimana o un mese l'incrocio sarebbe quasi sempre vuoto in
  // due celle su quattro, e la lettura "fiscale" che questa griglia serve è annuale per
  // natura (è il dato che si porta al commercialista).
  function importoCrociato(percorso: 'Privato' | 'SSN', tipo: 'Manuale' | 'Strumentale') {
    return andamento?.perCategoria.find((v) => v.percorso === percorso && v.tipoPrestazione === tipo)?.importo ?? 0
  }
  const privManuale = importoCrociato('Privato', 'Manuale')
  const privStrumentale = importoCrociato('Privato', 'Strumentale')
  const ssnManuale = importoCrociato('SSN', 'Manuale')
  const ssnStrumentale = importoCrociato('SSN', 'Strumentale')
  const totaleGriglia = privManuale + privStrumentale + ssnManuale + ssnStrumentale
  const pctGriglia = (n: number) => (totaleGriglia > 0 ? `${Math.round((n / totaleGriglia) * 100)}%` : '—')

  // Include il prenotato nella scala: senza, una barra futura col solo prenotato
  // sembrerebbe piena quanto un mese reale molto più alto, o non sembrerebbe nulla.
  const massimoBarra = Math.max(
    1,
    ...(andamento?.barre.map((b) => Math.max(b.importo, b.importoPrenotato)) ?? [1]),
  )
  const oggiIso = isoData(new Date())

  // Vs anno precedente: percentuale solo se c'era già qualcosa da cui partire, altrimenti
  // una crescita "infinita" da zero non è un dato, è rumore (stesso principio già applicato
  // alle barre future).
  const deltaAnnoPrecedente =
    andamento && andamento.totaleAnnoPrecedente > 0
      ? Math.round(((andamento.totale - andamento.totaleAnnoPrecedente) / andamento.totaleAnnoPrecedente) * 100)
      : null

  const valoreMedioSeduta = andamento && andamento.seduteErogate > 0 ? andamento.totale / andamento.seduteErogate : null

  const righeTabella = andamento
    ? raggruppaRigheTabella(andamento.barre, oggiIso, andamento.finestraPrenotazioneGiorni)
    : []

  return (
    <>
      <header className="mb-6">
        <h1 className="view-title">Andamento</h1>
        <p className="view-sub">Come si dividono le entrate, e quanto è cambiato rispetto al periodo prima.</p>
      </header>

      <div className="flex items-center justify-between gap-3 mb-5 flex-wrap">
        <div className="inline-flex rounded-full p-1" style={{ background: 'var(--surface-2)', border: '1px solid var(--border)' }}>
          {PERIODI.map((p) => (
            <button
              key={p.valore}
              type="button"
              onClick={() => cambiaPeriodo(p.valore)}
              className="rounded-full px-4 py-1.5 text-sm font-bold"
              style={
                periodo === p.valore
                  ? { background: 'var(--accent)', color: 'var(--accent-ink)' }
                  : { color: 'var(--muted)' }
              }
            >
              {p.etichetta}
            </button>
          ))}
        </div>

        <div className="flex items-center gap-3">
          <button type="button" onClick={() => sposta(-1)} className="btn btn-ghost btn-sm" aria-label="Periodo precedente">
            ←
          </button>
          <span className="text-sm font-bold" style={{ minWidth: '11ch', textAlign: 'center' }}>
            {andamento?.etichetta ?? '…'}
          </span>
          <button type="button" onClick={() => sposta(1)} className="btn btn-ghost btn-sm" aria-label="Periodo successivo">
            →
          </button>
        </div>
      </div>

      {errore && (
        <p className="mb-4 rounded-lg px-3 py-2 text-sm" style={{ background: 'var(--danger-soft)', color: 'var(--danger)' }} role="alert">
          {errore}
        </p>
      )}
      {!andamento && !errore && <p style={{ color: 'var(--muted)' }}>Caricamento…</p>}

      {andamento && (
        <>
          <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4 mb-4">
            <div className="card">
              <div className="eyebrow mb-1">Incassato · {andamento.etichetta}</div>
              <div style={{ fontFamily: 'Petrona, serif', fontSize: '1.5rem', fontWeight: 600 }}>
                {EURO.format(andamento.totale)}
              </div>
              <div className="text-[0.78rem] mt-1" style={{ fontWeight: 700, color: deltaAnnoPrecedente === null ? 'var(--muted)' : deltaAnnoPrecedente >= 0 ? 'var(--good)' : 'var(--danger)' }}>
                {deltaAnnoPrecedente === null
                  ? 'Nessun dato un anno prima'
                  : `${deltaAnnoPrecedente >= 0 ? '▲' : '▼'} ${Math.abs(deltaAnnoPrecedente)}% vs anno prima (${EURO.format(andamento.totaleAnnoPrecedente)})`}
              </div>
            </div>
            <div className="card">
              <div className="eyebrow mb-1">Da riscuotere</div>
              <div style={{ fontFamily: 'Petrona, serif', fontSize: '1.5rem', fontWeight: 600 }}>
                {EURO.format(andamento.daRiscuotere)}
              </div>
              <div className="text-[0.78rem] mt-1" style={{ color: 'var(--muted)', fontWeight: 600 }}>
                {andamento.numeroVociDaRiscuotere} {andamento.numeroVociDaRiscuotere === 1 ? 'voce in attesa' : 'voci in attesa'}
              </div>
            </div>
            <div className="card">
              <div className="eyebrow mb-1">Sedute erogate</div>
              <div style={{ fontFamily: 'Petrona, serif', fontSize: '1.5rem', fontWeight: 600 }}>
                {andamento.seduteErogate}
              </div>
              <div className="text-[0.78rem] mt-1" style={{ color: 'var(--muted)', fontWeight: 600 }}>
                nel periodo
              </div>
            </div>
            <div className="card">
              <div className="eyebrow mb-1">Valore medio seduta</div>
              <div style={{ fontFamily: 'Petrona, serif', fontSize: '1.5rem', fontWeight: 600 }}>
                {valoreMedioSeduta === null ? '—' : EURO.format(valoreMedioSeduta)}
              </div>
              <div className="text-[0.78rem] mt-1" style={{ color: 'var(--muted)', fontWeight: 600 }}>
                {valoreMedioSeduta === null ? 'nessuna seduta erogata' : `${EURO.format(andamento.totale)} ÷ ${andamento.seduteErogate} sedute`}
              </div>
            </div>
          </div>

          {periodo === 'anno' && (
            <section className="card mb-4">
              <h2 className="eyebrow mb-1">Le due dimensioni incrociate</h2>
              <p className="text-[0.82rem] mb-3" style={{ color: 'var(--muted)' }}>
                Non tre categorie separate: la stessa terapia strumentale nasce dalla ricetta
                se il percorso è convenzionato, dalla richiesta del paziente se è privato.
              </p>
              <div
                className="grid gap-px overflow-hidden rounded-xl"
                style={{ gridTemplateColumns: 'auto 1fr 1fr', background: 'var(--border)' }}
              >
                <div style={{ background: 'var(--surface-2)' }} />
                <div className="p-3 text-center text-sm font-bold" style={{ background: 'var(--surface-2)' }}>Privato</div>
                <div className="p-3 text-center text-sm font-bold" style={{ background: 'var(--surface-2)' }}>Convenzionato SSN</div>

                <div className="p-3 flex items-center text-sm font-bold" style={{ background: 'var(--surface-2)' }}>Terapia manuale</div>
                <div className="p-3 text-center" style={{ background: 'var(--surface)' }}>
                  <div className="mono font-bold text-[1.1rem]">{EURO.format(privManuale)}</div>
                  <div className="text-[0.74rem]" style={{ color: 'var(--muted)' }}>{pctGriglia(privManuale)} del totale</div>
                </div>
                <div className="p-3 text-center" style={{ background: 'var(--surface)' }}>
                  <div className="mono font-bold text-[1.1rem]">{EURO.format(ssnManuale)}</div>
                  <div className="text-[0.74rem]" style={{ color: 'var(--muted)' }}>{pctGriglia(ssnManuale)} del totale</div>
                </div>

                <div className="p-3 flex items-center text-sm font-bold" style={{ background: 'var(--surface-2)' }}>Terapia strumentale</div>
                <div className="p-3 text-center" style={{ background: 'var(--surface)' }}>
                  <div className="mono font-bold text-[1.1rem]">{EURO.format(privStrumentale)}</div>
                  <div className="text-[0.74rem]" style={{ color: 'var(--muted)' }}>{pctGriglia(privStrumentale)} del totale</div>
                </div>
                <div className="p-3 text-center" style={{ background: 'var(--surface)' }}>
                  <div className="mono font-bold text-[1.1rem]">{EURO.format(ssnStrumentale)}</div>
                  <div className="text-[0.74rem]" style={{ color: 'var(--muted)' }}>{pctGriglia(ssnStrumentale)} del totale</div>
                </div>

                <div className="p-3 flex items-center text-sm font-bold" style={{ background: 'var(--surface-2)' }}>Totale</div>
                <div className="p-3 text-center" style={{ background: 'var(--accent-soft)' }}>
                  <div className="mono font-bold text-[1.1rem]" style={{ color: 'var(--accent)' }}>{EURO.format(privManuale + privStrumentale)}</div>
                  <div className="text-[0.74rem]" style={{ color: 'var(--muted)' }}>{pctGriglia(privManuale + privStrumentale)}</div>
                </div>
                <div className="p-3 text-center" style={{ background: 'var(--accent-soft)' }}>
                  <div className="mono font-bold text-[1.1rem]" style={{ color: 'var(--accent)' }}>{EURO.format(ssnManuale + ssnStrumentale)}</div>
                  <div className="text-[0.74rem]" style={{ color: 'var(--muted)' }}>{pctGriglia(ssnManuale + ssnStrumentale)}</div>
                </div>
              </div>
            </section>
          )}

          <section className="card">
            <div className="flex items-center justify-between gap-3 flex-wrap mb-3">
              <h2 className="eyebrow" style={{ margin: 0 }}>
                {periodo === 'anno' ? 'Entrate mese per mese' : periodo === 'mese' ? 'Entrate settimana per settimana' : 'Entrate giorno per giorno'}
              </h2>
              <div className="flex gap-3 text-[0.78rem]" style={{ color: 'var(--muted)' }}>
                <span>
                  <span className="inline-block h-[0.6rem] w-[0.6rem] rounded-sm mr-1" style={{ background: 'var(--accent)' }} />
                  Privato · {privatoPct}%
                </span>
                <span>
                  <span className="inline-block h-[0.6rem] w-[0.6rem] rounded-sm mr-1" style={{ background: 'var(--warm)' }} />
                  SSN · {ssnPct}%
                </span>
                <span>
                  <span
                    className="inline-block h-[0.6rem] w-[0.6rem] rounded-sm mr-1"
                    style={{ background: 'repeating-linear-gradient(45deg, var(--muted) 0, var(--muted) 2px, transparent 2px, transparent 4px)' }}
                  />
                  Già prenotato
                </span>
              </div>
            </div>

            <div className="flex items-end gap-2" style={{ height: '12rem', padding: '1.6rem .2rem .3rem' }}>
              {andamento.barre.map((barra, indice) => {
                const precedente = indice > 0 ? andamento.barre[indice - 1].importo : null

                // Una barra non ancora arrivata non ha una variazione da mostrare: senza
                // questo controllo un mese/settimana/giorno futuro mostrerebbe un calo mai
                // avvenuto.
                const nelFuturo = barra.da > oggiIso
                const eBarraCorrente = barra.da <= oggiIso && oggiIso <= barra.a

                const delta = !nelFuturo && precedente !== null ? barra.importo - precedente : null
                const haDati = barra.importo > 0
                const haPrenotato = nelFuturo && barra.importoPrenotato > 0
                const valoreBarra = haPrenotato ? barra.importoPrenotato : barra.importo
                const altezza = haDati || haPrenotato ? Math.max(6, Math.round((valoreBarra / massimoBarra) * 100)) : 6
                // Segmenti proporzionati dentro la barra stessa: percentuale di SSN
                // sull'altezza totale, così il colore mostra il mix senza un grafico a parte.
                const pctSsnBarra = haDati && barra.importo > 0 ? Math.round((barra.importoSsn / barra.importo) * 100) : 0

                return (
                  <div key={barra.da} className="flex-1 flex flex-col items-center justify-end h-full">
                    <div className="w-full flex items-end justify-center relative" style={{ maxWidth: '2.8rem', height: '100%' }}>
                      <div
                        className="w-full relative"
                        style={{
                          height: `${altezza}%`,
                          opacity: haDati ? 1 : haPrenotato ? 0.55 : 0.35,
                        }}
                      >
                        {/* L'overflow-hidden sta solo qui, sui segmenti colorati (serve per
                            gli angoli arrotondati): messo sul contenitore esterno taglierebbe
                            anche le etichette assolute qui sotto, che devono sporgere sopra
                            la barra. */}
                        <div
                          className="w-full h-full rounded-t flex flex-col-reverse overflow-hidden"
                          style={{ background: haDati ? undefined : haPrenotato ? undefined : 'var(--muted)' }}
                        >
                          {haDati && (
                            <>
                              <div style={{ height: `${100 - pctSsnBarra}%`, background: 'var(--accent)', flexShrink: 0 }} />
                              {pctSsnBarra > 0 && <div style={{ height: `${pctSsnBarra}%`, background: 'var(--warm)', flexShrink: 0 }} />}
                            </>
                          )}
                          {!haDati && haPrenotato && (
                            <div
                              style={{
                                height: '100%',
                                background: 'repeating-linear-gradient(45deg, var(--muted) 0, var(--muted) 3px, transparent 3px, transparent 6px)',
                              }}
                            />
                          )}
                        </div>

                        {/* Valore assoluto sempre visibile: prima si vedeva solo la
                            variazione, e una barra senza variazione (prima della serie, o
                            invariata) restava senza nessuna cifra sopra. */}
                        {(haDati || haPrenotato) && (
                          <span
                            className="mono"
                            style={{
                              position: 'absolute',
                              top: '-2.55rem',
                              left: '50%',
                              transform: 'translateX(-50%)',
                              fontSize: '.72rem',
                              fontWeight: 700,
                              whiteSpace: 'nowrap',
                              color: haDati ? 'var(--ink)' : 'var(--muted)',
                              fontStyle: haDati ? 'normal' : 'italic',
                            }}
                          >
                            {haDati ? EURO_TONDO.format(barra.importo) : `${EURO_TONDO.format(barra.importoPrenotato)} prenotati`}
                          </span>
                        )}
                        {delta !== null && delta !== 0 && (
                          <span
                            className="mono"
                            style={{
                              position: 'absolute',
                              top: '-1.35rem',
                              left: '50%',
                              transform: 'translateX(-50%)',
                              fontSize: '.68rem',
                              fontWeight: 700,
                              whiteSpace: 'nowrap',
                              color: delta > 0 ? 'var(--good)' : 'var(--danger)',
                            }}
                          >
                            {delta > 0 ? '+' : ''}
                            {EURO_TONDO.format(delta)}
                          </span>
                        )}
                      </div>
                    </div>
                    <span
                      className="text-[0.76rem] mt-2"
                      style={{ color: eBarraCorrente ? 'var(--accent)' : 'var(--muted)', fontWeight: eBarraCorrente ? 800 : 600 }}
                    >
                      {barra.etichetta}
                    </span>
                  </div>
                )
              })}
            </div>
            <p className="mt-3 text-[0.8rem]" style={{ color: 'var(--muted)', borderTop: '1px solid var(--border)', paddingTop: '.7rem' }}>
              Le barre tratteggiate non sono vuote: mostrano il valore già impegnato in
              agenda (appuntamenti confermati), non una previsione — il ticket SSN non ci
              entra, perché è dovuto una volta per ciclo e non per seduta. Una barra futura
              resta a zero solo oltre i {andamento.finestraPrenotazioneGiorni} giorni della
              finestra massima di prenotazione: nessuno può ancora prenotarci nulla, non è un
              segnale di calo.
            </p>
          </section>

          <section className="mt-4">
            <h2 className="eyebrow mb-3">Il dettaglio, periodo per periodo</h2>
            {/* "table-wrap" non è una classe reale in questo progetto — la tabella restava
                testo nudo sull'avorio della pagina, mentre tutto il resto
                della vista sta dentro una .card bianca con ombra. Stessa classe usata da KPI
                e grafico qui sopra, non una nuova: altrimenti sarebbe un terzo linguaggio
                visivo sulla stessa pagina. Il padding di .card è azzerato perché la tabella
                porta già il proprio (celle), e overflow-hidden mantiene gli angoli
                arrotondati sullo scroll orizzontale interno. */}
            <div className="card p-0 overflow-hidden">
              <div className="overflow-x-auto">
              <table className="w-full text-[0.86rem] border-collapse">
                <thead>
                  <tr>
                    <th className="eyebrow p-2 text-left border-b border-[var(--border)]">Periodo</th>
                    <th className="eyebrow p-2 text-right border-b border-[var(--border)]">Privato</th>
                    <th className="eyebrow p-2 text-right border-b border-[var(--border)]">SSN</th>
                    <th className="eyebrow p-2 text-right border-b border-[var(--border)]">Totale</th>
                    <th className="eyebrow p-2 text-right border-b border-[var(--border)] whitespace-nowrap">Δ vs prec.</th>
                    <th className="eyebrow p-2 text-right border-b border-[var(--border)]">Sedute</th>
                  </tr>
                </thead>
                <tbody>
                  {righeTabella.map((riga) => {
                    const periodoEtichetta =
                      riga.tipo === 'reale' || riga.tipo === 'prenotato'
                        ? riga.barra.etichetta
                        : riga.daEtichetta === riga.aEtichetta
                          ? riga.daEtichetta
                          : `${riga.daEtichetta}–${riga.aEtichetta}`

                    if (riga.tipo === 'zero') {
                      return (
                        <tr key={riga.chiave} style={{ color: 'var(--muted)' }}>
                          <td className="p-2 border-b border-[var(--border)]">{periodoEtichetta}</td>
                          <td colSpan={5} className="p-2 border-b border-[var(--border)] italic">
                            Nessuna attività in questo periodo
                          </td>
                        </tr>
                      )
                    }

                    if (riga.tipo === 'vuoto-futuro') {
                      return (
                        <tr key={riga.chiave} style={{ color: 'var(--muted)' }}>
                          <td className="p-2 border-b border-[var(--border)]">{periodoEtichetta}</td>
                          <td colSpan={5} className="p-2 border-b border-[var(--border)] italic">
                            Nessuna prenotazione — {riga.ragione}
                          </td>
                        </tr>
                      )
                    }

                    if (riga.tipo === 'prenotato') {
                      return (
                        <tr key={riga.barra.da} style={{ color: 'var(--muted)' }}>
                          <td className="p-2 border-b border-[var(--border)]" style={{ color: 'var(--ink)' }}>
                            {periodoEtichetta}
                          </td>
                          <td colSpan={5} className="p-2 border-b border-[var(--border)] italic">
                            {EURO.format(riga.barra.importoPrenotato)} già prenotati in agenda
                          </td>
                        </tr>
                      )
                    }

                    const { barra, delta, eCorrente } = riga
                    return (
                      <tr key={barra.da} style={eCorrente ? { background: 'var(--accent-soft)' } : undefined}>
                        <td className="p-2 border-b border-[var(--border)]" style={{ fontWeight: eCorrente ? 700 : 400 }}>
                          {barra.etichetta}
                          {eCorrente && <span style={{ color: 'var(--muted)', fontWeight: 400 }}> · in corso</span>}
                        </td>
                        {/* Un importo a zero dentro una riga altrimenti reale (es. nessun
                            incasso SSN quel mese) resta un dato vero, non va accorpato come
                            i mesi del tutto vuoti — ma non deve competere visivamente con le
                            cifre che contano, quindi resta smorzato. */}
                        <td className="mono p-2 text-right border-b border-[var(--border)]" style={{ color: barra.importoPrivato === 0 ? 'var(--muted)' : undefined }}>
                          {EURO.format(barra.importoPrivato)}
                        </td>
                        <td className="mono p-2 text-right border-b border-[var(--border)]" style={{ color: barra.importoSsn === 0 ? 'var(--muted)' : undefined }}>
                          {EURO.format(barra.importoSsn)}
                        </td>
                        <td className="mono p-2 text-right border-b border-[var(--border)]" style={{ fontWeight: 700 }}>
                          {EURO.format(barra.importo)}
                        </td>
                        <td
                          className="mono p-2 text-right border-b border-[var(--border)]"
                          style={{ color: !delta ? 'var(--muted)' : delta > 0 ? 'var(--good)' : 'var(--danger)' }}
                        >
                          {/* "0 €" per un periodo piatto rispetto al precedente (entrambi a
                              zero) direbbe più di quel che è successo: nessuna variazione da
                              segnalare, stessa regola già applicata al grafico sopra. */}
                          {!delta ? '—' : `${delta > 0 ? '+' : ''}${EURO_TONDO.format(delta)}`}
                        </td>
                        <td className="mono p-2 text-right border-b border-[var(--border)]">{barra.seduteErogate}</td>
                      </tr>
                    )
                  })}
                </tbody>
              </table>
              </div>
            </div>
          </section>
        </>
      )}
    </>
  )
}
