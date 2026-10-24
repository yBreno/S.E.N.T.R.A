// Bibliotecas externas
import test from 'node:test';
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import process from 'node:process';

// Serviços e utilitários
import { agentRoleLabel } from '../src/components/map/markerData.js';
import {
  normalizeCoordinates,
  prepareMarkers,
  getEquipmentState,
  getAgentState,
  displayText,
  formatLocationUpdatedAt
} from '../src/components/map/markerData.js';

test('categorias de agentes recebem tradução somente na apresentação, com fallback seguro', () => {
  const record = {
    role: 'TrafficAgent'
  };
  assert.equal(agentRoleLabel(record.role), 'Agente de trânsito');
  assert.equal(record.role, 'TrafficAgent');
  assert.equal(agentRoleLabel('MunicipalGuard'), 'Guarda municipal');
  assert.equal(agentRoleLabel('PoliceOfficer'), 'Policial militar');
  assert.equal(agentRoleLabel(null), 'Não informado');
});
test('timestamp UTC do contrato é exibido em São Paulo sem deslocamento duplicado', () => {
  const module = new URL('../src/components/map/markerData.js', import.meta.url).href;
  const child = spawnSync(process.execPath, [
    '--input-type=module',
    '-e',
    `import { formatLocationUpdatedAt } from ${JSON.stringify(module)}; console.log(formatLocationUpdatedAt('2026-10-20T23:23:39.7999673Z')); console.log(formatLocationUpdatedAt('2026-10-20T20:23:39-03:00'));`
  ], {
    env: {
      ...process.env,
      TZ: 'America/Sao_Paulo'
    },
    encoding: 'utf8'
  });
  assert.equal(child.status, 0);
  const lines = child.stdout.trim().split(/\r?\n/);
  assert.match(lines[0], /20\/10\/2026.*20:23:39/);
  assert.equal(lines[0], lines[1]);
});
test('coordenadas preservam zero e seguem a ordem latitude/longitude', () => {
  assert.deepEqual(normalizeCoordinates(0, 0), [0, 0]);
  assert.deepEqual(normalizeCoordinates(0, -46.6333), [0, -46.6333]);
  assert.deepEqual(normalizeCoordinates(-23.5505, 0), [-23.5505, 0]);
  assert.deepEqual(normalizeCoordinates(-23.5505, -46.6333), [-23.5505, -46.6333]);
});
test('os limites geográficos são inclusivos', () => {
  assert.deepEqual(normalizeCoordinates(90, 180), [90, 180]);
  assert.deepEqual(normalizeCoordinates(-90, -180), [-90, -180]);
  for (const coordinates of [[90.0001, 0], [-90.0001, 0], [0, 180.0001], [0, -180.0001]]) {
    assert.equal(normalizeCoordinates(...coordinates), null);
  }
});
test('nulos, strings e valores não finitos não criam posições falsas em zero', () => {
  for (const value of [null, undefined, '', '0', '-23.5', false, true, NaN, Infinity, -Infinity, {}, []]) {
    assert.equal(normalizeCoordinates(value, 0), null);
    assert.equal(normalizeCoordinates(0, value), null);
  }
});
test('todos os status de equipamento possuem apresentação distinta e rótulo', () => {
  const statuses = ['Operational', 'Malfunctioning', 'UnderMaintenance', 'Deactivated', 'Stolen', 'NotInstalled'];
  const states = statuses.map(getEquipmentState);
  assert.equal(new Set(states.map(state => state.tone)).size, statuses.length);
  assert.equal(new Set(states.map(state => state.label)).size, statuses.length);
  assert.ok(states.every(state => state.tone !== 'unknown'));
});
test('as três disponibilidades de agentes são distinguíveis', () => {
  const states = ['Available', 'Busy', 'Offline'].map(getAgentState);
  assert.equal(new Set(states.map(state => state.tone)).size, 3);
  assert.deepEqual(states.map(state => state.label), ['Disponível', 'Ocupado', 'Offline']);
});
test('estados desconhecidos e nomes de propriedades de protótipo usam apresentação neutra', () => {
  for (const value of ['Unknown', 'operational', '', '__proto__', 'toString', null, undefined, 0, {
    toString: null
  }]) {
    assert.equal(getEquipmentState(value).tone, 'unknown');
    assert.equal(getAgentState(value).tone, 'unknown');
  }
});
test('normalização descarta registros inválidos e IDs duplicados sem alterar a entrada', () => {
  const records = [
    {
      id: 'a',
      latitude: 0,
      longitude: 0
    },
    {
      id: 'b',
      latitude: null,
      longitude: 0
    },
    {
      id: 'a',
      latitude: 1,
      longitude: 1
    },
    {
      id: '',
      latitude: 0,
      longitude: 0
    },
    null,
    {
      id: 'c',
      latitude: -23,
      longitude: -46
    }
  ];
  const original = structuredClone(records);
  const result = prepareMarkers(records);
  assert.deepEqual(result.markers.map(record => record.id), ['a', 'c']);
  assert.deepEqual(result.markers[0].position, [0, 0]);
  assert.equal(result.discarded, 4);
  assert.deepEqual(records, original);
});
test('arrays vazios não criam registros fictícios', () => {
  assert.deepEqual(prepareMarkers([]), {
    markers: [],
    discarded: 0
  });
});
test('texto de API permanece texto, sem interpolação em HTML, e datas inválidas têm fallback', () => {
  const text = '<img src=x onerror=alert(1)>';
  assert.equal(displayText(text), text);
  for (const value of [null, undefined, '', '   ', {}]) assert.equal(displayText(value), 'Não informado');
  for (const value of [null, '', 'invalid', {}]) assert.equal(formatLocationUpdatedAt(value), 'Não informado');
  assert.notEqual(formatLocationUpdatedAt('2026-10-20T12:00:00Z'), 'Não informado');
});
