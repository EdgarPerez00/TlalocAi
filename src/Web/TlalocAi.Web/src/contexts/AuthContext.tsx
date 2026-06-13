import {
  createContext,
  useContext,
  useEffect,
  useState,
  type PropsWithChildren,
} from 'react'
import * as authApi from '../api/authApi'
import { configureApiClient } from '../api/apiClient'
import type { LoginRequest, RegisterRequest, UserDto } from '../types/auth'
import {
  clearStoredAuthSession,
  getStoredAuthSession,
  setStoredAuthSession,
  type StoredAuthSession,
} from '../utils/storage'

interface AuthContextValue {
  user: UserDto | null
  token: string | null
  isAuthenticated: boolean
  isInitializing: boolean
  login: (data: LoginRequest) => Promise<void>
  register: (data: RegisterRequest) => Promise<void>
  logout: () => void
}

const AuthContext = createContext<AuthContextValue | undefined>(undefined)

function isSessionExpired(expiresAtUtc: string): boolean {
  return new Date(expiresAtUtc).getTime() <= Date.now()
}

function AuthProvider({ children }: PropsWithChildren) {
  const [session, setSession] = useState<StoredAuthSession | null>(null)
  const [isInitializing, setIsInitializing] = useState(true)

  const persistSession = (nextSession: StoredAuthSession) => {
    setSession(nextSession)
    setStoredAuthSession(nextSession)
  }

  function clearSessionAndRedirect() {
    clearStoredAuthSession()
    setSession(null)
    setIsInitializing(false)

    if (typeof window !== 'undefined' && window.location.pathname !== '/login') {
      window.location.replace('/login')
    }
  }

  useEffect(() => {
    configureApiClient({
      getAccessToken: () => session?.accessToken ?? null,
      onUnauthorized: clearSessionAndRedirect,
    })
  }, [session?.accessToken])

  useEffect(() => {
    const storedSession = getStoredAuthSession()

    if (!storedSession || isSessionExpired(storedSession.expiresAtUtc)) {
      clearStoredAuthSession()
      setIsInitializing(false)
      return
    }

    setSession(storedSession)

    authApi
      .getMe()
      .then((user) => {
        persistSession({
          ...storedSession,
          user,
        })
      })
      .catch(() => {
        clearSessionAndRedirect()
      })
      .finally(() => {
        setIsInitializing(false)
      })
  }, [])

  async function login(data: LoginRequest) {
    const response = await authApi.login(data)

    persistSession({
      accessToken: response.accessToken,
      expiresAtUtc: response.expiresAtUtc,
      user: response.user,
    })
  }

  async function register(data: RegisterRequest) {
    const response = await authApi.register(data)

    persistSession({
      accessToken: response.accessToken,
      expiresAtUtc: response.expiresAtUtc,
      user: response.user,
    })
  }

  function logout() {
    clearStoredAuthSession()
    setSession(null)

    if (typeof window !== 'undefined' && window.location.pathname !== '/login') {
      window.location.replace('/login')
    }
  }

  const value: AuthContextValue = {
    user: session?.user ?? null,
    token: session?.accessToken ?? null,
    isAuthenticated: Boolean(session?.accessToken),
    isInitializing,
    login,
    register,
    logout,
  }

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

function useAuth() {
  const context = useContext(AuthContext)

  if (!context) {
    throw new Error('useAuth must be used within AuthProvider')
  }

  return context
}

export { AuthProvider, useAuth }
