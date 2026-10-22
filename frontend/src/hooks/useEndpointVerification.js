// Bibliotecas externas
import { useCallback, useEffect, useState } from 'react';

// Serviços e utilitários
import { loadVerification } from '../services/verification.js';
import { isAbortError } from '../services/http.js';
import { mergeVerificationResults } from '../services/verificationState.js';

export function useEndpointVerification(token, onLogout) {
  const [revision, setRevision] = useState(0);
  const [state, setState] = useState({
    loading: true,
    results: [],
    updatedAt: null,
    error: ''
  });

  const refresh = useCallback(() => {
    setState(current => ({
      ...current,
      loading: true,
      error: ''
    }));
    setRevision(value => value + 1);
  }, []);

  useEffect(() => {
    const controller = new AbortController();

    async function load() {
      try {
        const results = await loadVerification(token, {
          signal: controller.signal
        });
        if (controller.signal.aborted) return;
        if (results.some(result => result.error?.status === 401)) {
          onLogout('A API recusou a sessão (401). Entre novamente.');

          return;
        }
        const now = new Date();
        setState(current => ({
          loading: false,
          results: mergeVerificationResults(current.results, results, now.toISOString()),
          updatedAt: now,
          error: ''
        }));
      } catch (error) {
        if (!controller.signal.aborted && !isAbortError(error)) {
          setState(current => ({
            ...current,
            loading: false,
            results: current.results.map(result => ({
              ...result,
              stale: Object.hasOwn(result, 'data')
            })),
            error: 'Não foi possível concluir a verificação. Os dados anteriores podem estar desatualizados.'
          }));
        }
      }
    }
    // O primeiro ciclo de teste do StrictMode é limpo antes de iniciar os GETs.

    queueMicrotask(() => {
      if (!controller.signal.aborted) void load();
    });

    return () => controller.abort();
  }, [token, onLogout, revision]);

  return {
    ...state,
    refresh
  };
}
