import { http, invalidResponse } from './http.js'
import { normalizeHeatmapResponse } from './heatmap.js'

function dashboardSummary(data) {
  if (!data || !Number.isFinite(data.totalEquipments) || !Number.isFinite(data.activeAgents) ||
    !Number.isFinite(data.availabilityRatePercent) || !data.byStatus || Array.isArray(data.byStatus) ||
    typeof data.byStatus !== 'object' || !Object.values(data.byStatus).every(Number.isFinite)) {
    throw invalidResponse()
  }
  return data
}

function arrayCount(data) {
  if (!Array.isArray(data)) throw invalidResponse()
  return data.length
}

function selectFields(records, fields) {
  // Mantém apenas os dados operacionais necessários; CPF e atributos extras não são retidos.
  return records.map((record) => Object.fromEntries(fields.map((field) => [field, record?.[field]])))
}

export const verificationEndpoints = [
  { id: 'dashboard', label: 'Dashboard', path: '/api/dashboard', summarize: dashboardSummary },
  { id: 'equipments', label: 'Equipamentos', path: '/api/equipments', summarize: arrayCount,
    selectItems: (records) => selectFields(records, ['id', 'type', 'brand', 'model', 'serialNumber', 'status', 'neighborhood', 'latitude', 'longitude']) },
  { id: 'agents', label: 'Agentes', path: '/api/agents', summarize: arrayCount,
    selectItems: (records) => selectFields(records, ['id', 'name', 'badgeNumber', 'role', 'availability', 'neighborhood', 'latitude', 'longitude', 'locationUpdatedAt']) },
  { id: 'heatmap', label: 'Pontos do heatmap (Incidents)', path: '/api/heatmap?metric=Incidents' },
]

export async function loadVerification(token, { signal, client = http } = {}) {
  const responses = await Promise.allSettled(verificationEndpoints.map(async (endpoint) => {
    const data = await client.getJson(endpoint.path, { token, signal })
    if (endpoint.id === 'heatmap') {
      const heatmap = normalizeHeatmapResponse(data, 'Incidents')
      return { data: heatmap.totalPoints, heatmap }
    }
    const summary = endpoint.summarize(data)
    return { data: summary, ...(endpoint.selectItems ? { items: endpoint.selectItems(data) } : {}) }
  }))
  signal?.throwIfAborted()
  return responses.map((response, index) => ({
    id: verificationEndpoints[index].id,
    label: verificationEndpoints[index].label,
    path: verificationEndpoints[index].path,
    ...(response.status === 'fulfilled' ? response.value : { error: response.reason }),
  }))
}
