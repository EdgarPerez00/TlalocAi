import type {
  ActuatorSummaryDto,
  AnalyticsSummaryDto,
  FlowSeriesPointDto,
  LevelSummaryDto,
} from '../types/analytics'
import { request } from './apiClient'

interface AnalyticsQuery {
  deviceId: string
  fromUtc?: string
  toUtc?: string
  bucketSeconds?: number
}

export function getAnalyticsSummary(query: AnalyticsQuery): Promise<AnalyticsSummaryDto> {
  return request<AnalyticsSummaryDto>('analytics', {
    method: 'GET',
    url: '/api/analytics/summary',
    params: query,
  })
}

export function getFlowSeries(query: AnalyticsQuery): Promise<FlowSeriesPointDto[]> {
  return request<FlowSeriesPointDto[]>('analytics', {
    method: 'GET',
    url: '/api/analytics/flow-series',
    params: query,
  })
}

export function getLevelsSummary(query: AnalyticsQuery): Promise<LevelSummaryDto[]> {
  return request<LevelSummaryDto[]>('analytics', {
    method: 'GET',
    url: '/api/analytics/levels-summary',
    params: query,
  })
}

export function getActuatorsSummary(query: AnalyticsQuery): Promise<ActuatorSummaryDto[]> {
  return request<ActuatorSummaryDto[]>('analytics', {
    method: 'GET',
    url: '/api/analytics/actuators-summary',
    params: query,
  })
}

export function getExperimentAnalyticsSummary(experimentId: string): Promise<AnalyticsSummaryDto> {
  return request<AnalyticsSummaryDto>('analytics', {
    method: 'GET',
    url: `/api/analytics/experiments/${experimentId}/summary`,
  })
}
