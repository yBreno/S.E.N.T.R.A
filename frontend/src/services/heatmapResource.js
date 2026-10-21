// Serviços e utilitários
import { defaultHeatmapQuery } from './heatmapQuery.js';
import {
  isAbortError
} from './http.js';

// Cache em memória por consulta e por sessão. Nenhum token ou dado persiste fora do hook.
export function createHeatmapResource({
  load,
  onUnauthorized,
  now = () => new Date().toISOString()
}) {
  const cache = new Map();
  const listeners = new Set();
  let query = defaultHeatmapQuery;
  let sharedLoading = true;
  let sharedStamp = null;
  let active = null;
  let sequence = 0;
  let snapshot = {
    key: query.key,
    data: null,
    receivedAt: null,
    error: null,
    stale: false,
    loading: false
  };

  const publish = next => {
    snapshot = next;
    listeners.forEach(listener => listener());
  };

  const entry = () => cache.get(query?.key);

  const show = (overrides = {}) => publish({
    key: query?.key ?? null,
    data: null,
    receivedAt: null,
    error: null,
    stale: false,
    loading: false,
    ...entry(),
    ...overrides
  });

  function remember(key, value) {
    cache.delete(key);
    cache.set(key, value);
    if (cache.size > 8) cache.delete(Array.from(cache.keys()).find(item => item !== query?.key));
  }

  function cancel(update = true) {
    sequence++;
    active?.controller.abort();
    active = null;
    if (update) show();
  }

  function setQuery(next) {
    if (query?.key === next?.key) return;
    cancel(false);
    query = next;
    show();
  }

  function setShared({
    result,
    loading,
    attemptedAt
  }) {
    sharedLoading = loading;
    if (loading) return;
    const stamp = `${result?.receivedAt || ''}:${attemptedAt || ''}:${Boolean(result?.error)}`;
    if (sharedStamp === stamp) return;
    sharedStamp = stamp;
    const previous = cache.get(defaultHeatmapQuery.key);
    if (result?.heatmap && (!previous?.data || result.receivedAt >= previous.receivedAt)) {
      remember(defaultHeatmapQuery.key, {
        data: result.heatmap,
        receivedAt: result.receivedAt,
        stale: Boolean(result.stale),
        error: result.error || null
      });
    } else if (result?.error && (!previous?.data || attemptedAt >= previous.receivedAt)) {
      remember(defaultHeatmapQuery.key, {
        data: previous?.data || null,
        receivedAt: previous?.receivedAt || null,
        stale: Boolean(previous?.data),
        error: result.error
      });
    }
    if (query?.key === defaultHeatmapQuery.key && !active) show();
  }

  function request({
    force = false
  } = {}) {
    if (!query) return Promise.resolve();
    if (active?.key === query.key) return active.promise;
    if (query.key === defaultHeatmapQuery.key && sharedLoading) {
      show({
        loading: true,
        stale: Boolean(entry()?.data)
      });

      return Promise.resolve();
    }
    if (!force && entry()?.data && !entry().stale) {
      show();

      return Promise.resolve();
    }
    cancel(false);
    const ticket = sequence;
    const requested = query;
    const controller = new AbortController();
    const task = {
      key: requested.key,
      controller,
      promise: null
    };
    active = task;
    show({
      loading: true,
      stale: Boolean(entry()?.data),
      error: null
    });
    task.promise = (async () => {
      try {
        const data = await load(requested, {
          signal: controller.signal
        });
        if (controller.signal.aborted || sequence !== ticket || query?.key !== requested.key) return;
        remember(requested.key, {
          data,
          receivedAt: now(),
          stale: false,
          error: null
        });
        show();
      } catch (error) {
        if (controller.signal.aborted || sequence !== ticket || query?.key !== requested.key || isAbortError(error)) return;
        if (error?.status === 401) {
          onUnauthorized();

          return;
        }
        const last = cache.get(requested.key);
        remember(requested.key, {
          data: last?.data || null,
          receivedAt: last?.receivedAt || null,
          stale: Boolean(last?.data),
          error
        });
        show();
      } finally {
        if (active === task) active = null;
      }
    })();

    return task.promise;
  }

  return {
    getSnapshot: () => snapshot,
    subscribe: listener => {
      listeners.add(listener);

      return () => listeners.delete(listener);
    },
    setQuery,
    setShared,
    request,
    cancel
  };
}
