import { http, invalidResponse } from './http.js';
import { canAdministerUsers, isUserRole } from './authorization.js';

export function normalizeAccount(value) {
  if (!value || typeof value.id !== 'string' || !value.id || typeof value.name !== 'string' || typeof value.email !== 'string' ||
      !isUserRole(value.role) || typeof value.isActive !== 'boolean' || !Number.isSafeInteger(value.revision) || value.revision < 1 ||
      !Number.isFinite(Date.parse(value.createdAt))) throw invalidResponse();
  return Object.fromEntries(['id', 'name', 'email', 'role', 'isActive', 'revision', 'createdAt'].map(key => [key, value[key]]));
}
function page(value, normalize) {
  if (!value || !Array.isArray(value.items) || !Number.isSafeInteger(value.total) || value.total < 0 ||
      !Number.isInteger(value.page) || value.page < 1 || !Number.isInteger(value.pageSize) || value.pageSize < 1 || value.pageSize > 100 || value.items.length > value.pageSize) throw invalidResponse();
  return { items: value.items.map(normalize), total: value.total, page: value.page, pageSize: value.pageSize };
}
function audit(value) {
  if (!value || typeof value.id !== 'string' || typeof value.action !== 'string' || !Number.isFinite(Date.parse(value.createdAt)) ||
      (value.actorUserId !== null && typeof value.actorUserId !== 'string') ||
      (value.previousRole !== null && !isUserRole(value.previousRole)) || (value.newRole !== null && !isUserRole(value.newRole))) throw invalidResponse();
  return Object.fromEntries(['id', 'createdAt', 'action', 'actorUserId', 'previousRole', 'newRole'].map(key => [key, value[key]]));
}
function change(value) {
  if (!value || typeof value.reauthenticate !== 'boolean') throw invalidResponse();
  return { account: normalizeAccount(value.account), reauthenticate: value.reauthenticate };
}
export function userError(error) {
  if (error?.status === 409) return 'A conta mudou ou o e-mail/documento já está cadastrado. Atualize os dados antes de tentar novamente.';
  if (error?.status === 400) return 'Confira os campos e a ação solicitada. O último Admin ativo não pode ser desativado ou rebaixado.';
  if (error?.status === 405) return 'O ambiente DEMO permite somente consultar contas.';
  return error?.message || 'Não foi possível concluir a operação. Atualize os dados antes de repetir o envio.';
}
export function createUsersApi(session, client = http) {
  const options = signal => {
    if (!canAdministerUsers(session.user)) throw new Error('Somente Admin pode administrar usuários.');
    return { token: session.token, signal, validation: true };
  };
  const path = id => `/api/users/${encodeURIComponent(id)}`;
  return {
    async list(query, signal) {
      const config = options(signal); const params = new URLSearchParams();
      for (const [key, value] of Object.entries(query)) if (value !== '' && value !== null && value !== undefined) params.set(key, String(value).trim());
      return page(await client.getJson(`/api/users?${params}`, config), normalizeAccount);
    },
    async details(id, signal) {
      const data = await client.getJson(path(id), options(signal));
      if (!data || (data.agentId !== null && typeof data.agentId !== 'string')) throw invalidResponse();
      return { account: normalizeAccount(data.account), agentId: data.agentId };
    },
    async history(id, historyPage, signal) { return page(await client.getJson(`${path(id)}/history?page=${historyPage}&pageSize=10`, options(signal)), audit); },
    async requestPasswordReset(id, signal) { await client.postJson(`/api/auth/password-recovery/users/${encodeURIComponent(id)}`, {}, options(signal)); },
    async create(payload, signal) { return change(await client.postJson('/api/users', payload, options(signal))); },
    async update(id, payload, signal) { return change(await client.putJson(path(id), payload, options(signal))); },
    async role(id, role, revision, signal) { return change(await client.putJson(`${path(id)}/role`, { role, revision }, options(signal))); },
    async status(id, isActive, revision, signal) { return change(await client.putJson(`${path(id)}/status`, { isActive, revision }, options(signal))); }
  };
}
