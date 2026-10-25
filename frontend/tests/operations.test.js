import test from 'node:test';
import assert from 'node:assert/strict';
import {
  maintenanceAttention,
  buildCityOverview,
  prioritizeAlerts,
  normalizeAlerts,
  MAINTENANCE_LOOKAHEAD_DAYS,
} from '../src/services/operations.js';
import { markerPresentation, markerSymbols } from '../src/components/map/markerPresentation.js';
import {
  buildOperationalLayers,
  createEmptyFilters,
  neighborhoodFilterValue,
} from '../src/components/map/operationalFilters.js';
import { loadVerification } from '../src/services/verification.js';
const now = Date.parse('2026-10-23T12:00:00Z');
const eq = {
  id: 'eq',
  type: 'Camera',
  status: 'Operational',
  neighborhood: 'Sé',
  maintenanceNeed: 'Scheduled',
  nextMaintenanceDate: new Date(now + 7 * 86400000).toISOString(),
};
const alert = {
  id: 'a',
  equipmentId: 'eq',
  status: 'Open',
  severity: 'Critical',
  message: 'Falha observada',
  createdAt: '2026-10-23T12:00:00Z',
  type: 'Failure',
};
test('proximidade centralizada inclui limite de sete dias e vencidas sem mudar disponibilidade', () => {
  assert.equal(MAINTENANCE_LOOKAHEAD_DAYS, 7);
  assert.deepEqual(maintenanceAttention(eq, now), {
    immediate: false,
    upcoming: true,
    overdue: false,
    inProgress: false,
  });
  assert.equal(
    maintenanceAttention(
      { ...eq, nextMaintenanceDate: new Date(now + 7 * 86400000 + 1).toISOString() },
      now,
    ).upcoming,
    false,
  );
  assert.equal(
    maintenanceAttention({ ...eq, nextMaintenanceDate: '2026-10-20T12:00:00Z' }, now).overdue,
    true,
  );
  for (const value of [null, '', 'invalid'])
    assert.equal(maintenanceAttention({ ...eq, nextMaintenanceDate: value }, now).upcoming, false);
  assert.equal(maintenanceAttention({ ...eq, maintenanceNeed: 'None' }, now).upcoming, false);
  assert.equal(maintenanceAttention({ status: 'Malfunctioning' }, now).immediate, true);
});
test('consolidação por bairro soma estados existentes e mantém agendamento operacional', () => {
  const equipment = [
    eq,
    {
      ...eq,
      id: 'bad',
      status: 'Malfunctioning',
      neighborhood: ' SE ',
      maintenanceNeed: 'Immediate',
    },
    { ...eq, id: 'new', status: 'NotInstalled', neighborhood: null, maintenanceNeed: 'None' },
  ];
  const data = buildCityOverview(
    equipment,
    [
      alert,
      { ...alert, id: 'orphan', equipmentId: 'absent', severity: 'High' },
      { ...alert, id: 'resolved', status: 'Resolved' },
    ],
    now,
  );
  assert.equal(data.rows.length, 2);
  assert.deepEqual(data.rows[0], {
    key: 'se',
    label: 'Sé',
    total: 2,
    operational: 1,
    unavailable: 1,
    immediate: 1,
    upcoming: 1,
    planned: 1,
    inProgress: 0,
    critical: 1,
  });
  assert.equal(data.unlocatedAlerts, 1);
  assert.equal(data.rows[1].unavailable, 0);
  assert.equal(buildCityOverview([eq], null, now).rows[0].critical, null);
});
test('alertas priorizam severidade real antes de recência e excluem resolvidos', () => {
  const items = [
    { ...alert, id: 'low', severity: 'Low', createdAt: '2026-10-24T12:00:00Z' },
    alert,
    { ...alert, id: 'high', severity: 'High' },
    { ...alert, id: 'closed', status: 'Resolved' },
  ];
  assert.deepEqual(
    prioritizeAlerts(normalizeAlerts(items)).map((item) => item.id),
    ['a', 'high', 'low'],
  );
  assert.equal(items.length, 4);
  assert.throws(() => normalizeAlerts([{ ...alert, severity: 'Urgent' }]));
  assert.throws(() => normalizeAlerts([{ ...alert, createdAt: null }]));
  assert.ok(!Object.hasOwn(normalizeAlerts([{ ...alert, password: 'private' }])[0], 'password'));
});
test('oito tipos e três categorias têm símbolos diferentes; situação e manutenção não se confundem', () => {
  const types = [
    'Camera',
    'Radar',
    'Sensor',
    'Vehicle',
    'ElectricalScooter',
    'ParkingMeter',
    'Motorcycle',
    'AutonomousVehicle',
  ];
  const presentations = types.map((type) =>
    markerPresentation('equipment', type, { tone: 'operational' }),
  );
  assert.equal(new Set(presentations.map((item) => item.html)).size, 8);
  assert.equal(
    new Set(['TrafficAgent', 'MunicipalGuard', 'PoliceOfficer'].map((type) => markerSymbols[type]))
      .size,
    3,
  );
  const scheduled = markerPresentation('equipment', 'Camera', { tone: 'operational' }, 'upcoming');
  assert.match(scheduled.className, /state-operational attention-upcoming/);
  const unsafe = markerPresentation('equipment', '<script>', { tone: '" onclick="bad' }, 'bad');
  assert.match(unsafe.className, /type-Unknown state-unknown/);
  assert.ok(!unsafe.html.includes('<script>'));
  assert.ok(!unsafe.className.includes('onclick'));
});
test('bairro e categoria/disponibilidade de agentes combinam sem fabricar coordenadas', () => {
  const layers = buildOperationalLayers(
    [
      { id: 'equipments', data: 1, items: [{ ...eq, latitude: null, longitude: 0 }] },
      {
        id: 'agents',
        data: 3,
        items: [
          {
            id: 'a',
            role: 'PoliceOfficer',
            availability: 'Available',
            neighborhood: 'Sé',
            latitude: 0,
            longitude: 0,
          },
          {
            id: 'b',
            role: 'MunicipalGuard',
            availability: 'Available',
            neighborhood: 'Sé',
            latitude: 1,
            longitude: 1,
          },
          {
            id: 'c',
            role: 'PoliceOfficer',
            availability: 'Available',
            neighborhood: 'Mooca',
            latitude: 1,
            longitude: 1,
          },
        ],
      },
    ],
    {
      ...createEmptyFilters(),
      neighborhood: neighborhoodFilterValue('se'),
      agentRole: 'PoliceOfficer',
      agentAvailability: 'Available',
    },
  );
  assert.deepEqual(
    layers.agents.filtered.map((item) => item.id),
    ['a'],
  );
  assert.equal(layers.equipments.discarded, 1);
  assert.equal(layers.equipments.filtered.length, 0);
});
test('sincronização consulta ordens somente quando autorizada e não retém custos/agente/CPF', async () => {
  const calls = [];
  const client = {
    getJson: async (path) => {
      calls.push(path);
      if (path.startsWith('/api/maintenances'))
        return [{ id: 'order', equipmentId: 'eq', status: 'Open', cost: 100, agentId: 'private' }];
      if (path.startsWith('/api/alerts')) return [alert];
      return [];
    },
  };
  const central = await loadVerification('test', { client });
  assert.ok(!calls.some((path) => path.startsWith('/api/maintenances')));
  assert.equal(central.find((item) => item.id === 'alerts').items.length, 1);
  calls.length = 0;
  const manager = await loadVerification('test', { client, includeMaintenance: true });
  assert.equal(calls.filter((path) => path.startsWith('/api/maintenances')).length, 2);
  const order = manager.find((item) => item.id === 'maintenanceOpen').items[0];
  assert.ok(!Object.hasOwn(order, 'cost'));
  assert.ok(!Object.hasOwn(order, 'agentId'));
});
