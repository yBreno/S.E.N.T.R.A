# S.E.N.T.R.A. — Etapa 7.3: validação relacional autorizada

## Destino e autorização

Execução explicitamente autorizada pelo usuário exclusivamente em `SENTRA_DEMO_Stage73`,
instância `(localdb)\MSSQLLocalDB`. Conexão Windows integrada dedicada, fornecida apenas
ao processo por `SENTRA_DEMO_CONNECTION_STRING`; não foi utilizada a conexão da API.
Nenhum segredo ou connection string de execução é armazenado neste documento.

A instância inicialmente parada foi iniciada. Consulta inicial confirmou `IsLocalDB = 1`,
versão `17.0.4025.3` e ausência de `SENTRA_DEMO_Stage73`. O executor conferiu novamente a
ausência antes de criar o banco. Se já existisse, a criação/migração teria sido recusada.

Não houve acesso ao Azure, inicialização da API, seeders, worker, criação de usuários,
alteração de autenticação/permissões ou escrita em dados de outros bancos. Criar o banco
necessariamente registra seus metadados de criação na instância; nenhum banco existente
foi selecionado como destino de carga ou migração.

## Executor isolado

Novos arquivos:

- `backend/DemoData.Validation/DemoData.Validation.csproj`
- `backend/DemoData.Validation/Program.cs`
- Este relatório.

Modificado: `backend/Solution/Solution.slnx` para incluir o executor na compilação.
O projeto referencia somente Infrastructure; não referencia nem inicializa a API.
As proteções existentes de DemoData permanecem inalteradas.

Modos manuais, sempre com ambiente `Demo` e a conexão dedicada definida externamente:

```powershell
# Exclusivamente para a criação inicial autorizada. Agora recusará, pois o banco já existe.
dotnet run --project backend/DemoData.Validation -- --prepare-new --confirm SENTRA_DEMO_Stage73

# Somente leitura: contagens e fingerprint dos campos persistidos.
dotnet run --project backend/DemoData.Validation -- --inspect

# Validação com inserções temporárias e rollback: requer autorização para esses testes.
dotnet run --project backend/DemoData.Validation -- --verify --expected-fingerprint <fingerprint-da-inspecao> --confirm SENTRA_DEMO_Stage73
```

O executor exige exatamente `SENTRA_DEMO_Stage73`, a conexão LocalDB validada pelo guard
existente e confirmação do destino pelo próprio SQL Server (`DB_NAME()` e `IsLocalDB`).
O modo de preparação exige as três migrations abaixo, rejeita banco existente e não
possui modo para retomada automática de migrations em outro banco. Não há execução automática.

## Migrations efetivamente aplicadas

1. `20261018170813_InitialCreate`
2. `20261018220253_RemoveOrganization`
3. `20261019031247_AddPredictiveCity`

Histórico SQL confirmado com EF Core `10.0.13`. A remoção da estrutura `Organizations`
ocorreu apenas no banco recém-criado e vazio, conforme autorização. Nenhuma migration
foi criada, alterada ou aplicada em outro destino. As 12 tabelas de aplicação estavam
vazias após a preparação e antes da primeira carga.

Houve um erro `InvalidCastException` no executor ao representar o resultado JSON das tabelas
vazias, depois da conclusão das migrations. Corrigido o tratamento do resultado vazio.
Uma consulta independente pelo sqlcmd confirmou o destino e as três migrations antes de
continuar. O banco não foi recriado, nenhuma migration foi repetida para corrigir esse erro
e não houve falha SQL nas migrations.

## Persistência e idempotência reais

A CLI existente `backend/DemoData` foi executada manualmente em dois processos separados,
sempre com `--apply --database SENTRA_DEMO_Stage73 --confirm DEMO` e ambiente Demo.

| Tabela | Primeira execução | Segunda execução | Total persistido |
| --- | ---: | ---: | ---: |
| Addresses | 60 | 0 | 60 |
| Equipments | 60 | 0 | 60 |
| Agents | 20 | 0 | 20 |
| Incidents | 100 | 0 | 100 |
| Alerts | 15 | 0 | 15 |
| MaintenanceRecords | 10 | 0 | 10 |
| **Total** | **265** | **0** | **265** |

Users, TelemetryLogs, Recommendations, AgentPositionLogs, Suppliers e OperationalBases
continuaram com zero registros.

Fingerprint SHA-256 das linhas de todas as tabelas de aplicação e do histórico de migrations,
incluindo todos os campos persistidos, ordenado por chave:

`E2FD78E378604958E760670567A412379C2F7279D8E5F7CD0335EE5F4355A6B3`

Esse valor permaneceu igual antes e depois da segunda execução e depois dos testes de
constraints e rollback. Não foram sobrescritos status, datas ou quaisquer campos da carga.

## Constraints e transações reais

- Nenhuma FK desabilitada ou não confiável.
- `DBCC CHECKCONSTRAINTS WITH ALL_CONSTRAINTS`: nenhuma violação encontrada.
- Equipamentos UnderMaintenance possuem a manutenção aberta correspondente.
- Os sete testes abaixo tentaram inserir registros temporários em transações separadas.
  Cada transação foi revertida; mensagens completas do provider não são impressas para
  evitar exibição de documentos sintéticos ou outros valores.

| Tentativa inválida | Resultado SQL esperado e observado |
| --- | --- |
| Serial duplicado | 2601, índice único |
| Placa duplicada | 2601, índice único |
| Matrícula duplicada | 2601, índice único |
| Documento sintético duplicado | 2601, índice único |
| Ocorrência vinculada a equipamento inexistente | 547, FK |
| Endereço sem Street | 515, NOT NULL |
| Alerta com mensagem de 501 caracteres | 2628, tamanho máximo |

Teste adicional: um endereço temporário válido foi inserido e confirmado dentro de uma
transação; após rollback, uma consulta independente não o encontrou. O fingerprint de toda
a base de aplicação confirmou ausência de resíduos. Nenhum registro original foi atualizado
ou excluído. A carga utiliza sua transação Serializable existente; não foram feitos testes
de disputa concorrente entre cargas.

## Serviços existentes contra SQL Server

Verificação concluída em **20/10/2026 às 20:24:05, UTC−03:00** (`2026-10-20T23:24:05Z`).
Serviços e repositórios existentes utilizados diretamente, com AppDbContext SQL Server;
não são chamadas HTTP e não são resultados de InMemory.

| Consulta | Resultado real | Coordenadas válidas |
| --- | ---: | ---: |
| EquipmentService.SearchAsync sem filtros | 60 equipamentos | 60 |
| AgentService.GetAllAsync | 20 agentes | 20 |
| Heatmap Incidents, período padrão | 100 pontos | 100 |
| Heatmap Failures, período padrão | 25 pontos | 25 |
| Heatmap Risk | 5 pontos | 5 |

Os pontos possuem intensidade finita, maior que zero e até 1. Os agentes não têm UserId.
Indicadores auxiliares confirmados: 13 agentes ativos; dez equipamentos em cada um dos
seis estados; disponibilidade de 16,67% pelos totais consultados.

| Bairro | Score calculado | Nível |
| --- | ---: | --- |
| Vila Mariana | 79 | Critical |
| Pinheiros | 79 | Critical |
| Mooca | 78 | Critical |
| Bela Vista | 78 | Critical |
| Centro | 78 | Critical |

Scores representam o horário da consulta e as opções padrão do backend, não valores fixos
para futuras execuções. Incidents/Failures podem expirar após a janela temporal padrão;
repetir a carga não renova datas nem duplica registros.

## Pendências

Build final da solução: aprovado, sem warnings ou erros. Suíte backend existente:
72 testes aprovados, zero falhas e zero ignorados. As verificações SQL relatadas acima
foram executadas adicionalmente pelo executor manual, não são testes InMemory contabilizados
como se fossem SQL Server.

HTTP autenticado, CORS e frontend não foram testados nesta etapa, pois a API não foi iniciada.
A próxima etapa depende de aprovação para uma execução local isolada da API, com revisão
explícita de ambiente, seeders e `Simulation:Enabled`, e uso de uma conta de monitoramento
autorizada. Não deve haver criação automática de conta ou uso de credenciais/token do Azure.
Nenhuma configuração da API ou do frontend foi alterada para apontar para este banco.
