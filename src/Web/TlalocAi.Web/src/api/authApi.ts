import type { LoginRequest, LoginResponse, RegisterRequest, UserDto } from '../types/auth'
import { request } from './apiClient'

export function login(data: LoginRequest): Promise<LoginResponse> {
  return request<LoginResponse>('identity', {
    method: 'POST',
    url: '/api/auth/login',
    data,
  })
}

export function register(data: RegisterRequest): Promise<LoginResponse> {
  return request<LoginResponse>('identity', {
    method: 'POST',
    url: '/api/auth/register',
    data,
  })
}

export function getMe(): Promise<UserDto> {
  return request<UserDto>('identity', {
    method: 'GET',
    url: '/api/auth/me',
  })
}
