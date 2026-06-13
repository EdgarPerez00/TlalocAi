interface ActuatorControlCardProps {
  title: string
  target: string
  isOn: boolean
  disabled?: boolean
  busy?: boolean
  onToggle: (target: string, nextState: boolean) => void
}

function ActuatorControlCard({
  title,
  target,
  isOn,
  disabled = false,
  busy = false,
  onToggle,
}: ActuatorControlCardProps) {
  return (
    <div className="card surface-card border-0 h-100">
      <div className="card-body">
        <div className="d-flex align-items-start justify-content-between mb-3">
          <div>
            <div className="metric-label mb-1">Actuador</div>
            <h3 className="h5 mb-0">{title}</h3>
          </div>
          <span className={`badge ${isOn ? 'text-bg-success' : 'text-bg-secondary'}`}>{isOn ? 'Activo' : 'Inactivo'}</span>
        </div>
        <p className="text-secondary small mb-4">Comando backend: {target}</p>
        <button
          type="button"
          className={`btn w-100 ${isOn ? 'btn-outline-danger' : 'btn-primary'}`}
          onClick={() => onToggle(target, !isOn)}
          disabled={disabled || busy}
        >
          {busy ? 'Enviando...' : isOn ? 'Apagar / Cerrar' : 'Encender / Abrir'}
        </button>
      </div>
    </div>
  )
}

export default ActuatorControlCard
