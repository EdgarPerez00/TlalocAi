const decimalFormatter = new Intl.NumberFormat('es-MX', {
  maximumFractionDigits: 2,
  minimumFractionDigits: 0,
})

export function formatFlow(value?: number | null): string {
  return `${decimalFormatter.format(value ?? 0)} L/min`
}

export function formatLiters(value?: number | null): string {
  return `${decimalFormatter.format(value ?? 0)} L`
}

export function formatInteger(value?: number | null): string {
  return decimalFormatter.format(value ?? 0)
}

export function formatDurationSeconds(value?: number | null): string {
  const totalSeconds = value ?? 0
  const hours = Math.floor(totalSeconds / 3600)
  const minutes = Math.floor((totalSeconds % 3600) / 60)

  if (hours > 0) {
    return `${hours} h ${minutes} min`
  }

  return `${minutes} min`
}
