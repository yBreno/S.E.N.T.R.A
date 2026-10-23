import { useEffect, useMemo, useSyncExternalStore } from 'react';
import { loadVerification } from '../services/verification.js';
import { createVerificationResource, resolvePollingInterval } from '../services/verificationResource.js';

export function useEndpointVerification(token, onLogout, enabled = true, includeMaintenance = false) {
  const resource = useMemo(() => createVerificationResource({
    load: signal => loadVerification(token, { signal, includeMaintenance }),
    onUnauthorized: () => onLogout('A API recusou a sessão (401). Entre novamente.'),
    intervalMs: resolvePollingInterval(import.meta.env?.VITE_POLL_INTERVAL_MS)
  }), [token, onLogout, includeMaintenance]);
  const state = useSyncExternalStore(resource.subscribe, resource.getSnapshot);
  useEffect(() => { if (enabled) resource.start(); return resource.stop; }, [resource, enabled]);
  return { ...state, refresh: resource.refresh, intervalMs: resolvePollingInterval(import.meta.env?.VITE_POLL_INTERVAL_MS) };
}
