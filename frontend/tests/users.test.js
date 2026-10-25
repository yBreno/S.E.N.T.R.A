import test from 'node:test';
import assert from 'node:assert/strict';
import { createUsersApi, normalizeAccount } from '../src/services/users.js';
import { canAdministerUsers } from '../src/services/authorization.js';
import { buildUserForm } from '../src/components/users/userModel.js';

const account = { id: 'account-id', name: 'DEMO Conta', email: 'conta@sentra.invalid', role: 'Agent', isActive: true, revision: 2, createdAt: '2026-10-24T12:00:00Z' };
const admin = { token: 'test-token', user: { role: 'Admin' } };
test('normalização de contas remove campos confidenciais do transporte', () => {
  assert.deepEqual(normalizeAccount({ ...account, cpf: 'sensitive', passwordHash: 'sensitive', authVersion: 9, token: 'sensitive' }), account);
  for (const invalid of [null, { ...account, role: 'PoliceOfficer' }, { ...account, revision: 0 }, { ...account, isActive: 'false' }]) assert.throws(() => normalizeAccount(invalid));
});
for (const role of ['OperationManager', 'CentralOperator', 'MaintenanceTechnician', 'Agent']) {
  test(`${role} não executa consultas ou escritas de usuários pela interface`, async () => {
    assert.equal(canAdministerUsers({ role }), false);
    const api = createUsersApi({ token: 'test', user: { role } }, { getJson: () => assert.fail('Não chamar transporte'), postJson: () => assert.fail('Não chamar transporte'), putJson: () => assert.fail('Não chamar transporte') });
    for (const action of [() => api.list({}), () => api.details('x'), () => api.history('x', 1), () => api.create({}), () => api.update('x', {}), () => api.role('x', 'Admin', 1), () => api.status('x', false, 1)]) await assert.rejects(action);
  });
}
test('busca e paginação usam o contrato do servidor e token autenticado', async () => {
  const api = createUsersApi(admin, { getJson: async (path, options) => {
    const url = new URL(path, 'http://local.invalid'); assert.equal(url.pathname, '/api/users'); assert.equal(url.searchParams.get('page'), '2'); assert.equal(url.searchParams.get('role'), 'Agent'); assert.equal(url.searchParams.get('isActive'), 'false'); assert.equal(options.token, 'test-token');
    return { items: [account], total: 11, page: 2, pageSize: 10 };
  } });
  assert.equal((await api.list({ search: ' Conta ', role: 'Agent', isActive: false, page: 2, pageSize: 10 })).total, 11);
});
test('alteração de cargo e estado preserva revisão otimista e sinal de nova autenticação', async () => {
  const requests = [];
  const api = createUsersApi(admin, { putJson: async (path, body) => { requests.push({ path, body }); return { account, reauthenticate: true }; } });
  assert.equal((await api.role(account.id, 'Agent', 2)).reauthenticate, true);
  await api.status(account.id, false, 2);
  assert.deepEqual(requests, [{ path: '/api/users/account-id/role', body: { role: 'Agent', revision: 2 } }, { path: '/api/users/account-id/status', body: { isActive: false, revision: 2 } }]);
});
test('formulário de criação valida CPF, cargo e limite de senha em bytes', () => {
  const valid = { name: ' DEMO ', email: ' conta@sentra.invalid ', cpf: '52998224725', role: 'Agent', password: 'TestePrivado@2026' };
  const built = buildUserForm(valid); assert.equal(built.payload.name, 'DEMO'); assert.equal(built.payload.email, 'conta@sentra.invalid');
  assert.ok(buildUserForm({ ...valid, password: 'é'.repeat(40) + 'Aa1!' }).errors.password);
  assert.ok(buildUserForm({ ...valid, role: 'PoliceOfficer', cpf: '00000000000' }).errors.role);
  const updated = buildUserForm({ name: 'Novo nome', email: 'novo@sentra.invalid', password: 'não encaminhar', role: 'Admin' }, account);
  assert.deepEqual(updated.payload, { name: 'Novo nome', email: 'novo@sentra.invalid', revision: 2 });
});
test('detalhes e histórico não repassam dados confidenciais', async () => {
  const api = createUsersApi(admin, { getJson: async path => path.includes('/history') ? {
    items: [{ id: 'event', action: 'RoleChanged', actorUserId: 'admin', previousRole: 'Agent', newRole: 'Admin', createdAt: account.createdAt, password: 'sensitive' }], total: 1, page: 1, pageSize: 10
  } : { account: { ...account, passwordHash: 'sensitive' }, agentId: 'professional-id' } });
  assert.equal((await api.details(account.id)).agentId, 'professional-id');
  assert.ok(!JSON.stringify(await api.history(account.id, 1)).includes('sensitive'));
});
test('páginas inválidas e resposta de mutação ambígua são recusadas', async () => {
  const api = createUsersApi(admin, { getJson: async () => ({ items: [], page: 0, pageSize: 1000, total: -1 }), postJson: async () => ({ account }) });
  await assert.rejects(api.list({})); await assert.rejects(api.create({}));
});
