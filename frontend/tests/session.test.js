// Bibliotecas externas
import test from 'node:test';
import assert from 'node:assert/strict';
import { Buffer } from 'node:buffer';

// Serviços e utilitários
import {
  createSessionStore,
  normalizeSession,
  restoreSession,
  scheduleSessionExpiration
} from '../src/services/session.js';
import { createHttpClient, HttpError } from '../src/services/http.js';
import { createHeatmapResource } from '../src/services/heatmapResource.js';
import { buildHeatmapQuery, createHeatmapFilters } from '../src/services/heatmapQuery.js';

const instant = Date.parse('2026-10-21T12:00:00Z');
const baseUrl = 'http://127.0.0.1:5180';
const encode = value => Buffer.from(JSON.stringify(value)).toString('base64url');

function session(overrides = {}) {
  const payload = {
    sub: 'demo-user',
    role: 'Admin',
    exp: (instant + 60_000) / 1000,
    ...overrides
  };

  return {
    token: `${encode({ alg: 'HS256', typ: 'JWT' })}.${encode(payload)}.test-signature`,
    expiresAt: new Date(instant + 120_000).toISOString(),
    user: { name: 'Administrador fictício', role: 'Admin' }
  };
}

function storage() {
  const items = new Map();

  return {
    getItem: key => items.get(key) ?? null,
    setItem: (key, value) => items.set(key, value),
    removeItem: key => items.delete(key),
    items
  };
}

function setup() {
  const memory = storage();
  let currentTime = instant;
  const options = { baseUrl, getStorage: () => memory, now: () => currentTime };

  return {
    memory,
    options,
    store: createSessionStore(options),
    advance: milliseconds => { currentTime += milliseconds; }
  };
}

test('F5 recria o armazenamento e só restaura a sessão depois da validação protegida pela API', async () => {
  const { store, options } = setup();
  store.save(session());
  let release;
  const response = new Promise(resolve => { release = resolve; });
  let restored = false;
  const client = createHttpClient({ baseUrl }, async (url, request) => {
    assert.equal(url, `${baseUrl}/api/dashboard`);
    assert.equal(request.headers.Authorization, `Bearer ${session().token}`);
    await response;
    return new Response('{}');
  });
  const pending = restoreSession({ store: createSessionStore(options), client }).then(value => {
    restored = true;
    return value;
  });

  await Promise.resolve();
  assert.equal(restored, false);
  release();
  assert.deepEqual(await pending, normalizeSession(session(), instant));
});

test('logout remove apenas a sessão e impede restauração após outro F5', async () => {
  const { store, memory, options } = setup();
  memory.setItem('preferencia-nao-relacionada', 'preservar');
  store.save(session());
  store.clear();

  assert.equal(await restoreSession({
    store: createSessionStore(options),
    client: { getJson: () => assert.fail('Não deve consultar sem sessão') }
  }), null);
  assert.equal(memory.getItem('preferencia-nao-relacionada'), 'preservar');
});

test('exp real do JWT vencido impede restauração mesmo com expiresAt futuro no armazenamento', async () => {
  const { store, advance, memory } = setup();
  store.save(session());
  advance(60_000);

  assert.equal(await restoreSession({ store, client: { getJson: () => assert.fail() } }), null);
  assert.equal(memory.items.size, 0);
});

test('a menor validade entre exp e expiresAt prevalece', () => {
  const value = session();
  value.expiresAt = new Date(instant + 10_000).toISOString();

  assert.equal(normalizeSession(value, instant).expiresAt, value.expiresAt);
  assert.equal(normalizeSession(value, instant + 10_000), null);
});

test('sessões malformadas, roles adulteradas e tokens ainda não válidos são recusados', () => {
  const value = session();
  for (const invalid of [
    null,
    { ...value, token: 'qualquer-token' },
    { ...value, expiresAt: 'inválido' },
    { ...value, user: { name: 'Nome', role: 'OperationManager' } },
    session({ exp: '9999999999' }),
    session({ nbf: instant / 1000 + 1 }),
    { ...value, token: `${encode({ alg: 'none' })}.${encode({ exp: instant / 1000 + 60 })}.signature` }
  ]) assert.equal(normalizeSession(invalid, instant), null);
});

test('JSON corrompido é descartado e nenhuma senha ou propriedade extra é persistida', () => {
  const { store, memory } = setup();
  store.save({ ...session(), password: 'fixture-only', extra: 'ignorar' });
  const key = [...memory.items.keys()][0];
  const record = JSON.parse(memory.getItem(key));

  assert.deepEqual(Object.keys(record.session).sort(), ['expiresAt', 'token', 'user']);
  memory.setItem(key, '{invalid');
  assert.equal(store.read(), null);
  assert.equal(memory.items.size, 0);
});

for (const status of [401, 403]) {
  test(`HTTP ${status} na restauração limpa o armazenamento e não autentica`, async () => {
    const { store, memory } = setup();
    store.save(session());
    const client = createHttpClient({ baseUrl }, async () => new Response(null, { status }));

    await assert.rejects(restoreSession({ store, client }), { status });
    assert.equal(store.read(), null);
    assert.equal(memory.items.size, 0);
  });
}

test('falha de rede impede restauração, sem presumir que token armazenado autoriza acesso', async () => {
  const { store } = setup();
  store.save(session());
  const client = createHttpClient({ baseUrl }, async () => { throw new TypeError('offline'); });

  await assert.rejects(restoreSession({ store, client }), { kind: 'network' });
  assert.equal(store.read(), null);
});

test('expiração durante validação HTTP não restaura a sessão', async () => {
  const { store, advance } = setup();
  store.save(session());

  assert.equal(await restoreSession({ store, client: {
    getJson: async () => { advance(60_000); return {}; }
  } }), null);
  assert.equal(store.read(), null);
});

test('cancelamento de restauração não apaga a sessão nem publica resposta tardia', async () => {
  const { store } = setup();
  store.save(session());
  const controller = new AbortController();

  await assert.rejects(restoreSession({
    store,
    signal: controller.signal,
    client: { getJson: async () => { controller.abort(); return {}; } }
  }), { name: 'AbortError' });
  assert.ok(store.read());
});

test('sessão vinculada a outra URL de API não é enviada ao destino atual', () => {
  const { store, options } = setup();
  store.save(session());
  const other = createSessionStore({ ...options, baseUrl: 'http://localhost:5129' });

  assert.equal(other.read(), null);
});

test('armazenamento bloqueado não interrompe login em memória ou logout', () => {
  const store = createSessionStore({ baseUrl, now: () => instant, getStorage: () => {
    throw new Error('Storage bloqueado');
  } });

  assert.doesNotThrow(() => store.save(session()));
  assert.doesNotThrow(() => store.clear());
  assert.equal(store.read(), null);
});

test('timer de expiração limpa a sessão e sua desmontagem cancela o agendamento', () => {
  const { store, advance, memory, options } = setup();
  store.save(session());
  let callback;
  let cancelled;
  let expired = 0;
  const cleanup = scheduleSessionExpiration(store.read(), () => {
    expired++;
    store.clear();
  }, {
    now: options.now,
    schedule: (fn, delay) => { callback = fn; assert.equal(delay, 60_000); return 7; },
    cancel: id => { cancelled = id; }
  });
  advance(60_000);
  callback();
  assert.equal(expired, 1);
  assert.equal(memory.items.size, 0);
  cleanup();
  assert.equal(cancelled, 7);
});

test('401 vigente no fluxo de calor invoca logout e impede restaurar a sessão', async () => {
  const { store } = setup();
  store.save(session());
  let currentSession = store.read();
  const resource = createHeatmapResource({
    load: async () => { throw new HttpError('Sessão recusada', { status: 401 }); },
    onUnauthorized: () => { store.clear(); currentSession = null; }
  });
  resource.setQuery(buildHeatmapQuery({ ...createHeatmapFilters(), metric: 'Failures' }).query);
  await resource.request();

  assert.equal(currentSession, null);
  assert.equal(store.read(), null);
});
