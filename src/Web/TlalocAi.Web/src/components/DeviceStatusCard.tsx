import type { DeviceDto } from '../types/devices'
import type { MeasurementDto } from '../types/telemetry'
import { formatDateTime } from '../utils/dateFormat'

interface DeviceStatusCardProps {
  device: DeviceDto
  latestMeasurement?: MeasurementDto | null
}

function DeviceStatusCard({ device, latestMeasurement }: DeviceStatusCardProps) {
  return (
    <div className="card surface-card border-0 h-100">
      <div className="card-header">
        <h2 className="h6 mb-0">Estado del dispositivo</h2>
      </div>
      <div className="card-body d-grid gap-3">
        <div className="d-flex justify-content-between align-items-center">
          <span className="text-secondary">Dispositivo</span>
          <strong>{device.name}</strong>
        </div>
        <div className="d-flex justify-content-between align-items-center">
          <span className="text-secondary">Estado</span>
          <span className={`badge ${device.isActive ? 'text-bg-success' : 'text-bg-secondary'}`}>
            {device.isActive ? 'Activo' : 'Inactivo'}
          </span>
        </div>
        <div className="d-flex justify-content-between align-items-center">
          <span className="text-secondary">Última conexión</span>
          <span>{formatDateTime(device.lastSeenAtUtc)}</span>
        </div>
        <div className="d-flex justify-content-between align-items-center">
          <span className="text-secondary">Última medición</span>
          <span>{formatDateTime(latestMeasurement?.timestampUtc)}</span>
        </div>
        <div className="d-flex justify-content-between align-items-center">
          <span className="text-secondary">Bomba</span>
          <span className={`badge ${latestMeasurement?.pumpOn ? 'text-bg-primary' : 'text-bg-light text-dark'}`}>
            {latestMeasurement?.pumpOn ? 'Encendida' : 'Apagada'}
          </span>
        </div>
      </div>
    </div>
  )
}

export default DeviceStatusCard
