export interface ActuatorUsageDto {
  name: string
  activeSeconds: number
  estimatedLiters: number
}

export interface AnalyticsSummaryDto {
  deviceId: string
  fromUtc: string
  toUtc: string
  totalLiters: number
  averageFlowLpm: number
  maxFlowLpm: number
  minFlowLpm: number
  pumpRuntimeSeconds: number
  measurementsCount: number
  lastMeasurementAtUtc?: string | null
  actuators: ActuatorUsageDto[]
}

export interface FlowSeriesPointDto {
  bucketUtc: string
  averageFlowLpm: number
  measurementsCount: number
}

export interface ActuatorSummaryDto {
  name: string
  onCount: number
  offCount: number
  activeSeconds: number
}

export interface LevelSummaryDto {
  name: string
  activeCount: number
  inactiveCount: number
}
