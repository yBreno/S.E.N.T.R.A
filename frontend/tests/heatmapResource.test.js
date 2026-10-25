// Bibliotecas externas
import test from 'node:test';
import assert from 'node:assert/strict';

// Serviços e utilitários
import { createHeatmapResource } from '../src/services/heatmapResource.js';
import {
  buildHeatmapQuery,
  createHeatmapFilters,
  defaultHeatmapQuery,
} from '../src/services/heatmapQuery.js';
import { HttpError } from '../src/services/http.js';

const query = (metric = 'Failures', neighborhood = '') =>
  buildHeatmapQuery({
    ...createHeatmapFilters(),
    metric,
    neighborhood,
  }).query;

const deferred = () => {
  let resolve, reject;
  const promise = new Promise((yes, no) => {
    resolve = yes;
    reject = no;
  });

  return {
    promise,
    resolve,
    reject,
  };
};

const resource = (load, onUnauthorized = () => {}) =>
  createHeatmapResource({
    load,
    onUnauthorized,
    now: () => '2026-01-02T00:00:00Z',
  });

const completeShared = (store) =>
  store.setShared({
    loading: false,
    result: null,
    attemptedAt: '2026-01-01T00:00:00Z',
  });

test('polling do calor não duplica consulta padrão nem aplica filtro ainda não consultado', async () => {
  let calls = 0;
  const store = resource(async () => {
    calls++;
    return { points: [] };
  });
  completeShared(store);
  await store.poll();
  assert.equal(calls, 0);
  store.setQuery(query('Failures'));
  await store.poll();
  assert.equal(calls, 0);
  await store.request();
  assert.equal(calls, 1);
  await store.poll();
  assert.equal(calls, 2);
  store.cancel();
});

test('reutiliza a resposta compartilhada e evita GET equivalente até atualização explícita', async () => {
  let calls = 0;
  const store = resource(async () => {
    calls++;

    return {
      metric: 'Incidents',
    };
  });
  store.setShared({
    loading: false,
    result: {
      heatmap: {
        metric: 'Incidents',
      },
      receivedAt: '2026-01-01T00:00:00Z',
    },
  });
  await store.request();
  assert.equal(calls, 0);
  await store.request({
    force: true,
  });
  assert.equal(calls, 1);
});
test('consulta padrão aguarda uma requisição compartilhada pendente sem duplicá-la', async () => {
  let calls = 0;
  const store = resource(async () => {
    calls++;
  });
  await store.request();
  assert.equal(calls, 0);
  assert.equal(store.getSnapshot().loading, true);
  store.setShared({
    loading: false,
    result: {
      heatmap: {
        metric: 'Incidents',
      },
      receivedAt: '2026-01-01T00:00:00Z',
    },
  });
  assert.equal(store.getSnapshot().loading, false);
  assert.equal(store.getSnapshot().data.metric, 'Incidents');
});
test('deduplica requisições pendentes da mesma consulta', async () => {
  const pending = deferred();
  let calls = 0;
  const store = resource(() => {
    calls++;

    return pending.promise;
  });
  store.setQuery(query());
  const first = store.request();
  const second = store.request({
    force: true,
  });
  assert.equal(first, second);
  assert.equal(calls, 1);
  pending.resolve({
    metric: 'Failures',
  });
  await first;
});
test('mudar filtros aborta a requisição anterior e ignora sucesso atrasado mesmo sem cooperação do transporte', async () => {
  const old = deferred();
  const latest = deferred();
  const signals = [];
  const store = resource((current, { signal }) => {
    signals.push(signal);

    return current.metric === 'Failures' ? old.promise : latest.promise;
  });
  store.setQuery(query('Failures'));
  const first = store.request();
  store.setQuery(query('Risk'));
  assert.equal(signals[0].aborted, true);
  const second = store.request();
  latest.resolve({
    metric: 'Risk',
  });
  await second;
  old.resolve({
    metric: 'Failures',
  });
  await first;
  assert.equal(store.getSnapshot().data.metric, 'Risk');
});
test('401 atrasado de consulta cancelada não encerra a sessão atual', async () => {
  const old = deferred();
  let unauthorized = 0;
  const store = resource(
    () => old.promise,
    () => unauthorized++,
  );
  store.setQuery(query());
  const first = store.request();
  store.setQuery(query('Risk'));
  old.reject(
    new HttpError('401', {
      status: 401,
    }),
  );
  await first;
  assert.equal(unauthorized, 0);
  assert.equal(store.getSnapshot().error, null);
});
test('falha preserva somente o último sucesso dos mesmos filtros', async () => {
  let fail = false;
  const store = resource(async () => {
    if (fail)
      throw new HttpError('Rede', {
        kind: 'network',
      });

    return {
      metric: 'Failures',
    };
  });
  store.setQuery(query());
  await store.request();
  const validTime = store.getSnapshot().receivedAt;
  fail = true;
  await store.request({
    force: true,
  });
  assert.equal(store.getSnapshot().data.metric, 'Failures');
  assert.equal(store.getSnapshot().stale, true);
  assert.equal(store.getSnapshot().receivedAt, validTime);
  store.setQuery(query('Risk'));
  assert.equal(store.getSnapshot().data, null);
  await store.request();
  assert.equal(store.getSnapshot().data, null);
  assert.equal(store.getSnapshot().stale, false);
});
test('403 mantém a sessão e informa erro; 401 vigente chama encerramento existente', async () => {
  let status = 403;
  let unauthorized = 0;
  const store = resource(
    async () => {
      throw new HttpError(String(status), {
        status,
      });
    },
    () => unauthorized++,
  );
  store.setQuery(query());
  await store.request();
  assert.equal(store.getSnapshot().error.status, 403);
  assert.equal(unauthorized, 0);
  status = 401;
  await store.request({
    force: true,
  });
  assert.equal(unauthorized, 1);
});
test('cancelar remove a pendência e impede publicação após desmontagem/desativação', async () => {
  const task = deferred();
  let signal;
  const store = resource((current, options) => {
    signal = options.signal;

    return task.promise;
  });
  store.setQuery(query());
  const pending = store.request();
  store.cancel();
  assert.equal(signal.aborted, true);
  assert.equal(store.getSnapshot().loading, false);
  task.resolve({
    metric: 'Failures',
  });
  await pending;
  assert.equal(store.getSnapshot().data, null);
});
test('cache é reutilizado ao alternar as métricas e é isolado por consulta', async () => {
  let calls = 0;
  const store = resource(async (current) => {
    calls++;

    return {
      metric: current.metric,
    };
  });
  completeShared(store);
  for (const metric of ['Incidents', 'Failures', 'Risk']) {
    store.setQuery(query(metric));
    await store.request();
  }
  store.setQuery(defaultHeatmapQuery);
  await store.request();
  assert.equal(calls, 3);
  assert.equal(store.getSnapshot().data.metric, 'Incidents');
});
test('consulta inválida limpa a visão e não dispara HTTP', async () => {
  let calls = 0;
  const store = resource(async () => {
    calls++;
  });
  store.setQuery(null);
  await store.request();
  assert.equal(calls, 0);
  assert.equal(store.getSnapshot().data, null);
});
test('resposta compartilhada antiga não substitui uma atualização manual mais recente', async () => {
  const store = resource(async () => ({
    manual: true,
  }));
  completeShared(store);
  await store.request();
  store.setShared({
    loading: false,
    result: {
      heatmap: {
        manual: false,
      },
      receivedAt: '2026-01-01T00:00:00Z',
    },
    attemptedAt: '2026-01-03T00:00:00Z',
  });
  assert.equal(store.getSnapshot().data.manual, true);
});
test('dados compartilhados se recuperam após erro inicial sem resposta válida', async () => {
  const store = resource(async () => {});
  store.setShared({
    loading: false,
    result: {
      error: new Error('Falha'),
    },
    attemptedAt: '2026-01-01T00:00:00Z',
  });
  assert.ok(store.getSnapshot().error);
  store.setShared({
    loading: false,
    result: {
      heatmap: {
        valid: true,
      },
      receivedAt: '2026-01-02T00:00:00Z',
    },
    attemptedAt: '2026-01-02T00:00:00Z',
  });
  assert.equal(store.getSnapshot().data.valid, true);
  assert.equal(store.getSnapshot().error, null);
});
test('falha compartilhada posterior marca como antigo um sucesso obtido manualmente para os mesmos filtros', async () => {
  const store = resource(async () => ({
    valid: true,
  }));
  store.setShared({
    loading: false,
    result: {
      error: new Error('Inicial'),
    },
    attemptedAt: '2026-01-01T00:00:00Z',
  });
  await store.request();
  store.setShared({
    loading: false,
    result: {
      error: new Error('Nova falha'),
    },
    attemptedAt: '2026-01-03T00:00:00Z',
  });
  assert.equal(store.getSnapshot().data.valid, true);
  assert.equal(store.getSnapshot().stale, true);
  assert.equal(store.getSnapshot().receivedAt, '2026-01-02T00:00:00Z');
});
