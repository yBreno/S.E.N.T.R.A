# Documentação do S.E.N.T.R.A.

O [README principal](../README.md) descreve arquitetura, instalação, módulos e
limitações. O [guia do frontend](../frontend/README.md) resume a execução da interface.
As dependências reproduzíveis estão nos projetos .NET e no lockfile do frontend.

## Entrega atual — Sprint 08

- [Instalação e configuração privada](instalacao.md): banco local novo, primeiro Admin e execução.
- [Arquitetura, permissões e limites](arquitetura.md): camadas, contratos, UTC e concorrência.
- [Validação final](reports/sprint-08-validacao-final.md): requisitos das três partes,
  correções, testes, revisão de produção somente leitura e ressalvas.

## Administração de Usuários

- [Relatório e procedimento de atualização](reports/administracao-usuarios.md):
  permissões, endpoints, provisionamento, sessões, testes e ordem schema → API → frontend.
- [SQL incremental revisado](sql/20261024223405-administracao-usuarios.sql): aplicação
  manual com parada ao primeiro erro, somente após conferência do destino e baseline.

## Recuperação de senha por e-mail

- [Fluxo, configuração privada, DNS, testes e publicação](reports/recuperacao-senha-email.md).
- [SQL incremental de duas migrations](sql/20261024234039-recuperacao-senha-email.sql),
  para uma baseline antiga de AddMaintenanceScheduling até AddPasswordRecovery.
  O responsável informou as sete migrations já aplicadas em produção; não reaplicar
  este incremento apenas para publicar a Sprint 08.
- [Roteiro para Query Editor e consulta de conferência](sql/README-query-editor.md).
- [Validação local do SQL e cenários de rollback](reports/sql-query-editor.md).

## Demonstração para avaliação

- [Preparação local segura](demo/preparacao-ambiente.md): perfil evaluation, banco
  LocalDB novo, provisionamento manual e API/frontend DEMO.
- [Roteiro de apresentação](demo/roteiro-apresentacao.md): cenários A–E em cerca de 6min30.
- [Remoção das ferramentas históricas](reports/remocao-ferramentas-demo-historicas.md):
  referências revisadas, guarda de segurança preservada nos testes e validação do evaluation.

O inicializador atual é `scripts/demo/Start-DemoEvaluation.ps1`. Ferramentas DEMO
não são chamadas automaticamente pela API. O manifesto de preparação de banco novo
inclui agora as sete migrations; bancos DEMO antigos não são atualizados pela carga.
A recuperação de senha permanece desabilitada no host DEMO.

## Histórico

Os relatórios anteriores registram o estado e as quantidades de testes de cada
etapa. Para a situação atual, consultar o relatório final e o README principal.

Relatórios das sprints e das Etapas 01–02 foram removidos no commit `a1f4544`, antes
desta implementação. Podem ser consultados no histórico do Git; não foram recriados.
A auditoria RBAC usada nesta etapa foi lida com
`git show fbb198b:docs/reports/revisao-arquitetura-rbac.md`.
Referências históricas não autorizam repetir operações de escrita em bancos ou Azure.
