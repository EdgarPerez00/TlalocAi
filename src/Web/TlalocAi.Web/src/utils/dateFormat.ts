const dateTimeFormatter = new Intl.DateTimeFormat('es-MX', {
  dateStyle: 'medium',
  timeStyle: 'short',
})

const timeFormatter = new Intl.DateTimeFormat('es-MX', {
  hour: '2-digit',
  minute: '2-digit',
})

export function formatDateTime(value?: string | null): string {
  if (!value) {
    return 'Sin dato'
  }

  return dateTimeFormatter.format(new Date(value))
}

export function formatShortTime(value?: string | null): string {
  if (!value) {
    return '--'
  }

  return timeFormatter.format(new Date(value))
}

export function toLocalDateTimeInputValue(date: Date): string {
  const offsetMs = date.getTimezoneOffset() * 60_000
  return new Date(date.getTime() - offsetMs).toISOString().slice(0, 16)
}

export function toUtcIsoOrUndefined(value: string): string | undefined {
  if (!value) {
    return undefined
  }

  return new Date(value).toISOString()
}
