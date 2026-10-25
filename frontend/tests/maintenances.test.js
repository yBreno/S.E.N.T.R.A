import test from 'node:test';
import assert from 'node:assert/strict';
import { canMaintain } from '../src/services/auth.js';
import {
  createMaintenanceApi,
  filterMaintenances,
  maintenanceError,
} from '../src/services/maintenances.js';
const order = {
  id: 'order-id',
  equipmentId: 'equipment-id',
  kind: 'Preventive',
  status: 'Scheduled',
  description: 'Inspeção',
  initDate: null,
  scheduledDate: '2027-01-01T12:00:00Z',
};
const session = (role) => ({ token: 'synthetic-test-token', user: { role } });

test('permissões de manutenção preservam Admin, gestor e técnico sem conceder acesso ao operador ou agente', () => {
  for (const role of ['Admin', 'OperationManager', 'MaintenanceTechnician'])
    assert.equal(canMaintain({ role }), true);
  for (const role of ['CentralOperator', 'Agent', undefined])
    assert.equal(canMaintain({ role }), false);
});
test('cliente bloqueia todas as operações de manutenção antes de enviar HTTP para perfil sem acesso', async () => {
  let calls = 0;
  const client = {
    getJson: () => {
      calls++;
    },
    postJson: () => {
      calls++;
    },
  };
  for (const role of ['Agent', 'CentralOperator']) {
    const api = createMaintenanceApi(session(role), client);
    for (const action of [
      () => api.list(),
      () => api.detail('id'),
      () => api.create({}, true),
      () => api.create({}, false),
      () => api.start('id'),
      () => api.close('id', {}),
    ])
      await assert.rejects(action);
  }
  assert.equal(calls, 0);
});
test('cliente integra rotas reais, token, validação de campos, abort e identificadores escapados', async () => {
  const calls = [];
  const client = {
    getJson: async (path, options) => {
      calls.push({ path, options });
      return path.includes('/id') ? order : [order];
    },
    postJson: async (path, body, options) => {
      calls.push({ path, body, options });
      return order;
    },
  };
  const api = createMaintenanceApi(session('MaintenanceTechnician'), client);
  const signal = new AbortController().signal;
  await api.list('equipment/id', signal);
  await api.detail('id', signal);
  const plan = {
    equipmentId: 'equipment-id',
    description: 'Inspeção',
    scheduledDate: order.scheduledDate,
  };
  await api.create(plan, true, signal);
  await api.create(plan, false, signal);
  await api.start('order/id', signal);
  await api.close('order/id', { cost: 123 }, signal);
  assert.deepEqual(
    calls.map((call) => call.path),
    [
      '/api/maintenances?equipmentId=equipment%2Fid',
      '/api/maintenances/id',
      '/api/maintenances/schedule',
      '/api/maintenances',
      '/api/maintenances/order%2Fid/start',
      '/api/maintenances/order%2Fid/close',
    ],
  );
  assert.deepEqual(calls[2].body, plan);
  assert.deepEqual(calls[4].body, {});
  for (const call of calls) {
    assert.equal(call.options.token, 'synthetic-test-token');
    assert.equal(call.options.signal, signal);
    assert.equal(call.options.validation, true);
  }
});
test('respostas malformadas ou estados desconhecidos não se tornam ordens na tela', async () => {
  for (const response of [
    null,
    {},
    [{ ...order, status: 'Unknown' }],
    [{ ...order, kind: 'Unknown' }],
  ]) {
    const api = createMaintenanceApi(session('Admin'), { getJson: async () => response });
    await assert.rejects(() => api.list());
  }
});
test('filtros combinam histórico, equipamento, natureza e necessidade atual sem alterar os registros', () => {
  const items = [
    order,
    { ...order, id: 'old', status: 'Closed' },
    { ...order, id: 'other', equipmentId: 'other-id', kind: 'Corrective' },
  ];
  const equipment = [
    { id: 'equipment-id', serialNumber: 'CAM-01', brand: 'Marca', maintenanceNeed: 'Scheduled' },
  ];
  assert.equal(filterMaintenances(items, equipment, { equipmentId: 'equipment-id' }).length, 2);
  assert.equal(
    filterMaintenances(items, equipment, {
      search: 'cam-01',
      status: 'Scheduled',
      kind: 'Preventive',
      need: 'Scheduled',
    }).length,
    1,
  );
  assert.equal(filterMaintenances(items, equipment, { need: 'Immediate' }).length, 0);
  assert.equal(filterMaintenances(items, [], { search: 'other-id' }).length, 1);
  assert.equal(items.length, 3);
});
test('erros orientam recuperação de conflitos, consulta demo e resultado de rede incerto', () => {
  assert.match(maintenanceError({ status: 409 }), /ordem agendada/);
  assert.match(maintenanceError({ status: 400 }), /Preventiva exige/);
  assert.match(maintenanceError({ status: 405 }), /somente consulta/);
  assert.match(maintenanceError({ kind: 'network' }), /Atualize a lista/);
});
