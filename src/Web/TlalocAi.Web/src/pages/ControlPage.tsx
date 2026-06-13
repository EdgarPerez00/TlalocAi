import { useEffect, useState } from 'react'
import { getApiErrorMessage } from '../api/apiClient'
import * as controlApi from '../api/controlApi'
import * as devicesApi from '../api/devicesApi'
import * as telemetryApi from '../api/telemetryApi'
import ActuatorControlCard from '../components/ActuatorControlCard'
import EmptyState from '../components/EmptyState'
import LoadingSpinner from '../components/LoadingSpinner'
import type { DeviceCommandDto } from '../types/control'
import type { ActuatorDto, DeviceDto } from '../types/devices'
import type { MeasurementDto } from '../types/telemetry'
import { formatDateTime } from '../utils/dateFormat'

const actuatorTargets = [
  { key: 'pump', label: 'Bomba' },
  { key: 'valve_1', label: 'Valve 1' },
  { key: 'valve_2', label: 'Valve 2' },
  { key: 'valve_3', label: 'Valve 3' },
  { key: 'valve_4', label: 'Valve 4' },
]

function ControlPage() {
  const [devices, setDevices] = useState<DeviceDto[]>([])
  const [selectedDeviceId, setSelectedDeviceId] = useState('')
  const [actuators, setActuators] = useState<ActuatorDto[]>([])
  const [commands, setCommands] = useState<DeviceCommandDto[]>([])
  const [latestMeasurement, setLatestMeasurement] = useState<MeasurementDto | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [successMessage, setSuccessMessage] = useState<string | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [busyTarget, setBusyTarget] = useState<string | null>(null)

  useEffect(() => {
    async function loadDevices() {
      setIsLoading(true)
      setError(null)

      try {
        const devicesResponse = await devicesApi.getDevices()
        setDevices(devicesResponse)
        setSelectedDeviceId((current) => current || devicesResponse[0]?.id || '')
      } catch (loadError) {
        setError(getApiErrorMessage(loadError, 'No se pudo cargar la página de control'))
      } finally {
        setIsLoading(false)
      }
    }

    void loadDevices()
  }, [])

  async function loadDeviceData(deviceId: string) {
    setIsLoading(true)
    setError(null)

    try {
      const [actuatorsResponse, commandsResponse] = await Promise.all([
        devicesApi.getActuators(deviceId),
        controlApi.getCommands(deviceId),
      ])

      const latestResponse = await telemetryApi.getLatestTelemetry(deviceId).catch(() => null)

      setActuators(actuatorsResponse)
      setCommands(commandsResponse)
      setLatestMeasurement(latestResponse)
    } catch (loadError) {
      setActuators([])
      setCommands([])
      setLatestMeasurement(null)
      setError(getApiErrorMessage(loadError, 'No se pudieron cargar los actuadores o comandos'))
    } finally {
      setIsLoading(false)
    }
  }

  useEffect(() => {
    if (!selectedDeviceId) {
      return
    }

    void loadDeviceData(selectedDeviceId)
  }, [selectedDeviceId])

  async function handleToggle(target: string, nextState: boolean) {
    if (!selectedDeviceId) {
      return
    }

    setBusyTarget(target)
    setError(null)
    setSuccessMessage(null)

    try {
      await controlApi.createCommand({
        deviceId: selectedDeviceId,
        type: 'SetActuatorState',
        target,
        state: nextState,
      })

      setSuccessMessage(`Comando enviado para ${target}.`)
      await loadDeviceData(selectedDeviceId)
    } catch (toggleError) {
      setError(getApiErrorMessage(toggleError, 'No se pudo enviar el comando'))
    } finally {
      setBusyTarget(null)
    }
  }

  async function handleCancel(commandId: string) {
    setError(null)
    setSuccessMessage(null)

    try {
      await controlApi.cancelCommand(commandId)
      setSuccessMessage('Comando cancelado.')
      await loadDeviceData(selectedDeviceId)
    } catch (cancelError) {
      setError(getApiErrorMessage(cancelError, 'No se pudo cancelar el comando'))
    }
  }

  function getCurrentState(target: string): boolean {
    if (target === 'pump') {
      return Boolean(latestMeasurement?.pumpOn)
    }

    return Boolean(latestMeasurement?.actuators.find((item) => item.name === target)?.isOn)
  }

  function isRegisteredTarget(target: string): boolean {
    return actuators.some((actuator) => actuator.name === target && actuator.isActive)
  }

  if (isLoading && devices.length === 0) {
    return <LoadingSpinner message="Cargando panel de control..." />
  }

  if (devices.length === 0) {
    return <EmptyState title="No hay dispositivos registrados" message="Primero registra un dispositivo y sus actuadores." />
  }

  return (
    <section className="page-section d-grid gap-4">
      <div className="d-flex flex-column flex-lg-row justify-content-between align-items-lg-center gap-3">
        <div>
          <h1 className="h3 mb-1">Control</h1>
          <p className="text-secondary mb-0">Envía comandos al backend para bomba y electroválvulas.</p>
        </div>
        <div className="col-lg-3">
          <label htmlFor="control-device" className="form-label mb-1">
            Dispositivo
          </label>
          <select
            id="control-device"
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

      {error ? <div className="alert alert-danger">{error}</div> : null}
      {successMessage ? <div className="alert alert-success">{successMessage}</div> : null}

      <div className="row g-4">
        {actuatorTargets.map((actuator) => (
          <div key={actuator.key} className="col-md-6 col-xl-4">
            <ActuatorControlCard
              title={actuator.label}
              target={actuator.key}
              isOn={getCurrentState(actuator.key)}
              disabled={!isRegisteredTarget(actuator.key)}
              busy={busyTarget === actuator.key}
              onToggle={handleToggle}
            />
          </div>
        ))}
      </div>

      <div className="card surface-card border-0">
        <div className="card-header">
          <h2 className="h6 mb-0">Historial reciente de comandos</h2>
        </div>
        <div className="card-body">
          {commands.length > 0 ? (
            <div className="table-responsive">
              <table className="table align-middle mb-0">
                <thead>
                  <tr>
                    <th>Fecha</th>
                    <th>Target</th>
                    <th>Estado solicitado</th>
                    <th>Status</th>
                    <th></th>
                  </tr>
                </thead>
                <tbody>
                  {commands.slice(0, 10).map((command) => (
                    <tr key={command.id}>
                      <td>{formatDateTime(command.createdAtUtc)}</td>
                      <td>{command.target}</td>
                      <td>{command.state ? 'Encender / Abrir' : 'Apagar / Cerrar'}</td>
                      <td>{command.status}</td>
                      <td className="text-end">
                        {command.status === 'Pending' ? (
                          <button
                            type="button"
                            className="btn btn-sm btn-outline-danger"
                            onClick={() => handleCancel(command.id)}
                          >
                            Cancelar
                          </button>
                        ) : null}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : (
            <p className="text-secondary mb-0">No hay comandos recientes para este dispositivo.</p>
          )}
        </div>
      </div>
    </section>
  )
}

export default ControlPage
