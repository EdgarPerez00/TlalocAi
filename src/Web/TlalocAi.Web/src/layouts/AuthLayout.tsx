import { Outlet } from 'react-router-dom'

function AuthLayout() {
  return (
    <main className="container py-5 page-section">
      <div className="row justify-content-center">
        <div className="col-lg-10">
          <div className="card surface-card border-0 overflow-hidden">
            <div className="row g-0">
              <div className="col-lg-5 hero-panel p-4 p-lg-5 d-flex flex-column justify-content-between">
                <div>
                  <div className="brand-mark mb-4">TA</div>
                  <h1 className="display-6 fw-semibold">TlalocAI Platform</h1>
                  <p className="mb-0 text-white-50">
                    Monitoreo de flujo, nivel y actuadores para el modelo a escala de calle.
                  </p>
                </div>
                <div className="small text-white-50 mt-4">
                  Frontend React + Bootstrap conectado al Gateway de TlalocAi.Platform.
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
