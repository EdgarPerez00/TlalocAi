import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { getApiErrorMessage } from '../api/apiClient'
import * as devicesApi from '../api/devicesApi'
import EmptyState from '../components/EmptyState'
import LoadingSpinner from '../components/LoadingSpinner'
import type { DeviceCreatedDto, DeviceDto } from '../types/devices'
import { formatDateTime } from '../utils/dateFormat'

const initialForm = {
  id: '',
  name: '',
  description: '',
}

function DevicesPage() {
  const [devices, setDevices] = useState<DeviceDto[]>([])
  const [form, setForm] = useState(initialForm)
  const [createdDevice, setCreatedDevice] = useState<DeviceCreatedDto | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [isSubmitting, setIsSubmitting] = useState(false)

  async function loadDevices() {
    setIsLoading(true)
    setError(null)

    try {
      setDevices(await devicesApi.getDevices())
    } catch (loadError) {
      setError(getApiErrorMessage(loadError, 'No se pudo cargar la lista de dispositivos'))
    } finally {
      setIsLoading(false)
    }
  }

  useEffect(() => {
    void loadDevices()
  }, [])

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setIsSubmitting(true)
    setError(null)

    try {
      const response = await devicesApi.createDevice(form)
      setCreatedDevice(response)
      setForm(initialForm)
      await loadDevices()
    } catch (submitError) {
      setError(getApiErrorMessage(submitError, 'No se pudo crear el dispositivo'))
    } finally {
      setIsSubmitting(false)
    }
  }

  if (isLoading) {
    return <LoadingSpinner message="Cargando dispositivos..." />
  }

  return (
    <section className="page-section d-grid gap-4">
      <div>
        <h1 className="h3 mb-1">Dispositivos</h1>
        <p className="text-secondary mb-0">Administra los equipos conectados al modelo de medición.</p>
      </div>

      {error ? <div className="alert alert-danger">{error}</div> : null}
      {createdDevice ? (
        <div className="alert alert-success">
          Dispositivo <strong>{createdDevice.device.name}</strong> creado. API key generada: <code>{createdDevice.apiKey}</code>
        </div>
      ) : null}

      <div className="card surface-card border-0">
        <div className="card-header">
          <h2 className="h6 mb-0">Crear dispositivo</h2>
        </div>
        <div className="card-body">
          <form className="row g-3" onSubmit={handleSubmit}>
            <div className="col-md-4">
              <label htmlFor="device-id" className="form-label">
                Id técnico
              </label>
              <input
                id="device-id"
                className="form-control"
                value={form.id}
                onChange={(event) => setForm((current) => ({ ...current, id: event.target.value }))}
                required
              />
            </div>
            <div className="col-md-4">
              <label htmlFor="device-name" className="form-label">
                Nombre
              </label>
              <input
                id="device-name"
                className="form-control"
                value={form.name}
                onChange={(event) => setForm((current) => ({ ...current, name: event.target.value }))}
                required
              />
            </div>
            <div className="col-md-4">
              <label htmlFor="device-description" className="form-label">
                Descripción
              </label>
              <input
                id="device-description"
                className="form-control"
                value={form.description}
                onChange={(event) => setForm((current) => ({ ...current, description: event.target.value }))}
              />
            </div>
            <div className="col-12">
              <button type="submit" className="btn btn-primary" disabled={isSubmitting}>
                {isSubmitting ? 'Creando...' : 'Crear dispositivo'}
              </button>
            </div>
          </form>
        </div>
      </div>

      {devices.length === 0 ? (
        <EmptyState
          title="No hay dispositivos registrados"
          message="El backend todavía no tiene dispositivos activos para mostrar."
        />
      ) : (
        <div className="row g-4">
          {devices.map((device) => (
            <div key={device.id} className="col-xl-4 col-md-6">
              <div className="card surface-card border-0 h-100">
                <div className="card-body d-grid gap-3">
                  <div className="d-flex justify-content-between align-items-start">
                    <div>
                      <h2 className="h5 mb-1">{device.name}</h2>
                      <div className="text-secondary small">{device.id}</div>
                    </div>
                    <span className={`badge ${device.isActive ? 'text-bg-success' : 'text-bg-secondary'}`}>
                      {device.isActive ? 'Activo' : 'Inactivo'}
                    </span>
                  </div>
                  <p className="text-secondary mb-0">{device.description || 'Sin descripción'}</p>
                  <div className="small text-secondary">
                    <div>Creado: {formatDateTime(device.createdAtUtc)}</div>
                    <div>Última conexión: {formatDateTime(device.lastSeenAtUtc)}</div>
                  </div>
                  <Link to={`/devices/${device.id}`} className="btn btn-outline-primary mt-auto">
                    Ver detalle
                  </Link>
                </div>
              </div>
            </div>
          ))}
        </div>
      )}
    </section>
  )
}

export default DevicesPage
