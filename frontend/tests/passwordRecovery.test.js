import test from 'node:test';
import assert from 'node:assert/strict';
import { buildResetPassword, createPasswordRecoveryApi, parseRecoveryRoute, recoveryError, recoveryMessage } from '../src/services/passwordRecovery.js';
import { createUsersApi } from '../src/services/users.js';

const token = 'A'.repeat(43);
test('rotas de recuperação aceitam segredo apenas no fragmento previsto e recusam duplicação', () => {
  assert.deepEqual(parseRecoveryRoute('#esqueci-minha-senha'), { kind: 'request', token: '' });
  assert.deepEqual(parseRecoveryRoute(`#redefinir-senha?token=${token}`), { kind: 'reset', token });
  for (const route of ['#redefinir-senha', '#redefinir-senha?token=bad', `#redefinir-senha?token=${token}&token=${token}`]) assert.equal(parseRecoveryRoute(route).token, '');
  assert.equal(parseRecoveryRoute('#usuarios'), null); assert.equal(parseRecoveryRoute(`?token=${token}`), null);
});
test('nova senha reutiliza complexidade e limite BCrypt em bytes UTF8, exigindo confirmação', () => {
  assert.ok(buildResetPassword(token, 'ForteSenha@2026', 'ForteSenha@2026').payload);
  for (const password of ['', 'abc', 'apenasminusculas@2026', `Aa1@${'á'.repeat(35)}`]) assert.ok(buildResetPassword(token, password, password).errors.password);
  assert.ok(buildResetPassword(token, 'ForteSenha@2026', 'Diferente').errors.confirmation);
  assert.ok(buildResetPassword('invalid', 'ForteSenha@2026', 'ForteSenha@2026').errors.token);
});
test('solicitação pública sempre apresenta mensagem genérica e não usa token de sessão', async () => {
  const api = createPasswordRecoveryApi({ postJson: async (path, body, options) => {
    assert.equal(path, '/api/auth/password-recovery/request'); assert.deepEqual(body, { email: 'conta@sentra.invalid' }); assert.equal(options.token, undefined);
    return { message: 'provider-secrets-do-not-display' };
  } });
  assert.equal(await api.request(' conta@sentra.invalid '), recoveryMessage);
});
test('confirmação envia segredo apenas no corpo da rota fixa e aceita 204', async () => {
  const payload = buildResetPassword(token, 'ForteSenha@2026', 'ForteSenha@2026').payload;
  const api = createPasswordRecoveryApi({ postJson: async (path, body) => { assert.equal(path, '/api/auth/password-recovery/confirm'); assert.deepEqual(body, payload); return null; } });
  assert.equal(await api.confirm(payload), undefined);
});
test('disponibilidade exige contrato booleano e falha fechada', async () => {
  assert.equal(await createPasswordRecoveryApi({ getJson: async () => ({ enabled: true, apiKey: 'secret' }) }).availability(), true);
  await assert.rejects(() => createPasswordRecoveryApi({ getJson: async () => ({ enabled: 'true' }) }).availability());
});
test('erros de recuperação não reproduzem mensagens privadas da API ou do provedor', () => {
  for (const status of [400, 401, 405, 429, 500, undefined]) assert.ok(!recoveryError({ status, message: token }).includes(token));
});
test('reset administrativo usa endpoint do backend, sem endereço, link ou senha na entrada', async () => {
  const api = createUsersApi({ user: { role: 'Admin' }, token: 'admin-session' }, { postJson: async (path, body, options) => {
    assert.equal(path, '/api/auth/password-recovery/users/account-id'); assert.deepEqual(body, {}); assert.equal(options.token, 'admin-session'); return { message: 'not-a-secret' };
  } });
  assert.equal(await api.requestPasswordReset('account-id'), undefined);
});
for (const role of ['OperationManager', 'CentralOperator', 'MaintenanceTechnician', 'Agent']) test(`${role} não solicita reset administrativo no transporte`, async () => {
  const api = createUsersApi({ user: { role } }, { postJson: () => assert.fail('Não chamar transporte') });
  await assert.rejects(() => api.requestPasswordReset('account-id'));
});
