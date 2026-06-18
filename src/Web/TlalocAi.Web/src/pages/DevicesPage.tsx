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
  const [deviceToDelete, setDeviceToDelete] = useState<DeviceDto | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [isDeleting, setIsDeleting] = useState(false)

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

  async function handleDeleteDevice() {
    if (!deviceToDelete) {
      return
    }

    setIsDeleting(true)
    setError(null)

    try {
      await devicesApi.deleteDevice(deviceToDelete.id)
      setCreatedDevice(null)
      setDeviceToDelete(null)
      await loadDevices()
    } catch (deleteError) {
      setError(getApiErrorMessage(deleteError, 'No se pudo eliminar el dispositivo'))
    } finally {
      setIsDeleting(false)
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
                  <div className="d-grid gap-2 mt-auto">
                    <Link to={`/devices/${device.id}`} className="btn btn-outline-primary">
                      Ver detalle
                    </Link>
                    <button
                      type="button"
                      className="btn btn-outline-danger"
                      onClick={() => setDeviceToDelete(device)}
                    >
                      Eliminar
                    </button>
                  </div>
                </div>
              </div>
            </div>
          ))}
        </div>
      )}

      {deviceToDelete ? (
        <div className="modal-backdrop-custom" role="presentation">
          <div className="modal-dialog modal-dialog-centered" role="dialog" aria-modal="true" aria-labelledby="delete-device-title">
            <div className="modal-content">
              <div className="modal-header">
                <h2 id="delete-device-title" className="modal-title h5">
                  Eliminar dispositivo
                </h2>
                <button
                  type="button"
                  className="btn-close"
                  aria-label="Cerrar"
                  onClick={() => setDeviceToDelete(null)}
                  disabled={isDeleting}
                />
              </div>
              <div className="modal-body">
                <p>
                  Se eliminara el dispositivo <strong>{deviceToDelete.name}</strong>.
                </p>
                <p className="text-secondary mb-0">
                  Id tecnico: <code>{deviceToDelete.id}</code>
                </p>
              </div>
              <div className="modal-footer">
                <button
                  type="button"
                  className="btn btn-outline-secondary"
                  onClick={() => setDeviceToDelete(null)}
                  disabled={isDeleting}
                >
                  Cancelar
                </button>
                <button type="button" className="btn btn-danger" onClick={handleDeleteDevice} disabled={isDeleting}>
                  {isDeleting ? 'Eliminando...' : 'Eliminar dispositivo'}
                </button>
              </div>
            </div>
          </div>
        </div>
      ) : null}
    </section>
  )
}

export default DevicesPage
