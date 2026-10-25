import test from 'node:test';
import assert from 'node:assert/strict';
import {
  availableStatusTransitions,
  buildEquipmentPayload,
  createEquipmentForm,
  filterEquipments,
  specificFields,
} from '../src/components/equipment/equipmentModel.js';
import { canManageEquipments, canMonitor, canReadEquipments } from '../src/services/auth.js';
import { createEquipmentApi, inventoryError } from '../src/services/equipments.js';
import { createHttpClient } from '../src/services/http.js';
import { restoreSession } from '../src/services/session.js';

const address = {
  id: 'address-id',
  street: 'Via de teste',
  number: 'S/N',
  complement: '',
  neighborhood: 'Sé',
  city: 'São Paulo',
  state: 'SP',
  zipCode: '01001-000',
  country: 'Brasil',
  locationReference: 'Oposto ao 181',
  latitude: 0,
  longitude: 0,
};
const equipment = {
  id: 'equipment-id',
  type: 'Camera',
  serialNumber: 'SN-1',
  brand: 'Marca',
  model: 'Modelo',
  status: 'Operational',
  maintenanceNeed: 'None',
  nextMaintenanceDate: null,
  addressId: address.id,
  category: 'Videomonitoramento',
  attributes: { hasNightVision: true, resolution: '4K', isColor: true },
};
function validForm(type = 'Camera') {
  return {
    ...createEquipmentForm(),
    type,
    serialNumber: 'SN-NEW',
    brand: 'Marca',
    model: 'Modelo',
    address: { ...address },
  };
}

test('cadastro envia endereço completo e somente características do tipo escolhido, sem status arbitrário', () => {
  const parsed = buildEquipmentPayload({
    ...validForm(),
    licensePlate: 'ABC1234',
    autonomyLevel: 5,
    status: 'Stolen',
  });
  assert.deepEqual(parsed.errors, {});
  assert.equal(parsed.payload.type, 'Camera');
  assert.equal(parsed.payload.address.latitude, 0);
  assert.equal(parsed.payload.address.zipCode, '01001000');
  assert.equal(parsed.payload.address.locationReference, 'Oposto ao 181');
  assert.equal(parsed.payload.hasNightVision, false);
  assert.ok(!Object.hasOwn(parsed.payload, 'licensePlate'));
  assert.ok(!Object.hasOwn(parsed.payload, 'autonomyLevel'));
  assert.ok(!Object.hasOwn(parsed.payload, 'status'));
});

test('edição preserva tipo, endereço compartilhado e agente vinculado, sem acrescentar mutações de agentes', () => {
  const vehicle = {
    ...equipment,
    type: 'Vehicle',
    category: 'Transporte escolar',
    attributes: { licensePlate: 'ABC1234', currentAgentId: 'agent-id', manufactureYear: 2024 },
  };
  const form = createEquipmentForm(vehicle, address);
  const { payload, errors } = buildEquipmentPayload(
    { ...form, brand: 'Outra', currentAgentId: 'forged-agent' },
    vehicle,
  );
  assert.deepEqual(errors, {});
  assert.equal(payload.addressId, address.id);
  assert.ok(!Object.hasOwn(payload, 'address'));
  assert.ok(!Object.hasOwn(payload, 'type'));
  assert.equal(payload.currentAgentId, 'agent-id');
  assert.equal(payload.category, 'Transporte escolar');
  assert.equal(buildEquipmentPayload({ ...form, type: 'Camera' }, vehicle).payload, null);
});

test('mudança de localização usa uma cópia completa em vez de atualizar o endereço compartilhado', () => {
  const form = createEquipmentForm(equipment, address);
  const { payload } = buildEquipmentPayload(
    { ...form, addressMode: 'new', address: { ...form.address, street: 'Outra via' } },
    equipment,
  );
  assert.equal(payload.address.street, 'Outra via');
  assert.ok(!Object.hasOwn(payload, 'addressId'));
  assert.equal(address.street, 'Via de teste');
});

test('validação rejeita campos vazios, GPS vazio/inválido, CEP inválido e agendamento sem data', () => {
  const parsed = buildEquipmentPayload({
    ...validForm(),
    serialNumber: ' ',
    maintenanceNeed: 'Scheduled',
    address: { ...address, latitude: '', longitude: 181, zipCode: '123' },
  });
  assert.equal(parsed.payload, null);
  for (const key of [
    'serialNumber',
    'nextMaintenanceDate',
    'address.latitude',
    'address.longitude',
    'address.zipCode',
  ])
    assert.ok(parsed.errors[key]);
  assert.equal(
    buildEquipmentPayload({ ...validForm(), address: { ...address, latitude: -91 } }).payload,
    null,
  );
});

test('necessidade e data são coerentes e planejamento de falha/manutenção é preservado', () => {
  const scheduled = buildEquipmentPayload({
    ...validForm(),
    maintenanceNeed: 'Periodic',
    nextMaintenanceDate: '2027-01-01T12:30',
  }).payload;
  assert.equal(scheduled.nextMaintenanceDate, new Date('2027-01-01T12:30').toISOString());
  assert.equal(
    buildEquipmentPayload({
      ...validForm(),
      maintenanceNeed: 'None',
      nextMaintenanceDate: '2027-01-01T12:30',
    }).payload.nextMaintenanceDate,
    null,
  );
  for (const status of ['Malfunctioning', 'UnderMaintenance']) {
    const original = { ...equipment, status, maintenanceNeed: 'Immediate' };
    const parsed = buildEquipmentPayload(
      { ...createEquipmentForm(original, address), maintenanceNeed: 'None' },
      original,
    );
    assert.ok(!Object.hasOwn(parsed.payload, 'maintenanceNeed'));
    assert.ok(!Object.hasOwn(parsed.payload, 'nextMaintenanceDate'));
  }
});

test('todos os oito tipos possuem campos pertinentes e validações de veículos/números', () => {
  assert.equal(Object.keys(specificFields).length, 8);
  for (const type of Object.keys(specificFields)) {
    const form = {
      ...validForm(type),
      licensePlate: 'abc-1234',
      autonomyLevel: '0',
      engineCapacityCc: '150',
    };
    assert.deepEqual(buildEquipmentPayload(form).errors, {}, type);
  }
  assert.ok(
    buildEquipmentPayload({ ...validForm('Vehicle'), licensePlate: 'INVALID' }).errors.licensePlate,
  );
  assert.ok(
    buildEquipmentPayload({ ...validForm('Radar'), maxSpeedLimit: '0' }).errors.maxSpeedLimit,
  );
  assert.ok(
    buildEquipmentPayload({
      ...validForm('AutonomousVehicle'),
      licensePlate: 'ABC1234',
      autonomyLevel: 6,
    }).errors.autonomyLevel,
  );
});

test('busca/filtros se combinam e normalizam acentos sem alterar os registros originais', () => {
  const items = [
    { ...equipment, neighborhood: 'Sé' },
    {
      ...equipment,
      id: 'other',
      type: 'Sensor',
      category: 'Balança',
      maintenanceNeed: 'Immediate',
      neighborhood: 'Mooca',
    },
  ];
  assert.equal(
    filterEquipments(items, { search: 'balanca', type: 'Sensor', maintenanceNeed: 'Immediate' })
      .length,
    1,
  );
  assert.equal(filterEquipments(items, { neighborhood: 'se', status: 'Operational' }).length, 1);
  assert.equal(filterEquipments(items, { search: 'balanca', type: 'Camera' }).length, 0);
  assert.equal(items.length, 2);
});

test('roles separam leitura, gestão e reativação sem ampliar monitoramento do técnico', () => {
  for (const role of ['Admin', 'OperationManager'])
    assert.equal(canManageEquipments({ role }), true);
  for (const role of ['CentralOperator', 'MaintenanceTechnician']) {
    assert.equal(canReadEquipments({ role }), true);
    assert.equal(canManageEquipments({ role }), false);
  }
  assert.equal(canMonitor({ role: 'MaintenanceTechnician' }), false);
  assert.equal(canReadEquipments({ role: 'Agent' }), false);
  assert.deepEqual(availableStatusTransitions('Deactivated', false), []);
  assert.deepEqual(availableStatusTransitions('Deactivated', true), ['Operational']);
  assert.ok(!availableStatusTransitions('Operational', true).includes('UnderMaintenance'));
  assert.deepEqual(availableStatusTransitions('Stolen', true), []);
  assert.deepEqual(availableStatusTransitions('UnderMaintenance', true), []);
});

test('editar identificação preserva previsão intocada com precisão de segundos', () => {
  const current = {
    ...equipment,
    maintenanceNeed: 'Scheduled',
    nextMaintenanceDate: '2027-01-01T15:30:42.123Z',
  };
  const form = { ...createEquipmentForm(current), brand: 'Marca atualizada' };
  const parsed = buildEquipmentPayload(form, current);
  assert.deepEqual(parsed.errors, {});
  assert.ok(!Object.hasOwn(parsed.payload, 'maintenanceNeed'));
  assert.ok(!Object.hasOwn(parsed.payload, 'nextMaintenanceDate'));
});

test('API de inventário usa GET/POST/PUT/PATCH autenticados e bloqueia escrita local antes do transporte', async () => {
  const requests = [];
  const client = createHttpClient({ baseUrl: 'http://test.invalid' }, async (url, options) => {
    requests.push({ url, ...options });
    return new Response(
      JSON.stringify(
        url.endsWith('/api/addresses')
          ? [address]
          : url.endsWith('/api/equipments') && options.method === 'GET'
            ? [equipment]
            : equipment,
      ),
    );
  });
  const api = createEquipmentApi({ token: 'test-only', user: { role: 'Admin' } }, client);
  await api.list();
  await api.addresses();
  await api.detail(equipment.id);
  await api.save({}, null);
  await api.save({}, equipment.id);
  await api.status(equipment, 'Deactivated', ' Motivo ');
  assert.deepEqual(
    requests.map((request) => request.method),
    ['GET', 'GET', 'GET', 'POST', 'PUT', 'PATCH'],
  );
  assert.ok(requests.every((request) => request.headers.Authorization === 'Bearer test-only'));
  assert.deepEqual(JSON.parse(requests.at(-1).body), {
    newStatus: 'Deactivated',
    reason: 'Motivo',
  });
  const restricted = createEquipmentApi(
    { token: 'test-only', user: { role: 'CentralOperator' } },
    client,
  );
  const before = requests.length;
  await assert.rejects(restricted.save({}, null));
  await assert.rejects(
    restricted.status({ ...equipment, status: 'Deactivated' }, 'Operational', ''),
  );
  assert.equal(requests.length, before);
});

test('validação HTTP opt-in retém somente chaves e não reproduz conteúdo de erros do servidor', async () => {
  const client = createHttpClient(
    { baseUrl: 'http://test.invalid' },
    async () =>
      new Response(
        JSON.stringify({
          detail: 'private-detail',
          errors: { SerialNumber: ['private-value'], 'Address.ZipCode': ['private-value'] },
        }),
        { status: 400, headers: { 'Content-Type': 'application/problem+json' } },
      ),
  );
  await assert.rejects(client.putJson('/api/equipments/id', {}, { validation: true }), (error) => {
    assert.deepEqual(error.fieldErrors, { SerialNumber: true, 'Address.ZipCode': true });
    assert.ok(!JSON.stringify(error).includes('private-'));
    return true;
  });
  assert.match(inventoryError({ status: 409 }), /série ou placa/);
  assert.match(inventoryError({ status: 405 }), /somente consulta/);
});

test('técnico restaura sessão validando equipamentos sem chamar dashboard protegido de monitoramento', async () => {
  const cached = { token: 'isolated-test-token', user: { role: 'MaintenanceTechnician' } };
  const store = {
    read: () => cached,
    clear: () => assert.fail('Sessão válida não deve ser apagada'),
  };
  const client = {
    getJson: async (path) => {
      assert.equal(path, '/api/equipments');
      return [];
    },
  };
  assert.equal(await restoreSession({ store, client }), cached);
});
