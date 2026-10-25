import { mergeVerificationResults } from './verificationState.js';

export function resolvePollingInterval(value) {
  const parsed = Number(value);
  return Number.isFinite(parsed) && parsed >= 5000 && parsed <= 300000 ? Math.round(parsed) : 30000;
}
export function createVerificationResource({
  load,
  onUnauthorized,
  intervalMs = 30000,
  poll = true,
  now = () => new Date(),
  setTimer = setTimeout,
  clearTimer = clearTimeout,
}) {
  let state = { loading: false, results: [], updatedAt: null, attemptedAt: null, error: '' };
  let running = false,
    timer = null,
    deadlineTimer = null,
    controller = null,
    generation = 0;
  const listeners = new Set();
  const publish = (next) => {
    state = next;
    listeners.forEach((listener) => listener());
  };
  const clear = () => {
    if (timer !== null) clearTimer(timer);
    timer = null;
  };
  async function request() {
    if (!running || controller) return;
    clear();
    const current = new AbortController();
    controller = current;
    const revision = generation;
    publish({ ...state, loading: true });
    let rejectOnAbort;
    const aborted = new Promise((_, reject) => {
      rejectOnAbort = () => reject(current.signal.reason);
      current.signal.addEventListener('abort', rejectOnAbort, { once: true });
    });
    const deadline = setTimer(
      () => current.abort(new Error('Tempo de sincronização excedido.')),
      20000,
    );
    deadlineTimer = deadline;
    try {
      const results = await Promise.race([load(current.signal), aborted]);
      if (!running || revision !== generation) return;
      if (current.signal.aborted) throw current.signal.reason;
      if (results.some((result) => result.error?.status === 401)) {
        stop();
        onUnauthorized();
        return;
      }
      const date = now(),
        failed = results.some((result) => result.error);
      publish({
        loading: false,
        results: mergeVerificationResults(state.results, results, date.toISOString()),
        updatedAt: failed ? state.updatedAt : date,
        attemptedAt: date,
        error: failed
          ? 'Sincronização parcial: consulte os avisos. Os últimos dados válidos foram preservados.'
          : '',
      });
    } catch {
      if (!running || revision !== generation) return;
      publish({
        ...state,
        loading: false,
        attemptedAt: now(),
        error: 'Falha de sincronização. Os últimos dados válidos foram preservados.',
        results: state.results.map((result) => ({
          ...result,
          stale: Object.hasOwn(result, 'data'),
        })),
      });
    } finally {
      clearTimer(deadline);
      current.signal.removeEventListener('abort', rejectOnAbort);
      if (deadlineTimer === deadline) deadlineTimer = null;
      if (revision === generation) {
        controller = null;
        if (running && poll)
          timer = setTimer(() => {
            timer = null;
            void request();
          }, intervalMs);
      }
    }
  }
  function start() {
    if (running) return;
    running = true;
    const revision = ++generation;
    queueMicrotask(() => {
      if (running && generation === revision) void request();
    });
  }
  function stop() {
    running = false;
    generation++;
    clear();
    if (deadlineTimer !== null) clearTimer(deadlineTimer);
    deadlineTimer = null;
    controller?.abort();
    controller = null;
  }
  return {
    start,
    stop,
    refresh: () => {
      void request();
    },
    getSnapshot: () => state,
    subscribe: (listener) => {
      listeners.add(listener);
      return () => listeners.delete(listener);
    },
  };
}
