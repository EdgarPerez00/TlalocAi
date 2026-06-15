import { Outlet } from 'react-router-dom'
import BrandMark from '../components/BrandMark'

function AuthLayout() {
  return (
    <main className="container py-5 page-section">
      <div className="row justify-content-center">
        <div className="col-lg-10">
          <div className="card surface-card border-0 overflow-hidden">
            <div className="row g-0">
              <div className="col-lg-5 hero-panel p-4 p-lg-5 d-flex flex-column justify-content-between">
                <div>
                  <BrandMark className="mb-4" />
                  <h1 className="display-6 fw-semibold">TlalocAI</h1>
                  <p className="mb-0 text-white-50">
                    Monitoreo de flujo, nivel y actuadores.
                  </p>
                </div>
                <div className="small text-white-50 mt-4">
                  ESCOM - IPN &copy; 2026. Equipo 1.
                </div>
              </div>
              <div className="col-lg-7 p-4 p-lg-5">
                <Outlet />
              </div>
            </div>
          </div>
        </div>
      </div>
    </main>
  )
}

export default AuthLayout
