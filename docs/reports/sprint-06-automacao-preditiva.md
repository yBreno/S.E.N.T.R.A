# Sprint 06 — Automação Preditiva e Integração Operacional

## Decisões anteriores à implementação — 24/10/2026

- Inspecionados relatório da Sprint 05, motor, recomendações, workers, bootstrap, dashboard, mapa e contratos frontend. A automação atual não existe; a simulação é outro worker e não será habilitada nem usada como fonte de cenários reais.
- Reutilizar a análise existente. Automação desligada por padrão, intervalo padrão de 60 minutos, mínimo de 15 e máximo de 1440; primeira execução apenas depois de um intervalo completo. Ciclos sequenciais com cancelamento.
- Usar `sys.sp_getapplock` exclusivo, com proprietário transação, no SQL Server existente. Análise e criação de recomendações compartilharão o recurso de bloqueio. Transação externa engloba os saves da análise; falha/cancelamento causa rollback. Sem tabela, migration ou infraestrutura paga nova. Provedores sem coordenação distribuída não poderão executar o worker.
- Preservar o semáforo local para análise e acrescentar estado em memória desta instância, com origem manual/automática e contagens somente após sucesso. Expor consulta autorizada, sem escrever durante leituras.
- Integrar riscos e recomendações na Central e nos marcadores atuais, preservando cores de situação operacional. Leitura preditiva independente em intervalo conservador de cinco minutos; navegação para os módulos já existentes.
- Produção permanece desligada; nenhuma alteração Azure, commit, push, deploy ou migration remota. Validar bloqueio entre processos em LocalDB exclusivo antes de declarar suporte a múltiplas instâncias.

Referência técnica: [documentação Microsoft de sp_getapplock](https://learn.microsoft.com/pt-br/sql/relational-databases/system-stored-procedures/sp-getapplock-transact-sql?view=sql-server-ver17).

## Entrega

| Requisito | Resultado |
| --- | --- |
| Worker periódico desligado por padrão | Completo: `Enabled=false`, intervalo de 60 minutos |
| Parâmetros seguros | Completo: aceita 15–1440 minutos; valida no startup; rejeita automação em Demo |
| Primeiro ciclo e sobreposição | Completo: espera um intervalo inteiro; próximo intervalo após o término; semáforo local e coordenação SQL |
| Segurança entre processos | Validada em LocalDB: bloqueio exclusivo compartilhado pelos geradores desta versão; concorrente recebe conflito sem gravar novas recomendações |
| Falha e cancelamento | Completo: rollback da análise SQL; worker registra tipo de falha e aguarda próximo intervalo, sem derrubar a API |
| Estado da execução | Completo em memória: origem, estado, UTC e contagens apenas após conclusão bem-sucedida |
| Central e mapa | Completo: painel integrado, equipamentos de risco alto, bairros prioritários, recomendações, indicadores e navegação; marcadores independentes da situação operacional |
| Carga, erro, vazio e desatualização | Completo: leitura independente, retenção de dados válidos, aviso por fonte e recuperação manual/periódica |
| Execução global uma única vez por intervalo | Não implementada: cada réplica possui seu relógio; SQL coordena as escritas, sem eleger um agendador global |
| Histórico persistente/global de execução | Não implementado: estado perdido no restart e sem consolidação entre réplicas |
| Ativação e homologação Azure | Não executadas; permanece desligado por padrão até validação pelo responsável |

Nenhuma migration nova, tabela, índice ou infraestrutura adicional. Não houve commit, push, deploy, seed na aplicação real, acesso ao Azure ou alteração direta em produção.

## Backend e coordenação

`PredictiveAutomationWorker` reutiliza `IPredictiveAnalysisService.RunAutomaticAsync`. A análise manual continua em `POST /api/predictions/analyze` e mantém o contrato de resposta. Não existe endpoint público que permita selecionar uma origem automática ou alterar configurações. O worker é registrado no bootstrap normal fora de Demo; desabilitado, termina sem criar timer, abrir escopo de análise ou conectar ao banco.

O intervalo é contado depois de cada ciclo, incluindo após falha. Não há recuperação em rajada de períodos perdidos, execução no startup ou alteração dinâmica por `IOptionsMonitor`: alterações de configuração exigem reinício. Desligamento cancela espera e execução, com escopos descartados. Os logs novos contêm somente contagens confirmadas e tipo da exceção, sem corpo de resposta, tokens, credenciais, dados pessoais ou mensagem de erro do provedor.

`IPredictiveWriteCoordinator` mantém a separação entre Application e persistência. Em SQL Server, `SqlPredictiveWriteCoordinator` abre transação no mesmo `AppDbContext`, adquire `sys.sp_getapplock` em modo exclusivo, proprietário `Transaction`, recurso constante `SENTRA.PredictiveWrites.v1`. O timeout de aquisição é zero: outra instância em execução resulta em conflito, sem fila de transações aguardando indefinidamente. Erro de obtenção não dispara fallback sem proteção.

Análise e as quatro entradas de geração de recomendações usam o mesmo coordenador; isso inclui recomendações regionais geradas pelo fluxo existente de ocorrências. Chamadas aninhadas aproveitam a transação existente. Saves intermediários da análise ficam dentro da transação externa; somente o commit permite registrar sucesso e contagens. Falha/cancelamento desfaz as gravações dessa transação; o bloqueio é liberado pelo SQL ao encerrá-la. Não foram introduzidos retries automáticos de escrita ou mudanças na estratégia de conexão.

`InProcessPredictiveWriteCoordinator` é o fallback usado com os testes InMemory e não anuncia suporte entre processos. O worker verifica essa capacidade e recusa execução automática nesse provedor. O semáforo anterior continua protegendo análises da mesma instância; não é apresentado como proteção distribuída.

A idempotência existente foi preservada: recomendação pendente por tipo/equipamento, mesmo após mudança de bairro; recomendação regional por tipo/bairro; alerta preventivo aberto; equipamento com ordem ativa não recebe nova recomendação de manutenção. A análise não abre ordens nem aceita recomendações. A unicidade de ordens agendadas/em andamento continua protegida pelo índice SQL existente.

## Configurações e estado

| Chave | Padrão | Validação / comportamento |
| --- | --- | --- |
| `PredictiveAutomation:Enabled` | `false` | Opt-in; Demo não permite `true`; não ativa simulação |
| `PredictiveAutomation:IntervalMinutes` | `60` | Inteiro entre 15 e 1440; valores inválidos impedem inicialização |

Em variáveis de ambiente, usar `PredictiveAutomation__Enabled` e `PredictiveAutomation__IntervalMinutes`. Não foi alterado nenhum appsetting privado, secret, identidade Azure, workflow, recurso ou valor remoto. O worker antigo de telemetria/simulação permanece separado e com sua configuração anterior: a validação operacional deve confirmar `Simulation:Enabled=false`. Nenhum seeder foi chamado para gerar cenários da aplicação real.

`PredictiveExecutionState` é um singleton desta instância. Estados: `NotRun`, `Running`, `Completed`, `Failed`. A origem é `Manual`, `Automatic` ou nula antes da primeira execução. Início, fim, última conclusão e última automática concluída são UTC. Cancelamento após início é registrado como falha, sem contagem de sucesso. A última automática concluída é preservada ao executar uma manual ou ao falhar depois.

Antes da primeira conclusão e após falha, contagens são nulas. Uma execução concluída que não criou nada retorna zero real. O contrato traz `scope: InstanceMemory`, e a interface explica que restart apaga esse estado e que ele não representa outras réplicas. Recomendações persistidas podem existir enquanto esta instância indica `NotRun`, por terem sido geradas em outra execução/processo.

`multipleInstancesSupported` indica a capacidade do coordenador SQL selecionado; não é uma auditoria de permissões nem uma homologação do Azure. Leituras de estado não adquirem bloqueio SQL nem executam a análise.

## Integração operacional e interface

Foi acrescentado o painel **Prevenção operacional** à Central existente, sem nova página ou alteração de sidebar. Ele mostra a quantidade de equipamentos de risco alto, bairros de índice alto/crítico, recomendações pendentes priorizadas, horário da leitura e estado da execução. Os critérios e os pesos da Sprint 05 permanecem inalterados.

Os dados vêm de uma consulta autorizada e somente de leitura, reutilizando `EquipmentRiskService`, `RiskService` e a consulta de recomendações. Apenas os equipamentos de risco alto são retornados nessa visão; ela não se apresenta como lista completa do inventário. As recomendações continuam refletindo a prioridade/evidência registradas no momento de sua geração.

O polling preditivo é separado do polling operacional: cinco minutos, contados após a consulta anterior, ou pelo botão **Atualizar prevenção**. Isso evita recalcular o motor junto de cada ciclo de 30 segundos da Central. Sair para outro módulo cancela a leitura/polling; retornar consulta novamente. Erro preserva a última leitura válida com indicação explícita de desatualização. Uma resposta vazia válida substitui os dados antigos; indisponibilidade inicial não produz contagens fictícias.

O marcador de equipamento conserva símbolo e cor de situação. Um pequeno **P** independente indica risco preditivo alto; a sinalização de manutenção existente continua separada. Leitura antiga usa P com contorno tracejado e aviso no título/popup. O popup apresenta índice e data da avaliação, além de acesso aos fatores no módulo existente. Bairros de índice alto/crítico recebem círculo tracejado no centro calculado pelo motor, com tooltip, valor, data e indicação de índice sem probabilidade calibrada. Coordenadas inválidas não geram círculo.

O painel permite ir ao equipamento, consultar manutenção quando autorizado, filtrar o bairro no mapa ou abrir Cidade preditiva para examinar e decidir sobre a recomendação. O painel novo não oferece aceite ou descarte e não cria uma segunda lista de falhas atuais; alertas operacionais permanecem no painel existente. Preto/cinza/amarelo, componentes, tokens CSS e responsividade anteriores foram mantidos.

## Endpoints

| Método e rota | Alteração / autorização |
| --- | --- |
| `GET /api/dashboard/predictive` | Novo: leitura de riscos altos, bairros, recomendações e estado; Monitoring |
| `GET /api/predictions/execution-status` | Novo: configuração pública da automação e estado em memória; Monitoring |
| `POST /api/predictions/analyze` | Contrato preservado; coordenação SQL, transação e registro de origem manual; Monitoring |
| Demais endpoints das Sprints 01–05 | Contratos preservados; entradas geradoras de recomendações agora compartilham a coordenação |

Monitoring permanece Admin/OperationManager/CentralOperator. MaintenanceTechnician e Agent não acessam os endpoints operacionais novos. Não houve mudança de autenticação, promoção de perfil ou ampliação do aceite/descarte, restritos a gestores. Navegar e consultar não executa POST, não gera recomendações e não abre manutenção.

## Arquivos

Criados:

- `backend/Api/Workers/PredictiveAutomationWorker.cs`
- `backend/Application/DTOs/Predictive/PredictiveExecutionDtos.cs`
- `backend/Application/Interfaces/IServices/IPredictiveWriteCoordinator.cs`
- `backend/Application/Predictive/PredictiveAutomationSettings.cs`
- `backend/Application/Services/InProcessPredictiveWriteCoordinator.cs`
- `backend/Application/Services/PredictiveExecutionState.cs`
- `backend/Application/Services/PredictiveOperationsService.cs`
- `backend/Infrastructure/Data/SqlPredictiveWriteCoordinator.cs`
- `backend/Tests/PredictiveAutomationTests.cs`
- `backend/Tests/PredictiveOperationsHttpTests.cs`
- `frontend/src/components/dashboard/PredictiveOperations.jsx`
- `frontend/src/components/dashboard/PredictiveOperations.css`
- `frontend/src/components/map/NeighborhoodRiskMarkers.jsx`
- `frontend/src/hooks/usePredictiveOperations.js`
- `frontend/tests/predictiveOperations.test.js`
- `docs/reports/sprint-06-automacao-preditiva.md`

Modificados:

- `backend/Api/Controllers/DashboardController.cs`
- `backend/Api/Controllers/PredictionsController.cs`
- `backend/Api/Program.cs`
- `backend/Application/DependencyInjection.cs`
- `backend/Application/Interfaces/IServices/IPredictiveServices.cs`
- `backend/Application/Services/PredictiveAnalysisService.cs`
- `backend/Application/Services/RecommendationService.cs`
- `backend/Infrastructure/DependencyInjection.cs`
- `backend/Tests/PredictiveCityTests.cs`
- `backend/Tests/Support/EquipmentTestHost.cs`
- `frontend/src/components/map/EquipmentMarkers.jsx`
- `frontend/src/components/map/MapView.jsx`
- `frontend/src/components/map/Markers.css`
- `frontend/src/components/map/OperationalRecordDetails.jsx`
- `frontend/src/components/map/markerIcons.js`
- `frontend/src/components/map/markerPresentation.js`
- `frontend/src/hooks/useOperationalMarkers.js`
- `frontend/src/pages/DashboardPage.jsx`
- `frontend/src/services/predictive.js`
- `README.md`

O apoio de SQL/navegador está em `artifacts/sprint-06/`, ignorado pelo Git, separado do código da aplicação e das suítes versionadas.

## Testes executados

| Verificação | Resultado |
| --- | --- |
| `dotnet build backend/Solution/Solution.slnx --no-restore` | Aprovado; zero erros/avisos |
| `dotnet test backend/Tests/Tests.csproj --no-build --no-restore` | 235 aprovados; zero falhas/ignorados |
| `node --test frontend/tests/*.test.js` | 164 aprovados; zero falhas/ignorados |
| `npm.cmd --prefix frontend run lint` | Aprovado |
| `npm.cmd --prefix frontend run build` | Aprovado; sem nova biblioteca |
| Dois processos e rollback em LocalDB exclusivo | Aprovados nos cenários abaixo |
| Chrome headless com bundle compilado, API real e LocalDB | Aprovado nos cenários abaixo |

Adicionados 20 casos backend e oito frontend, com nomes em português. Backend cobre padrão desabilitado, intervalo inválido, ausência de coordenador distribuído, espera inicial, ciclos, falha/recuperação, cancelamento, ausência de sobreposição, coordenação local aninhada, estado, autorização, contratos, ausência de escrita por leitura e risco alto com equipamento ainda operacional. O serviço real também foi exercitado em falha de repositório e cancelamento, verificando estado sem contagens e liberação do semáforo. Os testes anteriores de idempotência e permissões foram preservados; o teste legado de criação de recomendações recebeu a dependência explícita de coordenação.

Frontend cobre GET sem análise/aceite, autorização, contratos e campos privados descartados, estado sem contagens inventadas, coordenadas regionais, risco independente de falha/manutenção, marcador antigo, erro inicial, retenção e recuperação da leitura, polling conservador e cancelamento ao sair da Central.

### Evidência SQL entre processos

O host de teste só aceita banco local com prefixo restrito `SENTRA_Sprint06_*`, usa `(localdb)\\MSSQLLocalDB`, não carrega conexões privadas e não inicia Program/seeders/workers normais. As migrations existentes foram aplicadas somente a um banco novo exclusivo de teste. Dados e usuários sintéticos foram inseridos somente nesse banco, fora da aplicação real.

1. Primeiro processo adquiriu o bloqueio e manteve a transação aberta (`LOCK_HELD`).
2. Segundo processo independente, com outro host/contêiner de DI/semáforo, tentou executar e recebeu `BLOCKED_BY_SQL_LOCK`.
3. O primeiro concluiu criando seis recomendações e três alertas preventivos.
4. A repetição após liberação concluiu com zero novas recomendações e zero alertas.
5. Inserção seguida de falha e inserção seguida de cancelamento foram revertidas (`ROLLBACK_FAILURE_AND_CANCELLATION_CONFIRMED`).
6. A consulta final confirmou ausência de grupos de recomendações pendentes duplicadas e ausência de ordens de manutenção (`NO_DUPLICATE_PENDING_RECOMMENDATIONS_NO_ORDERS`).

Isso valida o mecanismo SQL entre processos em LocalDB; não equivale à homologação de permissões, falhas de conexão, carga ou topologia Azure. O agendamento foi testado com relógio controlado na suíte; não foi habilitado um worker contra produção.

### Evidência de navegador

Chrome headless com quatro equipamentos e dados SQL exclusivos confirmou: P de risco alto conservando `Operational`, círculo de bairro, ausência de POST ao visitar/atualizar, leituras repetidas preservando quantidades de recomendações/ordens, popup e navegação para fatores, análise manual refletida no estado, filtragem de bairro, carregamento, erro parcial com marcador antigo, recuperação, vazio sem apagar os equipamentos operacionais e permissões. A resposta vazia usou interceptação somente no transporte do teste, sem mock ou dados substitutos na aplicação.

Larguras 320/390/768/1440 px, sem transbordamento horizontal ou exceção JavaScript não tratada. Perfis Admin, CentralOperator, MaintenanceTechnician e Agent exercitados. Evidências locais: `browser-results.json`, `central-marcadores-preditivos.png`, `central-prevencao.png` e `central-prevencao-mobile.png`, em `artifacts/sprint-06/`. Não foram homologados dispositivos físicos nem o site publicado.

## Limitações e riscos conhecidos

- Todas as instâncias participantes devem usar esta versão, o mesmo banco e o mesmo recurso de bloqueio. Escritores antigos, scripts SQL externos e acessos que contornem o serviço não são coordenados. Duplicatas legadas não são removidas automaticamente.
- O agendamento é local, sem eleição de líder ou cooldown global persistente. Réplicas podem executar ciclos consecutivos em horários diferentes; a coordenação evita sobreposição de escritas e as regras reaproveitam pendências, mas o custo pode aumentar com muitas réplicas. Recomenda-se começar a homologação com uma instância.
- O estado é volátil e local. Após restart, ou ao receber resposta de outra réplica, `NotRun` não comprova ausência de análise anterior. Não há histórico global, jobs duráveis, recuperação de ciclos perdidos ou endpoint de configuração.
- O App Service precisa permanecer ativo para contar o intervalo. Reciclagem, suspensão ou scale-to-zero reiniciam a espera. A sprint não altera Always On, plano ou número de instâncias.
- A análise mantém uma transação SQL durante os cálculos existentes; históricos grandes podem aumentar duração, custo e bloqueios. Não foi feito teste de carga nem criado índice especulativo. A consulta da Central também recalcula riscos em leitura; o intervalo frontend de cinco minutos reduz repetição, sem cache global.
- Conflito entre processos retorna 409; falha SQL/permite bloqueio indisponível não executa uma análise sem proteção. Na análise principal, falha/cancelamento reverte a transação. O registro de ocorrências/telemetria conserva suas etapas anteriores de gravação: conflito ao gerar recomendações não deve ser interpretado como prova de que o evento anterior não foi registrado; conferir dados antes de repetir.
- Falha de rede no commit pode ter resultado incerto. Não se publica contagem de sucesso sem confirmação. Atualizar e conferir pendências antes de repetir continua necessário.
- O estado e as fontes operacionais/preditivas são lidos em instantes diferentes. Índices são heurísticos, sem probabilidade calibrada; risco alto não implica equipamento quebrado. Marcadores coincidentes ainda podem se sobrepor, conforme limitação anterior sem clustering.
- A simulação existente não foi alterada nem habilitada. Sua procedência histórica não pode ser deduzida apenas de uma linha de telemetria. Conferir configuração e qualidade dos dados antes de avaliar resultados reais.

## Checklist de validação em produção — não executado

- [ ] Manter `PredictiveAutomation__Enabled=false` durante a publicação e a homologação inicial; publicar frontend/backend compatíveis sem ativação automática.
- [ ] Confirmar todas as instâncias/escritores atualizados, connection string correta e banco compartilhado; evitar coexistência com versões antigas gerando recomendações.
- [ ] Confirmar `Simulation:Enabled=false`, seeders desabilitados e ausência de cargas DEMO na operação real.
- [ ] Validar com a identidade real do App Service as permissões de `sys.sp_getapplock`, transação e gravação. Não conceder privilégios adicionais amplos para facilitar o teste.
- [ ] Auditar pendências/duplicatas legadas e ordens ativas sem exclusão automática. Validar idempotência com dados autorizados e revisão do responsável.
- [ ] Confirmar por leitura que a Central mostra índices, horários, avisos e permissões, sem gravar recomendações ou ordens. Conferir distinção P/situação e recomendações regionais.
- [ ] Revisar plano, Always On, reinícios, duração de análise, bloqueios, volume histórico e custo. Começar com uma instância e intervalo de 60 minutos.
- [ ] Somente após aprovação operacional e comprovação de segurança, configurar opt-in; aguardar um intervalo inteiro antes de esperar a primeira execução. Nenhuma ativação foi feita pelo agente.
- [ ] Conferir origem automática, UTC, contagens confirmadas e logs sem dados privados; testar desligamento/reinício e comportamento de perda do estado local.
- [ ] Antes de escalar, repetir concorrência com identidades/topologia reais, medir ciclos consecutivos entre réplicas e avaliar se precisa de agendamento global durável.

## Teste manual local

1. Usar somente banco local autorizado e configurado conforme README, sem simulação ou seed automático; entrar como gestor ou operador.
2. Abrir Central. Conferir P e círculo por bairro quando houver dados justificáveis, painel Prevenção operacional e horário próprio. Clicar Atualizar prevenção não deve gerar recomendações/ordens.
3. Usar links para equipamento, bairro, manutenção autorizada e fatores. Confirmar que cor e status operacional não mudam por risco alto.
4. Executar análise manual pela tela existente, retornar à Central e conferir origem manual/contagens. Aceite continua apenas na tela preditiva e com gestor; o painel novo é consulta.
5. Simular indisponibilidade apenas no ambiente local, atualizar, verificar dado antigo sinalizado e recuperar. CentralOperator não deve receber acesso à manutenção; técnico/agente devem receber 403 nas consultas novas.
6. Para homologar o worker local, somente após conferir destino SQL e coordenação, configurar `PredictiveAutomation__Enabled=true` e `PredictiveAutomation__IntervalMinutes=15` ou 60, reiniciar e aguardar o intervalo completo. Conferir origem automática e repetir sem duplicar pendências. Restaurar `false` ao terminar. Não executar este procedimento em Azure sem o checklist e a autorização operacional.
