# Diagnóstico e correção dos HTTP 500 no Azure

Data da análise: 2026-10-23

## 1. Conclusão

Não foi possível comprovar, a partir deste workspace, uma causa exclusiva dos
HTTP 500 no App Service `sentra-api`. O SQL Server usado pela execução local
tem as cinco migrations aplicadas, mas essa verificação não identifica o banco
configurado no Azure.

As causas mais prováveis permanecem:

1. **Schema do Azure divergente do artefato publicado**, especialmente se
   `AddEquipmentCategory` ou `AddMaintenanceScheduling` não estiverem em
   `__EFMigrationsHistory`.
2. **Dados legados incompatíveis com o modelo atual**, principalmente valores
   inválidos nos campos enum/string ou discriminador TPH `Equipments.Type`.
3. **Configuração do App Service**, caso
   `ConnectionStrings__DefaultConnection` não esteja definida, aponte para
   outro banco ou não permita conexão a partir do App Service.
4. **Falha no ramo preditivo de `/api/dashboard`**, que executa consultas
   adicionais quando `includePredictive=true`; isso explica por que
   `/api/dashboard?includePredictive=false` é um teste de isolamento importante.

Não há evidência local de incompatibilidade de contrato para os endpoints
investigados. O frontend espera arrays em `/api/equipments` e
`/api/maintenances`, e os DTOs atuais produzem esses formatos com enums JSON
como strings (`Scheduled`, `Open`, `Closed`, etc.).

## 2. Evidências técnicas

### Dashboard

`DashboardController` chama `DashboardService.GetAsync`. Com o padrão atual,
além de contagem de equipamentos e agentes, o serviço consulta risco,
recomendações, anomalias e previsões. Portanto, uma falha em qualquer uma
dessas tabelas/colunas/dados faz o endpoint completo responder 500.

O parâmetro `includePredictive=false` desabilita esse ramo e deve ser usado
para distinguir falha básica de leitura de falha preditiva.

### Equipamentos

`EquipmentRepository.SearchAsync` usa TPH e `Include(e => e.Address)`.
O discriminador `Equipments.Type` é string e só aceita os tipos mapeados nas
entidades atuais. Uma linha legada com valor diferente pode falhar durante a
materialização EF Core antes de o DTO ser criado. A consulta também pressupõe
que as linhas tenham endereço válido, conforme a FK criada pelas migrations.

### Manutenções

Os filtros `Scheduled` e `Open` são convertidos pelo model binding para
`MaintenanceStatus` e consultam a coluna string `Status`. A migration final
também exige as colunas `Kind`, `RecommendationId` e `ScheduledDate`, além do
índice filtrado de ordens pendentes. Se o Azure estiver na migration anterior,
esses GETs podem falhar por coluna inexistente.

### Migrations e SQL Server

A execução local de `dotnet ef migrations list` retornou:

- `20261018170813_InitialCreate`
- `20261018220253_RemoveOrganization`
- `20261019031247_AddPredictiveCity`
- `20261023214553_AddEquipmentCategory`
- `20261023224842_AddMaintenanceScheduling`

O comando realizou consultas a `__EFMigrationsHistory` e não reportou
migrations pendentes. Isso comprova somente o banco local configurado nessa
execução; não houve acesso ao segredo nem uma leitura do banco Azure.

A migration `AddMaintenanceScheduling` contém uma verificação não destrutiva
que interrompe a aplicação se houver ordens `Open`/`Scheduled` duplicadas por
equipamento. Ela não apaga nem corrige dados antigos.

### Dados DEMO

Os carregadores DEMO exigem ambiente e destino explicitamente autorizados e
recusam schema pendente; não são executados no startup normal da API. Não foi
encontrado no código um fallback que limpe ou migre silenciosamente dados
antigos.

## 3. Alteração implementada

Arquivo alterado:

- `backend/Api/Middleware/ExceptionHandlingMiddleware.cs`

Para exceções que resultam em 500, o middleware agora grava log estruturado
com `TraceId`, método HTTP e rota. Não são registrados headers, query string,
corpo da requisição, connection string, tokens, CPF ou dados de entidade.
O corpo HTTP continua sendo um `ProblemDetails` genérico e não expõe detalhes
da exceção.

Essa alteração é diagnóstica e não altera o status nem o contrato das
respostas. Não foi aplicada uma correção de schema ou dados sem confirmação do
banco Azure.

## 4. Testes executados

- `dotnet build backend\Api\Api.csproj --configuration Release` — passou,
  0 avisos e 0 erros.
- `dotnet test backend\Tests\Tests.csproj --configuration Release --no-restore`
-  — o projeto compilou; a execução final do assembly com VSTest confirmou
  120 testes aprovados, 0 falhas e 0 ignorados.
- `dotnet ef migrations list --project backend\Infrastructure\Infrastructure.csproj
  --startup-project backend\Api\Api.csproj --no-build` — cinco migrations
  presentes e nenhuma pendência no banco local consultado.
- `Set-Location frontend; npm run lint` — passou.
- `Set-Location frontend; npm run build` — passou.
- `npm test -- --runInBand` na raiz — não aplicável: o projeto frontend não
  possui `package.json` na raiz nem script `test`.

Os testes HTTP existentes cobrem dashboard, equipamentos e manutenções em
provider de teste; não reproduzem incompatibilidade de schema/dados de um
Azure SQL real.

## 5. Pendências

Ainda é necessário, por operador com acesso ao Azure, confirmar:

1. qual commit/artefato está em execução no App Service;
2. se `ConnectionStrings__DefaultConnection` existe no App Service e aponta
   para o banco esperado, sem copiar ou revelar o valor;
3. resultado de `GetPendingMigrationsAsync` no banco Azure;
4. existência e tipos das colunas `Equipments.Category`,
   `MaintenanceRecords.Kind`, `MaintenanceRecords.RecommendationId` e
   `MaintenanceRecords.ScheduledDate`;
5. valores distintos de `Equipments.Type`, `Equipments.Status`,
   `MaintenanceRecords.Status` e `MaintenanceRecords.Kind`, reportando apenas
   contagens e valores, nunca dados pessoais;
6. logs estruturados do novo evento para obter o `TraceId` e a exceção real.

## 6. Procedimento seguro de validação no Azure

1. Publicar esta alteração pelo fluxo normal, sem alterar secrets.
2. Reiniciar apenas o App Service se necessário e confirmar o commit publicado.
3. Observar o Application Insights/diagnóstico estruturado pelo evento de
   erro, filtrando `TraceId`, `Method` e `Path`. Não usar ou registrar a
   connection string.
4. Executar com um token autorizado, nesta ordem:
   - `GET /api/equipments`;
   - `GET /api/maintenances?status=Scheduled`;
   - `GET /api/maintenances?status=Open`;
   - `GET /api/dashboard?includePredictive=false`;
   - `GET /api/dashboard`.
5. Consultar `__EFMigrationsHistory` e os metadados do schema em modo
   somente leitura. Se houver migration pendente, aplicar o procedimento de
   change management já aprovado; não executar `EnsureCreated`, não recriar
   migrations e não apagar tabelas.
6. Se apenas o dashboard completo falhar, investigar primeiro as tabelas
   preditivas. Se equipamentos falharem, investigar primeiro o discriminador
   TPH e a FK/endereço. Se manutenções filtradas falharem, comparar as
   colunas e valores enum string com a migration final.
7. Repetir os cinco GETs e confirmar respostas 2xx e o shape esperado pelo
   frontend. Remover ou reduzir a verbosidade do diagnóstico somente por
   mudança deliberada e registrada.
