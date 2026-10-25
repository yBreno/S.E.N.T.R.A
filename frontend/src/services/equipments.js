import { http, invalidResponse } from './http.js';
import { canReadEquipments, canManageEquipments } from './auth.js';
import { availableStatusTransitions } from '../components/equipment/equipmentModel.js';

function record(value) {
  if (
    !value ||
    typeof value.id !== 'string' ||
    !value.id ||
    typeof value.type !== 'string' ||
    typeof value.serialNumber !== 'string' ||
    typeof value.addressId !== 'string' ||
    !value.attributes ||
    typeof value.attributes !== 'object'
  )
    throw invalidResponse();
  return value;
}
function address(value) {
  if (
    !value ||
    typeof value.id !== 'string' ||
    typeof value.street !== 'string' ||
    typeof value.neighborhood !== 'string'
  )
    throw invalidResponse();
  return value;
}
function permitted(allowed) {
  if (!allowed) throw new Error('Seu perfil não tem permissão para executar esta ação.');
}
export function inventoryError(error) {
  if (error?.status === 409)
    return 'Conflito ao salvar: confira se a série ou placa já está cadastrada, ou se o vínculo do veículo está em uso. Atualize a lista antes de tentar novamente.';
  if (error?.status === 400)
    return 'Não foi possível salvar. Confira os campos indicados e as regras da situação operacional.';
  if (error?.status === 405)
    return 'Este ambiente permite somente consulta. Use um ambiente de desenvolvimento autorizado para salvar.';
  if (error?.kind === 'network')
    return 'Não foi possível confirmar a operação. Confira a conexão e atualize a lista antes de repetir um envio.';
  return error?.message || 'Não foi possível concluir a operação.';
}
export function createEquipmentApi(session, client = http) {
  const options = (signal) => ({ token: session.token, signal });
  return {
    async list(signal) {
      permitted(canReadEquipments(session.user));
      const data = await client.getJson('/api/equipments', options(signal));
      if (!Array.isArray(data)) throw invalidResponse();
      return data.map(record);
    },
    async addresses(signal) {
      permitted(canReadEquipments(session.user));
      const data = await client.getJson('/api/addresses', options(signal));
      if (!Array.isArray(data)) throw invalidResponse();
      return data.map(address);
    },
    async detail(id, signal) {
      permitted(canReadEquipments(session.user));
      return record(
        await client.getJson(`/api/equipments/${encodeURIComponent(id)}`, options(signal)),
      );
    },
    async address(id, signal) {
      permitted(canReadEquipments(session.user));
      return address(
        await client.getJson(`/api/addresses/${encodeURIComponent(id)}`, options(signal)),
      );
    },
    async save(payload, id, signal) {
      permitted(canManageEquipments(session.user));
      const config = { ...options(signal), validation: true };
      return record(
        id
          ? await client.putJson(`/api/equipments/${encodeURIComponent(id)}`, payload, config)
          : await client.postJson('/api/equipments', payload, config),
      );
    },
    async status(equipment, next, reason, signal) {
      permitted(
        canReadEquipments(session.user) &&
          availableStatusTransitions(equipment.status, canManageEquipments(session.user)).includes(
            next,
          ),
      );
      return record(
        await client.patchJson(
          `/api/equipments/${encodeURIComponent(equipment.id)}/status`,
          { newStatus: next, reason: reason.trim() || null },
          { ...options(signal), validation: true },
        ),
      );
    },
  };
}
