export interface LevelMeasurementDto {
  name: string
  isActive: boolean
}

export interface ActuatorSnapshotDto {
  name: string
  isOn: boolean
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
