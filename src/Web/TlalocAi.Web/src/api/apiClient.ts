import axios, { AxiosError, type AxiosRequestConfig } from 'axios'
import { clearStoredAuthSession, getStoredAuthSession } from '../utils/storage'

type ServiceName = 'identity' | 'devices' | 'telemetry' | 'control' | 'analytics'

const sharedClient = axios.create({
  timeout: 20_000,
})

let accessTokenProvider: () => string | null = () => getStoredAuthSession()?.accessToken ?? null
let unauthorizedHandler: () => void = () => {
  clearStoredAuthSession()

  if (typeof window !== 'undefined' && window.location.pathname !== '/login') {
    window.location.assign('/login')
  }
}

sharedClient.interceptors.request.use((config) => {
  const token = accessTokenProvider()

  if (token) {
    config.headers = config.headers ?? {}
    config.headers.Authorization = `Bearer ${token}`
  }

  return config
})

sharedClient.interceptors.response.use(
  (response) => response,
  (error: AxiosError) => {
    if (error.response?.status === 401) {
      unauthorizedHandler()
    }

    return Promise.reject(error)
  },
)

const serviceEnvMap: Record<ServiceName, string> = {
  identity: 'VITE_IDENTITY_API_BASE_URL',
  devices: 'VITE_DEVICES_API_BASE_URL',
  telemetry: 'VITE_TELEMETRY_API_BASE_URL',
  control: 'VITE_CONTROL_API_BASE_URL',
  analytics: 'VITE_ANALYTICS_API_BASE_URL',
}

function useGateway(): boolean {
  return String(import.meta.env.VITE_USE_GATEWAY ?? 'true').toLowerCase() !== 'false'
}

function normalizeBaseUrl(baseUrl?: string): string {
  const value = (baseUrl ?? '').trim()

  if (value === '' || value === '/') {
    return ''
  }

  return value.replace(/\/+$/, '')
}

function getServiceBaseUrl(service: ServiceName): string {
  if (useGateway()) {
    return normalizeBaseUrl(import.meta.env.VITE_API_BASE_URL)
  }

  const envKey = serviceEnvMap[service] as keyof ImportMetaEnv
  return normalizeBaseUrl(import.meta.env[envKey] ?? import.meta.env.VITE_API_BASE_URL)
}

export function configureApiClient(options: {
  getAccessToken: () => string | null
  onUnauthorized: () => void
}): void {
  accessTokenProvider = options.getAccessToken
  unauthorizedHandler = options.onUnauthorized
}

export async function request<T>(service: ServiceName, config: AxiosRequestConfig): Promise<T> {
  const baseURL = getServiceBaseUrl(service)

  const response = await sharedClient.request<T>({
    ...config,
    ...(baseURL ? { baseURL } : {}),
  })

  return response.data
}

export function getApiErrorMessage(error: unknown, fallback = 'No se pudo completar la solicitud.'): string {
  if (axios.isAxiosError(error)) {
    if (typeof error.response?.data === 'string' && error.response.data.trim().length > 0) {
      return error.response.data
    }

    const responseData = error.response?.data as { detail?: string; title?: string } | undefined

    if (responseData?.detail) {
      return responseData.detail
    }

    if (responseData?.title) {
      return responseData.title
    }

    if (error.message) {
      return error.message
    }
  }

  return fallback
}
