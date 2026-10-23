import { http, invalidResponse } from './http.js';
import { canReadAgents, canManageAgents } from './auth.js';
import { vehicleTypes } from '../components/equipment/equipmentModel.js';

export function normalizeAgent(value) {
  if (!value || typeof value.id !== 'string' || !value.id || typeof value.name !== 'string' || typeof value.badgeNumber !== 'string' || typeof value.role !== 'string' || typeof value.availability !== 'string' || (value.userId !== null && typeof value.userId !== 'string')) throw invalidResponse();
  const record = Object.fromEntries(['id', 'name', 'badgeNumber', 'role', 'availability', 'userId', 'neighborhood', 'latitude', 'longitude', 'locationUpdatedAt'].map(field => [field, value[field]]));
  record.maskedCpf = typeof value.maskedCpf === 'string' && /^\*{3}\.\d{3}\.\d{3}-\*{2}$/.test(value.maskedCpf) ? value.maskedCpf : '***.***.***-**';
  return record;
}
function vehicle(value) {
  if (!value || typeof value.id !== 'string' || !vehicleTypes.has(value.type) || !value.attributes || typeof value.attributes !== 'object') throw invalidResponse();
  return { id: value.id, type: value.type, serialNumber: value.serialNumber, brand: value.brand, model: value.model, status: value.status,
    licensePlate: value.attributes.licensePlate, currentAgentId: value.attributes.currentAgentId ?? null };
}
function relation(data, agentId) {
  if (!data || data.agentId !== agentId || !Object.hasOwn(data, 'vehicle')) throw invalidResponse();
  return data.vehicle === null ? null : vehicle(data.vehicle);
}
function permitted(allowed) {
  if (!allowed) throw new Error('Seu perfil não tem permissão para executar esta ação.');
}
export function agentError(error) {
  if (error?.status === 409) return 'Conflito ao salvar. CPF, matrícula ou conta de acesso podem já estar vinculados; o vínculo de veículo também pode ter mudado. Atualize os detalhes antes de tentar novamente.';
  if (error?.status === 400) return 'Não foi possível salvar. Confira os campos indicados e as regras do cadastro ou vínculo.';
  if (error?.status === 405) return 'Este ambiente permite somente consulta. Use um ambiente de desenvolvimento autorizado para salvar.';
  if (error?.kind === 'network' || error?.kind === 'response') return 'Não foi possível confirmar a operação. Confira a conexão e atualize os dados antes de repetir um envio.';
  return error?.message || 'Não foi possível concluir a operação.';
}
export function createAgentApi(session, client = http) {
  const config = signal => ({ token: session.token, signal, validation: true });
  const path = id => `/api/agents/${encodeURIComponent(id)}`;
  return {
    async list(signal) {
      permitted(canReadAgents(session.user));
      const records = await client.getJson('/api/agents', config(signal));
      if (!Array.isArray(records)) throw invalidResponse();
      return records.map(normalizeAgent);
    },
    async detail(id, signal) {
      permitted(canReadAgents(session.user));
      return normalizeAgent(await client.getJson(path(id), config(signal)));
    },
    async save(payload, id, signal) {
      permitted(canManageAgents(session.user));
      const data = id ? await client.putJson(path(id), payload, config(signal)) : await client.postJson('/api/agents', payload, config(signal));
      return normalizeAgent(data);
    },
    async location(id, payload, signal) {
      permitted(canReadAgents(session.user));
      const data = await client.putJson(`${path(id)}/location`, payload, config(signal));
      if (!data || data.agentId !== id || typeof data.recordedAt !== 'string') throw invalidResponse();
      return data;
    },
    async vehicle(id, signal) {
      permitted(canReadAgents(session.user));
      return relation(await client.getJson(`${path(id)}/vehicle`, config(signal)), id);
    },
    async vehicles(signal) {
      permitted(canManageAgents(session.user));
      const data = await client.getJson('/api/equipments', config(signal));
      if (!Array.isArray(data)) throw invalidResponse();
      return data.filter(item => vehicleTypes.has(item?.type)).map(vehicle);
    },
    async assignVehicle(id, vehicleId, expectedVehicleId, signal) {
      permitted(canManageAgents(session.user));
      return relation(await client.putJson(`${path(id)}/vehicle`, { vehicleId: vehicleId || null, expectedVehicleId: expectedVehicleId || null }, config(signal)), id);
    }
  };
}
