# Atualização pelo Azure Portal Query Editor

SQL preparado e validado somente em SQL Server LocalDB isolado. Nenhum acesso ao
Azure foi feito. Este roteiro descreve a execução futura pelo responsável autorizado.
Migrations, modelos EF e gates de publicação permanecem inalterados.

## 1. Conferir o destino e o estado atual — somente leitura

No Query Editor do **banco da aplicação**, conferir recurso/servidor/banco, janela
de manutenção e recuperação/backup/PITR. Não executar em master ou em outro projeto.
Suspender escritas da aplicação durante a atualização; não publicar a nova API ainda.

Executar separadamente:

```sql
SELECT DB_NAME() AS Banco,
       CONVERT(nvarchar(128), SERVERPROPERTY(N'ServerName')) AS Servidor,
       @@TRANCOUNT AS TransacoesAbertas;
```

Confirmar o destino no Portal e `TransacoesAbertas = 0`. O SQL não conhece o nome
privado do banco correto; essa conferência é responsabilidade do operador.

Abrir e copiar **todo** o arquivo
[`20261024234039-conferencia-query-editor.sql`](20261024234039-conferencia-query-editor.sql)
para uma consulta separada e executar. Essa consulta não grava nem altera dados.
Ela retorna um único conjunto de resultados, incluindo migrations, metadados e
quantidade de usuários; não exibe nomes, e-mails, senhas, hashes ou tokens.

As cinco linhas de baseline devem estar `PRESENTE`:

1. `20261018170813_InitialCreate`
2. `20261018220253_RemoveOrganization`
3. `20261019031247_AddPredictiveCity`
4. `20261023214553_AddEquipmentCategory`
5. `20261023224842_AddMaintenanceScheduling`

Estados aceitos das duas seguintes:

| Administração | Recuperação | Ação do arquivo principal |
| --- | --- | --- |
| PENDENTE | PENDENTE | Aplica as duas, na ordem |
| PRESENTE | PENDENTE | Confere a primeira e aplica a segunda |
| PRESENTE | PRESENTE | Confere o schema; não reaplica nem reinicializa dados |
| PENDENTE | PRESENTE | Recusa histórico fora de ordem |

Antes da aplicação, metadados correspondentes às migrations pendentes podem aparecer
`AUSENTE / DIVERGENTE`: isso é esperado quando os objetos ainda não existem. Se a
migration já estiver `PRESENTE`, seus objetos devem estar conferidos. Não preencher
o histórico manualmente para contornar divergências. Baseline ausente, migration
desconhecida ou schema parcial exigem revisão antes de prosseguir.

Registrar privadamente a quantidade de usuários apresentada em `Metrica` para
comparação posterior, sem exportar dados pessoais ou credenciais.

## 2. Executar a atualização — um único lote completo

Abrir
[`20261024234039-recuperacao-senha-email.sql`](20261024234039-recuperacao-senha-email.sql).
Copiar **todo o arquivo** para uma nova consulta, sem incluir esta documentação,
sem selecionar trechos e sem acrescentar GO, comandos de cliente ou BEGIN/COMMIT
externos. Executar uma vez e aguardar o resultado.

O arquivo:

- Reconfere as **cinco** migrations anteriores no servidor.
- Recusa transação prévia, histórico desconhecido/fora de ordem e objetos parciais
  sem a respectiva migration registrada. Não adota nem apaga objetos existentes.
- Usa `XACT_ABORT`, `TRY/CATCH`, `ROLLBACK` e `THROW`, sem SQLCMD/GO. O DDL é
  executado por `sp_executesql` para tratar também erros de compilação no nível interno.
- Usa bloqueio transacional compartilhado com a administração e bloqueio da tabela
  Users durante a conferência/aplicação. O limite de espera por locks é 15 segundos.
- Mantém **uma transação para as duas migrations**. A administração é aplicada e
  conferida antes da recuperação. Cada registro no histórico só é inserido após
  seu DDL e validação; ambos só se tornam permanentes no commit final.
- Confere tipos/tamanhos/nulabilidade das colunas, chaves primárias, índices e suas
  colunas/unicidade, FKs/checks habilitados e confiáveis, além da presença dos defaults.
- Não atualiza nomes, e-mails, CPF, cargos, senhas ou hashes existentes. As três
  colunas novas de Users recebem os defaults da migration oficial: ativo e versões 1.
  Se a administração já existia, estados/versões existentes são preservados.

Sucesso retorna sete linhas, em ordem. As duas últimas mostram
`APLICADA NESTA EXECUCAO` quando pendentes, ou `JA APLICADA / CONFERIDA` quando
já existentes. Conferir `UsuariosAntes = UsuariosDepois` e `TransacoesAbertas = 0`.

Se a segunda migration falhar, as alterações e o histórico **novos desta tentativa**
da primeira também são revertidos. Uma primeira migration já confirmada em execução
anterior não é removida. Não executar manualmente os INSERTs de histórico.

## 3. Conferir o resultado — somente leitura

Executar novamente o arquivo de conferência, em consulta separada.

Resultado esperado:

- Sete migrations `PRESENTE`, sem histórico fora do roteiro.
- 21 colunas, 7 posições de índices, 3 FKs, 3 checks e 3 PKs conferidos.
- Três defaults de Users `PRESENTE`.
- Quantidade de usuários igual à observada antes; transações abertas na sessão = 0.

São **49 linhas** no schema esperado. A presença dos defaults é conferida; essa
consulta não executa expressões arbitrárias de defaults/checks nem substitui uma
auditoria completa de qualquer customização manual anterior. O DDL desta entrega
reutiliza as definições das migrations oficiais, que não foram modificadas.

Somente após a conferência liberar os gates de schema já documentados no
[plano de publicação](../reports/recuperacao-senha-email.md#plano-ordenado-de-atualização-do-azure).
A sequência continua **Azure SQL → backend → frontend**. Este roteiro não publica
aplicações, configura provedor/DNS ou altera variáveis do GitHub/Azure.

## Erro, timeout ou resultado incerto

- Não continuar com fragmentos do arquivo nem tentar inserir histórico manualmente.
- Em erro tratado, a tentativa é revertida e o erro original é devolvido. Conferir
  novamente histórico/schema antes de repetir o **arquivo completo**.
- Em cancelamento, perda de conexão ou timeout, não presumir sucesso/falha apenas
  pela tela do navegador: reconectar e executar a conferência. Não manter transação
  entre cliques/abas. Se houver sessão bloqueadora/transação pendente, o responsável
  deve investigar a sessão correta; não executar rollback/kill em outras sessões
  indiscriminadamente.
- O editor tem limite de execução documentado de cinco minutos. Em banco grande ou
  com contenção, ensaiar em homologação com volume semelhante; usar ferramenta
  administrativa adequada se não couber nesse limite. Não remover as proteções.
- Falta de permissão ou visibilidade dos metadados impede a validação: utilizar
  identidade autorizada para esse banco, sem conceder permissões amplas à aplicação.

Referências oficiais:
[Query Editor](https://learn.microsoft.com/en-us/azure/azure-sql/database/query-editor),
[TRY/CATCH](https://learn.microsoft.com/en-us/sql/t-sql/language-elements/try-catch-transact-sql) e
[XACT_ABORT](https://learn.microsoft.com/en-us/sql/t-sql/statements/set-xact-abort-transact-sql).
