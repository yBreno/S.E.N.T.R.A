import test from 'node:test';
import assert from 'node:assert/strict';
import { dashboardTarget } from '../src/services/navigation.js';

const targets = [
  'dashboard',
  'equipamentos',
  'agentes',
  'manutencoes',
  'mapa-operacional',
  'mapa-de-calor',
  'cidade-preditiva',
  'usuarios',
];
const roles = {
  Admin: targets,
  OperationManager: targets.filter((t) => t !== 'usuarios'),
  CentralOperator: targets.filter((t) => !['usuarios', 'manutencoes'].includes(t)),
  MaintenanceTechnician: ['equipamentos', 'manutencoes'],
  Agent: [],
};
for (const [role, allowed] of Object.entries(roles))
  test(`histórico e links diretos respeitam permissões de ${role}`, () => {
    for (const target of targets)
      assert.equal(
        dashboardTarget('#' + target, { role }),
        allowed.includes(target) ? target : null,
      );
  });
test('âncoras de foco e erros não são tratadas como troca de módulo', () => {
  for (const hash of [
    '',
    null,
    '#equipment-address.street',
    '#agent-name',
    '#conteudo-operacional',
    '#redefinir-senha?token=private',
  ])
    assert.equal(dashboardTarget(hash, { role: 'Admin' }), null);
});
