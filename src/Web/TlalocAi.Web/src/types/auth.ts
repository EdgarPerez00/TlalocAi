export interface LoginRequest {
  email: string
  password: string
}

export interface RegisterRequest {
  fullName: string
  email: string
  password: string
  role?: string
}

export interface UserDto {
  id: string
  fullName: string
  email: string
  role: string
  createdAtUtc: string
}

export interface LoginResponse {
  accessToken: string
  expiresAtUtc: string
  user: UserDto
}
