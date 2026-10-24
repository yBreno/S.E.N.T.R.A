# Sprint 05 — Cidade Preditiva

Revisão em 24/10/2026, na branch `main`. Escopo: tornar os mecanismos preditivos existentes acessíveis à operação. Nenhum commit, push, deploy, acesso ao Azure ou alteração em banco de produção foi executado.

## Resultado e cobertura

| Requisito | Estado | Entrega / limite |
| --- | --- | --- |
| Risco por equipamento, 0–100 e baixo/médio/alto | Completo | Cálculo determinístico com falhas, corretivas, preventivas, situação e eventos conhecidos de telemetria |
| Fatores, evidências, janela, avaliação UTC e ação | Completo | Contagens, regra, peso e contribuição; IDs de evidência limitados a 50 por fator, sem limitar a contagem usada |
| Histórico insuficiente | Completo | Indicação explícita para menos de duas falhas recentes em instantes distintos; não confunde falta de registros com segurança |
| Exposição por bairro | Completo no domínio disponível | Reutiliza `RiskCalculator` e acrescenta disponibilidade de agentes do bairro cadastrado; mostra períodos e fatores |
| Recomendações com problema, evidência, prioridade e ação | Completo | Fluxo existente acrescido do gatilho de risco alto de equipamento; interface prioriza e permite consulta dos recursos |
| Integração com manutenção | Completo no fluxo existente | Aceite de equipamento reutiliza `PrepareRecommendationAsync`; análise não cria ordens; ordem ativa impede duplicação |
| Interface e permissões | Completo | Sidebar habilitada, leitura, filtros, detalhes de fatores, atualização manual, confirmação, sucesso, erro, vazio e recuperação |
| MTBF e detecção de picos | Parcial quanto à capacidade preditiva estatística | Algoritmos existentes expostos com limites explícitos; não há modelo calibrado nem probabilidade de falha |
| Anomalias numéricas de sensores | Pendente | O domínio tem eventos e payload livre, sem série tipada, unidades/limiares e qualidade de coleta para inferência confiável |
| Cobertura física e atualidade garantida dos agentes | Parcial | Usa bairro e disponibilidade declarados; não calcula raio de cobertura nem descarta automaticamente posição antiga |
| Concorrência de recomendações entre réplicas | Pendente | Análise serializada nesta instância; não há lock distribuído nem índice único para recomendações pendentes |

## Inspeção e reaproveitamento

Foram examinados domínio, DTOs, serviços, repositórios, controllers, workers, configurações e consumidores frontend de risco, incidentes, heatmap, MTBF, anomalias, telemetria e recomendações. Já existiam `Recommendation`, `RiskCalculator`, `FailurePredictor`, `AnomalyDetector`, `/api/risk`, `/api/predictions/*`, `/api/recommendations/*` e o aceite integrado à manutenção da Sprint 03.

Não havia índice normalizado por equipamento nem módulo visual preditivo. Foi adicionado somente o endpoint de avaliação de equipamentos dentro do controller existente. Nenhuma entidade persistente, CRUD, subclasse de equipamento ou migration foi criada.

`DashboardService`, indicadores operacionais, mapa, heatmap e contratos das Sprints 01–04 foram preservados. O risco regional também consumido pelo heatmap passa a incluir agentes. `NeighborhoodRiskDto` ganhou somente os campos opcionais `incidentWindowFrom` e `failureWindowFrom`. O resumo preditivo legado do dashboard continua contando equipamentos pelo MTBF; o indicador da nova tela conta o índice heurístico por equipamento e tem significado distinto.

## Cálculos e interpretação

### Equipamentos: índice heurístico

Janela padrão: 30 dias (`Predictive:FailureWindowDays`). A avaliação usa uma leitura por fonte, em lote; consultas do mesmo `DbContext` são sequenciais. Corretivas são selecionadas por início efetivo na janela; preventivas agendadas também são selecionadas. A consulta de manutenção projeta apenas os campos necessários. Telemetria projeta identificação, equipamento, instante e tipo, sem payload ou GPS.

| Fator | Pontos máximos | Regra |
| --- | ---: | --- |
| Falhas recentes | 30 | `min(alertas de falha / 3, 1)` |
| Frequência de corretivas | 20 | `min(ordens corretivas efetivamente iniciadas / 3, 1)` |
| Preventiva pendente | 25 | Data vencida: 25; até sete dias: 10; outra pendência: 5; sem pendência: 0 |
| Indisponibilidade | 15 | Falha, manutenção ou furto: 15; demais situações: 0 |
| Sinais categóricos de telemetria | 10 | `min(eventos conhecidos / 3, 1)` |

Os fatores somam até 100. Baixo: abaixo de 25; médio: de 25 a menos de 50; alto: a partir de 50. Esses limiares de equipamento são fixos nesta versão, separados dos parâmetros legados de risco regional. Eventos futuros ou anteriores à janela não pontuam. Ordens corretivas apenas agendadas não contam como corretivas executadas/iniciadas. Preventivas concluídas não contam como ordens pendentes. O planejamento atual do inventário também pode sinalizar pendência, mas não comprova uma ordem ou execução.

São reconhecidos eventos categóricos de conexão, bateria e temperatura já compatíveis com o sistema. Heartbeat e eventos desconhecidos não pontuam. Isso não transforma payload livre em anomalia estatística. Falha atual recomenda atendimento mesmo quando a pontuação é baixa; furto e manutenção em andamento têm orientações específicas.

Histórico suficiente neste contrato significa apenas existir ao menos um intervalo entre falhas distintas na janela, sem afirmar suficiência estatística. Falhas, corretivas e telemetria podem descrever o mesmo episódio: os pesos são regras operacionais, não variáveis independentes de um modelo probabilístico. Ausência de histórico não estima probabilidade zero.

### Bairros: índice heurístico existente ampliado

Com a fonte de agentes disponível, os pesos anteriores são multiplicados por 0,85 e adiciona-se cobertura com peso 0,15. Nos parâmetros padrão: densidade 34 pontos, falhas recorrentes 25,5, pico 17, indisponibilidade 8,5 e lacuna de agentes 15. Os limiares regionais existentes de 25/50/75 e o nível `Critical` permanecem.

Densidade e recorrência são relativas ao maior valor entre os bairros observados. Recorrência usa equipamentos com duas ou mais falhas na janela padrão de 30 dias. Densidade usa sete dias. Falhas/indisponibilidade e cobertura refletem os registros realmente disponíveis; o fator de pico é uma regra existente, não previsão de ocorrências. A lacuna de cobertura é `1 − disponíveis / total do bairro`; sem agentes cadastrados no bairro, é 1. Não equivale a necessidade calibrada de efetivo. Bairros só com agentes também podem aparecer.

Disponibilidade, endereço e posição são atuais. Consultar uma data passada não reconstrói inventário/cobertura histórica. Não são expostos CPF ou trajetos de agentes nas explicações de cobertura.

### Estatística descritiva existente

- MTBF: média dos intervalos de falha do equipamento, com fallback por tipo; amostra e base exibidas. `dias decorridos / MTBF` indica consumo do intervalo médio, sem probabilidade associada. A extrapolação restante é `max(0, MTBF − tempo decorrido)`; não é data garantida de falha. O registro da última falha é proxy legado de restauração, quando não há manutenção posterior. Foram excluídos intervalos nulos, registros futuros e encerramentos de manutenção futuros.
- Picos de volume: janela padrão de três horas versus janelas equivalentes dos mesmos dias da semana nas seis semanas anteriores; compara contagem com média mais duas vezes o desvio. Sem metadados de cobertura, semanas sem registros contam como zero. Mudanças de coleta/cadastro podem parecer anomalias.
- Falhas repetidas: limiar de contagem na janela, sem inferência probabilística.

Não há treinamento, calibração, intervalo de confiança, fornecedor de IA ou chatbot. A tela separa esses resultados do índice heurístico e destaca amostras pequenas e histórico insuficiente.

## Recomendações, ações e autorização

Risco alto de equipamento operacional ou em falha pode gerar `SendMaintenance`, com a avaliação completa no JSON explicativo existente. Equipamento com ordem agendada/em andamento não recebe nova sugestão de manutenção. Recomendações pendentes do mesmo tipo/equipamento são reaproveitadas mesmo após mudança de bairro. Gatilhos existentes de MTBF, anomalias e risco regional continuam ativos.

A prioridade exibida deriva do nível registrado; na ausência dele, do índice ou do gatilho conhecido de MTBF/anomalia. Legado sem evidência suficiente mostra prioridade não informada. Recomendações já persistidas não são reescritas automaticamente; representam a evidência do momento da geração.

`POST /api/predictions/analyze` é explícito, exige confirmação na tela e é serializado por um semáforo singleton durante a execução nesta instância. Não é disparado por visita à tela, atualização manual ou polling da Central. Ele pode criar alertas preventivos e recomendações, sem abrir manutenção.

O aceite de `SendMaintenance` com equipamento usa o fluxo da Sprint 03: preventiva para operacional ou corretiva para falha, respeitando as regras atuais e uma ordem ativa por equipamento. Aceite repetido de recomendação aceita é idempotente. O índice SQL já existente protege ordens ativas. Recomendações de manutenção apenas regionais exigem triagem e não oferecem aceite direto na tela. `DeployAgent` registra decisão para avaliação de reforço, sem alterar posição ou disponibilidade de agente.

| Papel | Consultar tela / bairros / anomalias | Executar análise | Aceitar / descartar |
| --- | --- | --- | --- |
| Admin / OperationManager | Sim | Sim | Sim |
| CentralOperator | Sim | Sim, conforme permissão existente | Não |
| MaintenanceTechnician | Sem módulo visual; mantém consulta Staff de risco por equipamento, MTBF e recomendações de manutenção | Não | Não |
| Agent / anônimo | Não | Não | Não |

JSON explicativo legado inválido deixa a recomendação visível sem evidência estruturada, em vez de quebrar toda a consulta. Criação e resolução das recomendações são retornadas com designação UTC, inclusive após leitura SQL.

## Endpoints utilizados

| Método e rota | Uso |
| --- | --- |
| `GET /api/predictions/equipment-risk` | Novo: avaliações heurísticas de todos os equipamentos; Staff |
| `GET /api/risk` | Índices regionais com fatores e janelas; Monitoring |
| `GET /api/predictions/failures` | Estimativas descritivas MTBF; contrato existente |
| `GET /api/predictions/anomalies` | Picos e falhas repetidas; Monitoring |
| `GET /api/recommendations?status=Pending` | Recomendações persistidas pendentes |
| `POST /api/predictions/analyze` | Análise com geração persistida de alertas/recomendações; Monitoring |
| `POST /api/recommendations/{id}/accept` | Aceite e integração existente com manutenção; Managers |
| `POST /api/recommendations/{id}/dismiss` | Descarte; Managers |

Os botões de equipamento, agentes e manutenção navegam aos módulos existentes, que continuam usando seus próprios endpoints. A tela preditiva reaproveita o cliente HTTP, o recurso de leitura com timeout/cancelamento, a retenção de dados válidos, os detalhes e o CSS do inventário. Não adiciona biblioteca nem consulta um provedor externo.

## Arquivos criados e modificados

Criados:

- `backend/Application/DTOs/Predictive/EquipmentRiskDto.cs`
- `backend/Application/Predictive/EquipmentRiskCalculator.cs`
- `backend/Application/Services/EquipmentRiskService.cs`
- `backend/Application/Services/PredictiveExecutionGate.cs`
- `backend/Tests/EquipmentRiskTests.cs`
- `backend/Tests/PredictiveHttpTests.cs`
- `frontend/src/services/predictive.js`
- `frontend/src/pages/PredictivePage.jsx`
- `frontend/src/pages/PredictivePage.css`
- `frontend/tests/predictive.test.js`
- `docs/reports/sprint-05-cidade-preditiva.md`

Modificados:

- `backend/Api/Controllers/PredictionsController.cs`
- `backend/Api/Controllers/RecommendationsController.cs`
- `backend/Application/DTOs/Predictive/RiskDtos.cs`
- `backend/Application/DependencyInjection.cs`
- `backend/Application/Interfaces/IServices/IPredictiveServices.cs`
- `backend/Application/Mappings/PredictiveMappings.cs`
- `backend/Application/Predictive/FailurePredictor.cs`
- `backend/Application/Predictive/RiskCalculator.cs`
- `backend/Application/Services/PredictiveAnalysisService.cs`
- `backend/Application/Services/RecommendationService.cs`
- `backend/Application/Services/RiskService.cs`
- `backend/Domain/IRepositories/IMaintenanceRecordRepository.cs`
- `backend/Domain/IRepositories/ITelemetryLogRepository.cs`
- `backend/Infrastructure/Repositories/MaintenanceRecordRepository.cs`
- `backend/Infrastructure/Repositories/RecommendationRepository.cs`
- `backend/Infrastructure/Repositories/TelemetryLogRepository.cs`
- `backend/Tests/Support/EquipmentTestHost.cs`
- `frontend/src/components/dashboard/DashboardHeader.jsx`
- `frontend/src/components/dashboard/DashboardSidebar.jsx`
- `frontend/src/pages/DashboardPage.jsx`
- `frontend/src/services/verificationResource.js`
- `README.md`

Os arquivos de apoio da checagem de navegador ficam somente em `artifacts/sprint-05/`, ignorado pelo Git. Incluem host isolado, roteiro CDP, resultados JSON e capturas; não integram a aplicação ou a lista versionada acima.

## Verificação executada

| Validação | Resultado |
| --- | --- |
| `dotnet build backend/Solution/Solution.slnx --no-restore` | Aprovado; zero avisos/erros |
| `dotnet test backend/Tests/Tests.csproj --no-build --no-restore` | 215 aprovados; zero falhas/ignorados |
| `node --test frontend/tests/*.test.js` | 156 aprovados; zero falhas/ignorados |
| `npm.cmd --prefix frontend run lint` | Aprovado |
| `npm.cmd --prefix frontend run build` | Aprovado; módulo preditivo carregado sob demanda, aproximadamente 23 kB / 7 kB gzip |
| `git diff --check` | Aprovado |
| Chrome headless, bundle compilado + API real + SQL Server LocalDB | Aprovado nos cenários descritos abaixo |

Foram adicionados 21 casos backend e oito frontend, com nomes em português. Cobrem baixo/médio/alto, histórico insuficiente, falha atual, recorrência, preventiva atrasada, eventos fora da janela, composição por bairro, pesos/evidências, privacidade do payload, contratos UTC, autorização, análises repetidas/concorrentes nesta instância, troca de bairro sem duplicação e aceite idempotente. Frontend cobre contratos, resposta inválida/vazia, carregamento/erro parcial, recuperação, ausência de análise automática, permissões, transporte HTTP e priorização. Os testes existentes foram preservados.

O host SQL de teste não inicia `Program` normal, não lê conexões privadas, não registra workers ou seeders da aplicação e só aceita nomes novos com prefixo restrito no `(localdb)\\MSSQLLocalDB`. As cinco migrations existentes foram aplicadas somente nesses bancos exclusivos, para conferir consultas SQL e integração real. As credenciais e os dados sintéticos pertencem exclusivamente ao teste; não foi inserido dado fictício na aplicação real.

No Chrome foram conferidos quatro equipamentos e dois bairros, fatores, histórico insuficiente, filtros, recomendações geradas, repetição da análise, aceite e reaceite com uma única ordem, navegação ao histórico de manutenção, dados preservados após falha parcial, recuperação manual, carregamento e permissões Admin/CentralOperator/MaintenanceTechnician/Agent. Resposta vazia foi verificada por interceptação exclusiva do transporte de teste; não há mock no código da aplicação. Larguras: 320, 390, 768 e 1440 px, sem transbordamento horizontal ou exceção JavaScript não tratada. Isso não homologa dispositivos físicos nem produção.

Evidências locais, fora do versionamento: `artifacts/sprint-05/browser-results.json`, `preditiva-desktop.png`, `preditiva-mobile.png` e `preditiva-recomendacoes.png`.

## Pendências e riscos de implantação no Azure SQL

- **Sem migration nova.** O código reutiliza as tabelas e colunas já existentes. Não altera connection strings, secrets, identidades Azure, workflows, configurações de deploy ou execução automática de migrations.
- Frontend e backend devem ser publicados na mesma versão; um backend antigo responderá 404 ao novo endpoint. A tela mostra erro de fonte, preservando as demais, sem criar dados substitutos.
- Avaliações não são persistidas como entidade nem cacheadas globalmente: cada atualização manual consulta os dados atuais. O novo cálculo usa lotes e projeções; os algoritmos legados de MTBF/anomalias e a geração por recurso ainda podem custar caro em históricos grandes. Não foi feito teste de carga Azure nem criado índice especulativo.
- O semáforo não coordena réplicas, processos distintos ou o registro de incidentes que gera recomendações pelo fluxo já existente. Para unicidade de recomendações sob esses cenários, auditar pendências legadas e adotar coordenação distribuída ou índice SQL compatível. Não foi aplicada restrição que pudesse falhar por duplicatas antigas. Ordens ativas mantêm a proteção SQL existente; conflitos exigem atualização da tela.
- A análise existente grava por etapas. Timeout/cancelamento pode deixar alertas ou recomendações já gravados; não é transação de toda a cidade. A interface informa resultado incerto e exige nova leitura completa antes de repetir uma ação. As cinco fontes lidas separadamente também não são um snapshot transacional.
- O worker de simulação existente tem padrão habilitado fora do ambiente Demo. A configuração não foi alterada. Para operação real, o responsável deve conferir `Simulation:Enabled=false` e a procedência dos registros. Eventos antigos não distinguem simulação de sensor físico; não foram apagados nem reclassificados.
- MTBF depende da qualidade do registro de falhas/restaurações; picos dependem da cobertura temporal; agentes podem ter posição antiga. Não há série tipada de telemetria, estimativa de probabilidade calibrada, cobertura geográfica garantida ou despacho automático.
- Recomendações regionais sem equipamento continuam exigindo triagem; reagendamento, cancelamento e atribuição de equipe mantêm as limitações anteriores. Não houve expansão desses módulos nesta sprint.

## Roteiro objetivo de teste manual

1. Prepare somente um banco local autorizado com o schema existente, conforme README. Desabilite simulação e seed automático; use conta local já provisionada. Inicie API e frontend com a URL local correta.
2. Entre como Admin/OperationManager e abra `/#cidade-preditiva` ou a sidebar. A primeira leitura não deve criar recomendação, alerta ou manutenção. Confira hora da leitura, janela e fatores dos equipamentos/bairros.
3. Use busca, bairro e nível. Em equipamento sem falhas suficientes, confira a indicação de histórico insuficiente; um índice baixo não deve prometer segurança. Expanda os fatores e confira regra, pontos e contagem.
4. Em dados locais autorizados com histórico de falhas/preventiva vencida, confirme **Executar análise**. Confira recomendações com motivo e prioridade. Repita a análise: a mesma pendência de equipamento não deve ser duplicada; nenhuma ordem nasce só pela análise.
5. Aceite uma recomendação de equipamento sem ordem ativa. Confira a ordem no módulo de manutenção. Repetir o aceite não deve gerar outra. Recomendações apenas regionais devem indicar triagem; reforço de agentes deve informar que não faz despacho automático.
6. Entre como CentralOperator: consulta e análise disponíveis; aceite/descarte e acesso à manutenção ausentes. MaintenanceTechnician não deve ver o módulo preditivo, mantendo suas consultas Staff existentes; Agent deve ser recusado pelo backend.
7. Desligue a rede e pressione **Atualizar dados**. Confira erro e leitura anterior sinalizada; reconecte e atualize para limpar o erro. Durante a consulta, botões ficam desabilitados. Uma fonte indisponível não deve ser apresentada como zero ou lista vazia bem-sucedida.
8. Confira MTBF/base/amostra em seção separada, sem probabilidade inventada. Redimensione para celular e navegue de volta à Central/equipamentos/manutenção para verificar os módulos anteriores.
