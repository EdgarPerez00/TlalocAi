import type { ReactNode } from 'react'

interface EmptyStateProps {
  title: string
  message: string
  action?: ReactNode
}

function EmptyState({ title, message, action }: EmptyStateProps) {
  return (
    <div className="card surface-card border-0">
      <div className="card-body text-center py-5">
        <div className="brand-mark mx-auto mb-3">TA</div>
        <h3 className="h5">{title}</h3>
        <p className="text-secondary mb-3">{message}</p>
        {action}
      </div>
    </div>
  )
}

export default EmptyState
