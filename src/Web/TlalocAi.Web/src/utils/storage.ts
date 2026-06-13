import type { UserDto } from '../types/auth'

const AUTH_STORAGE_KEY = 'tlalocai.auth'

export interface StoredAuthSession {
  accessToken: string
  expiresAtUtc: string
  user: UserDto
}

const isBrowser = typeof window !== 'undefined'

export function getStoredAuthSession(): StoredAuthSession | null {
  if (!isBrowser) {
    return null
  }

  const raw = window.localStorage.getItem(AUTH_STORAGE_KEY)

  if (!raw) {
    return null
  }

  try {
    return JSON.parse(raw) as StoredAuthSession
  } catch {
    window.localStorage.removeItem(AUTH_STORAGE_KEY)
    return null
  }
}

export function setStoredAuthSession(session: StoredAuthSession): void {
  if (!isBrowser) {
    return
  }

  window.localStorage.setItem(AUTH_STORAGE_KEY, JSON.stringify(session))
}

export function clearStoredAuthSession(): void {
  if (!isBrowser) {
    return
  }

  window.localStorage.removeItem(AUTH_STORAGE_KEY)
}
