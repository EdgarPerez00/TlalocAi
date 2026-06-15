export interface LevelMeasurementDto {
  name: string
  isActive: boolean
}

export interface ActuatorSnapshotDto {
  name: string
  isOn: boolean
}

export interface ReservoirStateDto {
  name: string
  level: number
  sensors: boolean[]
  isCritical: boolean
  hasInvalidReading: boolean
  message?: string | null
}

export interface FlowStateDto {
  litersPerMinute: number
  totalLiters: number
  pulses: number
  noFlowAlert: boolean
}

export interface PumpStateDto {
  pumpId: string
  isOn: boolean
  isBlocked: boolean
  blockReason?: string | null
}

export interface ValveStateDto {
  valveId: number
  isOpen: boolean
  isLocked: boolean
  lockReason?: string | null
}

export interface ContainerStateDto {
  containerId: number
  isFull: boolean
}

export interface DeviceStateDto {
  deviceId: string
  timestampUtc: string
  lastHeartbeatAtUtc?: string | null
  observedPublicIpAddress?: string | null
  hostname?: string | null
  agentVersion?: string | null
  tower: ReservoirStateDto
  cistern: ReservoirStateDto
  flow: FlowStateDto
  pumps: PumpStateDto[]
  valves: ValveStateDto[]
  containers: ContainerStateDto[]
  faults: string[]
  warnings: string[]
  rawInputs?: Record<string, boolean> | null
}

export interface MeasurementDto {
  id: string
  deviceId: string
  experimentId?: string | null
  timestampUtc: string
  flowLpm: number
  totalLiters: number
  pumpOn: boolean
  levels: LevelMeasurementDto[]
  actuators: ActuatorSnapshotDto[]
  detailedStateJson?: string | null
}

export interface ExperimentDto {
  id: string
  deviceId: string
  name: string
  description?: string | null
  startedAtUtc: string
  endedAtUtc?: string | null
  status: string
  createdAtUtc: string
}

export interface CreateExperimentRequest {
  deviceId: string
  name: string
  description?: string
  startedAtUtc?: string
}
