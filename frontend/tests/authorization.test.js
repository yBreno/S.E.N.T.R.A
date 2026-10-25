import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { Buffer } from 'node:buffer';
import {
  userRoles,
  userRoleLabel,
  isUserRole,
  canMonitor,
  canMaintain,
  canReadEquipments,
  canManageEquipments,
  canReadAgents,
  canManageAgents,
} from '../src/services/authorization.js';
import { login } from '../src/services/auth.js';
import { normalizeSession } from '../src/services/session.js';
import { agentRoleLabel } from '../src/components/map/markerData.js';

test('cargos de login do frontend correspondem exatamente ao contrato do backend', () => {
  const source = readFileSync(
    new URL('../../backend/Domain/Enums/UserRole.cs', import.meta.url),
    'utf8',
  );
  const names = source
    .match(/enum UserRole\s*\{([^}]+)\}/)[1]
    .split(',')
    .map((name) => name.trim());
  assert.deepEqual(Object.keys(userRoles), names);
  assert.equal(userRoleLabel('Agent'), 'Agente');
  assert.equal(agentRoleLabel('PoliceOfficer'), 'Policial militar');
});
for (const [role, monitor, read, manage, maintain] of [
  ['Admin', true, true, true, true],
  ['OperationManager', true, true, true, true],
  ['CentralOperator', true, true, false, false],
  ['MaintenanceTechnician', false, true, false, true],
  ['Agent', false, false, false, false],
]) {
  test(`matriz da interface preserva as permissões de ${role}`, () => {
    const user = { role };
    assert.equal(canMonitor(user), monitor);
    assert.equal(canReadAgents(user), monitor);
    assert.equal(canReadEquipments(user), read);
    assert.equal(canManageEquipments(user), manage);
    assert.equal(canManageAgents(user), manage);
    assert.equal(canMaintain(user), maintain);
  });
}
test('categorias profissionais e cargos desconhecidos não concedem permissões de login', () => {
  for (const role of [
    'TrafficAgent',
    'MunicipalGuard',
    'PoliceOfficer',
    'SuperAdmin',
    'admin',
    '__proto__',
    undefined,
    0,
  ]) {
    assert.equal(isUserRole(role), false);
    for (const permission of [
      canMonitor,
      canMaintain,
      canReadEquipments,
      canManageEquipments,
      canReadAgents,
      canManageAgents,
    ])
      assert.equal(permission({ role }), false);
  }
});
for (const role of ['PoliceOfficer', 'SuperAdmin', 'admin']) {
  test(`login e sessão recusam cargo fora do contrato: ${role}`, async () => {
    const data = {
      token: 'test-token',
      expiresAt: new Date(Date.now() + 60_000).toISOString(),
      user: { name: 'DEMO', role },
    };
    await assert.rejects(
      login(
        { email: 'demo@sentra.invalid', password: 'teste' },
        { client: { postJson: async () => data } },
      ),
    );
    const encode = (value) => Buffer.from(JSON.stringify(value)).toString('base64url');
    data.token = `${encode({ alg: 'HS256' })}.${encode({ sub: 'demo', role, exp: Math.floor(Date.now() / 1000) + 60 })}.signature`;
    assert.equal(normalizeSession(data), null);
  });
}
