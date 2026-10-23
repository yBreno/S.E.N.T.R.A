# Sprint 02 — Gestão de agentes

Revisão de 23/10/2026. Esta entrega complementa a Sprint 01 e mantém sua implementação existente.
Não houve alteração de Azure/deploy, execução de migrations, acesso a bancos de produção, commit ou push.

## 1. Análise e escopo

Foram examinados Agent, AgentRole/AgentAvailability, DTOs, validadores, AgentService, AgentLocationService,
AgentsController, repositórios, mapeamentos, autorizações e integração do frontend com dashboard/mapa.
Também foram reutilizados os padrões de interface, transporte HTTP, validação e cancelamento da Sprint 01.

O domínio já possui os campos necessários ao escopo: nome, CPF, matrícula, categoria profissional,
disponibilidade, bairro, GPS, horário da posição e vínculo opcional com usuário. Veículos possuem
CurrentAgentId e a aplicação já estabelece um veículo por agente. Não há vínculo genérico entre agentes
e qualquer equipamento, nem uma entidade de área geográfica permanente. Essas estruturas não foram inventadas.

## 2. Funcionalidades implementadas

| Funcionalidade | Estado e comportamento |
| --- | --- |
| Página de agentes | Completa; sidebar habilitada para Monitoring, acesso por `#agentes` e restauração após F5 |
| Listagem, busca e filtros | Completa no escopo; nome/matrícula/categoria/bairro, categoria e disponibilidade, paginação local de dez registros |
| Cadastro | Completo para gestores, com identificação profissional, CPF validado, categoria, localização e disponibilidade |
| Edição | Completa para gestores; CPF imutável, conta existente preservada e nenhuma concessão de role de acesso |
| Detalhes | Nome, matrícula, CPF mascarado, categoria, vínculo de conta, disponibilidade, situação derivada, GPS/bairro e timestamp |
| Categorias | Agente de trânsito, Guarda municipal e Policial em português, inclusive nos títulos de marcadores e popups |
| Localização e área atual | GPS e bairro existente; bairro é a área atual informada, sem polígonos/jurisdição permanente |
| Operação em campo | Atualização separada de posição/disponibilidade por Monitoring, usando endpoint existente com histórico |
| Associação móvel | Consulta, associação, substituição e liberação de Vehicle/Motorcycle/AutonomousVehicle; sem alterar outros atributos do equipamento |
| Feedback | Carregamento, vazio, sucesso, erros por campo, duplicidade 409, 401/403, modo Demo 405, cancelamento e recuperação de rede |
| Mapa/indicadores | Reconsulta ao retornar à central, marcadores atualizados e agentes ativos preservados como Available + Busy |
| Persistência SQL | Homologação em SQL Server real pendente; cenários executados com EF InMemory e controllers reais |

Não foram adicionadas exclusão na interface, gestão de contas de usuários, edição de CPF, abertura/fechamento
de manutenção ou funcionalidades de Cidade Preditiva.

## 3. Endpoints utilizados e adicionados

| Método/rota | Uso e autorização |
| --- | --- |
| `POST /api/auth/login` | Autenticação já existente |
| `GET /api/agents` | Listagem; Monitoring |
| `GET /api/agents/{id}` | Detalhes atuais/edição; Monitoring |
| `POST /api/agents` | Cadastro; Managers |
| `PUT /api/agents/{id}` | Edição cadastral; Managers |
| `PUT /api/agents/{id}/location` | Posição/disponibilidade; regras existentes de Monitoring ou próprio agente |
| `GET /api/agents/{id}/vehicle` | **Nova rota** para consultar a associação existente; Monitoring |
| `PUT /api/agents/{id}/vehicle` | **Nova rota** para associar/substituir/liberar; Managers |
| `GET /api/equipments` | Lista de candidatos, filtrada na tela às três classes de veículos; leitura Staff preexistente |
| `GET /api/dashboard` | Indicador de agentes ativos e restauração de sessão de Monitoring, já existentes |

`GET /api/agents/me` e `GET /api/agents/{id}/positions` permanecem com suas regras anteriores e foram
exercitados nos testes de regressão. A interface não acrescenta tela de histórico nem tela pessoal para Agent.
Os endpoints de equipamentos de escrita não são usados para associar veículo: isso evita PUT de um cadastro
inteiro com atributos possivelmente desatualizados.

## 4. Ajustes de aplicação e domínio

Não houve alteração de entidades, enums, colunas, schema ou migration nesta sprint. A migration aditiva
de Category da Sprint 01 continua fazendo parte do projeto, mas não foi reaplicada ou executada aqui.

Foi acrescentado IAgentVehicleService/AgentVehicleService e um método de consulta de veículos por agente
no contrato do repositório existente, implementado em Infrastructure. O serviço reutiliza CurrentAgentId:

- Confere existência do agente e recusa inconsistência preexistente de múltiplos veículos no mesmo agente.
- Aceita somente Vehicle e suas subclasses Motorcycle/AutonomousVehicle.
- Recusa veículo ocupado por outro profissional, sem transferi-lo silenciosamente.
- Confere ExpectedVehicleId contra o vínculo consultado para detectar alterações anteriores à gravação.
- Valida o destino antes de liberar o veículo anterior; troca/liberação usam um único SaveChanges.
- Mantém série, placa, atributos, endereço e situação do equipamento. A associação não movimenta agente,
  não altera disponibilidade e não representa despacho ou acionamento físico de veículo.

O contrato da nova rota retorna `{ agentId, vehicle }`, com vehicle nulo quando não há associação. O PUT
recebe `{ vehicleId, expectedVehicleId }`; vehicleId nulo libera o vínculo. No provider relacional o SaveChanges
usa a transação padrão do EF; essa garantia não foi exercitada contra SQL Server nesta execução.

O CRUD de agentes, seus validadores e a regra de agentes ativos não foram reescritos. A edição cadastral
continua atualizando posição/timestamp conforme o comportamento existente, sem gerar um log de posição;
o painel operacional usa a rota location, que efetivamente registra esse histórico.

## 5. Autorização e privacidade

| Perfil | Consulta/listagem/detalhes | Criar/editar identificação | Posição/disponibilidade pela tela | Alterar veículo |
| --- | --- | --- | --- | --- |
| Admin | Sim | Sim | Sim | Sim |
| OperationManager | Sim | Sim | Sim | Sim |
| CentralOperator | Sim | Não | Sim | Não |
| MaintenanceTechnician | Não | Não | Não | Não |
| Agent | Sem acesso à gestão geral | Não | Mantém somente endpoint próprio autorizado no backend | Não |
| Anônimo | Não, 401 | Não, 401 | Não, 401 | Não, 401 |

A interface não usa exceções de permissão para facilitar navegação. Técnico continua no inventário de
equipamentos, sem menu de agentes. As novas rotas mantêm Monitoring/Managers e as autorizações originais
não foram ampliadas. A categoria profissional é AgentRole, distinta do UserRole que autoriza o sistema.

CPF completo existe somente no formulário de cadastro e no envio necessário à API. Após cadastro, somente
MaskedCpf integra os dados da tela; campos extras como cpf/email/senha são descartados pela normalização
do frontend. CPF não é editável, não entra na busca, não integra marcadores, sessão ou evidências de tela.
Na edição, UserId é preservado a partir do registro consultado; não é um campo editável e não há criação de conta.
Esses cuidados preservam a minimização existente, sem alegação de certificação integral de privacidade.

## 6. Reutilização visual e arquivos

InventoryField e InventoryDetails foram extraídos dos componentes da Sprint 01 para uso nas duas sprints.
EquipmentForm conserva um adaptador para UF e EquipmentDetails reutiliza a mesma renderização de campos.
Não foram modificadas regras, endpoints ou fluxos de negócio de equipamentos da Sprint 01. A tela de agentes
usa o CSS de inventário existente, com pequenos estilos locais para disponibilidade e painéis operacionais.

Arquivos criados nesta sprint:

- `backend/Application/DTOs/Agent/AgentVehicleDtos.cs`
- `backend/Application/Interfaces/IServices/IAgentVehicleService.cs`
- `backend/Application/Services/AgentVehicleService.cs`
- `backend/Tests/AgentInventoryTests.cs`
- `backend/Tests/AgentHttpTests.cs`
- `frontend/src/components/inventory/InventoryField.jsx`
- `frontend/src/components/inventory/InventoryDetails.jsx`
- `frontend/src/components/agents/agentModel.js`
- `frontend/src/components/agents/AgentForm.jsx`
- `frontend/src/components/agents/AgentDetails.jsx`
- `frontend/src/components/agents/AgentLocationForm.jsx`
- `frontend/src/components/agents/AgentVehiclePanel.jsx`
- `frontend/src/pages/AgentPage.jsx`
- `frontend/src/pages/AgentPage.css`
- `frontend/src/services/agents.js`
- `frontend/tests/agents.test.js`
- `docs/reports/sprint-02-agentes.md`

Arquivos modificados em relação ao estado entregue pela Sprint 01:

- `backend/Domain/IRepositories/IEquipmentRepository.cs`
- `backend/Infrastructure/Repositories/EquipmentRepository.cs`
- `backend/Application/DependencyInjection.cs`
- `backend/Api/Controllers/AgentsController.cs`
- `backend/Tests/Support/EquipmentTestHost.cs`
- `frontend/src/services/auth.js`
- `frontend/src/pages/DashboardPage.jsx`
- `frontend/src/components/dashboard/DashboardSidebar.jsx`
- `frontend/src/components/dashboard/DashboardHeader.jsx`
- `frontend/src/components/equipment/EquipmentForm.jsx`
- `frontend/src/components/equipment/EquipmentDetails.jsx`
- `frontend/src/components/map/markerData.js`
- `frontend/src/components/map/AgentMarkers.jsx`
- `frontend/src/components/map/OperationalRecordDetails.jsx`
- `frontend/tests/markers.test.js`
- `README.md`

O diretório de trabalho já continha as alterações não commitadas da Sprint 01; elas foram preservadas.
O status agregado do Git inclui as duas sprints e não deve ser interpretado como a lista exclusiva desta.
Artefatos locais de teste em `artifacts/sprint-02/` são ignorados pelo Git e não integram os dados da aplicação.

## 7. Testes e evidências

| Verificação | Resultado |
| --- | --- |
| Backend | **168 aprovados**, zero falhas/ignorados; 142 anteriores + 26 novos casos |
| Frontend | **127 aprovados**, zero falhas/ignorados; 115 anteriores + 12 novos casos |
| Build da solução | Aprovado, zero avisos/erros |
| Build/lint frontend | Aprovados |
| HTTP/autorização | Controllers reais, Kestrel loopback, JWT válido de teste e EF InMemory; nenhum banco persistente |
| Navegador | Chrome com frontend real e host local isolado; zero exceções JavaScript nos fluxos exercitados |
| Responsividade | 320/390/768/1440 px sem transbordamento horizontal da página |

Os testes de serviço cobrem as três categorias, mascaramento, documento/matrícula duplicados, identidade
inválida, GPS, enum inválido, preservação de conta/CPF, indicador de ativos e vínculos móveis. Trocas recusadas
preservam o veículo anterior. Os testes HTTP conferem leitura 401/403/200 por perfil, criação/edição por gestores,
bloqueio de operadores/técnicos/Agent nas mutações restritas, atualização operacional, histórico, perfil próprio,
recusa de localização de terceiros e contratos das novas rotas. Os testes frontend cobrem payloads, validação,
CPF não editável, campos extras descartados, preservação de conta, filtros, roles, endpoints e dados dos marcadores.

### Evidência de integração com o mapa

No Chrome foram cadastrados pela interface três profissionais sintéticos: trânsito Available, guarda Busy e
policial Offline. A central mostrou três marcadores e dois agentes ativos. A edição do profissional de trânsito
alterou nome/GPS/bairro. Depois, como CentralOperator, o painel operacional mudou sua posição para Mooca
e sua disponibilidade para Offline. Ao retornar à central:

- Continuaram presentes os três marcadores, incluindo o profissional Offline.
- O indicador de ativos passou de dois para um, preservando Available + Busy.
- O título do marcador mostrou nome atualizado, categoria traduzida e Offline.
- O popup apresentou Mooca e GPS `-23.5558, -46.5969`, sem CPF.
- O endpoint de posições apresentou um registro da atualização operacional.

Também foram conferidos associação/substituição/liberação de veículos, bloqueio 409 de veículo ocupado,
duplicidade de matrícula sem expor documento no erro, validação local, F5, rede/recuperação e ausência do menu
de agentes para o técnico. O cadastro de moto pela tela de equipamentos, incluindo seleção de UF, verificou a
regressão do componente extraído da Sprint 01. Todos os endpoints de API observados pertenciam ao host loopback.

Evidências locais sem credenciais:

- `artifacts/sprint-02/browser-results.json`
- `artifacts/sprint-02/agents-list-desktop.png`
- `artifacts/sprint-02/agents-form-mobile.png`
- `artifacts/sprint-02/agents-map-updated.png`

O host reutiliza os controllers/services reais, mas usa exclusivamente InMemory, chaves JWT de teste e usuários
sintéticos. A URL da API foi definida somente no processo Vite; `.env.local` foi preservado. Não foram colocados
dados mockados ou registros sintéticos no fluxo real/banco da aplicação. Isso não comprova SQL Server real,
produção, concorrência entre servidores ou comportamento em celular físico.

Comandos para repetir testes na raiz, após dependências restauradas:

```powershell
dotnet build backend/Solution/Solution.slnx --no-restore
dotnet test backend/Tests/Tests.csproj --no-build --no-restore
npm.cmd --prefix frontend run build
npm.cmd --prefix frontend run lint
node --test frontend/tests/*.test.js
```

## 8. Instruções de teste manual

1. Execute a API e frontend desta versão conforme **Execução local** do README, usando banco local isolado
   e conta já provisionada. Não há migration nova da Sprint 02; o schema deve incluir a Sprint 01.
2. Abra `http://127.0.0.1:5173/#agentes` como Admin/OperationManager e confira a sidebar e listagem.
3. Cadastre profissionais das três categorias com dados de teste autorizados. Preencha matrícula, CPF válido,
   disponibilidade, bairro e GPS. Confira rótulos, CPF mascarado nos detalhes e ausência de campo de CPF na edição.
4. Combine busca por nome/matrícula/categoria/bairro e filtros de atuação/disponibilidade. Limpe filtros e
   confira o vazio, ausência de resultados e paginação quando houver mais de dez agentes.
5. Edite nome, matrícula, categoria, bairro/GPS e disponibilidade. Um vínculo de conta preexistente deve
   permanecer igual; a categoria profissional não deve criar usuário nem alterar permissões de login.
6. Em Equipamentos, cadastre veículos livres ou use registros de teste existentes. Nos detalhes do agente,
   associe um, substitua por outro e libere. Confirme placa/série/atributos inalterados no equipamento.
   Um veículo ocupado por outro agente não deve aparecer como opção livre nem ser tomado pelo endpoint.
7. Retorne à central: confira marcadores, popup com nome/matrícula/categoria/bairro/GPS e timestamp.
   Available e Busy contam como ativos; Offline continua no mapa, sem aumentar esse indicador.
8. Entre como CentralOperator: Novo/Editar agente e gerência de veículo não devem aparecer. Use Atualizar
   posição/disponibilidade, retorne ao mapa e confira localização/indicador. O endpoint de posições registra a ação.
9. Entre como MaintenanceTechnician: equipamentos continuam acessíveis, agentes não. Agent/anônimo não
   podem consultar a lista geral; os endpoints pessoais de Agent continuam protegidos pelo vínculo com usuário.
10. Tente CPF/matrícula duplicados e dados inválidos. Os campos devem ser preservados no formulário e os
    erros não devem reproduzir documentos completos. Após perda de rede, atualize antes de repetir envios.
11. Recarregue com F5, navegue por teclado e confira layout em desktop/celular. A tabela pode rolar dentro
    de seu contêiner; a página não deve ultrapassar a largura disponível.

## 9. Pendências e limites

- Homologação em SQL Server local permanece pendente; InMemory não reproduz constraints/transações/concorrência
  de SQL Server. Não houve acesso a banco persistente nem aplicação de migration nesta sprint.
- Busca/filtros/paginação são locais sobre o GET completo. Não foram acrescentadas paginação de servidor,
  sincronização automática, clustering ou reformulação geral da central.
- ExpectedVehicleId detecta divergência já observável na consulta antes de salvar. Não constitui controle
  completo de concorrência entre processos; não foi acrescentado rowversion ou índice único novo. A regra
  preexistente de vínculos continua implementada na aplicação e requer avaliação própria para alta concorrência.
- Não há associação genérica com equipamentos fora da hierarquia Vehicle. Patinetes, câmeras e sensores
  não receberam novos vínculos, relações muitos-para-muitos ou subclasses.
- Bairro é área atual informada; não representa jurisdição permanente, geofencing, escalas ou órgãos.
- Gestão de contas, edição de CPF e exclusão pela interface ficam fora da entrega. Cadastro do profissional
  não cria usuário de login. Edição cadastral não equivale a histórico contínuo de deslocamento.
- Não houve implementação de manutenção ou Cidade Preditiva. Modo HTTP Demo permanece somente leitura.
