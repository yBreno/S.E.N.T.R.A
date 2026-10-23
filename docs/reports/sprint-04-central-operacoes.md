# Sprint 04 — Central de Operações da Cidade

Revisão de 23/10/2026. Complementa as Sprints 01–03, sem alteração em Azure/deploy, produção,
commit ou push. Não houve migration nova. As migrations existentes foram aplicadas somente em
bancos novos e exclusivos de teste no SQL Server LocalDB. Dados sintéticos ficaram restritos a esses testes.

## 1. Auditoria: o que já existia

Foram inspecionados DashboardPage, indicadores, mapa Leaflet, camadas, filtros, marcadores, popups,
inspeção, autenticação, serviços HTTP, cache do calor, hooks de atualização e as integrações de
equipamentos, agentes e manutenções. No backend, foram conferidos controllers, DTOs, serviços e
permissões de dashboard, equipamentos, agentes, manutenção, alertas, ocorrências, calor e risco.
O enunciado oficial da Parte 2 também foi consultado no anexo original fornecido pelo usuário.

Já havia mapa funcional, validação de coordenadas, cores por situação, seleção de recursos, camadas
independentes, indicadores derivados da API e filtros de equipamento/disponibilidade de agente.
O calor possuía consultas próprias, cache em memória e proteção contra respostas antigas. As três
sprints anteriores ofereciam os fluxos de gestão e a manutenção preventiva/corretiva.

As lacunas reais eram: símbolo genérico por equipamento, detalhes sem planejamento/endereço completo,
categoria de agente sem filtro, bairro aplicado somente a equipamentos, painel de alertas vazio,
ausência de destaques de manutenção e consolidação atual por bairro, atualização somente manual e
bundle de aproximadamente 510 kB. O dashboard também executava resumos preditivos a cada consulta,
embora a Central não os apresentasse.

## 2. Requisitos oficiais atendidos

| Requisito da Parte 2 | Entrega e estado |
| --- | --- |
| Equipamentos e agentes no mapa com identificações diferentes | Atendido para identificadores/coordenadas válidos: oito símbolos de equipamento, três de categoria profissional; quadrados/círculos |
| Situação de cada equipamento | Atendido: cores/legenda para os seis estados existentes e fallback seguro para valores desconhecidos |
| Indicadores gerais | Atendido: totais, operacionais, indisponíveis, disponibilidade e agentes ativos, com definições anteriores preservadas |
| Filtros operacionais | Atendido: tipo, situação, bairro em ambas as camadas, categoria e disponibilidade de agente; alcance explicitado |
| Região e órgão responsável | Limitado pelos dados: bairro é a classificação real disponível; não foram inventados órgãos/regiões administrativas |
| Manutenção imediata/próxima | Atendido: regra única de sete dias, previsão vencida, sinalização em marcador/popup/painéis e agendamento sem indisponibilidade artificial |
| Alertas críticos | Atendido: alertas abertos reais, prioridade Critical/High, horário, referência ao equipamento e navegação |
| Consulta diretamente pelo mapa | Atendido: popup/inspeção enriquecidos e navegação contextual aos detalhes de equipamento/agente e manutenção autorizada |
| Visão consolidada da cidade | Atendido por bairro: contagens atuais de recursos, indisponibilidade, necessidades, intervenções e alertas High/Critical |
| Informações atualizadas continuamente | Atendido por polling HTTP configurável, padrão de 30 s após cada ciclo; não representa streaming físico de IoT |

Não foi criado outro motor de risco, nova funcionalidade preditiva, gestão administrativa de órgãos
ou tela de registro de ocorrências. O painel atual não apresenta estatística histórica como situação em tempo real.

## 3. Implementação e integração

### Mapa e detalhes

O Leaflet e seus controles foram preservados. O serviço de apresentação usa SVGs estáticos para
os tipos/categorias existentes; classe, cor e HTML são escolhidos exclusivamente de tabelas locais.
Entradas desconhecidas recebem fallback e não entram diretamente em HTML/classes. Os ícones são
reutilizados por combinação de tipo, situação e manutenção, sem biblioteca adicional.

Recursos com latitude/longitude ausentes, fora da faixa, não numéricas ou sem identificador válido
continuam descartados da camada. Não são deslocados para zero ou para o centro de São Paulo.
GPS zero numérico é válido. Os totais da API permanecem globais, inclusive recursos sem posição válida;
os contadores do mapa mostram recebidos, válidos, filtrados, descartados e visíveis.

Os popups e a inspeção apresentam identificação, situação, GPS, categoria, necessidade/próxima
previsão, características pertinentes ao tipo e endereço associado. Agentes mostram categoria,
matrícula, bairro, disponibilidade e horário de posição, sem CPF no mapa. Os botões abrem os detalhes
completos das sprints anteriores; isso inclui o veículo vinculado no módulo do agente. O contexto de
navegação é compartilhado com os popups Leaflet, sem reimplementar o mapa.

Filtros ficam no DashboardPage e permanecem ao ir à gestão e voltar. A sincronização não desmonta
o mapa, não limpa seleção/filtros nem força recentralização. Selecionar um bairro na consolidação aplica
esse filtro ao mapa; demais critérios continuam combinados. Os filtros locais não alteram indicadores,
alertas ou consolidação global. A rolagem da página/tabelas é independente da posição geográfica do mapa.

### Alertas e manutenção

RecentAlerts agora usa `GET /api/alerts?status=Open`. Ordena por severidade Critical, High, Medium,
Low e, em empate, por horário decrescente. High/Critical recebem destaque urgente, sem criar outro
valor de severidade. Exibe mensagem, data/hora, tipo e equipamento, com loading/vazio/erro e último
horário válido. Listas longas permitem revelar mais itens e rolar internamente.

MaintenanceHighlights mostra necessidades imediatas, previsões próximas/vencidas e equipamentos em
UnderMaintenance. Também consulta Scheduled/Open para Admin/OperationManager, com lista e navegação
ao histórico da Sprint 03. CentralOperator não recebe esses GETs nem acesso adicional: consulta o
equipamento e seus metadados. Técnico tem acesso ao módulo próprio, mas continua sem acesso à Central.

Uma previsão no inventário não é contada como ordem real. Quantidades de ordens vêm exclusivamente
dos endpoints de manutenção autorizados; as contagens de previsão na consolidação são identificadas
como metadados. Não foi implementado cancelamento, reagendamento ou resolução manual de alertas na tela.

## 4. Regras de cálculo

| Informação | Definição |
| --- | --- |
| Total | Quantidade total de equipamentos da API |
| Operacionais | ByStatus.Operational |
| Indisponíveis | Malfunctioning + UnderMaintenance + Stolen, preservando a definição anterior |
| Disponibilidade | Operational / total × 100; duas casas; zero para total zero |
| Agentes ativos | Available + Busy, equivalente ao CountActiveAsync existente; Offline excluído |
| Imediata | MaintenanceNeed.Immediate ou estado Malfunctioning, para preservar sinal de falha real em registros antigos |
| Próxima | Need Scheduled/Periodic e NextMaintenanceDate válida menor ou igual a agora + 7 × 24 horas; inclui vencidas |
| Vencida | Mesma previsão válida, anterior a agora |
| Em manutenção | Estado UnderMaintenance; não deriva de uma previsão agendada |
| Ordens agendadas/em andamento | Registros reais Scheduled/Open dos endpoints autorizados |
| Alertas urgentes por bairro | Alertas Open com severidade High/Critical, associados pelo EquipmentId ao bairro atual do equipamento |

`operations.js` centraliza a janela de sete dias, os estados indisponíveis da consolidação e as
regras de destaque. O percentual geral continua vindo do backend; não é recalculado sobre marcadores.
Deactivated/NotInstalled permanecem fora da soma de indisponíveis da definição anterior, embora
entrem no total e no denominador da disponibilidade. Portanto, indisponíveis não é o complemento dos operacionais.

A consolidação normaliza caixa, espaços e acentos do bairro e agrupa ausentes em “Bairro não informado”.
Conta recursos mesmo sem GPS válido e ordena pelas contagens observadas de alertas, necessidade imediata,
indisponibilidade e proximidade, sem criar score. Categorias podem se sobrepor. “Sem sinais nos dados
recebidos” não é garantia de ausência de problemas; falhas parciais ficam sinalizadas. Sem resposta válida
de alertas, sua contagem fica desconhecida, não zero. Alertas sem equipamento localizado são informados à parte.

## 5. Endpoints e extensões compatíveis

| Endpoint | Uso / mudança |
| --- | --- |
| GET `/api/dashboard?includePredictive=false` | Totais e indicadores operacionais sem executar análise histórica a cada ciclo |
| GET `/api/dashboard` | Comportamento anterior preservado: includePredictive padrão true |
| GET `/api/equipments` e `/api/equipments/{id}` | Marcadores, planejamento, bairros e detalhes; DTO passa a incluir Address opcional já associado |
| GET `/api/agents` e `/api/agents/{id}` | Marcadores e detalhes/contexto do profissional, preservando privacidade |
| GET `/api/alerts?status=Open` | Alertas reais; CreatedAt/ResolvedAt mapeados explicitamente para UTC |
| GET `/api/maintenances?status=Scheduled` | Ordens agendadas, somente perfis já autorizados |
| GET `/api/maintenances?status=Open` | Intervenções em andamento, somente perfis já autorizados |
| GET `/api/heatmap?metric=Incidents` | Resposta padrão compartilhada no ciclo da Central |
| GET `/api/heatmap` com filtros existentes | Calor aplicado pelo usuário; cache e cancelamento existentes preservados |
| GET `/api/addresses/{id}`, GET `/api/agents/{id}/vehicle` | Fluxos completos anteriores acionados pela navegação aos detalhes |

Ocorrências e risco foram auditados e preservados: `/api/incidents` alimenta histórico/calor e
`/api/risk` continua disponível aos perfis anteriores. Não foram criadas novas consultas de risco
para a consolidação atual nem um novo painel preditivo.

O parâmetro opcional do dashboard retorna `predictive: null` quando false, explicitando que esse
resumo não foi calculado; sem o parâmetro o contrato anterior permanece. Address reutiliza AddressResponseDto,
sem entidade/migration nova. Os dados de endereço já eram acessíveis pelos mesmos perfis em endpoints existentes.

## 6. Atualização automática, cache e falhas

- Padrão de 30000 ms, configurável por `VITE_POLL_INTERVAL_MS` no início do Vite/build.
  Valores entre 5000 e 300000 ms são aceitos; inválidos voltam a 30000.
- Um único agendador por sessão/contexto; o próximo ciclo começa após o término do anterior mais o intervalo.
  Atualização manual durante ciclo ativo não duplica as consultas.
- Os endpoints independentes são consultados em paralelo, com um AbortController compartilhado no ciclo.
  Limite de 20 s aborta o ciclo e permite recuperação futura, mesmo se um transporte ignorar cancelamento.
- Desmontagem, logout e saída para gestão cancelam requisições e timers. A geração da execução impede
  resposta antiga de sobrescrever uma nova; há testes de StrictMode, cancelamento e resposta tardia.
- Falha parcial preserva último dado válido por endpoint e seu horário. Sucessos substituem o dado anterior,
  inclusive quando a nova resposta está vazia. O horário geral avança apenas após ciclo completo bem-sucedido.
- HTTP 401 vigente encerra a sincronização e solicita logout. 403 é erro de autorização, sem promover o usuário.
- O calor padrão reutiliza o resultado compartilhado, sem GET duplicado. Consultas de calor já aplicadas
  com filtros próprios são renovadas após ciclos; um filtro ainda não aplicado não inicia consulta automática.
  Deduplicação de requisição ativa, cache por consulta e cancelamento do recurso de calor foram preservados.
- Não há timer de polling nos módulos de gestão nem sincronização entre abas/instâncias do navegador.

As fontes são endpoints separados, não uma fotografia transacional única. Mudanças durante uma rodada
podem aparecer em instantes diferentes; ciclos seguintes reconciliam a apresentação. Não se declara
integração IoT física, websocket, streaming ou latência garantida de sensores.

## 7. Bundle e decisões de interface

Mapa e páginas de equipamentos/agentes/manutenções passaram a carregar sob demanda com React.lazy/Suspense.
O chunk principal caiu de cerca de 510 kB para **282,15 kB** (89,56 kB gzip); o mapa ficou em **181,26 kB**
(53,88 kB gzip). O build terminou sem o aviso de chunk acima de 500 kB. Não houve biblioteca nova,
reescrita do dashboard, mudança de arquitetura ou configuração de deploy.

ModuleBoundary apresenta mensagem e recuperação por recarregamento em falha de carregamento do módulo.
A sessão continua sujeita à validação existente após recarregar. A falha controlada de um chunk de agentes
e a recuperação foram testadas no bundle compilado. Layout, cores, painéis, campos e botões reutilizam
o design existente; tabelas/listas extensas rolam internamente.

## 8. Autorizações preservadas

| Papel | Central/dashboard/agentes | Equipamentos/alertas | Ordens de manutenção |
| --- | --- | --- | --- |
| Admin / OperationManager | Sim | Sim | Sim |
| CentralOperator | Sim | Sim | Não |
| MaintenanceTechnician | Não | Sim, pelos módulos permitidos | Sim, no módulo de manutenção |
| Agent | Não | Não para inventário geral | Não |
| Sem token | HTTP 401 | HTTP 401 | HTTP 401 |

Nenhuma política Roles foi ampliada. O operador pode consultar alertas e equipamentos conforme a política
existente, mas não lê ordens completas, custos ou dados de equipe pela Central. A sincronização retém
apenas os campos necessários: CPF, email e campos extras de agentes não são conservados no mapa;
campos desconhecidos de atributos/endereço também são descartados.

## 9. Arquivos da Sprint 04

Há alterações anteriores não commitadas no workspace. Esta relação identifica a contribuição desta sprint,
inclusive em arquivos criados nas entregas anteriores, sem atribuir todo o git status à Sprint 04.

**Backend alterado:**

- `backend/Api/Controllers/DashboardController.cs`
- `backend/Application/DTOs/Dashboard/DashboardDto.cs`
- `backend/Application/DTOs/Equipment/EquipmentDtos.cs`
- `backend/Application/Interfaces/IServices/IServices.cs`
- `backend/Application/Mappings/Mappings.cs`
- `backend/Application/Services/DashboardService.cs`
- `backend/Tests/Support/EquipmentTestHost.cs` — destino de teste aceita também prefixo restrito Sprint04.

**Backend novo:** `backend/Tests/CentralOperationsHttpTests.cs`.

**Frontend alterado:**

- `frontend/src/components/dashboard/DashboardIndicators.jsx`
- `frontend/src/components/dashboard/RecentAlerts.jsx`
- `frontend/src/components/map/AgentMarkers.jsx`
- `frontend/src/components/map/EquipmentMarkers.jsx`
- `frontend/src/components/map/MapView.jsx`
- `frontend/src/components/map/Markers.css`
- `frontend/src/components/map/OperationalFilters.jsx`
- `frontend/src/components/map/OperationalRecordDetails.jsx`
- `frontend/src/components/map/markerIcons.js`
- `frontend/src/components/map/operationalFilters.js`
- `frontend/src/hooks/useEndpointVerification.js`
- `frontend/src/hooks/useHeatmap.js`
- `frontend/src/pages/DashboardPage.jsx`
- `frontend/src/pages/AgentPage.jsx`
- `frontend/src/services/heatmapResource.js`
- `frontend/src/services/verification.js`
- `frontend/tests/filters.test.js`
- `frontend/tests/heatmap.test.js`
- `frontend/tests/heatmapResource.test.js`
- `frontend/tests/services.test.js`

**Frontend novo:**

- `frontend/src/components/ModuleBoundary.jsx`
- `frontend/src/components/dashboard/OperationsOverview.jsx`
- `frontend/src/components/dashboard/OperationsOverview.css`
- `frontend/src/components/map/markerPresentation.js`
- `frontend/src/components/map/operationalNavigation.js`
- `frontend/src/services/operations.js`
- `frontend/src/services/verificationResource.js`
- `frontend/tests/operations.test.js`
- `frontend/tests/polling.test.js`

**Entrega:** `README.md` atualizado e este relatório criado. Helpers e evidências locais ficam em
`artifacts/sprint-04/`, diretório ignorado; não integram os dados nem a execução normal da aplicação.

## 10. Testes e evidências

| Verificação | Resultado |
| --- | --- |
| Build da solução backend | Aprovado, sem erros/avisos |
| `dotnet test backend/Tests/Tests.csproj --no-restore` | 194 aprovados, 0 falhas, 0 ignorados |
| `node --test frontend/tests/*.test.js` | 148 aprovados, 0 falhas |
| Lint frontend | Aprovado |
| Build frontend | Aprovado, sem aviso de chunk acima de 500 kB |
| `git diff --check` | Aprovado |
| Chrome, API real e SQL Server LocalDB exclusivo | Aprovado, inclusive bundle compilado servido por Vite preview |

Os testes acrescentados cobrem regras de proximidade no limite de sete dias/vencidas/datas inválidas,
agendamento sem indisponibilidade, consolidação/bairro ausente/alerta sem associação, severidade e ordem
de alertas, símbolos distintos e fallback seguro, filtros combinados de agentes, descarte de coordenadas,
campos privados descartados, consultas de ordem conforme perfil, polling/StrictMode/manual sem duplicidade,
timeout, cancelamento, resposta atrasada, preservação de horário, erro parcial, recuperação e HTTP 401.
O calor recebeu teste para evitar consulta padrão duplicada ou aplicação automática de filtro ainda não consultado.
No backend foram conferidos papéis, inventário vazio, indicadores, endereço completo, UTC de alertas e
compatibilidade do dashboard com/sem resumo preditivo. Os testes das três sprints anteriores continuam passando.

**Integração no Chrome:** nove equipamentos persistidos (oito posições válidas e uma inválida) e três agentes.
Os oito tipos e três categorias apareceram com SVGs diferentes; a posição inválida não virou marcador.
Indicadores iniciais: total 9, operacionais 4, indisponíveis 3, disponibilidade 44,44%, ativos 2.
Após uma falha real registrada por PATCH, o polling refletiu 9 / 3 / 4 / 33,33% / 2 sem F5; filtros,
popup selecionado e a instância do mapa foram preservados. Concluir a ordem existente da Sprint 03
restaurou 9 / 4 / 3 / 44,44% / 2 e removeu o alerta resolvido do painel aberto.

Também foram verificados navegação mapa → equipamento/agente, manutenção → Central, pausa do polling
na gestão, seleção de bairro pela tabela, perda de rede preservando marcadores/horário e recuperação
automática, operador sem requisições privilegiadas, técnico sem Central e agente sem inventário geral.
O intervalo foi reduzido para 5 s somente no ambiente de teste. A execução final observou 187 respostas
da API e nenhuma exceção JavaScript não tratada. As larguras 320/390/768/1440 px não produziram
transbordamento horizontal da página. Falha deliberada de chunk foi apresentada pela interface e a
recuperação restabeleceu a página de agentes com os registros reais do banco de teste.

Evidências locais inspecionadas: `artifacts/sprint-04/browser-results.json`, `central-desktop.png`,
`central-mobile.png`, `central-neighborhoods.png`, além de `browser-review.mjs` e `BrowserHost/`.
Os servidores e o Chrome isolado foram encerrados ao final; bancos de teste foram preservados sem exclusão.
Não se trata de homologação do Azure, de aparelhos físicos ou de integração IoT em campo.

## 11. Pendências e limites

- Órgão/região administrativa não existem como classificação confiável dos recursos; agrupamento por bairro.
- Ordenação por bairro é operacional e baseada em contagens, não previsão ou índice de risco.
- Ordens reais ficam restritas aos perfis anteriores; o operador vê necessidade/situação do equipamento,
  sem ganhar acesso à manutenção completa.
- Atualização é polling por aba e endpoints independentes, sem garantia de tempo real estrito/fotografia única.
- A listagem compartilhada carrega os recursos completos; grandes inventários exigirão paginação/agregação
  de servidor. Não foram introduzidos clustering/spiderfy; marcadores próximos podem se sobrepor.
- Classificação de bairros usa o texto atual cadastrado; não é uma malha geográfica administrativa oficial.
- Resolução manual de alertas, registro/gestão de ocorrências pela interface, cancelamento/reagendamento
  de manutenção e novas telas preditivas permanecem fora desta entrega.
- Janela do calor e métricas históricas existentes continuam independentes da consolidação atual.

## 12. Roteiro manual

1. Siga a execução local do README, com banco isolado, cinco migrations anteriores, simulação/seeders
   desativados e contas locais autorizadas. Não use produção ou HTTP Demo para testar gravações.
2. Antes do Vite/build, configure opcionalmente `VITE_POLL_INTERVAL_MS=5000` para uma demonstração rápida.
   O padrão é 30000. Acesse `/#dashboard` como Admin ou OperationManager.
3. Confira os cinco indicadores e seu alcance global. Observe os tipos/cores/legendas no mapa; recursos
   sem coordenadas válidas devem aparecer como descartados, sem marcador artificial.
4. Combine tipo/situação/bairro com categoria/disponibilidade de agente. Confira os contadores das camadas
   e que os cinco indicadores globais não mudam com filtros locais. Limpe os filtros.
5. Selecione equipamento e agente no mapa. Confira dados pertinentes, endereço/GPS, manutenção e botões
   para os detalhes completos. Volte à Central e confirme os filtros preservados.
6. Em outra aba/sessão autorizada, altere a situação de um equipamento ou a posição/disponibilidade de
   um agente pela API ou pelos módulos existentes. Sem F5, aguarde um ciclo e confira mapa/indicadores.
7. Cadastre/agende preventiva pela Sprint 03: equipamento deve continuar operacional, com destaque se a
   data estiver dentro de sete dias. Inicie/conclua uma intervenção e confira mudanças e histórico.
8. Com alertas reais abertos, confira prioridade Critical/High, horário e referência. Abra o equipamento
   pelo alerta. Resolver pelo endpoint existente ou concluir manutenção deve refletir no próximo ciclo.
9. Consulte Atenção por bairro e selecione um bairro para filtrar o mapa. Diferencie previsão de ordem
   real e alertas desconhecidos de zero quando houver falha de fonte.
10. Interrompa a conexão e aguarde a sincronização falhar. Os dados anteriores/horário devem permanecer
    com aviso. Restaure a conexão e aguarde recuperação automática. Navegue à gestão e confirme ausência
    de polling da Central enquanto estiver fora dela.
11. Como CentralOperator, confirme consulta da Central sem menu/GETs de ordens. Técnico continua restrito
    a inventário/manutenção e Agent não entra na Central. Chamadas diretas não autorizadas devem retornar 403.
12. Confira teclado, rolagem interna de listas/tabela e larguras de celular/desktop. Para revisar lazy loading,
    bloqueie temporariamente o chunk de uma página no navegador, navegue até ela, restaure a conexão e use
    Recarregar página. Nenhuma gravação é feita automaticamente nessa recuperação.
