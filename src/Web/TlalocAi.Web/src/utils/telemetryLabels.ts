export function formatSensorName(name: string): string {
  const normalized = name.toLowerCase()

  if (normalized.startsWith('tower_level_')) {
    return `Torre nivel ${suffixNumber(name)}`
  }

  if (normalized.startsWith('cistern_level_')) {
    return `Cisterna nivel ${suffixNumber(name)}`
  }

  if (normalized.startsWith('container_') && normalized.endsWith('_full')) {
    return `Recipiente ${suffixNumber(name.replace('_full', ''))} lleno`
  }

  if (normalized === 'flow_no_flow_alert') {
    return 'Alerta sin flujo'
  }

  return name
}

export function formatActuatorName(name: string): string {
  const normalized = name.toLowerCase()

  if (normalized === 'pump_tower' || normalized === 'tower') {
    return 'Bomba de torre'
  }

  if (normalized === 'pump_cistern' || normalized === 'cistern') {
    return 'Bomba de cisterna'
  }

  if (normalized === 'pump') {
    return 'Bomba'
  }

  if (normalized.startsWith('valve_')) {
    return `Valvula ${suffixNumber(name)}`
  }

  return name
}

function suffixNumber(value: string): string {
  const match = value.match(/(\d+)$/)
  return match?.[1] ?? value
}
