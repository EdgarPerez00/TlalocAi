import { useEffect, useState } from 'react'
import { getApiErrorMessage } from '../api/apiClient'
import * as devicesApi from '../api/devicesApi'
import * as telemetryApi from '../api/telemetryApi'
import EmptyState from '../components/EmptyState'
import LoadingSpinner from '../components/LoadingSpinner'
import type { DeviceDto } from '../types/devices'
import type { MeasurementDto } from '../types/telemetry'
import { formatDateTime, toLocalDateTimeInputValue, toUtcIsoOrUndefined } from '../utils/dateFormat'
import { formatFlow, formatLiters } from '../utils/numberFormat'
import { formatActuatorName, formatSensorName } from '../utils/telemetryLabels'

const pageSize = 10

function TelemetryPage() {
  const defaultTo = toLocalDateTimeInputValue(new Date())
  const defaultFrom = toLocalDateTimeInputValue(new Date(Date.now() - 24 * 60 * 60 * 1000))

  const [devices, setDevices] = useState<DeviceDto[]>([])
  const [selectedDeviceId, setSelectedDeviceId] = useState('')
  const [fromUtc, setFromUtc] = useState(defaultFrom)
  const [toUtc, setToUtc] = useState(defaultTo)
  const [measurements, setMeasurements] = useState<MeasurementDto[]>([])
  const [error, setError] = useState<string | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [page, setPage] = useState(1)

  useEffect(() => {
    async function loadDevices() {
      setIsLoading(true)

      try {
        const devicesResponse = await devicesApi.getDevices()
        setDevices(devicesResponse)
        setSelectedDeviceId((current) => current || devicesResponse[0]?.id || '')
      } catch (loadError) {
        setError(getApiErrorMessage(loadError, 'No se pudieron cargar los dispositivos'))
      } finally {
        setIsLoading(false)
      }
    }

    void loadDevices()
  }, [])

  async function handleSearch(event?: React.FormEvent<HTMLFormElement>) {
    event?.preventDefault()

    if (!selectedDeviceId) {
      return
    }

    setIsLoading(true)
    setError(null)
    setPage(1)

    try {
      const response = await telemetryApi.getTelemetryHistory({
        deviceId: selectedDeviceId,
        fromUtc: toUtcIsoOrUndefined(fromUtc),
        toUtc: toUtcIsoOrUndefined(toUtc),
      })
      setMeasurements(response)
    } catch (searchError) {
      setMeasurements([])
      setError(getApiErrorMessage(searchError, 'No se pudo consultar la telemetría'))
    } finally {
      setIsLoading(false)
    }
  }

  useEffect(() => {
    if (selectedDeviceId) {
      void handleSearch()
    }
  }, [selectedDeviceId])

  const totalPages = Math.max(1, Math.ceil(measurements.length / pageSize))
  const currentPageItems = measurements.slice((page - 1) * pageSize, page * pageSize)

  if (isLoading && devices.length === 0) {
    return <LoadingSpinner message="Cargando telemetría..." />
  }

  if (devices.length === 0) {
    return <EmptyState title="No hay dispositivos registrados" message="No hay dispositivos disponibles para consultar telemetría." />
  }

  return (
    <section className="page-section d-grid gap-4">
      <div>
        <h1 className="h3 mb-1">Telemetría</h1>
        <p className="text-secondary mb-0">Consulta mediciones filtradas por dispositivo y rango de fechas.</p>
      </div>

      <div className="card surface-card border-0">
        <div className="card-header">
          <h2 className="h6 mb-0">Filtros</h2>
        </div>
        <div className="card-body">
          <form className="row g-3" onSubmit={handleSearch}>
            <div className="col-md-4">
              <label htmlFor="telemetry-device" className="form-label">
                Dispositivo
              </label>
              <select
                id="telemetry-device"
                className="form-select"
                value={selectedDeviceId}
                onChange={(event) => setSelectedDeviceId(event.target.value)}
              >
                {devices.map((device) => (
                  <option key={device.id} value={device.id}>
                    {device.name}
                  </option>
                ))}
              </select>
            </div>
            <div className="col-md-4">
              <label htmlFor="telemetry-from" className="form-label">
                Fecha inicio
              </label>
              <input
                id="telemetry-from"
                type="datetime-local"
                className="form-control"
                value={fromUtc}
                onChange={(event) => setFromUtc(event.target.value)}
              />
            </div>
            <div className="col-md-4">
              <label htmlFor="telemetry-to" className="form-label">
                Fecha fin
              </label>
              <input
                id="telemetry-to"
                type="datetime-local"
                className="form-control"
                value={toUtc}
                onChange={(event) => setToUtc(event.target.value)}
              />
            </div>
            <div className="col-12">
              <button type="submit" className="btn btn-primary" disabled={isLoading}>
                {isLoading ? 'Consultando...' : 'Consultar'}
              </button>
            </div>
          </form>
        </div>
      </div>

      {error ? <div className="alert alert-danger">{error}</div> : null}

      <div className="card surface-card border-0">
        <div className="card-header d-flex justify-content-between align-items-center">
          <h2 className="h6 mb-0">Mediciones</h2>
          <span className="text-secondary small">{measurements.length} registros</span>
        </div>
        <div className="card-body">
          {measurements.length > 0 ? (
            <>
              <div className="table-responsive">
                <table className="table align-middle mb-0">
                  <thead>
                    <tr>
                      <th>Timestamp</th>
                      <th>FlowLpm</th>
                      <th>TotalLiters</th>
                      <th>PumpOn</th>
                      <th>Niveles</th>
                      <th>Actuadores</th>
                    </tr>
                  </thead>
                  <tbody>
                    {currentPageItems.map((measurement) => (
                      <tr key={measurement.id}>
                        <td>{formatDateTime(measurement.timestampUtc)}</td>
                        <td>{formatFlow(measurement.flowLpm)}</td>
                        <td>{formatLiters(measurement.totalLiters)}</td>
                        <td>{measurement.pumpOn ? 'Sí' : 'No'}</td>
                        <td>
                          {measurement.levels
                            .map((level) => `${formatSensorName(level.name)}: ${level.isActive ? 'Activo' : 'Inactivo'}`)
                            .join(', ')}
                        </td>
                        <td>
                          {measurement.actuators
                            .map((actuator) => `${formatActuatorName(actuator.name)}: ${actuator.isOn ? 'Encendido' : 'Apagado'}`)
                            .join(', ')}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>

              <div className="d-flex justify-content-between align-items-center mt-3">
                <button
                  type="button"
                  className="btn btn-outline-secondary btn-sm"
                  onClick={() => setPage((current) => Math.max(1, current - 1))}
                  disabled={page === 1}
                >
                  Anterior
                </button>
                <span className="small text-secondary">
                  Página {page} de {totalPages}
                </span>
                <button
                  type="button"
                  className="btn btn-outline-secondary btn-sm"
                  onClick={() => setPage((current) => Math.min(totalPages, current + 1))}
                  disabled={page === totalPages}
                >
                  Siguiente
                </button>
              </div>
            </>
          ) : (
            <EmptyState title="No hay mediciones disponibles" message="Ajusta el rango de fechas o espera nuevos datos." />
          )}
        </div>
      </div>
    </section>
  )
}

export default TelemetryPage
