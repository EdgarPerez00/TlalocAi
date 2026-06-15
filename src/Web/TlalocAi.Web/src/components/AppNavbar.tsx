import { NavLink } from 'react-router-dom'
import { useAuth } from '../contexts/AuthContext'

const navItems = [
  { to: '/dashboard', label: 'Dashboard' },
  { to: '/devices', label: 'Dispositivos' },
  { to: '/experiments', label: 'Experimentos' },
  { to: '/monitoring', label: 'Monitoreo' },
  { to: '/control', label: 'Control' },
  { to: '/telemetry', label: 'Telemetría' },
  { to: '/analytics', label: 'Estadísticas' },
]

function AppNavbar() {
  const { logout, user } = useAuth()

  return (
    <nav className="navbar navbar-expand-lg navbar-dark navbar-blur sticky-top border-bottom border-info-subtle">
      <div className="container-fluid px-4">
        <NavLink to="/dashboard" className="navbar-brand d-flex align-items-center gap-3">
          <span className="brand-mark">TA</span>
          <span className="fw-semibold">TlalocAI</span>
        </NavLink>

        <button
          className="navbar-toggler"
          type="button"
          data-bs-toggle="collapse"
          data-bs-target="#mainNavbar"
          aria-controls="mainNavbar"
          aria-expanded="false"
          aria-label="Alternar navegación"
        >
          <span className="navbar-toggler-icon" />
        </button>

        <div className="collapse navbar-collapse" id="mainNavbar">
          <div className="navbar-nav ms-auto me-lg-3">
            {navItems.map((item) => (
              <NavLink
                key={item.to}
                to={item.to}
                className={({ isActive }) =>
                  `nav-link px-3 rounded-pill ${isActive ? 'active bg-info bg-opacity-25 text-white' : ''}`
                }
              >
                {item.label}
              </NavLink>
            ))}
          </div>

          <div className="d-flex align-items-center gap-3 mt-3 mt-lg-0">
            <div className="text-end text-white-50 small">
              <div>{user?.fullName}</div>
              <div>{user?.role}</div>
            </div>
            <button type="button" className="btn btn-outline-light btn-sm" onClick={logout}>
              Cerrar sesión
            </button>
          </div>
        </div>
      </div>
    </nav>
  )
}

export default AppNavbar
