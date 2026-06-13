import { useEffect, useState } from 'react'
import { getApiErrorMessage } from '../api/apiClient'
import * as analyticsApi from '../api/analyticsApi'
import * as devicesApi from '../api/devicesApi'
import EmptyState from '../components/EmptyState'
import FlowChart from '../components/FlowChart'
import LoadingSpinner from '../components/LoadingSpinner'
import StatCard from '../components/StatCard'
import type {
  ActuatorSummaryDto,
  AnalyticsSummaryDto,
  FlowSeriesPointDto,
  LevelSummaryDto,
} from '../types/analytics'
import type { DeviceDto } from '../types/devices'
import { formatDateTime, toLocalDateTimeInputValue, toUtcIsoOrUndefined } from '../utils/dateFormat'
import { formatDurationSeconds, formatFlow, formatInteger, formatLiters } from '../utils/numberFormat'

function AnalyticsPage() {
  const defaultTo = toLocalDateTimeInputValue(new Date())
  const defaultFrom = toLocalDateTimeInputValue(new Date(Date.now() - 24 * 60 * 60 * 1000))

  const [devices, setDevices] = useState<DeviceDto[]>([])
  const [selectedDeviceId, setSelectedDeviceId] = useState('')
  const [fromUtc, setFromUtc] = useState(defaultFrom)
  const [toUtc, setToUtc] = useState(defaultTo)
  const [summary, setSummary] = useState<AnalyticsSummaryDto | null>(null)
  const [flowSeries, setFlowSeries] = useState<FlowSeriesPointDto[]>([])
  const [levelSummary, setLevelSummary] = useState<LevelSummaryDto[]>([])
  const [actuatorSummary, setActuatorSummary] = useState<ActuatorSummaryDto[]>([])
  const [error, setError] = useState<string | null>(null)
  const [isLoading, setIsLoading] = useState(true)

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

    const from = toUtcIsoOrUndefined(fromUtc)
    const to = toUtcIsoOrUndefined(toUtc)

    setIsLoading(true)
    setError(null)

    try {
      const [summaryResponse, flowResponse, levelResponse, actuatorResponse] = await Promise.all([
        analyticsApi.getAnalyticsSummary({ deviceId: selectedDeviceId, fromUtc: from, toUtc: to }),
        analyticsApi.getFlowSeries({ deviceId: selectedDeviceId, fromUtc: from, toUtc: to, bucketSeconds: 60 }),
        analyticsApi.getLevelsSummary({ deviceId: selectedDeviceId, fromUtc: from, toUtc: to }),
        analyticsApi.getActuatorsSummary({ deviceId: selectedDeviceId, fromUtc: from, toUtc: to }),
      ])

      setSummary(summaryResponse)
      setFlowSeries(flowResponse)
      setLevelSummary(levelResponse)
      setActuatorSummary(actuatorResponse)
    } catch (searchError) {
      setSummary(null)
      setFlowSeries([])
      setLevelSummary([])
      setActuatorSummary([])
      setError(getApiErrorMessage(searchError, 'No se pudieron consultar las estadísticas'))
    } finally {
      setIsLoading(false)
    }
  }

  useEffect(() => {
    if (selectedDeviceId) {
      void handleSearch()
    }
  }, [selectedDeviceId])

  if (isLoading && devices.length === 0) {
    return <LoadingSpinner message="Cargando estadísticas..." />
  }

  if (devices.length === 0) {
    return <EmptyState title="No hay dispositivos registrados" message="No existen dispositivos para analizar." />
  }

  return (
    <section className="page-section d-grid gap-4">
      <div>
        <h1 className="h3 mb-1">Estadísticas</h1>
        <p className="text-secondary mb-0">Resumen analítico por dispositivo y rango de fechas.</p>
      </div>

      <div className="card surface-card border-0">
        <div className="card-header">
          <h2 className="h6 mb-0">Filtros</h2>
        </div>
        <div className="card-body">
          <form className="row g-3" onSubmit={handleSearch}>
            <div className="col-md-4">
              <label htmlFor="analytics-device" className="form-label">
                Dispositivo
              </label>
              <select
                id="analytics-device"
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
              <label htmlFor="analytics-from" className="form-label">
                Fecha inicio
              </label>
              <input
                id="analytics-from"
                type="datetime-local"
                className="form-control"
                value={fromUtc}
                onChange={(event) => setFromUtc(event.target.value)}
              />
            </div>
            <div className="col-md-4">
              <label htmlFor="analytics-to" className="form-label">
                Fecha fin
              </label>
              <input
                id="analytics-to"
                type="datetime-local"
                className="form-control"
                value={toUtc}
                onChange={(event) => setToUtc(event.target.value)}
              />
            </div>
            <div className="col-12">
              <button type="submit" className="btn btn-primary" disabled={isLoading}>
                {isLoading ? 'Consultando...' : 'Consultar estadísticas'}
              </button>
            </div>
          </form>
        </div>
      </div>

      {error ? <div className="alert alert-danger">{error}</div> : null}

      <div className="row g-4">
        <div className="col-md-6 col-xl-3">
          <StatCard title="Litros totales" value={formatLiters(summary?.totalLiters)} />
        </div>
        <div className="col-md-6 col-xl-3">
          <StatCard title="Caudal promedio" value={formatFlow(summary?.averageFlowLpm)} />
        </div>
        <div className="col-md-6 col-xl-3">
          <StatCard title="Caudal máximo" value={formatFlow(summary?.maxFlowLpm)} />
        </div>
        <div className="col-md-6 col-xl-3">
          <StatCard title="Bomba encendida" value={formatDurationSeconds(summary?.pumpRuntimeSeconds)} />
        </div>
      </div>

      <div className="card surface-card border-0">
        <div className="card-header">
          <h2 className="h6 mb-0">Caudal promedio por tiempo</h2>
        </div>
        <div className="card-body">
          {flowSeries.length > 0 ? (
            <FlowChart data={flowSeries} />
          ) : (
            <EmptyState title="No hay mediciones disponibles" message="No hay puntos suficientes para construir la serie." />
          )}
        </div>
      </div>

      <div className="row g-4">
        <div className="col-xl-6">
          <div className="card surface-card border-0 h-100">
            <div className="card-header">
              <h2 className="h6 mb-0">Resumen de niveles</h2>
            </div>
            <div className="card-body">
              {levelSummary.length > 0 ? (
                <div className="table-responsive">
                  <table className="table align-middle mb-0">
                    <thead>
                      <tr>
                        <th>Sensor</th>
                        <th>Activo</th>
                        <th>Inactivo</th>
                      </tr>
                    </thead>
                    <tbody>
                      {levelSummary.map((item) => (
                        <tr key={item.name}>
                          <td>{item.name}</td>
                          <td>{formatInteger(item.activeCount)}</td>
                          <td>{formatInteger(item.inactiveCount)}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              ) : (
                <p className="text-secondary mb-0">No hay resumen de niveles.</p>
              )}
            </div>
          </div>
        </div>

        <div className="col-xl-6">
          <div className="card surface-card border-0 h-100">
            <div className="card-header">
              <h2 className="h6 mb-0">Resumen de actuadores</h2>
            </div>
            <div className="card-body">
              {actuatorSummary.length > 0 ? (
                <div className="table-responsive">
                  <table className="table align-middle mb-0">
                    <thead>
                      <tr>
                        <th>Actuador</th>
                        <th>On</th>
                        <th>Off</th>
                        <th>Tiempo activo</th>
                      </tr>
                    </thead>
                    <tbody>
                      {actuatorSummary.map((item) => (
                        <tr key={item.name}>
                          <td>{item.name}</td>
                          <td>{formatInteger(item.onCount)}</td>
                          <td>{formatInteger(item.offCount)}</td>
                          <td>{formatDurationSeconds(item.activeSeconds)}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              ) : (
                <p className="text-secondary mb-0">No hay resumen de actuadores.</p>
              )}
            </div>
          </div>
        </div>
      </div>

      {summary ? (
        <div className="card surface-card border-0">
          <div className="card-header">
            <h2 className="h6 mb-0">Tabla comparativa</h2>
          </div>
          <div className="card-body table-responsive">
            <table className="table align-middle mb-0">
              <thead>
                <tr>
                  <th>Métrica</th>
                  <th>Valor</th>
                </tr>
              </thead>
              <tbody>
                <tr>
                  <td>Mediciones</td>
                  <td>{formatInteger(summary.measurementsCount)}</td>
                </tr>
                <tr>
                  <td>Última medición</td>
                  <td>{formatDateTime(summary.lastMeasurementAtUtc)}</td>
                </tr>
                <tr>
                  <td>Caudal mínimo</td>
                  <td>{formatFlow(summary.minFlowLpm)}</td>
                </tr>
                <tr>
                  <td>Actuadores con actividad</td>
                  <td>{formatInteger(summary.actuators.length)}</td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
      ) : null}
    </section>
  )
}

export default AnalyticsPage
