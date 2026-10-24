import test from 'node:test';
import assert from 'node:assert/strict';
import { resolveApiConfig } from '../src/config/api.js';

test('modo DEMO só identifica API local isolada na porta esperada', () => {
  const demo = resolveApiConfig('http://127.0.0.1:5180', { demoMode: true });
  assert.equal(demo.error, null); assert.equal(demo.demoMode, true);
  assert.equal(resolveApiConfig('http://localhost:5180/', { demoMode: true }).demoMode, true);
  for (const url of ['https://sentra.uno', 'https://api.example.invalid', 'http://127.0.0.1:5435', 'http://127.0.0.1:5180/other', 'http://user:secret@127.0.0.1:5180']) {
    const result = resolveApiConfig(url, { demoMode: true }); assert.equal(result.baseUrl, null); assert.ok(result.error); assert.ok(!result.error.includes('secret'));
  }
});
test('configuração normal não habilita demonstração nem altera URL de produção', () => {
  assert.deepEqual(resolveApiConfig('https://api.example.invalid'), { baseUrl: 'https://api.example.invalid', error: null });
});
