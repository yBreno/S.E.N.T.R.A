# Documentação do S.E.N.T.R.A.

O [README principal](../README.md) é a referência atual para arquitetura, instalação,
configuração, funcionalidades e limitações. O [guia do frontend](../frontend/README.md)
resume a execução e a organização da interface. As dependências reproduzíveis estão
nos arquivos de projeto .NET e no `frontend/package-lock.json`.

## Demonstração para avaliação

- [Preparação local segura](demo/preparacao-ambiente.md): perfil `evaluation`, banco
  LocalDB novo e isolado, provisionamento manual e API/frontend DEMO.
- [Roteiro de apresentação](demo/roteiro-apresentacao.md): cenários A–E em cerca de 6min30.
- [Relatório da Sprint 07](reports/sprint-07-cenarios-demo.md): dados, decisões e validações.

O inicializador atual é `scripts/demo/Start-DemoEvaluation.ps1`. O antigo
`scripts/Start-DemoApi.ps1` não está no repositório. Referências históricas a ele
não são instruções atuais. A preparação DEMO não é executada ao iniciar a aplicação.

## Relatórios de implementação

Os relatórios registram o estado **na data de cada etapa**. Contagens de testes,
pendências, configurações e autorizações históricas não descrevem necessariamente
o ambiente atual nem autorizam repetir operações de escrita.

- [Sprint 01 — Equipamentos](reports/sprint-01-equipamentos.md).
- [Sprint 02 — Agentes](reports/sprint-02-agentes.md).
- [Sprint 03 — Manutenções](reports/sprint-03-manutencoes.md).
- [Sprint 04 — Central de Operações](reports/sprint-04-central-operacoes.md).
- [Sprint 05 — Cidade Preditiva](reports/sprint-05-cidade-preditiva.md).
- [Sprint 06 — Automação preditiva](reports/sprint-06-automacao-preditiva.md).
- [Sprint 07 — Cenários DEMO](reports/sprint-07-cenarios-demo.md).
- [Auditoria e limpeza final](reports/limpeza-final.md).
- [Revisão de arquitetura e RBAC](reports/revisao-arquitetura-rbac.md): cargos,
  matrizes de permissões, validação de sessões e preparação da Administração de Usuários.

## Histórico de operação

- [Configuração temporária finish-setup](reports/configuracao-branch-finish-setup.md):
  registro histórico; a configuração atual dos workflows utiliza `main` nos pushes.
- [Diagnóstico HTTP 500 no Azure](reports/correcao-http-500-azure.md): investigação anterior.

Os relatórios antigos das etapas 2–7.6 citados na versão anterior deste índice não
estão nesta cópia. Os links inexistentes foram substituídos pelos documentos
presentes; não houve exclusão desses históricos nesta limpeza.
