interface StatCardProps {
  title: string
  value: string
  caption?: string
}

function StatCard({ title, value, caption }: StatCardProps) {
  return (
    <div className="card surface-card stat-card border-0 h-100">
      <div className="card-body">
        <div className="metric-label mb-2">{title}</div>
        <div className="metric-value mb-1">{value}</div>
        {caption ? <div className="text-secondary small">{caption}</div> : null}
      </div>
    </div>
  )
}

export default StatCard
