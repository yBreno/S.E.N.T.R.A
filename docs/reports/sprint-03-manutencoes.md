# Sprint 03 — Gestão de manutenções

Revisão de 23/10/2026. Complementa as Sprints 01 e 02. Não houve alteração em Azure/deploy,
acesso a produção, commit ou push. Migrations foram executadas exclusivamente em bancos novos
de teste no SQL Server LocalDB. A aplicação normal não ganhou execução automática de migrations.

## 1. Diagnóstico inicial

Foram examinados MaintenanceRecord, MaintenanceService, DTOs/validators/controller, repositórios,
matriz de estados, planejamento no inventário, RecommendationService/SendMaintenance,
PredictiveAnalysisService, políticas de autorização, testes e componentes das sprints anteriores.

O fluxo existente atendia à corretiva: abrir exigia Malfunctioning, passava para UnderMaintenance;
fechar restaurava Operational, calculava periodicidade e resolvia alertas. Existiam apenas Open e
Closed. Aceitar SendMaintenance alterava MaintenanceNeed/NextMaintenanceDate, mas não criava uma ordem.
Não havia tela de manutenção e a navegação estava desabilitada. Era impossível representar uma
preventiva operacional sem simular uma falha ou confundir previsão com intervenção.

## 2. Estado da entrega

| Funcionalidade | Estado | Comportamento atual |
| --- | --- | --- |
| Navegação e permissões | Completa | Sidebar, acesso direto autorizado e controles compatíveis com a API |
| Listagem, busca, filtros e paginação | Completa | Equipamento, estado da ordem, natureza e necessidade atual; filtros/páginas no cliente |
| Detalhes e histórico por equipamento | Completa | Identificadores, descrição, natureza, urgência, previsão, início, fim, custo e periodicidade; sem exclusão |
| Agendamento preventivo | Completa | Cria ordem Scheduled sem mudar equipamento Operational e sem criar alerta de falha |
| Início efetivo | Completa | Confirmação explícita, início real agora, ordem Open e equipamento UnderMaintenance |
| Corretiva | Completa | Abertura imediata exige falha real; permanece integrada à resolução dos alertas |
| Conclusão | Completa | Closed, equipamento Operational, alertas resolvidos e próxima previsão por periodicidade |
| Integração com equipamentos | Completa | Histórico contextual, retorno aos detalhes atualizados e proteção do planejamento associado à ordem |
| Feedback | Completa | Carregamento, vazio, erro, sucesso, formulário preservado no erro, abort e bloqueio de envio repetido |
| Aceite SendMaintenance | Completa para equipamento identificado | Cria ordem e aceita recomendação na mesma unidade de trabalho; repetição idempotente |
| Recomendação regional sem equipamento | Parcial | Rejeitada com orientação de triagem; seleção de equipamento em uma futura interface não implementada |
| Agente vinculado | Parcial | Campo existente preservado na API e exibido pelo identificador; sem nova atribuição de equipe na tela |
| Cancelar, reagendar, auditoria completa | Pendente / evolução | Nenhum endpoint ou exclusão artificial acrescentado |
| Interface da Cidade Preditiva e gestão de alertas/ocorrências | Fora do escopo | Não implementadas nesta sprint |

## 3. Regras e decisões técnicas

1. Foram preservados MaintenanceRecord, DTOs de abertura/conclusão, endpoints existentes, oito tipos
   de equipamento e arquitetura Domain/Application/Infrastructure/Api. Novos casos de uso reutilizam
   os mesmos repositórios e unidade de trabalho. Não foram adicionadas subclasses nem dependências.
2. Scheduled foi acrescentado após os valores existentes do enum. Kind diferencia Corrective e
   Preventive; registros antigos recebem Corrective. ScheduledDate representa a previsão;
   InitDate é nulo até iniciar, evitando inventar uma data de intervenção. Datas da resposta recebem
   identificação UTC também quando vêm do SQL Server.
3. Agendamento preventivo exige Operational; agendamento corretivo pela API exige Malfunctioning.
   A interface oferece agendamento preventivo e corretiva imediata. Iniciar pode antecipar a previsão;
   sempre grava o horário real. Datas efetivas futuras são recusadas. Periodicidade aceita 1–36500 dias.
4. A matriz permite Operational → UnderMaintenance para intervenção preventiva. O PATCH genérico
   de equipamento continua proibindo a entrada direta em UnderMaintenance: exige uma ordem.
5. Só pode existir uma ordem Scheduled/Open por equipamento. A aplicação verifica conflitos e
   intervalos históricos; índice único filtrado no SQL Server protege inclusive solicitações concorrentes.
   A conclusão repetida, início repetido e conclusão antes do início são recusados.
6. Uma ordem pendente protege a necessidade/data no inventário. Alterações cadastrais continuam
   possíveis. O formulário omite planejamento intocado para não arredondar segundos nem sobrescrever
   previsões controladas pela ordem. Durante Open, mudanças manuais de situação são bloqueadas.
   Durante Scheduled, uma falha real ainda pode ser registrada; outras mudanças de situação são bloqueadas.
7. Se ocorrer falha após agendamento preventivo, iniciar transforma a mesma ordem em Corrective,
   mantendo ID, descrição, previsão e eventual recomendação de origem. Não cria outra ordem nem falha fictícia.
   Não foi criada trilha separada de cada alteração de natureza.
8. Fechar resolve os alertas abertos do equipamento, preservando a regra existente. Com periodicidade,
   define MaintenanceNeed.Periodic e a próxima data; sem ela, None/data nula. A próxima previsão é
   metadado: não há criação automática de ordens recorrentes.
9. Aceitar SendMaintenance prepara a ordem sem salvar separadamente; ordem, metadados e aceitação
   são persistidos em um único SaveChanges. RecommendationId opcional possui FK e índice único.
   Falta de equipamento ou ordem já pendente impede a aceitação e mantém recomendação Pending.
   Predições/anomalias não sugerem novamente um equipamento com ordem pendente; o filtro Pending
   existente continua evitando sugestões repetidas na execução sequencial.
10. Recomendações já aceitas e metadados antigos não são convertidos retroativamente. Repetir aceite
    de uma recomendação Accepted retorna seu estado; não dispara trabalho retroativo. Recomendações
    regionais sem EquipmentId exigem triagem; não se escolhe um equipamento arbitrariamente.
11. A interface reutiliza InventoryField, InventoryDetails, CSS do inventário, transporte HTTP e
    sessão existentes. Formulários têm labels, mensagens, fieldset, confirmação e foco na área de trabalho.
    Nenhuma biblioteca visual, dado operacional fictício ou novo design system foi acrescentado.

## 4. Migration e persistência

`20261023224842_AddMaintenanceScheduling` adiciona Kind (default Corrective), ScheduledDate e
RecommendationId; flexibiliza InitDate para nulo; acrescenta FK e índices únicos filtrados.
O snapshot foi atualizado. Não remove nem reclassifica registros antigos.

Antes de alterar o schema, uma verificação SQL interrompe a migration se houver múltiplas ordens
pendentes por equipamento. É necessário revisar esses dados explicitamente; a migration não elimina
nem encerra ordens para fazer o índice passar. O caminho Down gerado não foi executado e não deve ser
usado para retroceder uma base com agendamentos sem planejamento específico de preservação dos dados.
Backend e frontend devem ser atualizados juntos; um cliente anterior que enumera estados precisa
reconhecer Scheduled e InitDate nulo nas novas ordens.

## 5. Endpoints

| Endpoint | Uso |
| --- | --- |
| GET `/api/maintenances?status=&equipmentId=` | Listagem filtrada e histórico; tela também combina filtros localmente |
| GET `/api/maintenances/{id}` | Detalhes atualizados de uma ordem |
| POST `/api/maintenances` | Abertura corretiva existente: equipamento com falha, início imediato |
| POST `/api/maintenances/schedule` | Novo: EquipmentId, Description, Urgency, ScheduledDate, Kind e PeriodicityDays opcional |
| POST `/api/maintenances/{id}/start` | Novo: inicia ordem agendada; corpo vazio |
| POST `/api/maintenances/{id}/close` | Existente: conclusão; EndDate/Cost opcionais |
| GET `/api/equipments`, GET `/api/equipments/{id}` | Seleção, filtros, planejamento e retorno ao inventário |
| PUT `/api/equipments/{id}`, PATCH `/api/equipments/{id}/status` | Mantidos; protegem planejamento/situação ligados à ordem |
| GET `/api/addresses/{id}` | Endereço completo no retorno aos detalhes do equipamento |
| POST `/api/recommendations/{id}/accept` | Ajustado no backend; não foi criada interface preditiva |

A tela envia horários em UTC; a apresentação usa o fuso do navegador. AgentId opcional continua
aceito na abertura/agendamento para clientes da API e é validado contra agentes existentes.

## 6. Autorizações

| Papel | Listar/detalhar manutenção | Agendar/abrir/iniciar/concluir | Aceitar recomendação |
| --- | --- | --- | --- |
| Admin / OperationManager | Sim | Sim | Sim |
| MaintenanceTechnician | Sim | Sim | Não |
| CentralOperator | Não | Não | Não |
| Agent | Não | Não | Não |
| Sem autenticação | HTTP 401 | HTTP 401 | HTTP 401 |

A política Roles.Maintenance do controller foi preservada. Gestores já tinham esses direitos;
o técnico não ganhou gestão de agentes, cadastro de equipamentos ou aceite de recomendações.
Operador continua podendo registrar falha real pela política existente de equipamentos, sem
receber leitura ou operação de ordens. Interface oculta os controles e a API mantém a proteção.

## 7. Arquivos criados e alterados nesta sprint

O workspace também contém alterações anteriores das Sprints 01/02; a relação abaixo identifica
a contribuição da Sprint 03, inclusive em arquivos que já estavam novos ou modificados.

**Backend — alterados:**

- `backend/Api/Controllers/MaintenancesController.cs`
- `backend/Application/DTOs/Maintenance/MaintenanceDtos.cs`
- `backend/Application/Interfaces/IServices/IServices.cs`
- `backend/Application/Mappings/Mappings.cs`
- `backend/Application/Services/MaintenanceService.cs`
- `backend/Application/Services/EquipmentService.cs`
- `backend/Application/Services/RecommendationService.cs`
- `backend/Application/Validators/MaintenanceValidators.cs`
- `backend/Domain/Entities/MaintenanceRecord.cs`
- `backend/Domain/Enums/MaintenanceStatus.cs`
- `backend/Domain/Rules/EquipmentStatusRules.cs`
- `backend/Infrastructure/Data/Configurations/MaintenanceRecordConfiguration.cs`
- `backend/Infrastructure/Repositories/MaintenanceRecordRepository.cs`
- `backend/Infrastructure/Data/Migrations/AppDbContextModelSnapshot.cs`
- `backend/Tests/RulesTests.cs`
- `backend/Tests/PredictiveCityTests.cs`
- `backend/Tests/Support/EquipmentTestHost.cs` — opção explicitamente restrita a banco LocalDB de teste.

**Backend — novos:**

- `backend/Infrastructure/Data/Migrations/20261023224842_AddMaintenanceScheduling.cs`
- `backend/Infrastructure/Data/Migrations/20261023224842_AddMaintenanceScheduling.Designer.cs`
- `backend/Tests/MaintenanceHttpTests.cs`

**Frontend — alterados:**

- `frontend/src/components/dashboard/DashboardSidebar.jsx`
- `frontend/src/components/equipment/EquipmentDetails.jsx`
- `frontend/src/components/equipment/EquipmentForm.jsx`
- `frontend/src/components/equipment/equipmentModel.js`
- `frontend/src/pages/DashboardPage.jsx`
- `frontend/src/pages/EquipmentPage.jsx`
- `frontend/src/services/auth.js`
- `frontend/tests/equipments.test.js`

**Frontend — novos:**

- `frontend/src/pages/MaintenancePage.jsx`
- `frontend/src/components/inventory/MaintenanceForm.jsx`
- `frontend/src/services/maintenances.js`
- `frontend/tests/maintenances.test.js`

**Entrega:** `README.md` atualizado e este relatório criado. Scripts, perfis temporários e imagens
de verificação ficam no diretório local ignorado `artifacts/sprint-03/`, sem integrar a aplicação.

## 8. Verificações executadas

| Verificação | Resultado |
| --- | --- |
| `dotnet build backend/Solution/Solution.slnx --no-restore` | Aprovado: 0 erros, 0 avisos na execução final |
| `dotnet test backend/Tests/Tests.csproj --no-restore` | 186 aprovados, 0 falhas, 0 ignorados |
| `node --test frontend/tests/*.test.js` | 134 aprovados, 0 falhas |
| `npm.cmd --prefix frontend run lint` | Aprovado |
| `npm.cmd --prefix frontend run build` | Aprovado; aviso de bundle principal de aproximadamente 510 kB |
| `git diff --check` | Aprovado |
| Snapshot EF/modelo SQL | Consistentes |

Novos testes cobrem ciclo preventivo pelos três perfis autorizados, corretiva, alerta resolvido,
datas/custo/periodicidade inválidos, duplicidade, estados inválidos de ordem, início/conclusão repetidos,
proteção do planejamento, edição de identificação, falha após agendamento, histórico, filtros HTTP,
aceite idempotente, aceitação sem equipamento, conflitos sem aceitar indevidamente a recomendação,
supressão de sugestão com ordem pendente e restrições de roles. No frontend foram conferidos filtros,
rotas, token, abort, bloqueio antes do transporte, respostas incompatíveis, erros e precisão da previsão.
Os testes existentes foram preservados, ajustando a matriz e o uso nullable de InitDate onde necessário.

**SQL Server LocalDB real:** um banco novo com prefixo `SENTRA_Sprint03_` recebeu as cinco migrations.
Uma verificação adicional criou a estrutura anterior em outro banco exclusivo, inseriu uma ordem legada,
aplicou a migration nova e confirmou preservação do estado/data e Kind.Corrective. Pelos controllers
reais, concluiu a ordem antiga, aceitou/repetiu uma recomendação, verificou vínculo/estado operacional,
recusou inserção duplicada pelo índice SQL e executou início/conclusão da nova preventiva. Resultado: aprovado.
Não houve execução de seeders/worker da API normal nem leitura de conexão privada.

**Chrome + API real + LocalDB:** a execução final observou 125 respostas da API, sem exceções JavaScript
não tratadas. Foram testados preventiva operacional completa, corretiva por técnico, custo, periodicidade,
histórico contextual, retorno ao equipamento, edição preservando a previsão, filtros, indisponibilidade
de rede/recuperação, operador sem menu e HTTP 403, técnico sem agentes e agente impedido de entrar no inventário.
Duas requisições simultâneas ao agendamento retornaram 201/409, deixando uma única ordem pendente.
Layout conferido em 320/390/768/1440 px, sem transbordamento horizontal da página; tabelas podem rolar internamente.
As capturas desktop/mobile foram inspecionadas visualmente.

Evidências locais: `artifacts/sprint-03/browser-results.json`, `maintenance-scheduled-desktop.png`,
`maintenance-closed-desktop.png` e `maintenance-form-mobile.png`. Os helpers BrowserHost/SqlReview
nesse diretório usam apenas destinos LocalDB novos com prefixo validado e dados sintéticos de teste.
Serviços temporários foram encerrados ao final; bancos de teste foram preservados, sem exclusão automática.

## 9. Limitações e pendências

- Não há cancelamento, reagendamento, anexos, atribuição de equipe na tela nem auditoria separada de cada transição.
- A regra conservadora admite uma única ordem pendente por equipamento, sem agenda de múltiplas visitas futuras.
- Não há recorrência automática: periodicidade apenas calcula a próxima previsão.
- A deduplicação sequencial de recomendações foi mantida e ampliada; não foi acrescentada coordenação distribuída
  entre múltiplas instâncias do motor. O índice de ordens protege o efeito operacional duplicado no SQL Server.
- Não há controle geral por versão de edição/encerramento simultâneo; não se afirma concorrência plena de todo
  o inventário. Paginação e filtros são locais e a atualização continua manual, sem polling.
- Registros legados incoerentes exigem revisão antes da migration. A verificação não homologa produção, Azure,
  celulares físicos ou instalação sobre todas as variações de dados de terceiros.
- Novos dados sintéticos foram usados exclusivamente nos bancos de teste, sem mocks operacionais na aplicação.

## 10. Roteiro objetivo no navegador

1. Siga **Execução local** no README com banco isolado, migrations atualizadas, JWT e conta local autorizada.
   Desative simulação/seeders conforme o exemplo. Não use o ambiente HTTP Demo para validar gravações.
2. Entre como Admin ou OperationManager e abra **Equipamentos**. Cadastre um equipamento válido e passe-o
   de Não instalado para Operacional. Seus detalhes devem mostrar necessidade e próxima previsão.
3. Escolha **Manutenções e histórico deste equipamento**. A lista deve estar filtrada pelo equipamento;
   uma previsão antiga sem ordem não deve aparecer como ordem real.
4. Escolha **Agendar preventiva**, informe descrição, urgência, data futura e, opcionalmente, 30 dias.
   Confirme. A ordem deve estar Agendada, sem início efetivo, e o equipamento deve continuar Operacional.
5. Nos detalhes, confirme o início e escolha **Iniciar intervenção**. Confira Em andamento e o equipamento
   Em manutenção. Concluir antes de iniciar ou abrir outra ordem pendente deve ser recusado pela API.
6. Escolha **Concluir manutenção**. Deixe data vazia para usar agora e informe custo opcional. Confira
   Concluída, custo, fim e equipamento Operacional; com periodicidade, a próxima previsão deve ser calculada.
7. Use **Ver equipamento**, confira os dados atualizados e edite apenas marca/modelo. A previsão deve permanecer
   idêntica. Volte ao histórico e confira que a ordem concluída continua disponível.
8. Em outro equipamento operacional, registre uma falha real pelo inventário. Como MaintenanceTechnician,
   abra **Manutenções → Abrir corretiva**, selecione esse equipamento, informe o reparo e conclua-o depois.
   Não deve haver necessidade de cadastrar/editar equipamento como técnico.
9. Confira busca, estado, natureza e necessidade atual. Esta última representa o equipamento hoje, e não
   uma cópia histórica da necessidade no dia da ordem. Teste ausência de resultados e Limpar filtros.
10. Desconecte/reconecte a rede e atualize a lista. Após resultado de gravação incerto, consulte o histórico
    antes de repetir. Confira teclado e larguras de celular/desktop.
11. Entre como CentralOperator: Manutenções não deve aparecer; chamadas diretas recebem 403. Agent não
    recebe acesso ao inventário geral. Sem token, a API deve responder 401.
12. Para revisar SendMaintenance, use uma recomendação Pending de equipamento existente no banco local e
    aceite pela API como gestor. Confira uma ordem vinculada, estado operacional preservado na preventiva
    e repetição sem duplicidade. Recomendação regional sem equipamento deve continuar Pending após rejeição.
