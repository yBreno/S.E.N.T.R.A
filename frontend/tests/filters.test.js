import test from 'node:test'
import assert from 'node:assert/strict'
import {
  createEmptyFilters, buildOperationalLayers, getLayerCounts, UNKNOWN_FILTER_VALUE,
  neighborhoodFilterValue, getNeighborhoodOptions, resolveMarkerSelection,
} from '../src/components/map/operationalFilters.js'

const record = (id, values = {}) => ({ id, type: 'Camera', status: 'Operational', neighborhood: 'Centro', latitude: -23.55, longitude: -46.63, ...values })
const equipmentItems = [record('1'), record('2', { type: 'Radar', status: 'Malfunctioning', neighborhood: 'Mooca' }),
  record('3', { type: 'Camera', neighborhood: 'Mooca' }), record('4', { latitude: null }),
  record('1'), record('5', { type: null, status: { toString: null }, neighborhood: null })]
const agentItems = [record('a', { availability: 'Available' }), record('b', { availability: 'Busy' }),
  record('c', { availability: 'Offline' }), record('d', { availability: 'Other' }), record('bad', { latitude: 91, availability: 'Available' })]
const results = [
  { id: 'equipments', data: equipmentItems.length, items: equipmentItems },
  { id: 'agents', data: agentItems.length, items: agentItems },
]
const build = (filters = {}) => buildOperationalLayers(results, { ...createEmptyFilters(), ...filters })

test('filtros vazios mantêm todos os marcadores válidos e contadores independentes', () => {
  const layers = build()
  assert.deepEqual(getLayerCounts(layers.equipments, true), { received: 6, valid: 4, filtered: 4, discarded: 2, visible: 4 })
  assert.deepEqual(getLayerCounts(layers.agents, true), { received: 5, valid: 4, filtered: 4, discarded: 1, visible: 4 })
})

test('tipo, status e bairro se combinam por interseção somente em equipamentos', () => {
  const layers = build({ equipmentType: 'Camera', equipmentStatus: 'Operational', neighborhood: neighborhoodFilterValue('MOOCA') })
  assert.deepEqual(layers.equipments.filtered.map((item) => item.id), ['3'])
  assert.equal(layers.agents.filtered.length, 4)
  assert.deepEqual(getLayerCounts(layers.equipments, true), { received: 6, valid: 4, filtered: 1, discarded: 2, visible: 1 })
})

test('disponibilidade filtra agentes sem modificar equipamentos ou a entrada', () => {
  const original = structuredClone(results)
  for (const [value, id] of [['Available', 'a'], ['Busy', 'b'], ['Offline', 'c']]) {
    const layers = build({ agentAvailability: value })
    assert.deepEqual(layers.agents.filtered.map((item) => item.id), [id])
    assert.equal(layers.equipments.filtered.length, 4)
  }
  assert.deepEqual(results, original)
})

test('limpar filtros restaura todos os registros válidos sem alterar camadas', () => {
  const filters = { ...createEmptyFilters(), equipmentStatus: 'Stolen', agentAvailability: 'Available' }
  assert.equal(build(filters).equipments.filtered.length, 0)
  assert.deepEqual(createEmptyFilters(), { equipmentType: '', equipmentStatus: '', neighborhood: '', agentAvailability: '' })
  assert.equal(build(createEmptyFilters()).equipments.filtered.length, 4)
  assert.equal(build(createEmptyFilters()).agents.filtered.length, 4)
})

test('valores desconhecidos podem ser filtrados sem quebrar a renderização', () => {
  const layers = build({ equipmentType: UNKNOWN_FILTER_VALUE, equipmentStatus: UNKNOWN_FILTER_VALUE,
    neighborhood: UNKNOWN_FILTER_VALUE, agentAvailability: UNKNOWN_FILTER_VALUE })
  assert.deepEqual(layers.equipments.filtered.map((item) => item.id), ['5'])
  assert.deepEqual(layers.agents.filtered.map((item) => item.id), ['d'])
})

test('bairros normalizam espaços, caixa e acentos sem colidir com desconhecidos', () => {
  assert.equal(neighborhoodFilterValue(' São   José '), neighborhoodFilterValue('sao jose'))
  assert.notEqual(neighborhoodFilterValue('__unknown__'), UNKNOWN_FILTER_VALUE)
  const options = getNeighborhoodOptions([{ neighborhood: ' São José ' }, { neighborhood: 'SAO JOSE' }, { neighborhood: {} }])
  assert.equal(options.length, 2)
  assert.ok(options.some((option) => option.value === UNKNOWN_FILTER_VALUE))
})

test('arrays vazios têm contadores zero e não criam marcadores', () => {
  const empty = buildOperationalLayers([{ id: 'equipments', data: 0, items: [] }, { id: 'agents', data: 0, items: [] }], createEmptyFilters())
  assert.deepEqual(getLayerCounts(empty.equipments, true), { received: 0, valid: 0, filtered: 0, discarded: 0, visible: 0 })
  assert.equal(empty.agents.filtered.length, 0)
})

test('ausência de resposta não é apresentada como total zero', () => {
  const empty = buildOperationalLayers([], createEmptyFilters())
  assert.deepEqual(getLayerCounts(empty.equipments, true), { received: null, valid: null, filtered: null, discarded: null, visible: null })
})

test('marcadores inválidos continuam descartados mesmo correspondendo aos filtros', () => {
  const layers = build({ equipmentType: 'Camera', neighborhood: neighborhoodFilterValue('Centro') })
  assert.deepEqual(layers.equipments.filtered.map((item) => item.id), ['1'])
  assert.equal(layers.equipments.discarded, 2)
})

test('desligar uma camada zera somente a quantidade na camada', () => {
  const layers = build({ equipmentType: 'Camera' })
  assert.deepEqual(getLayerCounts(layers.equipments, false), { received: 6, valid: 4, filtered: 2, discarded: 2, visible: 0 })
  assert.equal(getLayerCounts(layers.agents, true).visible, 4)
})

test('seleção só resolve para registros ainda presentes na camada filtrada e habilitada', () => {
  const selection = { kind: 'equipment', id: '1' }
  const visibility = { showEquipments: true, showAgents: true }
  assert.equal(resolveMarkerSelection(selection, build(), visibility).record.id, '1')
  assert.equal(resolveMarkerSelection(selection, build({ equipmentType: 'Radar' }), visibility), null)
  assert.equal(resolveMarkerSelection(selection, build(), { ...visibility, showEquipments: false }), null)
  assert.equal(resolveMarkerSelection({ kind: '__proto__', id: '1' }, build(), visibility), null)
  assert.equal(resolveMarkerSelection({ kind: 'agent', id: 'a' }, build({ agentAvailability: 'Offline' }), visibility), null)
})

test('filtros são cálculos locais e repetíveis sem qualquer fetch', () => {
  // O módulo recebe somente arrays e filtros; nenhuma dependência de transporte é necessária.
  for (const equipmentStatus of ['', 'Operational', 'Malfunctioning', 'Stolen', UNKNOWN_FILTER_VALUE]) {
    const layers = build({ equipmentStatus })
    assert.ok(layers.equipments.filtered.length <= layers.equipments.markers.length)
    assert.equal(layers.equipments.total, equipmentItems.length)
  }
})
