import test from 'node:test';
import assert from 'node:assert/strict';
import { agentRoles, buildAgentLocation, buildAgentPayload, createAgentForm, filterAgents, validCpf } from '../src/components/agents/agentModel.js';
import { agentError, createAgentApi, normalizeAgent } from '../src/services/agents.js';
import { canManageAgents, canReadAgents } from '../src/services/auth.js';
import { createHttpClient } from '../src/services/http.js';
import { buildOperationalLayers } from '../src/components/map/operationalFilters.js';
import { agentRoleLabel, getAgentState } from '../src/components/map/markerData.js';

const agent = { id: 'agent-id', name: 'Profissional de teste', badgeNumber: 'MAT-01', role: 'MunicipalGuard', availability: 'Available', maskedCpf: '***.982.247-**', userId: 'existing-user', neighborhood: 'Sé', latitude: -23.55, longitude: -46.63, locationUpdatedAt: '2026-10-23T12:00:00Z' };
const mobile = { id: 'vehicle-id', type: 'Motorcycle', serialNumber: 'MOTO-01', brand: 'Marca', model: 'Modelo', status: 'Operational', attributes: { licensePlate: 'ABC1234', currentAgentId: null } };
const validForm = () => ({ ...createAgentForm(), name: 'Profissional', cpf: '529.982.247-25', badgeNumber: 'MAT-01', neighborhood: 'Centro', latitude: '0', longitude: '0', availability: 'Available' });

test('cadastro representa as três categorias, normaliza CPF e preserva GPS zero sem criar usuário', () => {
  assert.deepEqual(Object.values(agentRoles), ['Agente de trânsito', 'Guarda municipal', 'Policial militar']);
  for (const role of Object.keys(agentRoles)) {
    const { payload, errors } = buildAgentPayload({ ...validForm(), role, userId: 'forged-user', password: 'ignored' });
    assert.deepEqual(errors, {});
    assert.equal(payload.cpf, '52998224725'); assert.equal(payload.role, role);
    assert.equal(payload.latitude, 0); assert.equal(payload.longitude, 0); assert.equal(payload.userId, null);
    assert.ok(!Object.hasOwn(payload, 'password'));
  }
});

test('edição não envia CPF nem permite trocar conta por campos extras do formulário', () => {
  const form = createAgentForm(agent);
  assert.equal(form.cpf, '');
  const { payload, errors } = buildAgentPayload({ ...form, cpf: '11111111111', userId: 'other', name: 'Editado', availability: 'Offline' }, agent);
  assert.deepEqual(errors, {});
  assert.ok(!Object.hasOwn(payload, 'cpf'));
  assert.equal(payload.userId, 'existing-user'); assert.equal(payload.name, 'Editado');
});

test('validação de CPF usa dígitos verificadores e não aceita sequências ou documentos incompletos', () => {
  assert.equal(validCpf('529.982.247-25'), true);
  assert.equal(validCpf('11144477735'), true);
  for (const cpf of ['', null, '11111111111', '52998224726', '123']) assert.equal(validCpf(cpf), false);
});

test('campos obrigatórios, categorias, disponibilidade e limites GPS são validados sem coagir vazio para zero', () => {
  const parsed = buildAgentPayload({ ...validForm(), name: '', badgeNumber: '', role: 'Admin', availability: 'Unknown', neighborhood: '', latitude: '', longitude: 181, cpf: '123' });
  assert.equal(parsed.payload, null);
  for (const key of ['name', 'badgeNumber', 'role', 'availability', 'neighborhood', 'latitude', 'longitude', 'cpf']) assert.ok(parsed.errors[key]);
  assert.equal(buildAgentLocation({ ...validForm(), latitude: -90, longitude: -180 }).payload.latitude, -90);
  assert.equal(buildAgentLocation({ ...validForm(), latitude: 90, longitude: 180 }).payload.longitude, 180);
});

test('atualização operacional envia somente bairro, coordenadas e disponibilidade', () => {
  const { payload } = buildAgentLocation({ ...validForm(), name: 'Nao alterar', role: 'PoliceOfficer', cpf: '52998224725' });
  assert.deepEqual(Object.keys(payload).sort(), ['availability', 'latitude', 'longitude', 'neighborhood']);
});

test('busca/filtros combinam nome/matrícula/categoria/bairro e mantêm disponibilidade independente', () => {
  const items = [agent, { ...agent, id: 'other', role: 'PoliceOfficer', name: 'Outro profissional', availability: 'Offline', neighborhood: 'Mooca' }];
  assert.equal(filterAgents(items, { search: 'guarda municipal', neighborhood: 'se', availability: 'Available' }).length, 1);
  assert.equal(filterAgents(items, { search: 'MAT-01', role: 'PoliceOfficer' }).length, 1);
  assert.equal(filterAgents(items, { role: 'PoliceOfficer', availability: 'Available' }).length, 0);
  assert.equal(items.length, 2);
});

test('respostas retêm apenas CPF mascarado e dados necessários, descartando CPF completo ou campos extras', () => {
  const normalized = normalizeAgent({ ...agent, cpf: '52998224725', password: 'private-value', email: 'private-value' });
  assert.equal(normalized.maskedCpf, agent.maskedCpf);
  for (const field of ['cpf', 'password', 'email']) assert.ok(!Object.hasOwn(normalized, field));
  assert.equal(normalizeAgent({ ...agent, maskedCpf: '52998224725' }).maskedCpf, '***.***.***-**');
  assert.throws(() => normalizeAgent({}));
});

test('somente Monitoring consulta agentes; somente gestores alteram identificação e veículos', () => {
  for (const role of ['Admin', 'OperationManager', 'CentralOperator']) assert.equal(canReadAgents({ role }), true);
  for (const role of ['Agent', 'MaintenanceTechnician', undefined]) { assert.equal(canReadAgents({ role }), false); assert.equal(canManageAgents({ role }), false); }
  assert.equal(canManageAgents({ role: 'CentralOperator' }), false);
  assert.equal(canManageAgents({ role: 'Admin' }), true);
  assert.equal(canManageAgents({ role: 'OperationManager' }), true);
});

test('serviço usa endpoints autenticados e envia vínculo sem sobrescrever dados de equipamento', async () => {
  const requests = [];
  const client = createHttpClient({ baseUrl: 'http://test.invalid' }, async (url, options) => {
    requests.push({ url, ...options });
    const data = url.endsWith('/vehicle') ? { agentId: agent.id, vehicle: mobile } : url.endsWith('/location') ? { id: 'position-id', agentId: agent.id, recordedAt: '2026-10-23T12:00:00Z' } : url.endsWith('/api/equipments') ? [mobile, { id: 'camera', type: 'Camera', attributes: {} }] : url.endsWith('/api/agents') && options.method === 'GET' ? [agent] : agent;
    return new Response(JSON.stringify(data));
  });
  const api = createAgentApi({ token: 'test-only', user: { role: 'Admin' } }, client);
  await api.list(); await api.detail(agent.id); await api.save({}, null); await api.save({}, agent.id);
  await api.location(agent.id, buildAgentLocation(validForm()).payload); await api.vehicle(agent.id);
  assert.equal((await api.vehicles()).length, 1);
  await api.assignVehicle(agent.id, mobile.id, null);
  assert.ok(requests.every(request => request.headers.Authorization === 'Bearer test-only'));
  assert.deepEqual(JSON.parse(requests.at(-1).body), { vehicleId: mobile.id, expectedVehicleId: null });
  assert.equal(requests.at(-1).method, 'PUT');
  assert.ok(!requests.some(request => request.method === 'PUT' && request.url.includes('/api/equipments/')));
});

test('UI bloqueia chamadas restritas antes do transporte sem impedir localização do operador', async () => {
  let calls = 0;
  const client = { putJson: async () => { calls++; return { agentId: agent.id, recordedAt: '2026-10-23T12:00:00Z' }; } };
  const operator = createAgentApi({ token: 'test-only', user: { role: 'CentralOperator' } }, client);
  await assert.rejects(operator.save({}, agent.id)); await assert.rejects(operator.assignVehicle(agent.id, mobile.id, null));
  assert.equal(calls, 0);
  await operator.location(agent.id, buildAgentLocation(validForm()).payload); assert.equal(calls, 1);
  const tech = createAgentApi({ token: 'test-only', user: { role: 'MaintenanceTechnician' } }, client);
  await assert.rejects(tech.list()); await assert.rejects(tech.location(agent.id, {})); assert.equal(calls, 1);
});

test('dados de agentes alimentam marcadores com categoria, disponibilidade e coordenadas atualizadas sem CPF', () => {
  const items = [{ id: agent.id, name: agent.name, badgeNumber: agent.badgeNumber, role: 'PoliceOfficer', availability: 'Busy', neighborhood: 'Mooca', latitude: -23.56, longitude: -46.60, locationUpdatedAt: agent.locationUpdatedAt }];
  const layers = buildOperationalLayers([{ id: 'agents', data: 1, items }], { agentAvailability: 'Busy' });
  const marker = layers.agents.filtered[0];
  assert.deepEqual(marker.position, [-23.56, -46.60]);
  assert.equal(agentRoleLabel(marker.role), 'Policial militar'); assert.equal(getAgentState(marker.availability).label, 'Ocupado');
  assert.ok(!Object.hasOwn(marker, 'cpf')); assert.ok(!Object.hasOwn(marker, 'maskedCpf'));
});

test('conflitos, modo de consulta e rede fornecem mensagens sem dados pessoais do servidor', () => {
  assert.match(agentError({ status: 409 }), /CPF, matrícula/);
  assert.match(agentError({ status: 405 }), /somente consulta/);
  assert.match(agentError({ kind: 'network' }), /antes de repetir/);
});
