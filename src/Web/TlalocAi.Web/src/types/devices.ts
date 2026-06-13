export interface DeviceDto {
  id: string
  name: string
  description?: string | null
  isActive: boolean
  createdAtUtc: string
  lastSeenAtUtc?: string | null
}

export interface CreateDeviceRequest {
  id: string
  name: string
  description?: string
}

export interface DeviceCreatedDto {
  device: DeviceDto
  apiKey: string
}

export interface SensorDto {
  id: string
  deviceId: string
  name: string
  type: string
  gpioPin: number
  isActive: boolean
  createdAtUtc: string
}

export interface CreateSensorRequest {
  name: string
  type: string
  gpioPin: number
}

export interface ActuatorDto {
  id: string
  deviceId: string
  name: string
  type: string
  gpioPin: number
  activeLow: boolean
  isActive: boolean
  createdAtUtc: string
}

export interface CreateActuatorRequest {
  name: string
  type: string
  gpioPin: number
  activeLow: boolean
}
