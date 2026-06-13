export interface DeviceCommandDto {
  id: string
  deviceId: string
  type: string
  target: string
  state: boolean
  status: string
  createdAtUtc: string
  sentAtUtc?: string | null
  executedAtUtc?: string | null
  errorMessage?: string | null
}

export interface CreateCommandRequest {
  deviceId: string
  type: string
  target: string
  state: boolean
}
