import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { getApiErrorMessage } from '../api/apiClient'
import * as devicesApi from '../api/devicesApi'
import * as telemetryApi from '../api/telemetryApi'
import EmptyState from '../components/EmptyState'
import LoadingSpinner from '../components/LoadingSpinner'
import type { DeviceDto } from '../types/devices'
import type { ExperimentDto } from '../types/telemetry'
import { formatDateTime } from '../utils/dateFormat'

const initialForm = {
  deviceId: '',
  name: '',
  description: '',
}

function ExperimentsPage() {
  const [devices, setDevices] = useState<DeviceDto[]>([])
  const [experiments, setExperiments] = useState<ExperimentDto[]>([])
  const [form, setForm] = useState(initialForm)
  const [error, setError] = useState<string | null>(null)
  const [successMessage, setSuccessMessage] = useState<string | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [isSubmitting, setIsSubmitting] = useState(false)

  async function loadData() {
    setIsLoading(true)
    setError(null)

    try {
      const [devicesResponse, experimentsResponse] = await Promise.all([
        devicesApi.getDevices(),
        telemetryApi.getExperiments(),
      ])

      setDevices(devicesResponse)
      setExperiments(experimentsResponse)
      setForm((current) => ({
        ...current,
        deviceId: current.deviceId || devicesResponse[0]?.id || '',
      }))
    } catch (loadError) {
      setError(getApiErrorMessage(loadError, 'No se pudo cargar la información de experimentos'))
    } finally {
      setIsLoading(false)
    }
  }

  useEffect(() => {
    void loadData()
  }, [])

  async function handleCreate(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setIsSubmitting(true)
    setError(null)
    setSuccessMessage(null)

    try {
      await telemetryApi.createExperiment({
        deviceId: form.deviceId,
        name: form.name,
        description: form.description,
        startedAtUtc: new Date().toISOString(),
      })
      setSuccessMessage('Experimento creado correctamente.')
      setForm((current) => ({ ...initialForm, deviceId: current.deviceId }))
      await loadData()
    } catch (submitError) {
      setError(getApiErrorMessage(submitError, 'No se pudo crear el experimento'))
    } finally {
      setIsSubmitting(false)
    }
  }

  async function handleFinish(experimentId: string) {
    setError(null)
    setSuccessMessage(null)

    try {
      await telemetryApi.finishExperiment(experimentId)
      setSuccessMessage('Experimento finalizado.')
      await loadData()
    } catch (finishError) {
      setError(getApiErrorMessage(finishError, 'No se pudo finalizar el experimento'))
    }
  }

  if (isLoading) {
    return <LoadingSpinner message="Cargando experimentos..." />
  }

  return (
    <section className="page-section d-grid gap-4">
      <div>
        <h1 className="h3 mb-1">Experimentos</h1>
        <p className="text-secondary mb-0">Crea y supervisa ejecuciones sobre cada dispositivo.</p>
      </div>

      {error ? <div className="alert alert-danger">{error}</div> : null}
      {successMessage ? <div className="alert alert-success">{successMessage}</div> : null}

      <div className="card surface-card border-0">
        <div className="card-header">
          <h2 className="h6 mb-0">Crear experimento</h2>
        </div>
        <div className="card-body">
          <form className="row g-3" onSubmit={handleCreate}>
            <div className="col-md-4">
              <label htmlFor="experiment-device" className="form-label">
                Dispositivo
              </label>
              <select
                id="experiment-device"
                className="form-select"
                value={form.deviceId}
                onChange={(event) => setForm((current) => ({ ...current, deviceId: event.target.value }))}
                required
              >
                {devices.map((device) => (
                  <option key={device.id} value={device.id}>
                    {device.name}
                  </option>
                ))}
              </select>
            </div>
            <div className="col-md-4">
              <label htmlFor="experiment-name" className="form-label">
                Nombre
              </label>
              <input
                id="experiment-name"
                className="form-control"
                value={form.name}
                onChange={(event) => setForm((current) => ({ ...current, name: event.target.value }))}
                required
              />
            </div>
            <div className="col-md-4">
              <label htmlFor="experiment-description" className="form-label">
                Descripción
              </label>
              <input
                id="experiment-description"
                className="form-control"
                value={form.description}
                onChange={(event) => setForm((current) => ({ ...current, description: event.target.value }))}
              />
            </div>
            <div className="col-12">
              <button type="submit" className="btn btn-primary" disabled={isSubmitting || devices.length === 0}>
                {isSubmitting ? 'Creando...' : 'Crear experimento'}
              </button>
            </div>
          </form>
        </div>
      </div>

      {experiments.length === 0 ? (
        <EmptyState
          title="No hay experimentos registrados"
          message="Aún no existen ejecuciones almacenadas en el backend."
        />
      ) : (
        <div className="card surface-card border-0">
          <div className="card-header">
            <h2 className="h6 mb-0">Listado de experimentos</h2>
          </div>
          <div className="card-body table-responsive">
            <table className="table align-middle mb-0">
              <thead>
                <tr>
                  <th>Nombre</th>
                  <th>Dispositivo</th>
                  <th>Inicio</th>
                  <th>Fin</th>
                  <th>Estado</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                {experiments.map((experiment) => (
                  <tr key={experiment.id}>
                    <td>{experiment.name}</td>
                    <td>{devices.find((device) => device.id === experiment.deviceId)?.name ?? experiment.deviceId}</td>
                    <td>{formatDateTime(experiment.startedAtUtc)}</td>
                    <td>{formatDateTime(experiment.endedAtUtc)}</td>
                    <td>
                      <span
                        className={`badge ${
                          experiment.status === 'Running'
                            ? 'text-bg-primary'
                            : experiment.status === 'Finished'
                              ? 'text-bg-success'
                              : 'text-bg-secondary'
                        }`}
                      >
                        {experiment.status}
                      </span>
                    </td>
                    <td className="text-end">
                      <div className="d-flex justify-content-end gap-2">
                        <Link to={`/experiments/${experiment.id}`} className="btn btn-sm btn-outline-primary">
                          Ver detalle
                        </Link>
                        {experiment.status === 'Running' ? (
                          <button
                            type="button"
                            className="btn btn-sm btn-outline-danger"
                            onClick={() => handleFinish(experiment.id)}
                          >
                            Finalizar
                          </button>
                        ) : null}
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}
    </section>
  )
}

export default ExperimentsPage
