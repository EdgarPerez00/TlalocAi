import { useEffect, useState } from 'react'
import { useParams } from 'react-router-dom'
import { getApiErrorMessage } from '../api/apiClient'
import * as analyticsApi from '../api/analyticsApi'
import * as telemetryApi from '../api/telemetryApi'
import EmptyState from '../components/EmptyState'
import FlowChart from '../components/FlowChart'
import LoadingSpinner from '../components/LoadingSpinner'
import StatCard from '../components/StatCard'
import type { AnalyticsSummaryDto, FlowSeriesPointDto } from '../types/analytics'
import type { ExperimentDto, MeasurementDto } from '../types/telemetry'
import { formatDateTime } from '../utils/dateFormat'
import { formatDurationSeconds, formatFlow, formatLiters } from '../utils/numberFormat'

function ExperimentDetailPage() {
  const { experimentId = '' } = useParams()
  const [experiment, setExperiment] = useState<ExperimentDto | null>(null)
  const [summary, setSummary] = useState<AnalyticsSummaryDto | null>(null)
  const [flowSeries, setFlowSeries] = useState<FlowSeriesPointDto[]>([])
  const [measurements, setMeasurements] = useState<MeasurementDto[]>([])
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    async function loadExperiment() {
      setIsLoading(true)
      setError(null)

      try {
        const experimentResponse = await telemetryApi.getExperiment(experimentId)
        const [summaryResponse, telemetryResponse] = await Promise.all([
          analyticsApi.getExperimentAnalyticsSummary(experimentId),
          telemetryApi.getTelemetryHistory({
            deviceId: experimentResponse.deviceId,
            fromUtc: experimentResponse.startedAtUtc,
            toUtc: experimentResponse.endedAtUtc ?? new Date().toISOString(),
          }),
        ])

        const filteredMeasurements = telemetryResponse.filter((item) => item.experimentId === experimentId)

        setExperiment(experimentResponse)
        setSummary(summaryResponse)
        setMeasurements(filteredMeasurements)
        setFlowSeries(
          filteredMeasurements.map((item) => ({
            bucketUtc: item.timestampUtc,
            averageFlowLpm: item.flowLpm,
            measurementsCount: 1,
          })),
        )
      } catch (loadError) {
        setError(getApiErrorMessage(loadError, 'No se pudo cargar el experimento'))
      } finally {
        setIsLoading(false)
      }
    }

    if (experimentId) {
      void loadExperiment()
    }
  }, [experimentId])

  if (isLoading) {
    return <LoadingSpinner message="Cargando experimento..." />
  }

  if (error) {
    return <div className="alert alert-danger">{error}</div>
  }

  if (!experiment) {
    return <EmptyState title="Experimento no encontrado" message="No fue posible recuperar el experimento solicitado." />
  }

  return (
    <section className="page-section d-grid gap-4">
      <div>
        <h1 className="h3 mb-1">{experiment.name}</h1>
        <p className="text-secondary mb-0">{experiment.description || 'Sin descripción adicional'}</p>
      </div>

      <div className="row g-4">
        <div className="col-md-6 col-xl-3">
          <StatCard title="Inicio" value={formatDateTime(experiment.startedAtUtc)} />
        </div>
        <div className="col-md-6 col-xl-3">
          <StatCard title="Fin" value={formatDateTime(experiment.endedAtUtc)} />
        </div>
        <div className="col-md-6 col-xl-3">
          <StatCard title="Estado" value={experiment.status} />
        </div>
        <div className="col-md-6 col-xl-3">
          <StatCard title="Mediciones" value={String(measurements.length)} />
        </div>
      </div>

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
          <h2 className="h6 mb-0">Gráfica de caudal del experimento</h2>
        </div>
        <div className="card-body">
          {flowSeries.length > 0 ? (
            <FlowChart data={flowSeries} />
          ) : (
            <EmptyState title="No hay mediciones disponibles" message="No existen puntos de telemetría para este experimento." />
          )}
        </div>
      </div>

      <div className="card surface-card border-0">
        <div className="card-header">
          <h2 className="h6 mb-0">Uso de actuadores</h2>
        </div>
        <div className="card-body">
          {summary?.actuators.length ? (
            <div className="table-responsive">
              <table className="table align-middle mb-0">
                <thead>
                  <tr>
                    <th>Actuador</th>
                    <th>Tiempo activo</th>
                    <th>Litros estimados</th>
                  </tr>
                </thead>
                <tbody>
                  {summary.actuators.map((actuator) => (
                    <tr key={actuator.name}>
                      <td>{actuator.name}</td>
                      <td>{formatDurationSeconds(actuator.activeSeconds)}</td>
                      <td>{formatLiters(actuator.estimatedLiters)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : (
            <p className="text-secondary mb-0">No hay resumen de actuadores para este experimento.</p>
          )}
        </div>
      </div>
    </section>
  )
}

export default ExperimentDetailPage
