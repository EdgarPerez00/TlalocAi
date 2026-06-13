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
