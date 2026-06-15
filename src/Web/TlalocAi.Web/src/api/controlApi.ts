import type { CreateCommandRequest, DeviceCommandDto } from '../types/control'
import { request } from './apiClient'

export function createCommand(data: CreateCommandRequest): Promise<DeviceCommandDto> {
  return request<DeviceCommandDto>('control', {
    method: 'POST',
    url: '/api/commands',
    data,
  })
}

export function getCommands(deviceId?: string): Promise<DeviceCommandDto[]> {
  return request<DeviceCommandDto[]>('control', {
    method: 'GET',
    url: '/api/commands',
    params: deviceId ? { deviceId } : undefined,
  })
}

export function getCommand(commandId: string): Promise<DeviceCommandDto> {
  return request<DeviceCommandDto>('control', {
    method: 'GET',
    url: `/api/commands/${commandId}`,
  })
}

export function cancelCommand(commandId: string): Promise<DeviceCommandDto> {
  return request<DeviceCommandDto>('control', {
    method: 'POST',
    url: `/api/commands/${commandId}/cancel`,
  })
}

export function openValve(deviceId: string, valveId: number): Promise<DeviceCommandDto> {
  return request<DeviceCommandDto>('control', {
    method: 'POST',
    url: `/api/valves/${valveId}/open`,
    data: { deviceId },
  })
}

export function closeValve(deviceId: string, valveId: number): Promise<DeviceCommandDto> {
  return request<DeviceCommandDto>('control', {
    method: 'POST',
    url: `/api/valves/${valveId}/close`,
    data: { deviceId },
  })
}

export function startPump(deviceId: string, pumpId: string): Promise<DeviceCommandDto> {
  return request<DeviceCommandDto>('control', {
    method: 'POST',
    url: `/api/pumps/${pumpId}/start`,
    data: { deviceId },
  })
}

export function stopPump(deviceId: string, pumpId: string): Promise<DeviceCommandDto> {
  return request<DeviceCommandDto>('control', {
    method: 'POST',
    url: `/api/pumps/${pumpId}/stop`,
    data: { deviceId },
  })
}
