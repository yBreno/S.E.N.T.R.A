import { http, invalidResponse } from './http.js';
import { canMaintain } from './auth.js';

export const maintenanceStatuses = {
  Scheduled: 'Agendada',
  Open: 'Em andamento',
  Closed: 'Concluída',
};
export const maintenanceKinds = { Preventive: 'Preventiva', Corrective: 'Corretiva' };
export const urgencies = { Low: 'Baixa', Medium: 'Média', High: 'Alta', Critical: 'Crítica' };
function record(value) {
  if (
    !value ||
    typeof value.id !== 'string' ||
    typeof value.equipmentId !== 'string' ||
    !maintenanceStatuses[value.status] ||
    !maintenanceKinds[value.kind] ||
    typeof value.description !== 'string'
  )
    throw invalidResponse();
  return value;
}
export function maintenanceError(error) {
  if (error?.status === 409)
    return 'Este equipamento já possui uma ordem agendada, em andamento ou sobreposta. Atualize a lista e consulte o histórico antes de repetir.';
  if (error?.status === 400)
    return 'Confira os campos e as datas. Preventiva exige equipamento operacional; corretiva exige falha. Somente ordens agendadas podem iniciar e somente ordens em andamento podem concluir.';
  if (error?.status === 405)
    return 'Este ambiente permite somente consulta. Utilize um ambiente de desenvolvimento autorizado para registrar alterações.';
  if (error?.kind === 'network')
    return 'Não foi possível confirmar a operação. Atualize a lista antes de enviar novamente.';
  return error?.message || 'Não foi possível consultar ou salvar a manutenção.';
}
export function createMaintenanceApi(session, client = http) {
  function options(signal) {
    if (!canMaintain(session.user)) throw new Error('Seu perfil não tem acesso às manutenções.');
    return { token: session.token, signal, validation: true };
  }
  return {
    async list(equipmentId = '', signal) {
      const data = await client.getJson(
        `/api/maintenances${equipmentId ? `?equipmentId=${encodeURIComponent(equipmentId)}` : ''}`,
        options(signal),
      );
      if (!Array.isArray(data)) throw invalidResponse();
      return data.map(record);
    },
    async detail(id, signal) {
      return record(
        await client.getJson(`/api/maintenances/${encodeURIComponent(id)}`, options(signal)),
      );
    },
    async create(payload, schedule, signal) {
      return record(
        await client.postJson(
          `/api/maintenances${schedule ? '/schedule' : ''}`,
          payload,
          options(signal),
        ),
      );
    },
    async start(id, signal) {
      return record(
        await client.postJson(
          `/api/maintenances/${encodeURIComponent(id)}/start`,
          {},
          options(signal),
        ),
      );
    },
    async close(id, payload, signal) {
      return record(
        await client.postJson(
          `/api/maintenances/${encodeURIComponent(id)}/close`,
          payload,
          options(signal),
        ),
      );
    },
  };
}
export function filterMaintenances(items, equipments, filters) {
  const byId = new Map(equipments.map((item) => [item.id, item]));
  const search = (filters.search || '').trim().toLocaleLowerCase('pt-BR');
  return items.filter((item) => {
    const equipment = byId.get(item.equipmentId);
    return (
      (!filters.equipmentId || item.equipmentId === filters.equipmentId) &&
      (!filters.status || item.status === filters.status) &&
      (!filters.kind || item.kind === filters.kind) &&
      (!filters.need || equipment?.maintenanceNeed === filters.need) &&
      (!search ||
        [
          item.description,
          item.id,
          item.equipmentId,
          equipment?.serialNumber,
          equipment?.brand,
          equipment?.model,
        ]
          .filter(Boolean)
          .join(' ')
          .toLocaleLowerCase('pt-BR')
          .includes(search))
    );
  });
}
