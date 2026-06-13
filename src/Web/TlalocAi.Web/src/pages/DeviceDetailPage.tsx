import { useEffect, useState } from 'react'
import { useParams } from 'react-router-dom'
import { getApiErrorMessage } from '../api/apiClient'
import * as devicesApi from '../api/devicesApi'
import * as telemetryApi from '../api/telemetryApi'
import DeviceStatusCard from '../components/DeviceStatusCard'
import EmptyState from '../components/EmptyState'
import LevelSensorsGrid from '../components/LevelSensorsGrid'
import LoadingSpinner from '../components/LoadingSpinner'
import type { ActuatorDto, DeviceDto, SensorDto } from '../types/devices'
import type { MeasurementDto } from '../types/telemetry'
import { formatDateTime } from '../utils/dateFormat'
import { formatFlow, formatLiters } from '../utils/numberFormat'

function DeviceDetailPage() {
  const { deviceId = '' } = useParams()
  const [device, setDevice] = useState<DeviceDto | null>(null)
  const [sensors, setSensors] = useState<SensorDto[]>([])
  const [actuators, setActuators] = useState<ActuatorDto[]>([])
  const [latestMeasurement, setLatestMeasurement] = useState<MeasurementDto | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    async function loadDetail() {
      setIsLoading(true)
      setError(null)

      try {
        const [deviceResponse, sensorsResponse, actuatorsResponse] = await Promise.all([
          devicesApi.getDevice(deviceId),
          devicesApi.getSensors(deviceId),
          devicesApi.getActuators(deviceId),
        ])

        const latestResponse = await telemetryApi.getLatestTelemetry(deviceId).catch(() => null)

        setDevice(deviceResponse)
        setSensors(sensorsResponse)
        setActuators(actuatorsResponse)
        setLatestMeasurement(latestResponse)
      } catch (loadError) {
        setError(getApiErrorMessage(loadError, 'No se pudo cargar el detalle del dispositivo'))
      } finally {
        setIsLoading(false)
      }
    }

    if (deviceId) {
      void loadDetail()
    }
  }, [deviceId])

  if (isLoading) {
    return <LoadingSpinner message="Cargando detalle del dispositivo..." />
  }

  if (error) {
    return <div className="alert alert-danger">{error}</div>
  }

  if (!device) {
    return <EmptyState title="Dispositivo no encontrado" message="No fue posible encontrar este dispositivo." />
  }

  return (
    <section className="page-section d-grid gap-4">
      <div>
        <h1 className="h3 mb-1">{device.name}</h1>
        <p className="text-secondary mb-0">{device.description || 'Sin descripción adicional'}</p>
      </div>

      <div className="row g-4">
        <div className="col-xl-4">
          <DeviceStatusCard device={device} latestMeasurement={latestMeasurement} />
        </div>
        <div className="col-xl-8">
          <div className="row g-4">
            <div className="col-md-4">
              <div className="card surface-card border-0 h-100">
                <div className="card-body">
                  <div className="metric-label mb-2">Último caudal</div>
                  <div className="metric-value">{formatFlow(latestMeasurement?.flowLpm)}</div>
                </div>
              </div>
            </div>
            <div className="col-md-4">
              <div className="card surface-card border-0 h-100">
                <div className="card-body">
                  <div className="metric-label mb-2">Total litros</div>
                  <div className="metric-value">{formatLiters(latestMeasurement?.totalLiters)}</div>
                </div>
              </div>
            </div>
            <div className="col-md-4">
              <div className="card surface-card border-0 h-100">
                <div className="card-body">
                  <div className="metric-label mb-2">Última lectura</div>
                  <div className="fs-5 fw-semibold">{formatDateTime(latestMeasurement?.timestampUtc)}</div>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>

      <div className="row g-4">
        <div className="col-xl-6">
          <div className="card surface-card border-0 h-100">
            <div className="card-header">
              <h2 className="h6 mb-0">Sensores registrados</h2>
            </div>
            <div className="card-body">
              {sensors.length > 0 ? (
                <div className="table-responsive">
                  <table className="table align-middle mb-0">
                    <thead>
                      <tr>
                        <th>Nombre</th>
                        <th>Tipo</th>
                        <th>GPIO</th>
                        <th>Estado</th>
                      </tr>
                    </thead>
                    <tbody>
                      {sensors.map((sensor) => (
                        <tr key={sensor.id}>
                          <td>{sensor.name}</td>
                          <td>{sensor.type}</td>
                          <td>{sensor.gpioPin}</td>
                          <td>{sensor.isActive ? 'Activo' : 'Inactivo'}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              ) : (
                <p className="text-secondary mb-0">No hay sensores registrados.</p>
              )}
            </div>
          </div>
        </div>
        <div className="col-xl-6">
          <div className="card surface-card border-0 h-100">
            <div className="card-header">
              <h2 className="h6 mb-0">Actuadores registrados</h2>
            </div>
            <div className="card-body">
              {actuators.length > 0 ? (
                <div className="table-responsive">
                  <table className="table align-middle mb-0">
                    <thead>
                      <tr>
                        <th>Nombre</th>
                        <th>Tipo</th>
                        <th>GPIO</th>
                        <th>Último estado</th>
                      </tr>
                    </thead>
                    <tbody>
                      {actuators.map((actuator) => {
                        const latestState =
                          actuator.name === 'pump'
                            ? latestMeasurement?.pumpOn
                            : latestMeasurement?.actuators.find((item) => item.name === actuator.name)?.isOn

                        return (
                          <tr key={actuator.id}>
                            <td>{actuator.name}</td>
                            <td>{actuator.type}</td>
                            <td>{actuator.gpioPin}</td>
                            <td>{latestState ? 'Activo' : 'Inactivo'}</td>
                          </tr>
                        )
                      })}
                    </tbody>
                  </table>
                </div>
              ) : (
                <p className="text-secondary mb-0">No hay actuadores registrados.</p>
              )}
            </div>
          </div>
        </div>
      </div>

      <div className="card surface-card border-0">
        <div className="card-header">
          <h2 className="h6 mb-0">Estado de sensores de nivel</h2>
        </div>
        <div className="card-body">
          <LevelSensorsGrid levels={latestMeasurement?.levels ?? []} />
        </div>
      </div>
    </section>
  )
}

export default DeviceDetailPage
