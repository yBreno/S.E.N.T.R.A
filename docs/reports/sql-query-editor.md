# SQL para Azure Portal Query Editor

Base: `65e4bac`. Repositório inicialmente limpo. Alteração restrita ao SQL entregue
e documentação; nenhuma migration, modelo EF, lógica da aplicação ou configuração
de CI/CD foi alterada. Nenhuma conexão ao Azure foi realizada.

## Entrega

- `docs/sql/20261024234039-recuperacao-senha-email.sql`: um único lote T-SQL, sem
  SQLCMD/GO, conferência das cinco migrations anteriores e transação para as duas
  pendentes, preservando sua ordem. `TRY/CATCH`, `XACT_ABORT`, `sp_executesql`, rollback
  e histórico gravado somente após o DDL/verificação correspondente.
- `docs/sql/20261024234039-conferencia-query-editor.sql`: leitura independente antes
  e depois; resultado único com migrations/metadados/contagem, sem dados pessoais.
- `docs/sql/README-query-editor.md`: instruções de cópia/execução/conferência,
  tratamento de erros e publicação ordenada.
- README, índice de documentação e relatório de recuperação atualizados para
  apontar o procedimento atual. O SQL antigo exclusivo da Etapa 03 foi preservado.

Não há DELETE, UPDATE, DROP, criação de usuário, promoção de cargo ou alteração de
hash no SQL entregue. Os defaults das novas colunas são os das migrations oficiais.
Repetição valida o schema já existente e preserva seus valores, histórico e tokens.
Recusa drift identificado; não corrige/adota schema parcial silenciosamente.

## Testes executados

**22 cenários aprovados**, com SQL Server LocalDB e dados sintéticos. Os arquivos
principais foram enviados por SqlCommand como lote único, sem separar GO ou usar
processamento SQLCMD. As validações finais utilizaram sete bancos novos; um ensaio
auxiliar anterior também criou três fixtures novas, preservadas. Nenhum banco
preexistente do usuário foi usado ou alterado; não houve remoção de bancos.

- Baseline com cinco migrations: aplicação das duas novas, sete registros no
  histórico e 49 linhas da conferência com resultado esperado.
- Repetição sem mudanças em usuários/hash/perfil, endereços, auditoria, tokens e
  contadores. As comparações de dados foram feitas em memória, sem imprimir valores.
- Baseline com seis migrations: aplica só recuperação e preserva conta inativa,
  AuthVersion 8 e Revision 11, sem reativação/reset incidental.
- Ausência de **cada uma das cinco** migrations anteriores: recusa sem DDL/histórico novo.
- Histórico desconhecido ou recuperação registrada sem administração: recusa.
- Falhas deliberadas ao criar UserAuditEvents e PasswordResetTokens: rollback,
  incluindo colunas adicionadas antes da falha e registros de histórico pendentes.
- Falha deliberada no INSERT do histórico da recuperação: rollback de ambas.
- Erro de compilação tardio injetado em uma cópia **somente em memória**: rollback.
- Schema parcial sem histórico, histórico administrativo sem schema, índice de
  token sem unicidade, FK/check desabilitados: recusa, sem falsa idempotência.
- Ausência da tabela de histórico: recusa sem criação automática da baseline.
- Duas execuções integrais concorrentes: serialização; uma aplica, outra confere.
- Verificação de `@@TRANCOUNT` e `XACT_STATE()` na **mesma conexão** após erros,
  antes do descarte da conexão: ambos zero, sem depender de rollback no Dispose.

As falhas foram provocadas exclusivamente nas fixtures novas, usando triggers e
metadados de teste. Não se adicionou mecanismo de falha ao SQL entregue.
Hashes SHA-256 dos fontes Domain/Data/migrations/snapshot, registrados antes e
comparados depois, confirmaram ausência de alterações. Não foi necessário repetir
as suítes funcionais de backend/frontend para esta alteração de SQL/documentação.

Evidências locais ignoradas: `artifacts/query-editor-sql/resultados.json`, harness
e registros de compilação. A compilação do harness terminou sem erros; o restore
restrito em rede registrou avisos NU1900 de consulta ao índice de vulnerabilidades
do NuGet. Nenhuma dependência/pacote do projeto foi alterado nesta tarefa.

## Limites da verificação

Não houve execução no editor real nem inspeção do Azure SQL. O teste verificou o
T-SQL no motor local, a atomicidade e a repetição. Identidade do destino, permissões,
backup/PITR, volume, timeout de cinco minutos e locks devem ser conferidos pelo
responsável conforme o [roteiro](../sql/README-query-editor.md).

A baseline é conferida no histórico e o schema incremental nos catálogos. Isso não
reconstitui nem audita cada objeto das cinco migrations antigas. O roteiro também
recusa migrations futuras/desconhecidas para evitar uso em outra versão do schema.

Nenhum commit, push, deploy, alteração DNS ou atualização no Azure foi executado.
