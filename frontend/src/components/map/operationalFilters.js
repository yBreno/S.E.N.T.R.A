import { agentStates, equipmentStates, prepareMarkers } from './markerData.js'

export const UNKNOWN_FILTER_VALUE = '__unknown__'
export const equipmentTypes = Object.freeze({
  Camera: 'Câmera', Radar: 'Radar', Sensor: 'Sensor', Vehicle: 'Veículo',
  ElectricalScooter: 'Patinete elétrico', ParkingMeter: 'Parquímetro',
  Motorcycle: 'Moto', AutonomousVehicle: 'Veículo autônomo',
})

export function createEmptyFilters() {
  return { equipmentType: '', equipmentStatus: '', neighborhood: '', agentAvailability: '' }
}

export function enumFilterValue(value, knownValues) {
  const normalized = typeof value === 'string' ? value.trim() : ''
  return Object.hasOwn(knownValues, normalized) ? normalized : UNKNOWN_FILTER_VALUE
}

export function neighborhoodFilterValue(value) {
  if (typeof value !== 'string' || !value.trim()) return UNKNOWN_FILTER_VALUE
  return `neighborhood:${value.trim().replace(/\s+/g, ' ').normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLocaleLowerCase('pt-BR')}`
}

export function equipmentTypeLabel(value) {
  const normalized = enumFilterValue(value, equipmentTypes)
  return normalized === UNKNOWN_FILTER_VALUE ? 'Tipo não informado ou desconhecido' : equipmentTypes[normalized]
}

export function getNeighborhoodOptions(records) {
  const options = new Map()
  for (const record of records) {
    const value = neighborhoodFilterValue(record?.neighborhood)
    if (!options.has(value)) options.set(value, value === UNKNOWN_FILTER_VALUE ? 'Bairro não informado' : record.neighborhood.trim().replace(/\s+/g, ' '))
  }
  return Array.from(options, ([value, label]) => ({ value, label })).sort((a, b) => a.label.localeCompare(b.label, 'pt-BR'))
}

export function filterEquipmentMarkers(markers, filters) {
  return markers.filter((record) =>
    (!filters.equipmentType || enumFilterValue(record.type, equipmentTypes) === filters.equipmentType) &&
    (!filters.equipmentStatus || enumFilterValue(record.status, equipmentStates) === filters.equipmentStatus) &&
    (!filters.neighborhood || neighborhoodFilterValue(record.neighborhood) === filters.neighborhood))
}

export function filterAgentMarkers(markers, filters) {
  return markers.filter((record) => !filters.agentAvailability || enumFilterValue(record.availability, agentStates) === filters.agentAvailability)
}

export function buildOperationalLayers(results, filters) {
  function layer(id, applyFilters) {
    const result = results.find((item) => item.id === id)
    const records = result?.items || []
    const prepared = prepareMarkers(records)
    return {
      ...prepared,
      filtered: applyFilters(prepared.markers, filters),
      total: result?.data ?? null,
      hasData: Array.isArray(result?.items),
      stale: Boolean(result?.stale),
      receivedAt: result?.receivedAt,
      error: result?.error?.message,
      ...(id === 'equipments' ? { neighborhoodOptions: getNeighborhoodOptions(records) } : {}),
    }
  }
  return { equipments: layer('equipments', filterEquipmentMarkers), agents: layer('agents', filterAgentMarkers) }
}

export function getLayerCounts(layer, enabled) {
  if (!layer.hasData) return { received: null, valid: null, filtered: null, discarded: null, visible: null }
  return { received: layer.total, valid: layer.markers.length, filtered: layer.filtered.length,
    discarded: layer.discarded, visible: enabled ? layer.filtered.length : 0 }
}

export function resolveMarkerSelection(selection, layers, visibility) {
  if (!selection) return null
  const layer = selection.kind === 'equipment' ? (visibility.showEquipments ? layers.equipments : null) :
    selection.kind === 'agent' ? (visibility.showAgents ? layers.agents : null) : null
  const record = layer?.filtered.find((item) => item.id === selection.id)
  return record ? { kind: selection.kind, record, stale: layer.stale, receivedAt: layer.receivedAt } : null
}
