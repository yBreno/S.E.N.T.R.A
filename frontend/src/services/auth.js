// Serviços e utilitários
import { http, invalidResponse } from './http.js';

const monitoringRoles = new Set(['Admin', 'OperationManager', 'CentralOperator']);
export function canMonitor(user) {
  return monitoringRoles.has(user?.role);
}
export function canReadEquipments(user) {
  return canMonitor(user) || user?.role === 'MaintenanceTechnician';
}
export function canManageEquipments(user) {
  return user?.role === 'Admin' || user?.role === 'OperationManager';
}
export function canReadAgents(user) {
  return canMonitor(user);
}
export function canManageAgents(user) {
  return canManageEquipments(user);
}
export function canMaintain(user) {
  return canManageEquipments(user) || user?.role === 'MaintenanceTechnician';
}
export async function login(credentials, {
  signal,
  client = http
} = {}) {
  const data = await client.postJson('/api/auth/login', {
    email: credentials.email.trim(),
    password: credentials.password
  }, {
    signal
  });
  const expiresAt = Date.parse(data?.expiresAt);
  if (typeof data?.token !== 'string' || !data.token.trim() || typeof data?.expiresAt !== 'string' || !Number.isFinite(expiresAt) || expiresAt <= Date.now() || typeof data?.user?.name !== 'string' || typeof data?.user?.role !== 'string') {
    throw invalidResponse();
  }
  // A senha nunca integra o estado da sessão.

  return {
    token: data.token,
    expiresAt: data.expiresAt,
    user: {
      name: data.user.name,
      role: data.user.role
    }
  };
}
