import test from 'node:test';
import assert from 'node:assert/strict';
import {
  createPredictiveApi,
  predictiveEndpoints,
  prioritizeRecommendations,
  recommendationPriority,
  predictiveError,
} from '../src/services/predictive.js';
import { createVerificationResource } from '../src/services/verificationResource.js';
import { createHttpClient } from '../src/services/http.js';

const session = (role) => ({ token: 'token-de-teste', user: { role } });
const at = '2026-10-24T15:00:00Z';
const factor = {
  name: 'RecentFailures',
  label: 'Falhas recentes',
  rawDescription: '3 falhas reais',
  points: 30,
  weight: 0.3,
  normalized: 1,
  rule: 'contagem / 3',
  recordCount: 3,
  recordIds: ['registro'],
};
const risk = {
  equipmentId: 'equipamento',
  serialNumber: 'PRED-1',
  equipmentType: 'Camera',
  status: 'Operational',
  neighborhood: 'Centro',
  score: 55,
  level: 'High',
  evaluatedAt: at,
  windowFrom: '2026-09-24T15:00:00Z',
  hasSufficientHistory: true,
  historyDescription: 'Amostra descritiva',
  summary: 'Falhas recorrentes e preventiva atrasada',
  recommendedAction: 'Inspecionar',
  factors: [factor],
};
const recommendation = {
  id: 'recomendacao',
  type: 'SendMaintenance',
  neighborhood: 'Centro',
  reason: 'Inspeção por falhas',
  status: 'Pending',
  riskScore: 55,
  equipmentId: 'equipamento',
  createdAt: at,
  explanation: {
    assessment: risk,
    action: 'Inspecionar',
    rule: 'alto risco',
    cpf: 'privado',
    payloadData: 'privado',
  },
};
const fixtures = {
  equipment: [risk],
  neighborhoods: [{ ...risk, incidentWindowFrom: at, failureWindowFrom: at }],
  recommendations: [recommendation],
  failures: [],
  anomalies: [],
};
const clientFor = (data) => ({
  getJson: async (path) => data[predictiveEndpoints.find((endpoint) => endpoint.path === path).id],
});
const flush = async () => {
  for (let i = 0; i < 40; i++) await Promise.resolve();
};

test('consulta contratos existentes e novo risco sem expor campos privados extras', async () => {
  const result = await createPredictiveApi(session('CentralOperator'), clientFor(fixtures)).load();
  assert.equal(result.length, 5);
  assert.ok(result.every((item) => !item.error));
  assert.equal(result[0].data[0].score, 55);
  assert.equal(result[2].data[0].priority, 'High');
  assert.equal(JSON.stringify(result).includes('privado'), false);
  assert.equal(result[2].data[0].factors[0].recordCount, 3);
});

test('contrato inválido falha somente na fonte afetada e não vira dado vazio', async () => {
  for (const mutation of [
    { score: 101 },
    { evaluatedAt: 'inválida' },
    { level: 'toString' },
    { factors: null },
    { hasSufficientHistory: null },
  ]) {
    const result = await createPredictiveApi(
      session('Admin'),
      clientFor({ ...fixtures, equipment: [{ ...risk, ...mutation }] }),
    ).load();
    assert.equal(result[0].error.kind, 'response');
    assert.equal(result[1].data.length, 1);
    assert.equal(Object.hasOwn(result[0], 'data'), false);
  }
});

test('carregamento manual preserva leitura válida em erro e recupera sem polling ou análise automática', async () => {
  let release,
    fail = false,
    posts = 0;
  const pending = new Promise((resolve) => {
    release = resolve;
  });
  const api = createPredictiveApi(session('Admin'), {
    ...clientFor(fixtures),
    getJson: async (path) => {
      await pending;
      if (fail && path.endsWith('equipment-risk')) throw new Error('offline');
      return clientFor(fixtures).getJson(path);
    },
    postJson: () => posts++,
  });
  const timers = new Map();
  let id = 0,
    tick = 0;
  const resource = createVerificationResource({
    load: api.load,
    poll: false,
    onUnauthorized: () => assert.fail('sessão válida'),
    now: () => new Date(++tick * 1000),
    setTimer: (callback, delay) => {
      timers.set(++id, { callback, delay });
      return id;
    },
    clearTimer: (key) => timers.delete(key),
  });
  resource.start();
  await flush();
  assert.equal(resource.getSnapshot().loading, true);
  resource.refresh();
  release();
  await flush();
  assert.equal(resource.getSnapshot().loading, false);
  assert.equal(timers.size, 0);
  assert.equal(posts, 0);
  const initial = resource.getSnapshot().updatedAt;
  fail = true;
  resource.refresh();
  await flush();
  assert.ok(resource.getSnapshot().error);
  assert.equal(resource.getSnapshot().updatedAt, initial);
  assert.equal(resource.getSnapshot().results[0].stale, true);
  assert.equal(resource.getSnapshot().results[0].data[0].score, 55);
  fail = false;
  resource.refresh();
  await flush();
  assert.equal(resource.getSnapshot().error, '');
  assert.equal(resource.getSnapshot().results[0].stale, false);
  assert.notEqual(resource.getSnapshot().updatedAt, initial);
  resource.stop();
});

test('respostas vazias permanecem vazias e erro inicial não fabrica pontuação', async () => {
  const empty = Object.fromEntries(predictiveEndpoints.map((endpoint) => [endpoint.id, []]));
  const result = await createPredictiveApi(session('Admin'), clientFor(empty)).load();
  assert.ok(result.every((item) => item.data.length === 0));
  const failed = await createPredictiveApi(session('Admin'), {
    getJson: async () => {
      throw new Error('offline');
    },
  }).load();
  assert.ok(failed.every((item) => item.error && !Object.hasOwn(item, 'data')));
});

test('papéis sem monitoramento não enviam consultas nem análise e operador não aceita nem descarta', async () => {
  const forbidden = {
    getJson: () => assert.fail('HTTP indevido'),
    postJson: () => assert.fail('HTTP indevido'),
  };
  for (const role of ['Agent', 'MaintenanceTechnician']) {
    const api = createPredictiveApi(session(role), forbidden);
    await assert.rejects(api.load(), /permissão/);
    await assert.rejects(api.analyze(), /permissão/);
  }
  const operator = createPredictiveApi(session('CentralOperator'), forbidden);
  await assert.rejects(operator.resolve('id', 'accept'), /permissão/);
  await assert.rejects(operator.resolve('id', 'dismiss'), /permissão/);
});

test('análise explícita e aceite usam Bearer e endpoints reais sem enviar dados fictícios', async () => {
  const calls = [],
    controller = new AbortController();
  const transport = createHttpClient({ baseUrl: 'http://127.0.0.1:5129' }, async (url, options) => {
    calls.push({ url, options });
    return new Response(
      JSON.stringify(
        url.endsWith('/analyze')
          ? { preventiveAlertsCreated: 1, recommendationsCreated: 2 }
          : { ...recommendation, status: 'Accepted' },
      ),
      { headers: { 'Content-Type': 'application/json' } },
    );
  });
  const api = createPredictiveApi(session('OperationManager'), transport);
  assert.deepEqual(await api.analyze(controller.signal), {
    preventiveAlertsCreated: 1,
    recommendationsCreated: 2,
  });
  assert.equal((await api.resolve('recomendacao', 'accept', controller.signal)).status, 'Accepted');
  assert.ok(calls[0].url.endsWith('/api/predictions/analyze'));
  assert.ok(calls[1].url.endsWith('/api/recommendations/recomendacao/accept'));
  for (const call of calls) {
    assert.equal(call.options.method, 'POST');
    assert.equal(call.options.headers.Authorization, 'Bearer token-de-teste');
    assert.equal(call.options.signal, controller.signal);
    assert.equal(call.options.body, undefined);
  }
});

test('priorização respeita nível do equipamento sem promovê-lo artificialmente a crítico', () => {
  const high = { id: 'alto', riskScore: 90, priority: 'High', createdAt: at };
  const critical = { id: 'critico', riskScore: 75, priority: 'Critical', createdAt: at };
  const unknown = { id: 'sem-historico', riskScore: null, createdAt: at };
  assert.equal(recommendationPriority(high), 'High');
  assert.equal(recommendationPriority(unknown), undefined);
  assert.deepEqual(
    prioritizeRecommendations([unknown, high, critical]).map((item) => item.id),
    ['critico', 'alto', 'sem-historico'],
  );
});

test('conflito e resultado incerto orientam atualização antes de repetir operação', () => {
  assert.match(predictiveError({ status: 409 }), /ordem ativa/);
  assert.match(predictiveError({ kind: 'network' }), /antes de repetir/);
  assert.match(predictiveError({ name: 'AbortError' }), /confirmar/);
  assert.match(predictiveError({ kind: 'network' }, { reading: true }), /consultar esta fonte/);
});
