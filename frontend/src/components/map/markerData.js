export const equipmentStates = Object.freeze({
  Operational: {
    label: 'Operacional',
    tone: 'operational'
  },
  Malfunctioning: {
    label: 'Com falha',
    tone: 'malfunctioning'
  },
  UnderMaintenance: {
    label: 'Em manutenção',
    tone: 'maintenance'
  },
  Deactivated: {
    label: 'Desativado',
    tone: 'deactivated'
  },
  Stolen: {
    label: 'Furtado / roubado',
    tone: 'stolen'
  },
  NotInstalled: {
    label: 'Não instalado',
    tone: 'not-installed'
  }
});
export const agentStates = Object.freeze({
  Available: {
    label: 'Disponível',
    tone: 'available'
  },
  Busy: {
    label: 'Ocupado',
    tone: 'busy'
  },
  Offline: {
    label: 'Offline',
    tone: 'offline'
  }
});
const unknownState = Object.freeze({
  label: 'Estado não informado ou desconhecido',
  tone: 'unknown'
});
export function getEquipmentState(value) {
  const normalized = typeof value === 'string' ? value.trim() : '';

  return Object.hasOwn(equipmentStates, normalized) ? equipmentStates[normalized] : unknownState;
}
export function getAgentState(value) {
  const normalized = typeof value === 'string' ? value.trim() : '';

  return Object.hasOwn(agentStates, normalized) ? agentStates[normalized] : unknownState;
}
export function normalizeCoordinates(latitude, longitude) {
  // Sem coerção: Number(null), Number('') e Number(false) criariam posições falsas em zero.
  if (typeof latitude !== 'number' || typeof longitude !== 'number' || !Number.isFinite(latitude) || !Number.isFinite(longitude) || latitude < -90 || latitude > 90 || longitude < -180 || longitude > 180) return null;

  return [latitude, longitude];
}
export function prepareMarkers(records = []) {
  const markers = [];
  const seen = new Set();
  for (const record of records) {
    const position = normalizeCoordinates(record?.latitude, record?.longitude);
    const id = record?.id;
    if (!position || typeof id !== 'string' || !id.trim() || seen.has(id)) continue;
    seen.add(id);
    markers.push({
      ...record,
      position
    });
  }

  return {
    markers,
    discarded: records.length - markers.length
  };
}
export function displayText(value) {
  return typeof value === 'string' && value.trim() ? value : 'Não informado';
}
export function agentRoleLabel(value) {
  return value === 'TrafficAgent' ? 'Agente de trânsito' : displayText(value);
}
export function formatLocationUpdatedAt(value) {
  if (typeof value !== 'string' || !value.trim()) return 'Não informado';
  const date = new Date(value);

  return Number.isFinite(date.getTime()) ? date.toLocaleString('pt-BR') : 'Não informado';
}
