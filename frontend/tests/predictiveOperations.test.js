import test from 'node:test';
import assert from 'node:assert/strict';
import {
  attachPredictiveRisk,
  createPredictiveApi,
  normalizeExecutionStatus,
  normalizePredictiveOperations,
} from '../src/services/predictive.js';
import { markerPresentation } from '../src/components/map/markerPresentation.js';
import { getEquipmentState } from '../src/components/map/markerData.js';
import { createVerificationResource } from '../src/services/verificationResource.js';

const at = '2026-10-24T15:00:00Z';
const execution = {
  status: 'NotRun',
  origin: null,
  startedAt: null,
  finishedAt: null,
  lastCompletedAt: null,
  lastAutomaticCompletedAt: null,
  recommendationsCreated: null,
  preventiveAlertsCreated: null,
  scope: 'InstanceMemory',
};
const automation = {
  enabled: false,
  intervalMinutes: 60,
  multipleInstancesSupported: true,
  execution,
};
const risk = {
  equipmentId: 'camera',
  serialNumber: 'C1',
  equipmentType: 'Camera',
  status: 'Operational',
  neighborhood: 'Centro',
  score: 55,
  level: 'High',
  evaluatedAt: at,
  windowFrom: at,
  hasSufficientHistory: true,
  historyDescription: 'Amostra descritiva',
  summary: 'Falhas históricas e planejamento vencido',
  factors: [],
};
const response = {
  evaluatedAt: at,
  equipmentRisks: [risk],
  neighborhoods: [{ ...risk, latitude: -23.55, longitude: -46.64 }],
  recommendations: [],
  automation,
};
const session = { token: 'teste', user: { role: 'CentralOperator' } };

test('Central consulta riscos e estado com GET e não dispara análise nem aceite', async () => {
  const paths = [];
  const api = createPredictiveApi(session, {
    getJson: async (path, options) => {
      paths.push(path);
      assert.equal(options.token, 'teste');
      return path.includes('execution-status') ? automation : response;
    },
    postJson: () => assert.fail('Leitura não pode escrever'),
  });
  const data = await api.operations();
  assert.equal(data.equipmentRisks[0].level, 'High');
  assert.equal((await api.executionStatus()).execution.status, 'NotRun');
  assert.deepEqual(paths, ['/api/dashboard/predictive', '/api/predictions/execution-status']);
});
test('estado em memória não fabrica datas ou contagens e valida origem e contrato', () => {
  const initial = normalizeExecutionStatus(automation);
  assert.equal(initial.execution.lastAutomaticCompletedAt, null);
  assert.equal(initial.execution.recommendationsCreated, null);
  for (const invalid of [
    { status: 'desconhecido' },
    { origin: 'inventada' },
    { scope: 'Global' },
    { recommendationsCreated: -1 },
    { lastAutomaticCompletedAt: 'inválida' },
  ])
    assert.throws(
      () => normalizeExecutionStatus({ ...automation, execution: { ...execution, ...invalid } }),
      { kind: 'response' },
    );
  assert.equal(
    normalizeExecutionStatus({
      ...automation,
      execution: {
        ...execution,
        status: 'Completed',
        origin: 'Automatic',
        recommendationsCreated: 0,
        preventiveAlertsCreated: 0,
        lastAutomaticCompletedAt: at,
      },
    }).execution.recommendationsCreated,
    0,
  );
});
test('contrato operacional preserva coordenadas e descarta propriedades privadas', () => {
  const normalized = normalizePredictiveOperations({
    ...response,
    token: 'privado',
    equipmentRisks: [{ ...risk, cpf: 'privado', payloadData: 'privado' }],
  });
  assert.equal(normalized.neighborhoods[0].latitude, -23.55);
  assert.equal(normalized.neighborhoods[0].longitude, -46.64);
  assert.ok(!JSON.stringify(normalized).includes('privado'));
  assert.throws(() => normalizePredictiveOperations({ ...response, equipmentRisks: null }), {
    kind: 'response',
  });
});
test('risco alto acrescenta indicação independente sem transformar operacional em falha', () => {
  const original = [
    { id: 'camera', type: 'Camera', status: 'Operational' },
    { id: 'outra', status: 'Malfunctioning' },
  ];
  const records = attachPredictiveRisk(original, { data: response });
  assert.equal(records[0].status, 'Operational');
  assert.equal(records[0].predictiveRisk.score, 55);
  assert.equal(records[1].status, 'Malfunctioning');
  assert.equal(records[1].predictiveRisk, null);
  assert.equal(original[0].predictiveRisk, undefined);
  const marker = markerPresentation(
    'equipment',
    'Camera',
    getEquipmentState(records[0].status),
    '',
    true,
  );
  assert.match(marker.className, /state-operational/);
  assert.match(marker.className, /predictive-high/);
  assert.doesNotMatch(marker.className, /malfunctioning/);
  assert.match(marker.html, />P</);
});
test('falha real manutenção e risco coexistem sem trocar símbolo ou situação', () => {
  const actual = markerPresentation(
    'equipment',
    'Radar',
    getEquipmentState('Malfunctioning'),
    'immediate',
    true,
  );
  assert.match(actual.className, /state-malfunctioning/);
  assert.match(actual.className, /attention-immediate/);
  assert.match(actual.className, /predictive-high/);
  const agent = markerPresentation('agent', 'TrafficAgent', { tone: 'available' }, '', true);
  assert.doesNotMatch(agent.className, /predictive-high/);
});
test('erro preditivo inicial não inventa risco e leitura antiga permanece identificada', () => {
  const records = [{ id: 'camera', status: 'Operational' }];
  assert.equal(
    attachPredictiveRisk(records, { error: new Error('offline') })[0].predictiveRisk,
    null,
  );
  const stale = attachPredictiveRisk(records, {
    data: response,
    error: new Error('offline'),
    stale: true,
  });
  assert.equal(stale[0].predictiveRisk.stale, true);
  assert.equal(stale[0].status, 'Operational');
  const marker = markerPresentation(
    'equipment',
    'Camera',
    getEquipmentState('Operational'),
    '',
    true,
    true,
  );
  assert.match(marker.className, /predictive-stale/);
  assert.notEqual(
    marker.key,
    markerPresentation('equipment', 'Camera', getEquipmentState('Operational'), '', true).key,
  );
});
test('consulta periódica conservadora preserva dados em falha parcial e cancela ao sair da Central', async () => {
  let fail = false,
    loads = 0,
    latestSignal;
  const timers = new Map();
  let sequence = 0;
  const flush = async () => {
    for (let i = 0; i < 20; i++) await Promise.resolve();
  };
  const resource = createVerificationResource({
    intervalMs: 300000,
    onUnauthorized: () => assert.fail('Sem 401'),
    load: async (signal) => {
      latestSignal = signal;
      loads++;
      return [
        fail
          ? { id: 'predictive', error: new Error('fonte falhou') }
          : { id: 'predictive', data: response },
      ];
    },
    setTimer: (callback, delay) => {
      timers.set(++sequence, { callback, delay });
      return sequence;
    },
    clearTimer: (id) => timers.delete(id),
  });
  resource.start();
  await flush();
  assert.equal(loads, 1);
  assert.ok([...timers.values()].some((timer) => timer.delay === 300000));
  const successful = resource.getSnapshot().updatedAt;
  fail = true;
  resource.refresh();
  await flush();
  assert.equal(resource.getSnapshot().results[0].stale, true);
  assert.equal(resource.getSnapshot().updatedAt, successful);
  assert.equal(resource.getSnapshot().results[0].data.equipmentRisks[0].score, 55);
  fail = false;
  resource.refresh();
  await flush();
  assert.equal(resource.getSnapshot().error, '');
  resource.stop();
  assert.equal(timers.size, 0);
  assert.ok(latestSignal);
  resource.refresh();
  await flush();
  assert.equal(loads, 3);
});
test('perfis sem monitoramento não consultam endpoints preditivos operacionais', async () => {
  for (const role of ['MaintenanceTechnician', 'Agent']) {
    const api = createPredictiveApi(
      { ...session, user: { role } },
      { getJson: () => assert.fail('Consulta indevida') },
    );
    await assert.rejects(api.operations(), /permissão/);
    await assert.rejects(api.executionStatus(), /permissão/);
  }
});
