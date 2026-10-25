// Bibliotecas externas
import test from 'node:test';
import assert from 'node:assert/strict';

// Serviços e utilitários
import {
  buildHeatmapQuery,
  createHeatmapFilters,
  defaultHeatmapQuery,
} from '../src/services/heatmapQuery.js';
import { normalizeHeatmapResponse, getHeatmap } from '../src/services/heatmap.js';
import { createHttpClient } from '../src/services/http.js';
import { loadVerification } from '../src/services/verification.js';

const filters = (values = {}) => ({
  ...createHeatmapFilters(),
  ...values,
});

const response = (points = [], metric = 'Incidents') => ({
  metric,
  from: null,
  to: null,
  totalPoints: points.length,
  maxIntensity: 1,
  points,
});

test('conversão mantém ordem lat/lng e intensidade original, inclusive menor que 0.2 e maior que 1', () => {
  const input = response([
    {
      latitude: -23.55,
      longitude: -46.63,
      intensity: 0.01,
    },
    {
      latitude: 0,
      longitude: 0,
      intensity: 2,
    },
  ]);
  const result = normalizeHeatmapResponse(input, 'Incidents');
  assert.deepEqual(result.heatPoints, [
    [-23.55, -46.63, 0.01],
    [0, 0, 2],
  ]);
  assert.equal(result.renderMax, 2);
  assert.equal(input.points[0].intensity, 0.01);
});
test('zero é válido e contabilizado, sem enviar intensidade zero ao plugin', () => {
  const result = normalizeHeatmapResponse(
    response([
      {
        latitude: 0,
        longitude: 0,
        intensity: 0,
      },
    ]),
    'Incidents',
  );
  assert.equal(result.validCount, 1);
  assert.equal(result.discardedCount, 0);
  assert.equal(result.zeroCount, 1);
  assert.deepEqual(result.heatPoints, []);
});
test('coordenadas e intensidades inválidas são descartadas individualmente', () => {
  const points = [
    null,
    {
      latitude: null,
      longitude: 0,
      intensity: 1,
    },
    {
      latitude: 91,
      longitude: 0,
      intensity: 1,
    },
    ...[null, '1', NaN, Infinity, -1].map((intensity) => ({
      latitude: 0,
      longitude: 0,
      intensity,
    })),
    {
      latitude: -90,
      longitude: 180,
      intensity: 0.5,
    },
  ];
  const result = normalizeHeatmapResponse(response(points), 'Incidents');
  assert.equal(result.validCount, 1);
  assert.equal(result.discardedCount, points.length - 1);
  assert.deepEqual(result.heatPoints, [[-90, 180, 0.5]]);
});
test('resposta vazia é válida e não gera pontos de demonstração', () => {
  const result = normalizeHeatmapResponse(response(), 'Incidents');
  assert.equal(result.totalPoints, 0);
  assert.equal(result.validCount, 0);
  assert.equal(result.discardedCount, 0);
  assert.deepEqual(result.heatPoints, []);
});
test('envelope incompatível, contagem divergente e métrica errada são erros de contrato', () => {
  for (const value of [
    null,
    {},
    {
      ...response(),
      points: null,
    },
    {
      ...response(),
      totalPoints: 1,
    },
    {
      ...response(),
      maxIntensity: NaN,
    },
    {
      ...response(),
      from: 'invalid',
    },
    response([], 'Risk'),
  ]) {
    assert.throws(() => normalizeHeatmapResponse(value, 'Incidents'), {
      kind: 'response',
    });
  }
});
test('Incidents envia seus filtros sem parâmetros de equipamentos ou agentes', () => {
  const result = buildHeatmapQuery(
    filters({
      from: '2026-01-01T08:00',
      to: '2026-01-02T08:00',
      fromHour: '22',
      toHour: '2',
      dayOfWeek: 'Monday',
      neighborhood: ' Centro ',
      type: 'Flooding',
    }),
  );
  assert.equal(result.error, null);
  const params = new URL(`http://example.invalid${result.query.path}`).searchParams;
  assert.equal(params.get('metric'), 'Incidents');
  assert.equal(params.get('from'), new Date('2026-01-01T08:00').toISOString());
  assert.equal(params.get('to'), new Date('2026-01-02T08:00').toISOString());
  assert.equal(params.get('fromHour'), '22');
  assert.equal(params.get('toHour'), '2');
  assert.equal(params.get('type'), 'Flooding');
  assert.equal(params.get('dayOfWeek'), 'Monday');
  assert.equal(params.get('neighborhood'), 'Centro');
});
test('Failures ignora tipo arbitrário e mantém período/faixa/dia/bairro', () => {
  const query = buildHeatmapQuery(
    filters({
      metric: 'Failures',
      type: 'Flooding',
      fromHour: '0',
      toHour: '23',
      dayOfWeek: 'Friday',
      neighborhood: 'Centro',
    }),
  ).query;
  const params = new URL(`http://example.invalid${query.path}`).searchParams;
  assert.equal(params.has('type'), false);
  assert.equal(params.get('fromHour'), '0');
  assert.equal(params.get('dayOfWeek'), 'Friday');
});
test('Risk envia somente bairro e referência, ignorando filtros ocultos incompatíveis', () => {
  const query = buildHeatmapQuery(
    filters({
      metric: 'Risk',
      from: 'invalid',
      fromHour: '99',
      toHour: '',
      dayOfWeek: 'Invalid',
      type: 'Flooding',
      neighborhood: 'Centro',
      to: '2026-01-02T08:00',
    }),
  ).query;
  assert.ok(query);
  const params = new URL(`http://example.invalid${query.path}`).searchParams;
  assert.deepEqual(Array.from(params.keys()), ['metric', 'to', 'neighborhood']);
  assert.equal(params.get('metric'), 'Risk');
});
test('parâmetros equivalentes geram a mesma chave; vazio usa padrões da API', () => {
  assert.equal(buildHeatmapQuery(filters()).query.key, defaultHeatmapQuery.key);
  assert.equal(
    buildHeatmapQuery(
      filters({
        neighborhood: '  ',
      }),
    ).query.key,
    defaultHeatmapQuery.key,
  );
  assert.equal(
    buildHeatmapQuery(
      filters({
        fromHour: '00',
        toHour: '02',
      }),
    ).query.key,
    buildHeatmapQuery(
      filters({
        fromHour: '0',
        toHour: '2',
      }),
    ).query.key,
  );
  assert.equal(
    buildHeatmapQuery(
      filters({
        metric: 'Failures',
        type: 'Flooding',
      }),
    ).query.key,
    buildHeatmapQuery(
      filters({
        metric: 'Failures',
        type: 'Vandalism',
      }),
    ).query.key,
  );
});
test('validação rejeita datas inexistentes/invertidas e horas incompletas ou fora dos limites', () => {
  for (const value of [
    {
      from: '2026-02-30T12:00',
    },
    {
      to: '0000-01-01T00:00',
    },
    {
      to: 'invalid',
    },
    {
      from: '2026-01-02T00:00',
      to: '2026-01-01T00:00',
    },
    {
      fromHour: '0',
    },
    {
      toHour: '2',
    },
    {
      fromHour: '24',
      toHour: '0',
    },
    {
      fromHour: '-1',
      toHour: '2',
    },
    {
      fromHour: '1.5',
      toHour: '2',
    },
    {
      metric: '__proto__',
    },
    {
      type: 'Invalid',
    },
    {
      dayOfWeek: 'Invalid',
    },
  ]) {
    assert.ok(buildHeatmapQuery(filters(value)).error);
  }
});
test('consulta usa o cliente autenticado e aceita AbortSignal', async () => {
  const controller = new AbortController();
  const client = createHttpClient(
    {
      baseUrl: 'http://example.invalid',
      error: null,
    },
    async (url, options) => {
      assert.equal(options.headers.Authorization, 'Bearer test-token');
      assert.equal(options.signal, controller.signal);
      assert.ok(url.endsWith(defaultHeatmapQuery.path));

      return new Response(JSON.stringify(response()));
    },
  );
  const data = await getHeatmap(defaultHeatmapQuery, {
    token: 'test-token',
    signal: controller.signal,
    client,
  });
  assert.equal(data.validCount, 0);
});
test('verificação compartilha os pontos normalizados de Incidents com uma única consulta', async () => {
  let calls = 0;
  const client = {
    getJson: async (path) => {
      if (path.startsWith('/api/heatmap')) {
        calls++;

        return response([
          {
            latitude: 0,
            longitude: 0,
            intensity: 0.7,
          },
        ]);
      }
      if (path === '/api/dashboard?includePredictive=false')
        return {
          totalEquipments: 0,
          byStatus: {},
          activeAgents: 0,
          availabilityRatePercent: 0,
        };

      return [];
    },
  };
  const results = await loadVerification('test-token', {
    client,
  });
  assert.equal(calls, 1);
  const heatmap = results.find((item) => item.id === 'heatmap');
  assert.equal(heatmap.data, 1);
  assert.deepEqual(heatmap.heatmap.heatPoints, [[0, 0, 0.7]]);
});
