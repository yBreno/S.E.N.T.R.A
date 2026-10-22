// Bibliotecas externas
import test from 'node:test';
import assert from 'node:assert/strict';

// Serviços e utilitários
import { getDashboardSummary, userInitials, userRoleLabel } from '../src/components/dashboard/dashboardData.js';

const byStatus = {
  Operational: 10, Malfunctioning: 10, UnderMaintenance: 10,
  Deactivated: 10, Stolen: 10, NotInstalled: 10
};

test('indicadores derivam operacionais e indisponíveis dos estados reais, sem confundir indisponibilidade com complemento da disponibilidade', () => {
  const summary = getDashboardSummary({ totalEquipments: 60, byStatus });
  assert.equal(summary.operational, 10);
  assert.equal(summary.unavailable, 30);
  assert.equal(summary.consistent, true);
  assert.equal(summary.distribution.length, 6);
  assert.equal(summary.distribution.reduce((total, item) => total + item.count, 0), 60);
});

test('rosca usa proporções da resposta e preserva estados desconhecidos com rótulo seguro', () => {
  const summary = getDashboardSummary({ totalEquipments: 4, byStatus: { Operational: 3, FutureStatus: 1 } });
  assert.equal(summary.distribution[0].percentage, 75);
  assert.equal(summary.distribution[1].percentage, 25);
  assert.equal(summary.distribution[1].label, 'Outro: FutureStatus');
  assert.equal(summary.unavailable, null);
  assert.equal(summary.consistent, true);
});

test('dados ausentes não inventam indicadores e distribuição inconsistente não é apresentada como válida', () => {
  assert.equal(getDashboardSummary(null), null);
  assert.equal(getDashboardSummary({ totalEquipments: 2 }), null);
  const incomplete = getDashboardSummary({ totalEquipments: 2, byStatus: {} });
  assert.equal(incomplete.operational, null);
  assert.equal(incomplete.unavailable, null);
  assert.equal(incomplete.consistent, false);
});

test('inventário vazio mantém zeros reais e porcentagens finitas sem dividir por zero', () => {
  const summary = getDashboardSummary({ totalEquipments: 0, byStatus: Object.fromEntries(Object.keys(byStatus).map(key => [key, 0])) });
  assert.equal(summary.operational, 0);
  assert.equal(summary.unavailable, 0);
  assert.equal(summary.consistent, true);
  assert.ok(summary.distribution.every(item => item.percentage === 0));
});

test('contagens inválidas não geram cards ou gráfico com valores negativos, fracionários ou não finitos', () => {
  for (const value of [-1, 0.5, NaN, Infinity, '3', null]) {
    assert.equal(getDashboardSummary({ totalEquipments: value, byStatus }), null);
    assert.equal(getDashboardSummary({ totalEquipments: 60, byStatus: { ...byStatus, Operational: value } }), null);
  }
});

test('identificação visual usa o usuário autenticado e traduz apenas papéis conhecidos', () => {
  assert.equal(userInitials(' Ana Martins '), 'AM');
  assert.equal(userInitials('Álvaro'), 'Á');
  assert.equal(userInitials(null), '?');
  assert.equal(userRoleLabel('Admin'), 'Administrador');
  assert.equal(userRoleLabel('CustomRole'), 'CustomRole');
});
