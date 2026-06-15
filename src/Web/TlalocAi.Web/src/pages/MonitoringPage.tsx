import { useEffect, useMemo, useState } from 'react'
import { getApiErrorMessage } from '../api/apiClient'
import * as controlApi from '../api/controlApi'
import * as devicesApi from '../api/devicesApi'
import * as telemetryApi from '../api/telemetryApi'
import EmptyState from '../components/EmptyState'
import LoadingSpinner from '../components/LoadingSpinner'
import type { DeviceCommandDto } from '../types/control'
import type { DeviceDto } from '../types/devices'
import type { DeviceStateDto, PumpStateDto, ReservoirStateDto, ValveStateDto } from '../types/telemetry'
import { formatDateTime } from '../utils/dateFormat'

function MonitoringPage() {
  const [devices, setDevices] = useState<DeviceDto[]>([])
  const [selectedDeviceId, setSelectedDeviceId] = useState('')
  const [state, setState] = useState<DeviceStateDto | null>(null)
  const [commands, setCommands] = useState<DeviceCommandDto[]>([])
  const [error, setError] = useState<string | null>(null)
  const [successMessage, setSuccessMessage] = useState<string | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [busyAction, setBusyAction] = useState<string | null>(null)

  useEffect(() => {
    async function loadDevices() {
      setIsLoading(true)
      setError(null)

      try {
        const response = await devicesApi.getDevices()
        setDevices(response)
        setSelectedDeviceId((current) => current || response[0]?.id || '')
      } catch (loadError) {
        setError(getApiErrorMessage(loadError, 'No se pudieron cargar dispositivos.'))
      } finally {
        setIsLoading(false)
      }
    }

    void loadDevices()
  }, [])

  async function loadState(deviceId: string) {
    try {
      const [stateResponse, commandsResponse] = await Promise.all([
        telemetryApi.getDeviceState(deviceId),
        controlApi.getCommands(deviceId),
      ])

      setState(stateResponse)
      setCommands(commandsResponse)
      setError(null)
    } catch (loadError) {
      setState(null)
      setCommands([])
      setError(getApiErrorMessage(loadError, 'No se pudo cargar el estado del sistema.'))
    }
  }

  useEffect(() => {
    if (!selectedDeviceId) {
      return
    }

    void loadState(selectedDeviceId)
    const interval = window.setInterval(() => {
      void loadState(selectedDeviceId)
    }, 3000)

    return () => window.clearInterval(interval)
  }, [selectedDeviceId])

  const alerts = useMemo(() => {
    if (!state) {
      return []
    }

    const builtIn = [
      state.tower.isCritical ? 'Nivel bajo en torre.' : null,
      state.cistern.isCritical ? 'Nivel bajo en cisterna.' : null,
      state.cistern.level >= 5 ? 'Nivel alto en cisterna.' : null,
      state.flow.noFlowAlert ? 'Sin flujo con bomba encendida.' : null,
      state.tower.hasInvalidReading || state.cistern.hasInvalidReading ? 'Lectura invalida de nivel.' : null,
      state.pumps.some((pump) => pump.isBlocked) ? 'Bomba bloqueada por seguridad.' : null,
      state.valves.some((valve) => valve.isLocked) ? 'Electrovalvula bloqueada por llenado.' : null,
    ].filter(Boolean) as string[]

    return [...builtIn, ...state.warnings, ...state.faults]
  }, [state])

  async function runAction(actionKey: string, action: () => Promise<DeviceCommandDto>) {
    setBusyAction(actionKey)
    setError(null)
    setSuccessMessage(null)

    try {
      const command = await action()
      setSuccessMessage(`Comando ${command.commandType ?? command.type} creado para ${command.target}.`)
      await loadState(selectedDeviceId)
    } catch (actionError) {
      setError(getApiErrorMessage(actionError, 'No se pudo crear el comando.'))
    } finally {
      setBusyAction(null)
    }
  }

  if (isLoading && devices.length === 0) {
    return <LoadingSpinner message="Cargando monitoreo..." />
  }

  if (devices.length === 0) {
    return <EmptyState title="No hay dispositivos registrados" message="Registra la Raspberry y sus actuadores antes de operar." />
  }

  const online = isOnline(state)

  return (
    <section className="page-section d-grid gap-4">
      <div className="d-flex flex-column flex-xl-row justify-content-between align-items-xl-center gap-3">
        <div>
          <h1 className="h3 mb-1">Monitoreo ESCOM</h1>
          <p className="text-secondary mb-0">Estado de Raspberry, ESP32, bombas, electrovalvulas, recipientes y caudal.</p>
        </div>
        <div className="col-xl-3">
          <label htmlFor="monitoring-device" className="form-label mb-1">
            Dispositivo
          </label>
          <select
            id="monitoring-device"
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

      <div className="row g-3">
        <div className="col-md-6 col-xl-3">
          <div className="surface-card p-3 h-100">
            <div className="metric-label">Conexion Raspberry</div>
            <div className={`metric-value ${online ? 'text-success' : 'text-danger'}`}>{online ? 'En linea' : 'Sin heartbeat'}</div>
            <div className="small text-secondary">Ultimo heartbeat: {state?.lastHeartbeatAtUtc ? formatDateTime(state.lastHeartbeatAtUtc) : 'Sin datos'}</div>
          </div>
        </div>
        <div className="col-md-6 col-xl-3">
          <div className="surface-card p-3 h-100">
            <div className="metric-label">IP observada</div>
            <div className="h5 mb-1">{state?.observedPublicIpAddress ?? 'Sin datos'}</div>
            <div className="small text-secondary">Solo diagnostico. No se usa para comandos.</div>
          </div>
        </div>
        <ReservoirCard title="Torre" reservoir={state?.tower} />
        <ReservoirCard title="Cisterna" reservoir={state?.cistern} />
      </div>

      {state ? (
        <>
          <div className="row g-3">
            <div className="col-lg-4">
              <div className="surface-card p-3 h-100">
                <div className="d-flex justify-content-between align-items-start gap-3">
                  <div>
                    <div className="metric-label">Caudal</div>
                    <div className="metric-value">{state.flow.litersPerMinute.toFixed(2)} L/min</div>
                    <div className="small text-secondary">{state.flow.totalLiters.toFixed(2)} litros acumulados</div>
                  </div>
                  <span className={`badge ${state.flow.noFlowAlert ? 'text-bg-danger' : 'text-bg-success'}`}>
                    {state.flow.noFlowAlert ? 'Sin flujo' : 'Normal'}
                  </span>
                </div>
              </div>
            </div>
            {state.pumps.map((pump) => (
              <PumpCard
                key={pump.pumpId}
                pump={pump}
                busyAction={busyAction}
                onStart={() => runAction(`pump-${pump.pumpId}-start`, () => controlApi.startPump(selectedDeviceId, pump.pumpId))}
                onStop={() => runAction(`pump-${pump.pumpId}-stop`, () => controlApi.stopPump(selectedDeviceId, pump.pumpId))}
              />
            ))}
          </div>

          <div className="surface-card p-3">
            <div className="d-flex justify-content-between align-items-center gap-3 mb-3">
              <h2 className="h5 mb-0">Electrovalvulas</h2>
              <span className="small text-secondary">Abrir se bloquea cuando hay llenado o torre critica; cerrar siempre se permite.</span>
            </div>
            <div className="row g-3">
              {state.valves.map((valve) => (
                <ValveCard
                  key={valve.valveId}
                  valve={valve}
                  busyAction={busyAction}
                  onOpen={() => runAction(`valve-${valve.valveId}-open`, () => controlApi.openValve(selectedDeviceId, valve.valveId))}
                  onClose={() => runAction(`valve-${valve.valveId}-close`, () => controlApi.closeValve(selectedDeviceId, valve.valveId))}
                />
              ))}
            </div>
          </div>

          <div className="row g-3">
            <div className="col-xl-5">
              <div className="surface-card p-3 h-100">
                <h2 className="h5 mb-3">Recipientes</h2>
                <div className="row g-2">
                  {state.containers.map((container) => (
                    <div key={container.containerId} className="col-6">
                      <div className={`border rounded-3 p-3 ${container.isFull ? 'bg-danger-subtle border-danger-subtle' : 'bg-success-subtle border-success-subtle'}`}>
                        <div className="fw-semibold">Recipiente {container.containerId}</div>
                        <div className="small">{container.isFull ? 'Lleno' : 'Vacio / no lleno'}</div>
                      </div>
                    </div>
                  ))}
                </div>
              </div>
            </div>
            <div className="col-xl-7">
              <div className="surface-card p-3 h-100">
                <h2 className="h5 mb-3">Alertas</h2>
                {alerts.length > 0 ? (
                  <div className="d-grid gap-2">
                    {alerts.map((alert, index) => (
                      <div key={`${alert}-${index}`} className="alert alert-warning mb-0 py-2">
                        {alert}
                      </div>
                    ))}
                  </div>
                ) : (
                  <p className="text-secondary mb-0">Sin alertas activas.</p>
                )}
              </div>
            </div>
          </div>

          <div className="surface-card p-3">
            <h2 className="h5 mb-3">Comandos recientes</h2>
            <div className="table-responsive">
              <table className="table align-middle mb-0">
                <thead>
                  <tr>
                    <th>Fecha</th>
                    <th>Destino</th>
                    <th>Comando</th>
                    <th>Estado</th>
                    <th>Resultado</th>
                  </tr>
                </thead>
                <tbody>
                  {commands.slice(0, 10).map((command) => (
                    <tr key={command.id}>
                      <td>{formatDateTime(command.createdAtUtc)}</td>
                      <td>{command.target}</td>
                      <td>{command.commandType ?? (command.state ? 'Encender / abrir' : 'Apagar / cerrar')}</td>
                      <td>{command.status}</td>
                      <td>{command.resultMessage ?? command.errorMessage ?? ''}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        </>
      ) : null}
    </section>
  )
}

function ReservoirCard({ title, reservoir }: { title: string; reservoir?: ReservoirStateDto }) {
  return (
    <div className="col-md-6 col-xl-3">
      <div className="surface-card p-3 h-100">
        <div className="d-flex justify-content-between align-items-start gap-2">
          <div>
            <div className="metric-label">{title}</div>
            <div className="metric-value">{reservoir?.level ?? 0}/5</div>
          </div>
          <span className={`badge ${reservoir?.isCritical || reservoir?.hasInvalidReading ? 'text-bg-danger' : 'text-bg-success'}`}>
            {reservoir?.hasInvalidReading ? 'Invalida' : reservoir?.isCritical ? 'Critica' : 'Normal'}
          </span>
        </div>
        <div className="d-flex gap-1 mt-3">
          {(reservoir?.sensors ?? [false, false, false, false, false]).map((active, index) => (
            <span key={index} className={`d-inline-block rounded-2 flex-fill ${active ? 'bg-info' : 'bg-secondary-subtle'}`} style={{ height: 12 }} />
          ))}
        </div>
      </div>
    </div>
  )
}

function PumpCard({
  pump,
  busyAction,
  onStart,
  onStop,
}: {
  pump: PumpStateDto
  busyAction: string | null
  onStart: () => void
  onStop: () => void
}) {
  return (
    <div className="col-lg-4">
      <div className="surface-card p-3 h-100">
        <div className="d-flex justify-content-between align-items-start gap-3">
          <div>
            <div className="metric-label">Bomba {pump.pumpId}</div>
            <div className="h4 mb-1">{pump.isOn ? 'Encendida' : 'Apagada'}</div>
            <div className="small text-secondary">{pump.isBlocked ? pump.blockReason ?? 'Bloqueada' : 'Disponible'}</div>
          </div>
          <span className={`badge ${pump.isBlocked ? 'text-bg-danger' : pump.isOn ? 'text-bg-primary' : 'text-bg-light text-dark'}`}>
            {pump.isBlocked ? 'Bloqueada' : pump.isOn ? 'ON' : 'OFF'}
          </span>
        </div>
        <div className="btn-group w-100 mt-3">
          <button type="button" className="btn btn-outline-primary" disabled={pump.isBlocked || busyAction !== null} onClick={onStart}>
            Encender
          </button>
          <button type="button" className="btn btn-outline-secondary" disabled={busyAction !== null} onClick={onStop}>
            Apagar
          </button>
        </div>
      </div>
    </div>
  )
}

function ValveCard({
  valve,
  busyAction,
  onOpen,
  onClose,
}: {
  valve: ValveStateDto
  busyAction: string | null
  onOpen: () => void
  onClose: () => void
}) {
  return (
    <div className="col-md-6 col-xl-3">
      <div className="border rounded-3 p-3 h-100">
        <div className="d-flex justify-content-between align-items-start gap-2">
          <div>
            <div className="fw-semibold">Valvula {valve.valveId}</div>
            <div className="small text-secondary">{valve.isOpen ? 'Abierta' : 'Cerrada'}</div>
          </div>
          <span className={`badge ${valve.isLocked ? 'text-bg-danger' : valve.isOpen ? 'text-bg-primary' : 'text-bg-light text-dark'}`}>
            {valve.isLocked ? 'Bloqueada' : valve.isOpen ? 'Abierta' : 'Cerrada'}
          </span>
        </div>
        <div className="small text-secondary mt-2" style={{ minHeight: 38 }}>
          {valve.isLocked ? valve.lockReason ?? 'Bloqueada por llenado.' : 'Sin bloqueo.'}
        </div>
        <div className="btn-group w-100 mt-3">
          <button type="button" className="btn btn-outline-primary" disabled={valve.isLocked || busyAction !== null} onClick={onOpen}>
            Abrir
          </button>
          <button type="button" className="btn btn-outline-secondary" disabled={busyAction !== null} onClick={onClose}>
            Cerrar
          </button>
        </div>
      </div>
    </div>
  )
}

function isOnline(state: DeviceStateDto | null): boolean {
  if (!state?.lastHeartbeatAtUtc) {
    return false
  }

  const lastSeen = Date.parse(state.lastHeartbeatAtUtc)
  return Number.isFinite(lastSeen) && Date.now() - lastSeen < 30_000
}

export default MonitoringPage
