export const heatmapMetrics = { Incidents: 'Ocorrências', Failures: 'Falhas', Risk: 'Risco regional' }
export const incidentTypes = {
  TrafficAccident: 'Acidente de trânsito', Congestion: 'Congestionamento', EquipmentFailure: 'Falha de equipamento',
  Vandalism: 'Vandalismo', Flooding: 'Alagamento', PublicSafety: 'Segurança pública', Other: 'Outra ocorrência',
}
export const weekDays = { Sunday: 'Domingo', Monday: 'Segunda-feira', Tuesday: 'Terça-feira', Wednesday: 'Quarta-feira',
  Thursday: 'Quinta-feira', Friday: 'Sexta-feira', Saturday: 'Sábado' }

export function createHeatmapFilters() {
  return { metric: 'Incidents', from: '', to: '', fromHour: '', toHour: '', dayOfWeek: '', neighborhood: '', type: '' }
}

function localDate(value, label) {
  if (!value) return null
  const match = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})(?::(\d{2}))?$/.exec(value)
  if (!match) throw new Error(`${label}: informe uma data e hora válidas.`)
  const [, year, month, day, hour, minute, second = '0'] = match
  const date = new Date(value)
  if (+year < 1 || !Number.isFinite(date.getTime()) || date.getFullYear() !== +year || date.getMonth() !== +month - 1 ||
    date.getDate() !== +day || date.getHours() !== +hour || date.getMinutes() !== +minute || date.getSeconds() !== +second) {
    throw new Error(`${label}: informe uma data e hora válidas no fuso do navegador.`)
  }
  return date.toISOString()
}

export function buildHeatmapQuery(filters) {
  try {
    if (!Object.hasOwn(heatmapMetrics, filters.metric)) throw new Error('Selecione uma métrica válida.')
    const params = new URLSearchParams({ metric: filters.metric })
    const to = localDate(filters.to, filters.metric === 'Risk' ? 'Referência' : 'Fim')
    if (filters.metric !== 'Risk') {
      const from = localDate(filters.from, 'Início')
      if (from && Date.parse(from) > (to ? Date.parse(to) : Date.now())) throw new Error('A data inicial deve ser anterior ou igual à data final.')
      if (from) params.set('from', from)
      if (to) params.set('to', to)
      const fromHour = String(filters.fromHour ?? '').trim()
      const toHour = String(filters.toHour ?? '').trim()
      if (Boolean(fromHour) !== Boolean(toHour)) throw new Error('Informe hora inicial e hora final juntas.')
      if (fromHour && toHour) {
        if (![fromHour, toHour].every((value) => /^\d{1,2}$/.test(value) && +value >= 0 && +value <= 23)) {
          throw new Error('As horas devem ser inteiras entre 0 e 23.')
        }
        params.set('fromHour', String(+fromHour))
        params.set('toHour', String(+toHour))
      }
      if (filters.dayOfWeek) {
        if (!Object.hasOwn(weekDays, filters.dayOfWeek)) throw new Error('Selecione um dia da semana válido.')
        params.set('dayOfWeek', filters.dayOfWeek)
      }
    } else if (to) params.set('to', to)
    const neighborhood = typeof filters.neighborhood === 'string' ? filters.neighborhood.trim() : ''
    if (neighborhood) params.set('neighborhood', neighborhood)
    if (filters.metric === 'Incidents' && filters.type) {
      if (!Object.hasOwn(incidentTypes, filters.type)) throw new Error('Selecione um tipo de ocorrência válido.')
      params.set('type', filters.type)
    }
    const path = `/api/heatmap?${params.toString()}`
    return { query: { key: path, path, metric: filters.metric }, error: null }
  } catch (error) {
    return { query: null, error: error.message }
  }
}

export const defaultHeatmapQuery = buildHeatmapQuery(createHeatmapFilters()).query
