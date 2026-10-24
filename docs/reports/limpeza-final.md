# Auditoria e limpeza final — Etapa 01

Revisão local em `main`, 24/10/2026. O Git estava limpo no início. Foram examinados
os 366 arquivos versionados, diretórios locais/gerados, imports do frontend,
ProjectReferences/soluções, registros de DI, scripts, workflows e histórico dos candidatos.
Nenhum banco, Azure, usuário ou segredo foi acessado ou alterado. Sem commit, push ou deploy.

## Inventário e preservação

| Classificação | Arquivos/grupos e decisão |
| --- | --- |
| Essenciais para produção | `backend/Api`, `Application`, `Domain`, `Infrastructure`, `frontend/src`, `index.html` e assets utilizados: preservados; nenhuma alteração de lógica |
| Build, testes e deploy | Projetos/soluções, cinco migrations e snapshot, todos os testes, manifestos/lockfile, Vite/ESLint, configurações e os dois workflows ativos: preservados |
| Documentação permanente | README principal, guia frontend, índice, relatórios 01–07 e histórico operacional: preservados; guias consolidados e referências corrigidas |
| DEMO | `DemoData`, `DemoAdmin`, perfil evaluation, proteções, CLI históricas, script de inicialização e `docs/demo/`: preservados; caminho reproduzível da Sprint 07 inalterado |
| Temporários/locais | `artifacts/`, `bin/`, `obj/`, `node_modules/`, `dist/`, perfis de navegador e `.env.local`: ignorados e preservados, inclusive evidências históricas e arquivos do usuário |
| Código não utilizado | Gerador isolado `GerarHashTemp`: removido após conferir referências; nenhum arquivo-fonte frontend sem alcance a partir de imports da aplicação/testes |
| Duplicados/obsoletos | Guia frontend anterior e índice desatualizados: consolidados; quatro `.csproj` idênticos preservados porque têm entrypoints distintos; logo original e solução alternativa preservados |

A análise de imports alcançou 87 arquivos de produção frontend, sem import relativo
faltante. `Service`/`IService` são bases de serviços concretos. Fornecedores e bases
operacionais têm mappings, DbSets e registros de DI: ausência de tela não é código morto.
O inventário detalhado local está em `artifacts/limpeza-final/inventario-antes.json`;
não contém valores de variáveis privadas ou arquivos de configuração local.

## Arquivos removidos e motivo

- `backend/GerarHashTemp/Program.cs`: utilitário de uso pontual que apenas imprimia
  hash BCrypt de uma senha fixa. Nenhuma referência de entrada em solução, projeto,
  aplicação, testes, scripts, workflows ou documentação. Provisionamento DEMO atual
  usa `DemoAdmin`, senha privada e validação; não depende desse utilitário.
- `backend/GerarHashTemp/GerarHashTemp.csproj`: projeto exclusivo desse programa,
  fora de ambas as soluções. Histórico de introdução: `2dcdce1`; nenhuma ligação
  posterior ao build/deploy identificada. Seus antigos `bin/obj` locais não foram apagados.

Nenhum componente, serviço, migration, teste, workflow ou relatório histórico foi
excluído. Não houve exclusão recursiva nem `git clean`.

## Organização e documentação

- `.gitignore`: acrescentadas regras para cobertura, resultados de navegador,
  HAR/traces, caches Python, arquivos privados de clientes HTTP, chaves/certificados
  privados, backups SQL e sidecars SQLite. Sem regras genéricas para PNG, JSON ou SQL.
- `frontend/README.md`: consolidada a documentação repetida, removidas afirmações de
  que inventário/agentes/manutenção/Cidade Preditiva não tinham telas. Mantidos
  execução, arquitetura da interface, testes, diagnóstico e links para a documentação atual.
- `docs/README.md`: substituídos dez links para documentos inexistentes por links
  para os relatórios presentes; corrigido o caminho do inicializador DEMO.
- `README.md`: corrigidas descrições divergentes dos workflows atuais e do opt-in
  DEMO. As definições de CI/CD e os arquivos de demonstração não foram alterados.

## Validação antes e depois

| Verificação | Antes | Depois |
| --- | --- | --- |
| Build completo .NET | Aprovado, 0 avisos/erros | Aprovado, 0 avisos/erros |
| Testes backend | 267 aprovados, 0 falhas | 267 aprovados, 0 falhas |
| Testes frontend | 166 aprovados, 0 falhas | 166 aprovados, 0 falhas |
| ESLint | Aprovado | Aprovado |
| Build Vite | Aprovado | Aprovado, mesmos bundles de aplicação |
| actionlint 1.7.12 | Aprovado | Aprovado; workflows idênticos por SHA-256 |
| Caminhos de deploy | API `backend/Api/Api.csproj`; frontend `frontend` → `dist` | Preservados e existentes |
| Links locais Markdown | 10 destinos inexistentes | 0 destinos inexistentes |

Conferidos 28 caminhos sintéticos do `.gitignore`: 17 artefatos/arquivos privados
ignorados e 11 arquivos essenciais permitidos; nenhum arquivo versionado passou a
corresponder às regras de exclusão. Referências de projetos/soluções válidas.
Não foram encontradas regressões nas verificações executadas.

Os testes backend usam persistência InMemory; esta etapa não executou probes SQL,
migrations, cargas DEMO ou inicialização da API. Saídas da comparação ficam em
`artifacts/limpeza-final/`, ignoradas pelo Git. Nenhum teste válido foi retirado.

## Resíduos que exigem decisão humana e riscos remanescentes

- `artifacts/` reúne capturas, dumps de testes, scripts experimentais, resultados e
  perfis de navegador de revisões anteriores; alguns são evidências citadas nos
  relatórios. Definir retenção antes de apagar. Preservados arquivos locais, bancos,
  perfis, segredos e históricos; nada foi executado desses scripts experimentais.
- `DemoData.Validation` está no build e usa manifesto histórico de três migrations;
  não serve para preparar a Sprint 07. `DemoData.Azure` é referenciado pelos testes
  de proteção de destino. Decidir descontinuação em etapa própria, preservando testes
  e histórico; o caminho evaluation atual não usa essas duas ferramentas.
- `backend/Api/Api.slnx` continua como entrada alternativa de IDE; a solução completa
  é `backend/Solution/Solution.slnx`. Não assumir que um arquivo sem referência CLI
  é dispensável para o desenvolvedor. Metadados da skill frontend também preservados.
- `frontend/public/brand/sentra-logo-background.png` não é usado na interface, mas
  está documentado como original da identidade visual; preservado. `Api.http` é
  um cliente manual de desenvolvimento, não parte da execução; revisar exemplos
  em etapa própria, sem executar suas operações nesta auditoria.
- **CI/CD:** pushes dos dois workflows filtram `main`, com um publicador por serviço,
  sem gatilhos PR/schedule/dispatch externo. Backend possui `workflow_dispatch` sem
  condição explícita de branch nos jobs e sem concurrency; frontend tem ambos os
  controles aplicáveis ao push. Isso divergia do README. Requer revisão de CI/CD,
  sem presumir que a credencial OIDC aceite outras branches. Não houve login Azure,
  inspeção remota de federação/Deployment Center ou alteração dos workflows.
- Futuro push em `main` com mudanças sob `backend/` ou `frontend/` corresponde aos
  filtros ativos, mesmo quando a mudança é apenas documentação/remoção de utilitário.
  Esta etapa só valida localmente e não comprova o estado da aplicação publicada.
