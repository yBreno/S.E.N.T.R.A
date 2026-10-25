import assert from 'node:assert/strict';
import test from 'node:test';
import { createElement } from 'react';
import { renderToStaticMarkup } from 'react-dom/server';
import { registerJsxLoader } from './support/jsx-loader.js';

await registerJsxLoader();

const [{ default: AgentVehiclePanel }, { default: InventoryDetails }, { default: UserPage }] =
  await Promise.all([
    import('../src/components/agents/AgentVehiclePanel.jsx'),
    import('../src/components/inventory/InventoryDetails.jsx'),
    import('../src/pages/UserPage.jsx'),
  ]);

const vehicle = {
  id: 'current-vehicle',
  type: 'Motorcycle',
  serialNumber: 'MOTO-01',
  licensePlate: 'ABC1234',
  brand: 'Marca',
  model: 'Modelo',
  status: 'Operational',
  currentAgentId: 'agent-id',
};
const defaults = {
  agent: { id: 'agent-id' },
  vehicle: null,
  vehicles: [],
  error: '',
  vehiclesError: '',
  manager: true,
  saving: false,
  onSave: () => assert.fail('A renderização não deve salvar vínculos.'),
  onRetry: () => assert.fail('A renderização não deve consultar dados.'),
};
const renderPanel = (props = {}) =>
  renderToStaticMarkup(createElement(AgentVehiclePanel, { ...defaults, ...props }));

test('painel de veículo preserva os detalhes e não oferece gestão ao perfil de consulta', () => {
  const html = renderPanel({ vehicle, manager: false });
  assert.match(html, /ABC1234/);
  assert.match(html, /Marca \/ Modelo/);
  assert.match(html, /Operacional/);
  assert.doesNotMatch(html, /<form|<select|checkbox/);
});

test('seleção contém apenas veículos livres ou do agente e preserva o vínculo atual ausente da lista', () => {
  const vehicles = [
    { ...vehicle, id: 'free', currentAgentId: null },
    { ...vehicle, id: 'own', currentAgentId: 'agent-id' },
    { ...vehicle, id: 'other', currentAgentId: 'another-agent' },
  ];
  const before = structuredClone(vehicles);
  const html = renderPanel({ vehicle, vehicles });
  const options = [...html.matchAll(/<option\b[^>]*value="([^"]*)"[^>]*>/g)].map(
    (match) => match[1],
  );
  assert.deepEqual(options, ['', 'current-vehicle', 'free', 'own']);
  assert.deepEqual(vehicles, before);
  assert.match(html, /value="current-vehicle" selected=""/);
});

test('erro do vínculo atual oferece repetição e impede gestão mesmo para gestor', () => {
  const html = renderPanel({ vehicle, error: 'Falha de consulta', saving: true });
  assert.match(html, /role="alert"/);
  assert.match(html, /Tentar novamente/);
  assert.match(html, /<button[^>]*disabled=""/);
  assert.doesNotMatch(html, /<form|<dl/);
});

test('erro da lista de veículos mantém detalhes e substitui somente o formulário de gestão', () => {
  const html = renderPanel({ vehicle, vehiclesError: 'Falha da lista' });
  assert.match(html, /ABC1234/);
  assert.match(html, /Atualizar detalhes/);
  assert.doesNotMatch(html, /<form/);
  assert.doesNotMatch(
    renderPanel({ vehicle, vehiclesError: 'Falha', manager: false }),
    /Atualizar detalhes/,
  );
});

test('salvamento bloqueia os controles e mantém o feedback existente', () => {
  const html = renderPanel({ vehicle, saving: true });
  assert.match(html, /aria-busy="true"/);
  assert.match(html, /<fieldset disabled=""/);
  assert.match(html, /<button type="submit" disabled="">Salvando…/);
});

test('ausência de veículo e de candidatos permanece explícita e exige confirmação para salvar', () => {
  const html = renderPanel();
  assert.match(html, /Nenhum veículo associado a este agente/);
  assert.match(html, /Nenhum veículo livre/);
  assert.match(html, /<button type="submit" disabled="">Salvar vínculo/);
  assert.doesNotMatch(html, /checked=""/);
});

test('detalhes preservam booleanos, zero, valores ausentes, conversão e escape de texto', () => {
  const values = [true, false, null, undefined, '', 0, NaN, [], {}, '<script>'];
  const html = renderToStaticMarkup(
    createElement(InventoryDetails, {
      entries: values.map((value, index) => [String(index), value]),
    }),
  );
  const details = [...html.matchAll(/<dd>(.*?)<\/dd>/g)].map((match) => match[1]);
  assert.deepEqual(details, [
    'Sim',
    'Não',
    'Não informado',
    'Não informado',
    'Não informado',
    '0',
    'NaN',
    '',
    '[object Object]',
    '&lt;script&gt;',
  ]);
});

for (const role of [
  'Admin',
  'OperationManager',
  'CentralOperator',
  'MaintenanceTechnician',
  'Agent',
]) {
  test(`página de usuários preserva o acesso do cargo ${role}`, () => {
    const html = renderToStaticMarkup(
      createElement(UserPage, {
        session: { token: 'synthetic-test-token', user: { role } },
        onLogout: () => assert.fail('A renderização não deve alterar sessão.'),
        onSavingChange: () => assert.fail('A renderização não deve salvar dados.'),
      }),
    );
    if (role === 'Admin') {
      assert.match(html, /Administração de usuários/);
      assert.match(html, /Novo usuário/);
      assert.match(html, /Consultando usuários/);
    } else {
      assert.match(html, /Somente Admin pode administrar contas/);
      assert.doesNotMatch(html, /<form|Novo usuário/);
    }
  });
}
