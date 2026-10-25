// Serviços e utilitários
import { http, invalidResponse } from './http.js';

import { isUserRole } from './authorization.js';
export {
  canMonitor,
  canReadEquipments,
  canManageEquipments,
  canReadAgents,
  canManageAgents,
  canMaintain,
} from './authorization.js';
export async function login(credentials, { signal, client = http } = {}) {
  const data = await client.postJson(
    '/api/auth/login',
    {
      email: credentials.email.trim(),
      password: credentials.password,
    },
    {
      signal,
    },
  );
  const expiresAt = Date.parse(data?.expiresAt);
  if (
    typeof data?.token !== 'string' ||
    !data.token.trim() ||
    typeof data?.expiresAt !== 'string' ||
    !Number.isFinite(expiresAt) ||
    expiresAt <= Date.now() ||
    typeof data?.user?.name !== 'string' ||
    !isUserRole(data?.user?.role)
  ) {
    throw invalidResponse();
  }
  // A senha nunca integra o estado da sessão.

  return {
    token: data.token,
    expiresAt: data.expiresAt,
    user: {
      name: data.user.name,
      role: data.user.role,
      ...(typeof data.user.id === 'string' && data.user.id ? { id: data.user.id } : {}),
    },
  };
}
