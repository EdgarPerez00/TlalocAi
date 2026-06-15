import { useState } from 'react'
import { Navigate } from 'react-router-dom'
import { getApiErrorMessage } from '../api/apiClient'
import { useAuth } from '../contexts/AuthContext'

const initialRegisterForm = {
  fullName: '',
  email: '',
  password: '',
}

function LoginPage() {
  const { isAuthenticated, login, register } = useAuth()
  const [mode, setMode] = useState<'login' | 'register'>('login')
  const [loginForm, setLoginForm] = useState({ email: '', password: '' })
  const [registerForm, setRegisterForm] = useState(initialRegisterForm)
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)

  if (isAuthenticated) {
    return <Navigate to="/dashboard" replace />
  }

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setIsSubmitting(true)
    setError(null)

    try {
      if (mode === 'login') {
        await login(loginForm)
      } else {
        await register(registerForm)
      }
    } catch (submitError) {
      setError(getApiErrorMessage(submitError, 'No se pudo conectar con el backend'))
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <div className="page-section">
      <div className="d-flex justify-content-between align-items-center mb-4">
        <div>
          <h2 className="h3 mb-1">{mode === 'login' ? 'Iniciar sesión' : 'Registrar usuario'}</h2>
          <p className="text-secondary mb-0">Usa tu usuario y contraseña.</p>
        </div>
        <div className="btn-group">
          <button
            type="button"
            className={`btn btn-sm ${mode === 'login' ? 'btn-primary' : 'btn-outline-primary'}`}
            onClick={() => setMode('login')}
          >
            Login
          </button>
          <button
            type="button"
            className={`btn btn-sm ${mode === 'register' ? 'btn-primary' : 'btn-outline-primary'}`}
            onClick={() => setMode('register')}
          >
            Registro
          </button>
        </div>
      </div>

      {error ? <div className="alert alert-danger">{error}</div> : null}

      <form onSubmit={handleSubmit} className="d-grid gap-3">
        {mode === 'register' ? (
          <div>
            <label htmlFor="fullName" className="form-label">
              Nombre completo
            </label>
            <input
              id="fullName"
              className="form-control"
              value={registerForm.fullName}
              onChange={(event) => setRegisterForm((current) => ({ ...current, fullName: event.target.value }))}
              required
            />
          </div>
        ) : null}

        <div>
          <label htmlFor="email" className="form-label">
            Email
          </label>
          <input
            id="email"
            type="email"
            className="form-control"
            value={mode === 'login' ? loginForm.email : registerForm.email}
            onChange={(event) =>
              mode === 'login'
                ? setLoginForm((current) => ({ ...current, email: event.target.value }))
                : setRegisterForm((current) => ({ ...current, email: event.target.value }))
            }
            required
          />
        </div>

        <div>
          <label htmlFor="password" className="form-label">
            Contraseña
          </label>
          <input
            id="password"
            type="password"
            className="form-control"
            value={mode === 'login' ? loginForm.password : registerForm.password}
            onChange={(event) =>
              mode === 'login'
                ? setLoginForm((current) => ({ ...current, password: event.target.value }))
                : setRegisterForm((current) => ({ ...current, password: event.target.value }))
            }
            required
          />
        </div>

        <button type="submit" className="btn btn-primary" disabled={isSubmitting}>
          {isSubmitting ? 'Procesando...' : mode === 'login' ? 'Entrar' : 'Crear cuenta'}
        </button>
      </form>
    </div>
  )
}

export default LoginPage
