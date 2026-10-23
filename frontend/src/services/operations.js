import { invalidResponse } from './http.js';
// Regras operacionais atuais, sem calcular risco histórico ou predição.
export const MAINTENANCE_LOOKAHEAD_DAYS = 7;
export const unavailableStates = new Set(['Malfunctioning', 'UnderMaintenance', 'Stolen']);
export const alertSeverities = { Critical: 'Crítica', High: 'Alta', Medium: 'Média', Low: 'Baixa' };
export function maintenanceAttention(equipment, now = Date.now()) {
  const due = equipment?.nextMaintenanceDate ? Date.parse(equipment.nextMaintenanceDate) : NaN;
  const planned = ['Scheduled', 'Periodic'].includes(equipment?.maintenanceNeed);
  return { immediate: equipment?.maintenanceNeed === 'Immediate' || equipment?.status === 'Malfunctioning',
    upcoming: planned && Number.isFinite(due) && due <= now + MAINTENANCE_LOOKAHEAD_DAYS * 86400000,
    overdue: planned && Number.isFinite(due) && due < now, inProgress: equipment?.status === 'UnderMaintenance' };
}
export function normalizeAlerts(value) {
  if (!Array.isArray(value) || value.some(item => !item || typeof item.id !== 'string' || typeof item.equipmentId !== 'string' || typeof item.message !== 'string' || !Object.hasOwn(alertSeverities, item.severity) || !['Open', 'Resolved'].includes(item.status) || !Number.isFinite(Date.parse(item.createdAt)))) throw invalidResponse();
  return value.map(({ id, equipmentId, message, severity, type, status, createdAt, resolvedAt }) => ({ id, equipmentId, message, severity, type, status, createdAt, resolvedAt }));
}
export function prioritizeAlerts(alerts) {
  const rank = { Critical: 4, High: 3, Medium: 2, Low: 1 };
  return alerts.filter(item => item.status === 'Open').toSorted((a, b) => rank[b.severity] - rank[a.severity] || Date.parse(b.createdAt) - Date.parse(a.createdAt));
}
export function buildCityOverview(equipments, alerts, now = Date.now()) {
  const rows = new Map(), byId = new Map();
  for (const equipment of equipments) {
    const label = equipment.neighborhood?.trim().replace(/\s+/g, ' ') || 'Bairro não informado';
    const key = label.normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLocaleLowerCase('pt-BR');
    if (!rows.has(key)) rows.set(key, { key, label, total: 0, operational: 0, unavailable: 0, immediate: 0, upcoming: 0, planned: 0, inProgress: 0, critical: alerts == null ? null : 0 });
    const row = rows.get(key); byId.set(equipment.id, row);
    const attention = maintenanceAttention(equipment, now);
    row.total++; row.operational += equipment.status === 'Operational' ? 1 : 0;
    row.unavailable += unavailableStates.has(equipment.status) ? 1 : 0;
    row.immediate += attention.immediate ? 1 : 0; row.upcoming += attention.upcoming ? 1 : 0;
    row.planned += equipment.maintenanceNeed === 'Scheduled' ? 1 : 0;
    row.inProgress += attention.inProgress ? 1 : 0;
  }
  let unlocatedAlerts = 0;
  for (const alert of alerts || []) {
    if (alert.status !== 'Open' || !['High', 'Critical'].includes(alert.severity)) continue;
    const row = byId.get(alert.equipmentId);
    if (row) row.critical++; else unlocatedAlerts++;
  }
  return { rows: [...rows.values()].sort((a, b) => (b.critical || 0) - (a.critical || 0) || b.immediate - a.immediate || b.unavailable - a.unavailable || b.upcoming - a.upcoming || a.label.localeCompare(b.label, 'pt-BR')), unlocatedAlerts };
}
