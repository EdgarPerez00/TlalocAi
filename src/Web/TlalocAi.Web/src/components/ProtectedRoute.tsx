import { Navigate, Outlet, useLocation } from 'react-router-dom'
import { useAuth } from '../contexts/AuthContext'
import LoadingSpinner from './LoadingSpinner'

function ProtectedRoute() {
  const { isAuthenticated, isInitializing } = useAuth()
  const location = useLocation()

  if (isInitializing) {
    return <LoadingSpinner fullPage message="Verificando sesión..." />
  }

  if (!isAuthenticated) {
    return <Navigate to="/login" replace state={{ from: location.pathname }} />
  }

  return <Outlet />
}

export default ProtectedRoute
