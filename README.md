# S.E.N.T.R.A.

Plataforma de monitoramento e gestão de operações urbanas desenvolvida para o desafio
**F.O.G.O. — Ferramenta Onisciente de Gestão e Operações**, da DSIN/PROMETHEUS.
Integra inventário, monitoramento geográfico e análise preditiva explicável.

## Módulos e estado atual

| Módulo | Backend implementado | Interface React |
| --- | --- | --- |
| Inventário Inteligente da Cidade | Equipamentos e agentes: cadastro, consulta, edição, exclusão e estados; endereços; abertura/fechamento e histórico de manutenção | Marcadores, filtros e inspeção; telas de cadastro, edição e manutenção pendentes |
| Centro de Operações da Cidade | Dashboard, alertas, ocorrências, telemetria e localização de agentes | Login, indicadores, mapa, marcadores, popups, inspeção, filtros e atualização manual; gestão de alertas/ocorrências pendente |
| Cidade Preditiva | Risco regional, anomalias, previsão por MTBF e recomendações com explicabilidade | Heat Map Incidents/Failures/Risk e filtros; painel de fatores, anomalias, previsões e ações de recomendações pendente |

A presença de um endpoint não significa que o fluxo correspondente já possui tela.
Fornecedores e bases operacionais têm entidades/repositórios, sem telas ou controllers próprios.

## Stack e arquitetura

- Backend: C# / ASP.NET Core .NET 10, EF Core 10, SQL Server, FluentValidation, JWT e BCrypt.
- Frontend: React 19, JavaScript/JSX, Vite 8 e CSS; React Leaflet 5.0.0, Leaflet 1.9.4 e leaflet.heat 0.2.0.
- Testes: xUnit e EF Core InMemory no backend; `node:test` no frontend.
- Publicação existente: Azure App Service, Azure SQL e Azure Static Web Apps, com workflows GitHub Actions.

O backend é um monólito modular: Domain contém entidades e regras; Application contém
casos de uso, DTOs, validadores e cálculos; Infrastructure implementa EF Core e repositórios;
Api expõe controllers, autenticação, autorização e tarefas hospedadas.
O frontend separa páginas, componentes, hooks, configuração e serviços HTTP.

```text
backend/
  Domain/                 Entidades e regras de negócio
  Application/            Serviços, contratos e motor preditivo
  Infrastructure/         Persistência, migrations e ferramentas DEMO
  Api/                    API HTTP e configuração por ambiente
  Tests/                  Testes automatizados
  DemoData*/ e DemoAdmin/  Ferramentas manuais protegidas
  Solution/Solution.slnx   Solução completa
frontend/
  src/                    Interface React
  public/                 Recursos públicos, quando necessários
  tests/                  Testes frontend
scripts/                  Inicializador protegido da API DEMO local
docs/                     Relatórios históricos e índice de documentação
```

## Funcionalidades concluídas

- Login/logout e expiração; JWT em memória e, temporariamente para demonstração, em
  sessionStorage. Após F5, a restauração exige validade local e aceitação pela API.
- Monitoramento para Admin, OperationManager e CentralOperator, com autorização efetiva no backend.
- Indicadores da API, mapa de São Paulo e camadas independentes de equipamentos, agentes e calor.
- Marcadores por estado/disponibilidade, validação de coordenadas e detalhes sem CPF.
- Filtros locais por tipo, estado e bairro de equipamentos e disponibilidade dos agentes.
- Heat Map por ocorrências, falhas e risco, com filtros próprios, cache em memória e cancelamento.
- Atualização manual, dados anteriores sinalizados como desatualizados durante falhas,
  mensagens de erro/vazio e layout responsivo; mudanças de filtros preservam a instância do mapa.
- `locationUpdatedAt` explícito em UTC no DTO e apresentação no fuso do navegador;
  tradução visual de TrafficAgent para Agente de trânsito.

**Cartografia:** o bloqueio OpenStreetMap por ausência de Referer foi corrigido com
`strict-origin-when-cross-origin` no HTML e no TileLayer. A correção foi validada no site
publicado pelo responsável do projeto, conforme informado por ele. URL HTTPS oficial e
atribuição foram mantidas. A disponibilidade dos tiles depende do provedor e de sua política.

## Execução local

Pré-requisitos: .NET 10 SDK, Node compatível com Vite 8 (22.12+ na linha 22, ou 20.19+ na
linha 20) e npm. A validação local utilizou Node 26.7.0 e npm 11.16.0.
SQL Server/LocalDB e banco já preparado são necessários para usar a API, mas não para os testes InMemory.

Na raiz, os comandos de build e testes estão na seção seguinte. Para iniciar o frontend,
consulte o [guia específico](frontend/README.md); a API precisa estar disponível separadamente.

### API DEMO local isolada

O inicializador preservado exige `SENTRA_DEMO_CONNECTION_STRING` fornecida privadamente
no ambiente do terminal, apontando exclusivamente para o banco `SENTRA_DEMO_Stage73`
na instância `(localdb)\MSSQLLocalDB`, com autenticação integrada. Não use a conexão Azure.
O banco, schema, dados e conta devem estar previamente preparados e autorizados.

```powershell
# Na raiz, após configurar privadamente a conexão dedicada:
dotnet build backend/Solution/Solution.slnx
& ./scripts/Start-DemoApi.ps1 -ConfirmStart
```

A API fica somente em `http://127.0.0.1:5180`, ambiente Demo, sem simulação,
seeders ou migrations automáticas e com bloqueio de escrita. O inicializador gera uma
chave JWT exclusiva em memória; reiniciá-lo exige novo login. Não cria contas nem carga.
Esta limpeza não executou o inicializador ou qualquer ferramenta de banco.

Os perfis normais `http` e `https` de `backend/Api/Properties/launchSettings.json` usam
Development e portas 5129/7130. Iniciar esses perfis pode executar seeders e simulação;
use-os somente com destino privado previamente conferido e autorização para escrita.

### Configuração privada

- Frontend: `VITE_API_BASE_URL` é uma URL pública, sem `/api`, credenciais, query ou fragmento.
  Variáveis `VITE_*` entram no bundle e nunca devem conter segredos. Preserve `.env.local` existente.
- API normal: `ConnectionStrings:DefaultConnection`, `Jwt:Key`, `Jwt:Issuer` e `Jwt:Audience`
  são selecionados pela configuração .NET; guarde segredos em user-secrets locais ou variáveis privadas.
  Variáveis aninhadas usam `__`, como `Jwt__Key` e `ConnectionStrings__DefaultConnection`.
- `Cors:AllowedOrigins`, `Simulation:Enabled` e `SeedAdmin:Enabled` controlam comportamentos
  operacionais. Fora de Demo, a simulação tem padrão habilitado; não inicie apontando para banco não autorizado.
- Ferramentas DEMO são manuais e possuem verificações próprias de ambiente/destino.
  Não execute cargas, provisionamento ou migrations como parte de uma simples inicialização.

## Build e testes

Na raiz, após restaurar as dependências quando necessário (`npm.cmd ci --prefix frontend`
e `dotnet restore backend/Solution/Solution.slnx`):

```powershell
npm.cmd --prefix frontend run build
npm.cmd --prefix frontend run lint
node --test frontend/tests/*.test.js
dotnet build backend/Solution/Solution.slnx --no-restore
dotnet test backend/Tests/Tests.csproj --no-build --no-restore
```

A validação desta limpeza aprovou build/lint frontend, 72 testes frontend e 120 testes
backend; build backend sem avisos ou erros. Testes automatizados não substituem a
validação no navegador nem comprovam contagens atuais no Azure ou no SQL Server real.

## Limitações e próximos passos

- Concluir telas de inventário/manutenção, alertas/ocorrências e interface preditiva usando as APIs existentes.
- Não há polling automático nem clustering/spiderfy; marcadores próximos podem se sobrepor.
- O modelo preditivo é estatístico e baseado em regras, sem garantia de ocorrência futura.
  Revisar o efeito operacional de aceitar recomendações de deslocamento: hoje não movimenta agentes.
- A janela padrão de ocorrências do calor é de sete dias. Dados DEMO antigos podem deixar de aparecer;
  a carga idempotente não renova datas já persistidas.
- Confirmar requisitos no enunciado oficial antes de ampliar tipos de equipamento ou funcionalidades opcionais.
- Concluir documentação de entrega, declaração das ferramentas de IA utilizadas e roteiro de demonstração.

O [índice de documentação](docs/README.md) contextualiza os relatórios históricos e as
mudanças posteriores. As contagens e autorizações antigas não devem ser tratadas como estado atual.
