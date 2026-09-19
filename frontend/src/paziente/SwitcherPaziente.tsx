import { usePazienteAttivo } from './PazienteAttivoContext'

// Contenuto del popover che si apre cliccando l'avatar in sidebar (LayoutRuolo). Non è
// più un <select> sempre visibile: quello spariva del tutto con la sidebar collassata a
// icone (sotto i 900px, .solo-desktop nascosto), lasciando l'avatar cliccabile ma senza
// alcun effetto — esattamente il problema segnalato. Qui il trigger è l'avatar stesso, che
// funziona identico in entrambe le modalità.
export default function SwitcherPaziente({ onSelezionato }: { onSelezionato?: () => void }) {
  const { pazienti, pazienteId, setPazienteId } = usePazienteAttivo()
  if (pazienti.length <= 1) return null

  return (
    <div className="flex flex-col gap-1">
      <div className="eyebrow mb-1 px-1">Profilo</div>
      {pazienti.map((p) => (
        <button
          key={p.id}
          type="button"
          className={`menu-popover-item${p.id === pazienteId ? ' attivo' : ''}`}
          onClick={() => {
            setPazienteId(p.id)
            onSelezionato?.()
          }}
        >
          <span>
            {p.nome} {p.cognome}
            {p.titolo !== 'SeStesso' ? ' (minore)' : ''}
          </span>
        </button>
      ))}
    </div>
  )
}
