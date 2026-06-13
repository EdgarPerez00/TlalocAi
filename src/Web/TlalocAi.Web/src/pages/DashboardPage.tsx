import { useEffect, useState } from 'react'
import { getApiErrorMessage } from '../api/apiClient'
import * as analyticsApi from '../api/analyticsApi'
import * as devicesApi from '../api/devicesApi'
import * as telemetryApi from '../api/telemetryApi'
import DeviceStatusCard from '../components/DeviceStatusCard'
import EmptyState from '../components/EmptyState'
import FlowChart from '../components/FlowChart'
import LevelSensorsGrid from '../components/LevelSensorsGrid'
import LoadingSpinner from '../components/LoadingSpinner'
import StatCard from '../components/StatCard'
import type { AnalyticsSummaryDto, FlowSeriesPointDto } from '../types/analytics'
import type { ActuatorDto, DeviceDto } from '../types/devices'
import type { MeasurementDto } from '../types/telemetry'
import { formatDateTime } from '../utils/dateFormat'
import { formatFlow, formatLiters } from '../utils/numberFormat'

function DashboardPage() {
  const [devices, setDevices] = useState<DeviceDto[]>([])
  const [selectedDeviceId, setSelectedDeviceId] = useState('')
  const [latestMeasurement, setLatestMeasurement] = useState<MeasurementDto | null>(null)
  const [summary, setSummary] = useState<AnalyticsSummaryDto | null>(null)
  const [flowSeries, setFlowSeries] = useState<FlowSeriesPointDto[]>([])
  const [actuators, setActuators] = useState<ActuatorDto[]>([])
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  const selectedDevice = devices.find((device) => device.id === selectedDeviceId) ?? null

  useEffect(() => {
    async function loadDevices() {
      setIsLoading(true)
      setError(null)

      try {
        const devicesResponse = await devicesApi.getDevices()
        setDevices(devicesResponse)

        if (devicesResponse.length > 0) {
          setSelectedDeviceId((current) => current || devicesResponse[0].id)
        }
      } catch (loadError) {
        setError(getApiErrorMessage(loadError, 'No se pudo conectar con el backend'))
      } finally {
        setIsLoading(false)
      }
    }

    void loadDevices()
  }, [])

  useEffect(() => {
    if (!selectedDeviceId) {
      return
    }

    const toUtc = new Date().toISOString()
    const fromUtc = new Date(Date.now() - 24 * 60 * 60 * 1000).toISOString()

    async function loadDashboardData() {
      setIsLoading(true)
      setError(null)

      try {
        const [latestResult, summaryResult, flowSeriesResult, actuatorsResult] = await Promise.allSettled([
          telemetryApi.getLatestTelemetry(selectedDeviceId),
          analyticsApi.getAnalyticsSummary({ deviceId: selectedDeviceId, fromUtc, toUtc }),
          analyticsApi.getFlowSeries({ deviceId: selectedDeviceId, fromUtc, toUtc, bucketSeconds: 60 }),
          devicesApi.getActuators(selectedDeviceId),
        ])

        setLatestMeasurement(latestResult.status === 'fulfilled' ? latestResult.value : null)
        setSummary(summaryResult.status === 'fulfilled' ? summaryResult.value : null)
        setFlowSeries(flowSeriesResult.status === 'fulfilled' ? flowSeriesResult.value : [])
        setActuators(actuatorsResult.status === 'fulfilled' ? actuatorsResult.value : [])

        if (actuatorsResult.status === 'rejected') {
          setError(getApiErrorMessage(actuatorsResult.reason, 'No se pudo cargar el dashboard'))
        }
      } catch (loadError) {
        setLatestMeasurement(null)
        setSummary(null)
        setFlowSeries([])
        setActuators([])
        setError(getApiErrorMessage(loadError, 'No se pudo cargar el dashboard'))
      } finally {
        setIsLoading(false)
      }
    }

    void loadDashboardData()
  }, [selectedDeviceId])

  if (isLoading && devices.length === 0) {
    return <LoadingSpinner message="Cargando dashboard..." />
  }

  if (error && devices.length === 0) {
    return <div className="alert alert-danger">{error}</div>
  }

  if (devices.length === 0) {
    return (
      <EmptyState
        title="No hay dispositivos registrados"
        message="Crea tu primer dispositivo para comenzar a recibir mediciones."
      />
    )
  }

  return (
    <section className="page-section d-grid gap-4">
      <div className="d-flex flex-column flex-lg-row justify-content-between align-items-lg-center gap-3">
        <div>
          <h1 className="h3 mb-1">Dashboard operativo</h1>
          <p className="text-secondary mb-0">Vista rápida del flujo, niveles y actuadores del sistema.</p>
        </div>
        <div className="col-lg-3">
          <label htmlFor="dashboard-device" className="form-label mb-1">
            Dispositivo
          </label>
          <select
            id="dashboard-device"
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
      </div>

      {error ? <div className="alert alert-warning">{error}</div> : null}

      <div className="row g-4">
        <div className="col-md-6 col-xl-3">
          <StatCard title="Última medición" value={formatDateTime(latestMeasurement?.timestampUtc)} />
        </div>
        <div className="col-md-6 col-xl-3">
          <StatCard title="Litros totales" value={formatLiters(summary?.totalLiters ?? latestMeasurement?.totalLiters)} />
        </div>
        <div className="col-md-6 col-xl-3">
          <StatCard title="Caudal actual" value={formatFlow(latestMeasurement?.flowLpm)} />
        </div>
        <div className="col-md-6 col-xl-3">
          <StatCard title="Caudal promedio" value={formatFlow(summary?.averageFlowLpm)} />
        </div>
        <div className="col-md-6 col-xl-3">
          <StatCard title="Caudal máximo" value={formatFlow(summary?.maxFlowLpm)} />
        </div>
        <div className="col-md-6 col-xl-3">
          <StatCard title="Bomba" value={latestMeasurement?.pumpOn ? 'Encendida' : 'Apagada'} />
        </div>
        <div className="col-md-6 col-xl-3">
          <StatCard
            title="Válvulas activas"
            value={String(latestMeasurement?.actuators.filter((item) => item.isOn).length ?? 0)}
            caption={`${actuators.length} actuadores registrados`}
          />
        </div>
        <div className="col-md-6 col-xl-3">
          <StatCard
            title="Sensores de nivel"
            value={String(latestMeasurement?.levels.length ?? 0)}
            caption="Lectura más reciente"
          />
        </div>
      </div>

      <div className="row g-4">
        <div className="col-xl-8">
          <div className="card surface-card border-0 h-100">
            <div className="card-header">
              <h2 className="h6 mb-0">Gráfica de caudal</h2>
            </div>
            <div className="card-body">
              {flowSeries.length > 0 ? (
                <FlowChart data={flowSeries} />
              ) : (
                <EmptyState title="No hay mediciones disponibles" message="No hay datos suficientes para graficar." />
              )}
            </div>
          </div>
        </div>
        <div className="col-xl-4">
          {selectedDevice ? (
            <DeviceStatusCard device={selectedDevice} latestMeasurement={latestMeasurement} />
          ) : null}
        </div>
      </div>

      <div className="row g-4">
        <div className="col-xl-6">
          <div className="card surface-card border-0 h-100">
            <div className="card-header">
              <h2 className="h6 mb-0">Estado de sensores de nivel</h2>
            </div>
            <div className="card-body">
              <LevelSensorsGrid levels={latestMeasurement?.levels ?? []} />
            </div>
          </div>
        </div>
        <div className="col-xl-6">
          <div className="card surface-card border-0 h-100">
            <div className="card-header">
              <h2 className="h6 mb-0">Estado de actuadores</h2>
            </div>
            <div className="card-body">
              {latestMeasurement?.actuators.length ? (
                <div className="row g-3">
                  {latestMeasurement.actuators.map((actuator) => (
                    <div key={actuator.name} className="col-md-6">
                      <div className="card bg-light border-0 h-100">
                        <div className="card-body d-flex justify-content-between align-items-center">
                          <span className="fw-semibold">{actuator.name}</span>
                          <span className={`badge ${actuator.isOn ? 'text-bg-primary' : 'text-bg-secondary'}`}>
                            {actuator.isOn ? 'Abierta / Encendida' : 'Cerrada / Apagada'}
                          </span>
                        </div>
                      </div>
                    </div>
                  ))}
                </div>
              ) : (
                <p className="text-secondary mb-0">No hay estado de válvulas disponible en la última medición.</p>
              )}
            </div>
          </div>
        </div>
      </div>
    </section>
  )
}

export default DashboardPage
