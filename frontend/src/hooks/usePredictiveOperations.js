import { useEffect, useMemo, useSyncExternalStore } from 'react';
import { createPredictiveApi } from '../services/predictive.js';
import { createVerificationResource } from '../services/verificationResource.js';

export function usePredictiveOperations(session, onLogout, enabled) {
  const resource = useMemo(() => {
    const api = createPredictiveApi(session);
    return createVerificationResource({ intervalMs: 300000,
      onUnauthorized: () => onLogout('Sua sessão expirou. Entre novamente.'),
      load: async signal => {
        try { return [{ id: 'predictive', data: await api.operations(signal) }]; }
        catch (error) { return [{ id: 'predictive', error }]; }
      }
    });
  }, [session, onLogout]);
  const state = useSyncExternalStore(resource.subscribe, resource.getSnapshot);
  useEffect(() => { if (enabled) resource.start(); return resource.stop; }, [resource, enabled]);
  return { ...state, result: state.results[0], refresh: resource.refresh };
}
