import type { LevelMeasurementDto } from '../types/telemetry'

interface LevelSensorsGridProps {
  levels: LevelMeasurementDto[]
}

function LevelSensorsGrid({ levels }: LevelSensorsGridProps) {
  if (levels.length === 0) {
    return <p className="text-secondary mb-0">No hay sensores de nivel en la última lectura.</p>
  }

  return (
    <div className="row g-3">
      {levels.map((level) => (
        <div key={level.name} className="col-md-6 col-xl-4">
          <div className="card border-0 bg-light h-100">
            <div className="card-body">
              <div className="d-flex justify-content-between align-items-center">
                <span className="fw-semibold">{level.name}</span>
                <span className={`badge ${level.isActive ? 'text-bg-warning' : 'text-bg-success'}`}>
                  {level.isActive ? 'Detectado' : 'Libre'}
                </span>
              </div>
            </div>
          </div>
        </div>
      ))}
    </div>
  )
}

export default LevelSensorsGrid
