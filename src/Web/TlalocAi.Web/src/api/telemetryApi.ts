import type { CreateExperimentRequest, ExperimentDto, MeasurementDto } from '../types/telemetry'
import { request } from './apiClient'

interface TelemetryQuery {
  deviceId: string
  fromUtc?: string
  toUtc?: string
}

export function getTelemetryHistory(query: TelemetryQuery): Promise<MeasurementDto[]> {
  return request<MeasurementDto[]>('telemetry', {
    method: 'GET',
    url: '/api/telemetry',
    params: query,
  })
}

export function getLatestTelemetry(deviceId: string): Promise<MeasurementDto> {
  return request<MeasurementDto>('telemetry', {
    method: 'GET',
    url: '/api/telemetry/latest',
    params: { deviceId },
  })
}

export function getExperiments(deviceId?: string): Promise<ExperimentDto[]> {
  return request<ExperimentDto[]>('telemetry', {
    method: 'GET',
    url: '/api/experiments',
    params: deviceId ? { deviceId } : undefined,
  })
}

export function createExperiment(data: CreateExperimentRequest): Promise<ExperimentDto> {
  return request<ExperimentDto>('telemetry', {
    method: 'POST',
    url: '/api/experiments',
    data,
  })
}

export function getExperiment(experimentId: string): Promise<ExperimentDto> {
  return request<ExperimentDto>('telemetry', {
    method: 'GET',
    url: `/api/experiments/${experimentId}`,
  })
}

export function finishExperiment(experimentId: string): Promise<ExperimentDto> {
  return request<ExperimentDto>('telemetry', {
    method: 'POST',
    url: `/api/experiments/${experimentId}/finish`,
  })
}
