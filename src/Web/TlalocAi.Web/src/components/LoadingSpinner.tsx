interface LoadingSpinnerProps {
  message?: string
  fullPage?: boolean
}

function LoadingSpinner({ message = 'Cargando...', fullPage = false }: LoadingSpinnerProps) {
  return (
    <div
      className={`d-flex flex-column align-items-center justify-content-center gap-3 ${
        fullPage ? 'min-vh-100' : 'py-5'
      }`}
    >
      <div className="spinner-border text-info" role="status" aria-hidden="true" />
      <p className="text-secondary mb-0">{message}</p>
    </div>
  )
}

export default LoadingSpinner
