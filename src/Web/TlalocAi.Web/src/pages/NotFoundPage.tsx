import { Link } from 'react-router-dom'
import BrandMark from '../components/BrandMark'

function NotFoundPage() {
  return (
    <main className="container py-5">
      <div className="card surface-card border-0 text-center mx-auto" style={{ maxWidth: '560px' }}>
        <div className="card-body py-5">
          <BrandMark className="mx-auto mb-3" />
          <h1 className="h3 mb-2">Página no encontrada</h1>
          <p className="text-secondary mb-4">La ruta solicitada no existe dentro de TlalocAI.</p>
          <Link to="/dashboard" className="btn btn-primary">
            Ir al dashboard
          </Link>
        </div>
      </div>
    </main>
  )
}

export default NotFoundPage
