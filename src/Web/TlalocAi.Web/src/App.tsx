import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import ProtectedRoute from './components/ProtectedRoute'
import { AuthProvider } from './contexts/AuthContext'
import AuthLayout from './layouts/AuthLayout'
import MainLayout from './layouts/MainLayout'
import AnalyticsPage from './pages/AnalyticsPage'
import ControlPage from './pages/ControlPage'
import DashboardPage from './pages/DashboardPage'
import DeviceDetailPage from './pages/DeviceDetailPage'
import DevicesPage from './pages/DevicesPage'
import ExperimentDetailPage from './pages/ExperimentDetailPage'
import ExperimentsPage from './pages/ExperimentsPage'
import LoginPage from './pages/LoginPage'
import NotFoundPage from './pages/NotFoundPage'
import TelemetryPage from './pages/TelemetryPage'

function App() {
  return (
    <BrowserRouter>
      <AuthProvider>
        <Routes>
          <Route element={<AuthLayout />}>
            <Route path="/login" element={<LoginPage />} />
          </Route>
          <Route element={<ProtectedRoute />}>
            <Route element={<MainLayout />}>
              <Route path="/" element={<Navigate to="/dashboard" replace />} />
              <Route path="/dashboard" element={<DashboardPage />} />
              <Route path="/devices" element={<DevicesPage />} />
              <Route path="/devices/:deviceId" element={<DeviceDetailPage />} />
              <Route path="/experiments" element={<ExperimentsPage />} />
              <Route path="/experiments/:experimentId" element={<ExperimentDetailPage />} />
              <Route path="/control" element={<ControlPage />} />
              <Route path="/telemetry" element={<TelemetryPage />} />
              <Route path="/analytics" element={<AnalyticsPage />} />
            </Route>
          </Route>
          <Route path="*" element={<NotFoundPage />} />
        </Routes>
      </AuthProvider>
    </BrowserRouter>
  )
}

export default App
