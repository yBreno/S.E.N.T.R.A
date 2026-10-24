// Serviços e utilitários
import { apiConfig } from '../config/api.js';
import { http, isAbortError } from './http.js';
import { canMonitor, isUserRole } from './authorization.js';

const storageKey = 'sentra.session.v1';

export function normalizeSession(value, now = Date.now()) {
  try {
    if (typeof value?.token !== 'string' || typeof value?.expiresAt !== 'string' ||
      typeof value?.user?.name !== 'string' || !isUserRole(value?.user?.role)) return null;

    const parts = value.token.split('.');
    if (parts.length !== 3 || !parts.every(part => /^[A-Za-z0-9_-]+$/.test(part))) return null;

    const decode = part => JSON.parse(atob(part.replace(/-/g, '+').replace(/_/g, '/')));
    const header = decode(parts[0]);
    const payload = decode(parts[1]);
    if (header.alg !== 'HS256' || !Number.isSafeInteger(payload.exp) ||
      payload.role !== value.user.role || typeof payload.sub !== 'string' || !payload.sub) return null;
    if (payload.nbf !== undefined && (!Number.isFinite(payload.nbf) || payload.nbf * 1000 > now)) return null;

    // A leitura do exp não verifica a assinatura; a restauração ainda exige aceitação pela API.
    const expiration = Math.min(Date.parse(value.expiresAt), payload.exp * 1000);
    if (!Number.isFinite(expiration) || expiration <= now) return null;

    return {
      token: value.token,
      expiresAt: new Date(expiration).toISOString(),
      user: { name: value.user.name, role: value.user.role }
    };
  } catch {
    return null;
  }
}

export function createSessionStore({
  baseUrl = apiConfig.baseUrl,
  getStorage = () => globalThis.sessionStorage,
  now = Date.now
} = {}) {
  function clear() {
    try {
      getStorage()?.removeItem(storageKey);
    } catch {
      // Armazenamento bloqueado não pode impedir o logout em memória.
    }
  }

  function read() {
    try {
      const record = JSON.parse(getStorage()?.getItem(storageKey) || 'null');
      const session = record?.apiBaseUrl === baseUrl && baseUrl
        ? normalizeSession(record.session, now())
        : null;
      if (!session) clear();

      return session;
    } catch {
      clear();
      return null;
    }
  }

  function save(value) {
    const session = normalizeSession(value, now());
    if (!session || !baseUrl) {
      clear();
      return;
    }

    try {
      getStorage()?.setItem(storageKey, JSON.stringify({ apiBaseUrl: baseUrl, session }));
    } catch {
      clear();
    }
  }

  return { read, save, clear };
}

export const sessionStore = createSessionStore();

export async function restoreSession({ store = sessionStore, client = http, signal } = {}) {
  const cached = store.read();
  if (!cached) return null;

  try {
    await client.getJson(canMonitor(cached.user) ? '/api/dashboard' : '/api/equipments', { token: cached.token, signal });
    signal?.throwIfAborted();
    const current = store.read();

    return current?.token === cached.token ? current : null;
  } catch (error) {
    if (signal?.aborted) signal.throwIfAborted();
    if (!isAbortError(error)) store.clear();
    throw error;
  }
}

export function scheduleSessionExpiration(session, onExpire, {
  now = Date.now,
  schedule = globalThis.setTimeout,
  cancel = globalThis.clearTimeout
} = {}) {
  let timer;

  function check() {
    const remaining = Date.parse(session.expiresAt) - now();
    if (!Number.isFinite(remaining) || remaining <= 0) onExpire();
    else timer = schedule(check, Math.min(remaining, 2_147_483_647));
  }

  timer = schedule(check, Math.max(0, Math.min(Date.parse(session.expiresAt) - now(), 2_147_483_647)));

  return () => cancel(timer);
}
