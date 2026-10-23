# Deploy temporário pela branch finish-setup

Revisão de 23/10/2026. Foram alterados somente os dois workflows e a documentação.
Não houve migration, deploy, commit, push, merge, checkout de outra branch ou alteração no Azure.

## Estado do Git e preservação

- Branch atual confirmada: `finish-setup`.
- O workspace contém alterações modificadas e arquivos novos das Sprints 01–04, ainda sem commit.
- Um manifesto SHA256 antes/depois confirmou **107 arquivos preexistentes preservados**, excluindo
  README/workflows, que são os alvos autorizados desta tarefa. Nenhum arquivo de aplicação foi alterado.
- A consulta somente leitura `git ls-remote --heads origin` encontrou `backend`, `integration_docker`
  e `main`. Não encontrou `finish-setup` nem `leaflet` publicadas no momento da consulta.
- A referência local `origin/main` coincide com o hash remoto consultado. `origin/leaflet` existe
  apenas como referência local de acompanhamento antiga; isso não comprova uma branch remota vigente.

## Alterações feitas

| Arquivo | Alteração |
| --- | --- |
| `.github/workflows/main_sentra-api.yml` | Push de main para finish-setup; filtros de paths; guardas de branch/evento nos jobs build/deploy |
| `.github/workflows/azure-static-web-apps-agreeable-desert-08bba710f.yml` | Push de leaflet para finish-setup; paths; remoção de eventos/jobs de PR; guarda de branch; production_branch explícita; checkout v4 |
| `README.md` | Nota breve sobre configuração temporária, filtros e limitação dos workflows antigos |
| `docs/reports/configuracao-branch-finish-setup.md` | Este relatório |

Os nomes dos workflows e arquivos foram mantidos; `main_` no nome do arquivo do backend não é um
gatilho. App Service `sentra-api`, slot `Production`, Static Web App associado ao token, nomes de secrets,
URL pública da API, permissões, runners, versões das actions Azure, caminhos de build e comandos
dotnet existentes foram preservados. Não foram lidos valores de secrets.

O único ajuste de versão foi `actions/checkout@v3` → `@v4` no frontend: o actionlint apontou que
o runtime da versão v3 é antigo demais para GitHub Actions. Backend já utilizava v4.
Os workflows foram normalizados para LF e tiveram espaços finais removidos, sem mudança de parâmetros.

## Triggers resultantes nesta versão dos arquivos

| Situação | Backend | Frontend |
| --- | --- | --- |
| Push em finish-setup alterando backend/** | Publica | Não publica |
| Push em finish-setup alterando frontend/** | Não publica | Publica |
| Push em finish-setup alterando ambos | Publica | Publica |
| Push em finish-setup alterando o próprio workflow | Publica o respectivo serviço | Publica o respectivo serviço |
| Alteração somente de README/docs | Não publica | Não publica |
| Push em main ou leaflet | Não publica **com estas definições** | Não publica **com estas definições** |
| PR aberto, atualizado, reaberto ou fechado | Não publica | Não publica |
| Disparo manual em finish-setup | Permitido, como já existia no backend | Não foi acrescentado |
| Disparo manual selecionando outra branch | Jobs bloqueados **com esta definição** | Sem evento manual |

Backend mantém `workflow_dispatch`, mas ambos os jobs exigem o ref
`refs/heads/finish-setup` e evento push/workflow_dispatch. Frontend aceita exclusivamente push nesse ref.
Não há gatilhos schedule, repository_dispatch, workflow_run, workflow_call, deployment_status ou
pull_request_target nos arquivos inspecionados desta branch. Não foram encontrados pipelines adicionais
de publicação versionados no workspace inspecionado.

O frontend define `production_branch: finish-setup` para identificar essa branch como publicação de
produção do mesmo Static Web App. Esse parâmetro é previsto pela [action oficial](https://github.com/Azure/static-web-apps-deploy/blob/v1/action.yml)
e pela [documentação de ambientes de branch](https://learn.microsoft.com/en-us/azure/static-web-apps/branch-environments).
Não foi configurado outro recurso, URL ou deployment_environment de preview.

## Pendência: exclusividade remota ainda não garantida

**Editar finish-setup não modifica o YAML armazenado em main ou em outras branches.**
Foi confirmado que a cópia vigente em `origin/main` ainda tem:

- Backend com push de `main` e disparo manual sem a nova restrição.
- Frontend com push/PR de `leaflet` na cópia do arquivo armazenada em main. A branch remota leaflet
  não apareceu na consulta, mas recriá-la a partir de uma cópia antiga pode reintroduzir esses gatilhos.

Assim, a configuração local está pronta, mas não é correto afirmar que os deploys antigos já foram
desativados no GitHub. A restrição também precisa chegar às definições das branches antigas, sem
precisar mesclar a aplicação. Isso exige commits/push ou uma alteração administrativa pelo responsável,
ações que não foram executadas nesta tarefa conforme solicitado.

Antes de considerar a publicação exclusiva:

1. No GitHub, atualizar somente as definições de workflow das branches antigas que ainda existirem
   para os mesmos filtros/guardas desta versão, ou retirar delas os gatilhos de publicação. Fazer isso
   pela interface web evita trocar a branch local com as sprints ainda sem commit.
2. Conferir execuções antigas pendentes/em andamento e evitar reexecuções de definições antigas.
3. Conferir webhooks e integrações externas do repositório, além de pipelines que possam publicar
   diretamente no Azure. Não confundir ausência de gatilho no YAML local com ausência de integração externa.
4. No App Service, conferir o provedor/branch no Deployment Center; no Static Web Apps, conferir a
   origem/branch e os ambientes. Não criar outro workflow legado ao salvar essas telas. O App Service
   pode gerar workflows por sua integração, conforme a [documentação oficial](https://learn.microsoft.com/en-us/azure/app-service/deploy-continuous-deployment).
5. Conferir a credencial federada OIDC da identidade usada por `azure/login@v2`. Se ela estiver vinculada
   ao ref de main, poderá recusar finish-setup. A correspondência do subject/ref é descrita na
   [referência oficial de OIDC](https://docs.github.com/en/actions/reference/security/oidc).
   Nenhum secret/credencial Azure foi alterado ou ampliado.

Não foram encontrados `gh` ou `az` disponíveis neste ambiente. A consulta anônima da API de workflows
retornou 404 e a de webhooks retornou 401; não permitiram auditoria autenticada desses controles.
Configurações privadas do Azure, webhooks, permissões efetivas, estado de habilitação dos workflows
e credenciais federadas permanecem **não verificados**. As tarefas de bloqueio remoto/externo não
podem ser marcadas como concluídas sem essa conferência.

Desabilitar um workflow pela interface do GitHub é uma operação sobre o workflow, não um filtro de branch,
conforme a [documentação do GitHub](https://docs.github.com/en/actions/how-tos/manage-workflow-runs/disable-and-enable-workflows).
Ela afeta seu uso por arquivo, inclusive na branch nova;
não usar isso como se fosse uma restrição exclusiva de main. Qualquer pausa administrativa deve ser
coordenada com a reativação após atualização das definições antigas.

## Validações executadas

- `actionlint v1.7.12`: dois workflows aprovados após ajuste de checkout. O binário oficial foi
  baixado para `artifacts/deploy-branch-review/`, ignorado pelo Git, com SHA256 conferido contra a release.
- Verificação estática de **15 cenários de elegibilidade**: branches, paths, PR, manual, eventos externos
  e alterações somente de documentação. Todos aprovados. Isso verifica a política declarada, sem executar jobs.
- Comparação com HEAD: conjuntos de secrets/URLs, recursos Azure, comandos de build e versões das
  actions Azure preservados; retirada da duplicação do job de fechamento de PR foi intencional.
- Manifesto de preservação: 107 arquivos preexistentes fora dos alvos autorizados mantiveram seus hashes.
- `git diff --check`: aprovado. O Git pode avisar sobre futura conversão LF/CRLF no Windows.

Não foram repetidos testes de aplicação: não houve mudança em sua lógica. Não foram executados
workflows no GitHub, build de publicação, comandos Azure ou migrations. Validar o YAML não comprova
que a autenticação Azure ou o schema atualmente publicado já estejam preparados para as sprints.
Os workflows continuam sem etapa de aplicação automática de migrations.

## Comandos para publicar manualmente

Após resolver/conferir os controles remotos acima, executar na raiz. As alterações das sprints ainda
sem commit precisam integrar o commit para que o código atualizado seja publicado junto aos workflows:

```powershell
git branch --show-current
git status --short

git add .github/workflows README.md docs/reports backend frontend
git diff --cached --stat
git diff --cached -- .github/workflows

git commit -m "Finaliza sprints 01-04 e configura deploy pela finish-setup"
git push -u origin finish-setup
```

Os comandos são instruções para o responsável; não foram executados pelo agente. O push desse commit
inclui os dois workflows e, se estiverem habilitados no GitHub e autenticados no Azure, dispara os dois
pipelines. Acompanhar os jobs na aba Actions, sem executar migrations automaticamente. Commits futuros
que alterem somente um módulo publicam somente esse módulo pelos filtros de paths.

Nenhum conteúdo de `.env.local`, user-secrets ou artefatos ignorados é adicionado por esses comandos
de staging normal; não usar `git add -f` para incluir esses arquivos. A revisão do diff antes do commit
permanece necessária para confirmar exatamente o conteúdo que será enviado.

## Retorno futuro à configuração anterior

Quando encerrar a configuração temporária, revisar novamente branch de push, guardas dos jobs e
production_branch do Static Web Apps. Não basta trocar apenas a lista de branches. A reativação de
PR previews, se desejada, deve ser uma mudança explícita, e não consequência de restaurar um YAML antigo.
