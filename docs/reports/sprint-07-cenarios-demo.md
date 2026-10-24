# Sprint 07 — Cenários DEMO e preparação para avaliação

Revisão em 24/10/2026, branch `main`. Cenários A–E implementados e ensaiados em
bancos LocalDB novos de teste. Não houve acesso ao Azure, carga em banco anterior
à sprint, migration nova, alteração de autenticação/workflows/configuração de
produção, commit, push ou deploy.

## Inspeção e decisão antes da carga

Foram examinados README, relatórios 01–06, domínio/DTOs/validators, mappings e cinco
migrations, seeders, ferramentas DemoData/DemoAdmin e variantes históricas,
proteções do host Demo e serviços/telas de operação, manutenção e Cidade Preditiva.

O plano registrado antes da primeira carga foi ampliar o carregador existente com
um perfil explícito `evaluation`, preservando o legado. O dataset anterior não
oferecia telemetria e recomendações relacionadas ao aceite nem a história completa
com ordens programadas, iniciadas e concluídas. O HTTP Demo era somente leitura.
Foi acrescentado um opt-in exclusivamente local para um aceite preventivo conhecido.
Não se criou outro seeder, alteração de domínio ou migration para dados fictícios.

## Cenários e quantidades

| Cenário | Recursos e resultado |
| --- | --- |
| A — Cidade operacional | 12 equipamentos de oito tipos, nove profissionais de três categorias e cinco bairros; localização completa, sete equipamentos operacionais, sete agentes ativos e 58,33% de disponibilidade |
| B — Falha e resposta | `DEMO-S07-B-RADAR`: falha atual, incidente/alerta crítico correspondentes, corretiva iniciada e agente ocupado; `B-CAMERA`: defeito com corretiva programada |
| C — Prevenção | `DEMO-S07-C-PREV`: câmera operacional, três falhas restauradas/corretivas históricas e sinais sintéticos; risco 85/100, recomendação pendente e aceite com uma única preventiva agendada |
| D — Bairro prioritário | Centro concentra ocorrências/falhas, com agentes ocupado/indisponível e nenhum disponível; índice regional crítico, recomendação consultiva e destaque no mapa |
| E — Programação | `E-BALANCA` operacional com preventiva futura; radar/totem em intervenção; `E-HISTORICO` com preventiva concluída e próxima data coerente |

| Entidade | Inicial | Após aceite C |
| --- | ---: | ---: |
| Endereços | 12 | 12 |
| Equipamentos | 12 | 12 |
| Agentes | 9 | 9 |
| Incidentes | 19 | 19 |
| Alertas | 8 | 8 |
| Telemetria | 8 | 8 |
| Manutenções | 9 | 10 |
| Recomendações | 2 | 2 |
| Subtotal operacional | **79** | **80** |
| Usuários, provisionados separadamente | 5 | 5 |
| Total | **84** | **85** |

Inicialmente há duas ordens agendadas, duas em andamento, cinco concluídas e duas
recomendações pendentes. Após o aceite: três agendadas, duas em andamento, cinco
concluídas; uma recomendação aceita e uma pendente. Alertas: quatro falhas
resolvidas, duas falhas críticas abertas e dois preventivos abertos.
Agentes: quatro disponíveis, três ocupados, dois indisponíveis.

O risco da câmera soma 30 por falhas, 20 por corretivas, 25 por preventiva vencida,
dez por eventos categóricos conhecidos e zero pela situação operacional. As
explicações são calculadas pelos mesmos motores da aplicação sobre a fixture.
Após agendar, o risco atual passa a 70; a recomendação preserva a avaliação
histórica de 85. O histórico do radar evita a fronteira exata da janela de sete
dias, mantendo o índice regional estável durante os minutos da apresentação.

## Decisões técnicas e segurança

- Reutilizados `DemoDataPlan`, `DemoDataLoader`, CLI `DemoData` e provisionador
  `DemoAdmin`; perfil legado e seus testes preservados. IDs determinísticos por
  SHA-256 e datas relativas a uma referência UTC configurável.
- Avaliação exige ambiente `Demo`, confirmação explícita, `(localdb)\MSSQLLocalDB`,
  autenticação integrada e banco `SENTRA_DEMO_Avaliacao_*` igual ao confirmado.
  Recusa Azure/remoto antes da conexão e não admite substituir seu validador.
- `--plan` não acessa banco; `--validate` consulta faltantes/conflitos sem gravar
  (`TotalInserted` nesse modo significa quantidade prevista). `--apply` valida
  identidades, FKs e chaves antes de inserir faltantes em transação serializável.
  Recarga não atualiza datas/status/senhas nem desfaz decisões.
- `--prepare-new` é separado da carga: confirma LocalDB no servidor, exige banco
  inexistente e manifesto das cinco migrations atuais. Aplica somente essas
  migrations ao banco novo; não adota, limpa ou atualiza banco preexistente.
- Recusa mistura com outro dataset, documentos externos, telemetria estranha ou
  registros em tabelas fora do plano. Sem limpeza automática.
- Documentos são sentinelas repetidas de onze dígitos, **inválidas como CPF**.
  Somente o provisionamento sintético admite essa exceção; demais validações de
  DTO/senha permanecem. Nenhum validator/controller/serviço de autenticação foi
  alterado. E-mails `.invalid`, nomes/ruas/CEP/séries/placas/payloads fictícios;
  nenhum telefone ou dado pessoal real. Coordenadas ilustrativas de São Paulo.
- Balança/controlador de cancela usam Sensor com categoria; escolar/táxi usam
  Vehicle. Preservados os oito tipos, discriminadores e persistência.
- Host Demo somente leitura por padrão. `-InteractiveAcceptance` permite apenas o
  aceite da recomendação C, além do login. Mantidos JWT e roles: Admin/OperationManager
  aceitam; demais autenticados recebem 403 e anônimo 401. Interceptor limita as
  entidades/propriedades graváveis à recomendação C, planejamento do equipamento
  e sua nova ordem vinculada.
- Demais escritas recebem 405, incluindo análise persistida, outros aceites,
  descarte, CRUD e evolução de ordens. Simulação, automação e seeders desligados.
  O script de inicialização não carrega dados, provisiona usuários ou migra schema.
- Frontend DEMO opt-in aceita API HTTP loopback:5180 e exibe faixa persistente
  de dados sintéticos. Preservados `.env.local`, secrets e configurações Azure.

## Endpoints existentes utilizados

| Uso | Rotas |
| --- | --- |
| Sessão | `POST /api/auth/login` |
| Inventário/localização | `GET /api/equipments`, `/api/equipments/{id}`, `/api/addresses`, `/api/addresses/{id}` |
| Agentes | `GET /api/agents`, detalhes e associação com veículo existentes |
| Central/mapa | `GET /api/dashboard`, `/api/dashboard/predictive`, `/api/alerts`, `/api/incidents`, `/api/heatmap` |
| Evidências | `GET /api/predictions/equipment-risk`, `/api/predictions/anomalies`, `/api/predictions/failures`, `/api/predictions/execution-status`, `/api/risk`, `/api/telemetry/equipment/{id}` |
| Recomendações | `GET /api/recommendations`, `/api/recommendations/{id}` |
| Aceite restrito | `POST /api/recommendations/df1f8c49-d255-9661-24ff-d2ce69f4d784/accept` |
| Ordens | `GET /api/maintenances`, `/api/maintenances/{id}` e filtros existentes |

Nenhum endpoint de negócio novo. Permissões de consulta continuam variando por
módulo conforme os roles; o provisionador manual não amplia permissões HTTP.

## Validação executada

| Verificação | Resultado |
| --- | --- |
| Build solução .NET | Sucesso, zero avisos/erros |
| Backend completo | **267 aprovados**, zero falhas/ignorados |
| Casos novos Sprint 07 | **32 aprovados**: coerência, documentos, recarga, conflitos, usuários, guardas, configuração, interceptor e HTTP/roles |
| Frontend | **166 aprovados**, zero falhas; lint aprovado |
| Vite | Build DEMO e build com configuração habitual aprovados |
| PowerShell | Sintaxe e execução do script conferidas |
| Integridade SQL | `DBCC CHECKCONSTRAINTS` sem violações; FKs habilitadas e confiáveis |
| Índices/FKs reais | Recusados serial duplicado, endereço inexistente, segunda ordem ativa e segundo vínculo à recomendação; transações de prova revertidas |
| Carga SQL | Prévia 79, carga 79, repetição zero; usuários cinco/repetição zero, hashes preservados |
| Recarga após aceite | Zero inserções; decisão aceita e ordem única preservadas; equipamento operacional |
| Preparar banco existente | Recusado, sem atualizar schema existente |
| Navegador | Chrome headless 155, bundle Vite, Program/API real e LocalDB: cenários A–E, mapa, filtros, detalhes, aceite/ordem e roles |
| Responsividade | 320/390/768/1440 px sem overflow horizontal; faixa no login/navegação; sem exceções JavaScript não tratadas no ensaio |

Foram usados dois bancos **novos e exclusivos de teste**, preservados ao final.
O segundo validou o plano final com referência `2026-10-24T17:40:35Z`.
As cinco migrations existentes foram aplicadas somente nesses bancos novos.
O navegador acessou a API local 5180. Nenhum dado de produção participou do ensaio.

Evidências locais ignoradas pelo Git em `artifacts/sprint-07/`:
`browser-results.json`, `sql-results.json`, `frontend-tests.txt` e capturas
`central-cenarios-a-b-d-e.png`, `inventario-c-camera-operacional.png`,
`cidade-preditiva-c-fatores.png`, `manutencoes-e-historico.png`,
`aceite-c-ordem-unica.png` e `central-mobile-demo.png`.
Os testes reutilizáveis entregues são `backend/Tests/DemoEvaluation*Tests.cs` e
`frontend/tests/demo.test.js`; probes/capturas locais não são dependências da aplicação.

## Arquivos criados e modificados

Criados:

- `backend/Infrastructure/Demo/DemoEvaluationPlan.cs`
- `backend/Infrastructure/Demo/DemoEvaluationTargetGuard.cs`
- `backend/Infrastructure/Demo/DemoEvaluationSchema.cs`
- `backend/Api/Demo/DemoInteractionSettings.cs`
- `backend/Tests/DemoEvaluationTests.cs`
- `backend/Tests/DemoEvaluationSafetyTests.cs`
- `frontend/src/DemoBanner.css`
- `frontend/tests/demo.test.js`
- `scripts/demo/Start-DemoEvaluation.ps1`
- `docs/demo/preparacao-ambiente.md`
- `docs/demo/roteiro-apresentacao.md`
- `docs/reports/sprint-07-cenarios-demo.md`

Modificados:

- `backend/Infrastructure/Demo/DemoDataPlan.cs`
- `backend/Infrastructure/Demo/DemoDataLoader.cs`
- `backend/Infrastructure/Demo/DemoAdminProvisioner.cs`
- `backend/Infrastructure/Demo/DemoReadOnlyInterceptor.cs`
- `backend/DemoData/Program.cs`
- `backend/DemoAdmin/Program.cs`
- `backend/Api/Demo/DemoApiConfiguration.cs`
- `backend/Api/Demo/DemoReadOnlyMiddleware.cs`
- `backend/Api/Program.cs` — somente argumento da configuração do interceptor Demo;
  preservado o encoding original.
- `backend/Tests/Support/EquipmentTestHost.cs` — opções explícitas do host de testes.
- `frontend/src/App.jsx`
- `frontend/src/config/api.js`
- `README.md`

## Como testar e apresentar

Seguir os comandos da [preparação](../demo/preparacao-ambiente.md), começando pelo
plano offline e banco novo autorizado. Provisionar contas manualmente, gerar chave
privada efêmera e iniciar API DEMO/frontend local com a faixa sintética.
Seguir o [roteiro de 6min30](../demo/roteiro-apresentacao.md).

Aceitar C deve produzir uma única preventiva agendada, com vínculo à recomendação
e sem mudar o equipamento para Em manutenção. Conferir detalhes da ordem e
indicadores. Repetir a carga retorna zero e preserva a decisão. Para apresentar
desde o início novamente, preparar outro banco novo autorizado.

## Estado, pendências e limitações

**Concluído:** cenários A–E relacionados, carga reproduzível/aditiva, resumos,
proteções de destino, contas fictícias, faixa DEMO, aceite preventivo restrito,
testes automatizados/SQL/navegador, README, preparação e roteiro.

**Parcial por desenho:** somente uma decisão preventiva é habilitada neste host.
Outras transições são mostradas por registros preparados. Botões habituais de
escrita continuam visíveis conforme o role, mas operações fora do opt-in retornam
405. Não há edição/reset do ensaio pela interface. O roteiro evita operações
bloqueadas; nenhuma reformulação da UI existente é necessária para apresentá-lo.

**Pendente de autorização externa:** eventual ambiente DEMO hospedado separado
para avaliação remota. A entrega é local, não prepara carga remota nem comprova o
estado do Azure. Nenhuma publicação foi realizada.

Índices são heurísticos, sem calibração probabilística, e dependem das janelas
atuais. Preparar próximo da apresentação: datas persistidas não avançam com o
relógio e recarga não as atualiza. A explicação da recomendação é histórica; o
painel mostra o risco atual. Agendamento não é reparo concluído nem resolve
automaticamente alertas preventivos existentes.

Não há fonte oficial, sensores físicos, rastreamento real, atuação de cancela,
despacho automático de agentes ou movimentação de veículos. Bases, fornecedores
e histórico de posições não integram o plano. Tiles do mapa dependem de rede;
registros e marcadores DEMO vêm da API local. Os módulos de negócio e autenticação
não necessitaram alteração funcional para esta sprint.
