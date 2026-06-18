import type {
  ActuatorDto,
  CreateActuatorRequest,
  CreateDeviceRequest,
  CreateSensorRequest,
  DeviceCreatedDto,
  DeviceDto,
  SensorDto,
} from '../types/devices'
import { request } from './apiClient'

export function getDevices(): Promise<DeviceDto[]> {
  return request<DeviceDto[]>('devices', {
    method: 'GET',
    url: '/api/devices',
  })
}

export function getDevice(deviceId: string): Promise<DeviceDto> {
  return request<DeviceDto>('devices', {
    method: 'GET',
    url: `/api/devices/${deviceId}`,
  })
}

export function createDevice(data: CreateDeviceRequest): Promise<DeviceCreatedDto> {
  return request<DeviceCreatedDto>('devices', {
    method: 'POST',
    url: '/api/devices',
    data,
  })
}

export function deleteDevice(deviceId: string): Promise<DeviceDto> {
  return request<DeviceDto>('devices', {
    method: 'DELETE',
    url: `/api/devices/${deviceId}`,
  })
}

export function createSensor(deviceId: string, data: CreateSensorRequest): Promise<SensorDto> {
  return request<SensorDto>('devices', {
    method: 'POST',
    url: `/api/devices/${deviceId}/sensors`,
    data,
  })
}

export function getSensors(deviceId: string): Promise<SensorDto[]> {
  return request<SensorDto[]>('devices', {
    method: 'GET',
    url: `/api/devices/${deviceId}/sensors`,
  })
}

export function createActuator(deviceId: string, data: CreateActuatorRequest): Promise<ActuatorDto> {
  return request<ActuatorDto>('devices', {
    method: 'POST',
    url: `/api/devices/${deviceId}/actuators`,
    data,
  })
}

export function getActuators(deviceId: string): Promise<ActuatorDto[]> {
  return request<ActuatorDto[]>('devices', {
    method: 'GET',
    url: `/api/devices/${deviceId}/actuators`,
  })
}
