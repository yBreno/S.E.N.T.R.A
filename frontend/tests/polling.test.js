import test from 'node:test';
import assert from 'node:assert/strict';
import {
  createVerificationResource,
  resolvePollingInterval,
} from '../src/services/verificationResource.js';
const flush = async () => {
  for (let i = 0; i < 8; i++) await Promise.resolve();
};
const deferred = () => {
  let resolve, reject;
  const promise = new Promise((a, b) => {
    resolve = a;
    reject = b;
  });
  return { resolve, reject, promise };
};
const valid = (value = 1) => [{ id: 'equipments', data: value, items: [{ id: String(value) }] }];
function harness(load) {
  const timers = new Map();
  let sequence = 0,
    unauthorized = 0,
    date = 0;
  const resource = createVerificationResource({
    load,
    intervalMs: 5000,
    now: () => new Date(++date * 1000),
    onUnauthorized: () => unauthorized++,
    setTimer: (callback, delay) => {
      const id = ++sequence;
      timers.set(id, { callback, delay });
      return id;
    },
    clearTimer: (id) => timers.delete(id),
  });
  const run = (delay) => {
    const entry = [...timers].find(([, value]) => value.delay === delay);
    assert.ok(entry, `Timer ${delay} ausente`);
    timers.delete(entry[0]);
    entry[1].callback();
  };
  return { resource, timers, run, unauthorized: () => unauthorized };
}
test('polling tem padrão 30 s e aceita apenas 5–300 s sem valores inválidos', () => {
  assert.equal(resolvePollingInterval(undefined), 30000);
  assert.equal(resolvePollingInterval('5000'), 5000);
  for (const input of ['invalid', 0, -1, 4999, 300001])
    assert.equal(resolvePollingInterval(input), 30000);
});
test('StrictMode, refresh e ciclo não duplicam requisição nem timers', async () => {
  const pending = deferred();
  let calls = 0;
  const { resource, timers, run } = harness(() => {
    calls++;
    return pending.promise;
  });
  resource.start();
  resource.stop();
  resource.start();
  resource.start();
  await flush();
  resource.refresh();
  resource.refresh();
  assert.equal(calls, 1);
  assert.equal(timers.size, 1);
  pending.resolve(valid());
  await flush();
  assert.equal(timers.size, 1);
  assert.equal(resource.getSnapshot().loading, false);
  run(5000);
  await flush();
  assert.equal(calls, 2);
  assert.equal(timers.size, 1);
  resource.stop();
  assert.equal(timers.size, 0);
});
test('cancelamento impede resposta antiga após reinício e preserva o ciclo mais recente', async () => {
  const first = deferred(),
    second = deferred();
  const signals = [];
  const { resource, timers } = harness((signal) => {
    signals.push(signal);
    return signals.length === 1 ? first.promise : second.promise;
  });
  resource.start();
  await flush();
  resource.stop();
  assert.equal(signals[0].aborted, true);
  assert.equal(timers.size, 0);
  resource.start();
  await flush();
  second.resolve(valid(2));
  await flush();
  first.resolve(valid(1));
  await flush();
  assert.equal(resource.getSnapshot().results[0].data, 2);
  resource.stop();
});
test('falha parcial mantém resposta e horário válidos; recuperação atualiza sem F5', async () => {
  let calls = 0;
  const { resource, run } = harness(async () =>
    ++calls === 2 ? [{ id: 'equipments', error: new Error('offline') }] : valid(calls),
  );
  resource.start();
  await flush();
  const first = resource.getSnapshot().updatedAt;
  run(5000);
  await flush();
  const failed = resource.getSnapshot();
  assert.equal(failed.results[0].data, 1);
  assert.equal(failed.results[0].stale, true);
  assert.equal(failed.updatedAt, first);
  assert.ok(failed.error);
  run(5000);
  await flush();
  assert.equal(resource.getSnapshot().results[0].data, 3);
  assert.equal(resource.getSnapshot().error, '');
  assert.notEqual(resource.getSnapshot().updatedAt, first);
  resource.stop();
});
test('timeout recupera mesmo se transporte ignorar abort; resposta tardia não sobrescreve', async () => {
  const first = deferred();
  let calls = 0,
    signal;
  const { resource, run } = harness((current) => {
    signal = current;
    return ++calls === 1 ? first.promise : Promise.resolve(valid(2));
  });
  resource.start();
  await flush();
  run(20000);
  await flush();
  assert.equal(signal.aborted, true);
  assert.ok(resource.getSnapshot().error);
  run(5000);
  await flush();
  first.resolve(valid(1));
  await flush();
  assert.equal(resource.getSnapshot().results[0].data, 2);
  resource.stop();
});
test('401 encerra polling e solicita logout sem repetir consultas', async () => {
  const { resource, timers, unauthorized } = harness(async () => [
    { id: 'dashboard', error: { status: 401 } },
  ]);
  resource.start();
  await flush();
  assert.equal(unauthorized(), 1);
  assert.equal(timers.size, 0);
  resource.refresh();
  await flush();
  assert.equal(unauthorized(), 1);
});
test('erro geral preserva dados sem fabricar um novo horário de sucesso', async () => {
  let fail = false;
  const { resource } = harness(async () => {
    if (fail) throw new Error('rede');
    return valid();
  });
  resource.start();
  await flush();
  const before = resource.getSnapshot().updatedAt;
  fail = true;
  resource.refresh();
  await flush();
  assert.equal(resource.getSnapshot().results[0].stale, true);
  assert.equal(resource.getSnapshot().updatedAt, before);
  resource.stop();
});
