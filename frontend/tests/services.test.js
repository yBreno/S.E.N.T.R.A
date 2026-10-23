// Bibliotecas externas
import test from 'node:test';
import assert from 'node:assert/strict';

// Serviços e utilitários
import { resolveApiConfig } from '../src/config/api.js';
import { createHttpClient, HttpError } from '../src/services/http.js';
import { canMonitor, login } from '../src/services/auth.js';
import { loadVerification } from '../src/services/verification.js';

const config = resolveApiConfig('http://localhost:5129/');

const json = value => new Response(JSON.stringify(value), {
  headers: {
    'Content-Type': 'application/json'
  }
});

test('configuração normaliza a URL e rejeita valores inseguros ou incompletos sem reproduzi-los', () => {
  assert.equal(config.baseUrl, 'http://localhost:5129');
  for (const value of [
    undefined,
    '',
    '/api',
    'ftp://localhost',
    'http://user:private@localhost',
    'https://host/?private=value',
    'https://host/#value',
    'https://host/api/'
  ]) {
    const result = resolveApiConfig(value);
    assert.ok(result.error);
    assert.equal(result.baseUrl, null);
    assert.ok(!result.error.includes('private'));
  }
});
test('configuração inválida impede requisições', async () => {
  const client = createHttpClient(resolveApiConfig(''), () => assert.fail('Não deve chamar fetch'));
  await assert.rejects(client.getJson('/api/dashboard'), {
    kind: 'configuration'
  });
});
test('GET encaminha Bearer e signal, sem corpo ou cookies', async () => {
  const controller = new AbortController();
  const client = createHttpClient(config, async (url, options) => {
    assert.equal(url, 'http://localhost:5129/api/equipments');
    assert.equal(options.method, 'GET');
    assert.equal(options.headers.Authorization, 'Bearer test-token');
    assert.equal(options.signal, controller.signal);
    assert.equal(options.credentials, 'omit');
    assert.equal(options.body, undefined);

    return json([]);
  });
  assert.deepEqual(await client.getJson('/api/equipments', {
    token: 'test-token',
    signal: controller.signal
  }), []);
});
test('login envia os campos reais, limpa espaços do e-mail e não retém senha na sessão', async () => {
  const expiresAt = new Date(Date.now() + 60_000).toISOString();
  const client = createHttpClient(config, async (url, options) => {
    assert.equal(url, 'http://localhost:5129/api/auth/login');
    assert.equal(options.method, 'POST');
    assert.equal(options.headers['Content-Type'], 'application/json');
    assert.equal(options.headers.Authorization, undefined);
    assert.deepEqual(JSON.parse(options.body), {
      email: 'user@example.invalid',
      password: 'test-only'
    });

    return json({
      token: 'test-token',
      expiresAt,
      user: {
        name: 'Operador',
        role: 'CentralOperator',
        maskedCpf: 'unused'
      }
    });
  });
  const session = await login({
    email: ' user@example.invalid ',
    password: 'test-only'
  }, {
    client
  });
  assert.deepEqual(session, {
    token: 'test-token',
    expiresAt,
    user: {
      name: 'Operador',
      role: 'CentralOperator'
    }
  });
  assert.ok(!JSON.stringify(session).includes('test-only'));
});
for (const status of [400, 401, 403, 404, 500, 503]) {
  test(`HTTP ${status} é distinguível mesmo com corpo HTML ou dados sensíveis`, async () => {
    const client = createHttpClient(config, async () => new Response('<html>private-response</html>', {
      status
    }));
    await assert.rejects(client.getJson('/api/dashboard'), error => {
      assert.ok(error instanceof HttpError);
      assert.equal(error.status, status);
      assert.equal(error.kind, 'http');
      assert.ok(error.message.includes(String(status)));
      assert.ok(!error.message.includes('private-response'));

      return true;
    });
  });
}
test('falha de rede não é confundida com 401 nem expõe detalhes internos', async () => {
  const client = createHttpClient(config, async () => {
    throw new TypeError('private-details');
  });
  await assert.rejects(client.getJson('/api/dashboard'), error => {
    assert.equal(error.kind, 'network');
    assert.equal(error.status, null);
    assert.ok(!error.message.includes('private-details'));

    return true;
  });
});
test('JSON de sucesso inválido é erro de contrato; 204 é aceito sem leitura JSON', async () => {
  const malformed = createHttpClient(config, async () => new Response('<html>unexpected</html>'));
  await assert.rejects(malformed.getJson('/api/dashboard'), {
    kind: 'response'
  });
  const noContent = createHttpClient(config, async () => new Response(null, {
    status: 204
  }));
  assert.equal(await noContent.postJson('/api/example', {}), null);
});
test('cancelamento pendente permanece AbortError e não vira falha de rede', async () => {
  const controller = new AbortController();
  const client = createHttpClient(config, (url, {
    signal
  }) => new Promise((resolve, reject) => {
    signal.addEventListener('abort', () => reject(signal.reason), {
      once: true
    });
  }));
  const pending = client.getJson('/api/dashboard', {
    signal: controller.signal
  });
  controller.abort();
  await assert.rejects(pending, {
    name: 'AbortError'
  });
});
test('requisição já cancelada não chama fetch', async () => {
  const controller = new AbortController();
  controller.abort();
  const client = createHttpClient(config, () => assert.fail('Não deve chamar fetch'));
  await assert.rejects(client.getJson('/api/dashboard', {
    signal: controller.signal
  }), {
    name: 'AbortError'
  });
});
test('login rejeita contrato incompleto ou expirado', async () => {
  for (const data of [
    null,
    {
      token: 'test-token',
      expiresAt: 'invalid',
      user: {}
    },
    {
      token: 'test-token',
      expiresAt: '2000-01-01T00:00:00Z',
      user: {
        name: 'Nome',
        role: 'Admin'
      }
    }
  ]) {
    const client = createHttpClient(config, async () => json(data));
    await assert.rejects(login({
      email: 'user@example.invalid',
      password: 'test-only'
    }, {
      client
    }), {
      kind: 'response'
    });
  }
});
test('somente os três papéis de monitoramento são aceitos pela interface', () => {
  for (const role of ['Admin', 'OperationManager', 'CentralOperator']) assert.equal(canMonitor({
    role
  }), true);
  for (const role of ['Agent', 'MaintenanceTechnician', 'admin', '', undefined]) assert.equal(canMonitor({
    role
  }), false);
  assert.equal(canMonitor(null), false);
});
test('falha parcial preserva as demais consultas e mantém 403 distinguível', async () => {
  const client = createHttpClient(config, async url => {
    if (url.endsWith('/api/agents')) return new Response(null, {
      status: 403
    });
    if (url.endsWith('/api/equipments')) return json([{}, {}]);
    if (url.endsWith('metric=Incidents')) return json({
      metric: 'Incidents',
      from: null,
      to: null,
      totalPoints: 0,
      maxIntensity: 0,
      points: []
    });

    return json({
      totalEquipments: 2,
      activeAgents: 0,
      availabilityRatePercent: 50,
      byStatus: {
        Operational: 1
      }
    });
  });
  const results = await loadVerification('test-token', {
    client
  });
  assert.equal(results.find(result => result.id === 'agents').error.status, 403);
  assert.equal(results.find(result => result.id === 'equipments').data, 2);
  assert.equal(results.find(result => result.id === 'heatmap').data, 0);
  assert.equal(results.find(result => result.id === 'dashboard').data.totalEquipments, 2);
});
test('respostas malformadas dos endpoints não aparecem como contagens válidas', async () => {
  const client = createHttpClient(config, async () => json({
    unexpected: true
  }));
  const results = await loadVerification('test-token', {
    client
  });
  assert.ok(results.every(result => result.error.kind === 'response'));
});
test('uma verificação cancelada não publica resultados antigos', async () => {
  const controller = new AbortController();
  const client = createHttpClient(config, async () => json([]));
  const pending = loadVerification('test-token', {
    signal: controller.signal,
    client
  });
  controller.abort();
  await assert.rejects(pending, {
    name: 'AbortError'
  });
});
test('consultas compartilham os registros de mapa sem duplicar GETs nem reter CPF', async () => {
  const calls = [];
  const client = createHttpClient(config, async (url, options) => {
    calls.push(url);
    assert.equal(options.headers.Authorization, 'Bearer test-token');
    if (url.endsWith('/api/agents')) return json([
      {
        id: 'agent-1',
        name: 'Teste',
        badgeNumber: 'A1',
        role: 'TrafficAgent',
        availability: 'Available',
        neighborhood: 'Centro',
        latitude: 0,
        longitude: 0,
        locationUpdatedAt: '2026-10-20T12:00:00Z',
        maskedCpf: 'private',
        cpf: 'private',
        email: 'private'
      }
    ]);
    if (url.endsWith('/api/equipments')) return json([
      {
        id: 'equipment-1',
        type: 'Camera',
        brand: 'Teste',
        model: 'Modelo',
        serialNumber: 'E1',
        status: 'Operational',
        neighborhood: 'Centro',
        latitude: 0,
        longitude: 0,
        attributes: {
          private: true
        }
      }
    ]);
    if (url.includes('/api/heatmap')) return json({
      metric: 'Incidents',
      from: null,
      to: null,
      totalPoints: 0,
      maxIntensity: 0,
      points: []
    });

    return json({
      totalEquipments: 1,
      activeAgents: 1,
      availabilityRatePercent: 100,
      byStatus: {
        Operational: 1
      }
    });
  });
  const results = await loadVerification('test-token', {
    client
  });
  assert.equal(calls.length, 5);
  assert.equal(new Set(calls).size, 5);
  const agents = results.find(result => result.id === 'agents');
  const equipments = results.find(result => result.id === 'equipments');
  assert.equal(agents.data, 1);
  assert.equal(equipments.data, 1);
  assert.equal(agents.items[0].latitude, 0);
  assert.equal(equipments.items[0].longitude, 0);
  assert.equal(agents.items[0].name, 'Teste');
  assert.ok(!JSON.stringify(results).includes('private'));
  assert.ok(!Object.hasOwn(agents.items[0], 'maskedCpf'));
  assert.ok(!Object.hasOwn(equipments.items[0].attributes, 'private'));
});
