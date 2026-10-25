# S.E.N.T.R.A.

**Sistema Estratégico de Navegação, Tráfego e Resposta Ativa**, desenvolvido para o
DSIN Coder Challenge — F.O.G.O./PROMETHEUS. Integra inventário urbano, Central de
Operações e prevenção explicável em uma aplicação ASP.NET Core/React.

## Estado da entrega

A Sprint 08 foi validada localmente com **374 testes backend e 205 frontend**,
builds .NET/Vite, ESLint, actionlint, SQL Server isolado e navegador em
320/390/768/1440 px. Consulte o [relatório final](docs/reports/sprint-08-validacao-final.md)
para evidências, requisitos, correções e ressalvas.

Em 25/10/2026, o responsável informou sete migrations aplicadas no Azure SQL e login/
recuperação por Resend testados manualmente em produção. Nesta sprint, somente três
GETs públicos foram realizados: site 200, recuperação habilitada com CORS para
https://sentra.uno e rota de usuários anônima 401. Não houve login automatizado,
envio real de e-mail, consulta ao Azure SQL, deploy ou alteração de produção.

## As três partes do desafio

| Parte | Funcionalidades verificadas |
| --- | --- |
| Inventário Inteligente | Cadastro/edição/lista/detalhes de equipamentos e agentes; série, marca/modelo, endereço completo, CEP/bairro/referência/GPS, situação e planejamento de manutenção; características por tipo; categorias profissionais e vínculos |
| Central de Operações | Indicadores, Leaflet, marcadores por tipo/estado, filtros, detalhes, alertas integrados, manutenção e atenção por bairro; calor por ocorrências/falhas/risco; polling com sinalização de dados desatualizados |
| Cidade Preditiva | Índice explicável por equipamento/região, fatores e evidências, histórico/MTBF, anomalias, recomendações, aceite/descarte e ordem preventiva vinculada; análise manual com automação desligada |

Os oito tipos técnicos existentes são preservados. Balanças e controladores IoT de
cancela usam Sensor; táxi, escolar e viatura são categorias de veículos. Não há
atuação física de dispositivos. Fornecedores e bases têm persistência, sem telas
próprias. Não foram criadas telas separadas de Alertas ou Relatórios.

## Stack e arquitetura

- ASP.NET Core .NET 10, EF Core, SQL Server, FluentValidation, JWT e BCrypt.
- React 19, JavaScript, Vite 8, React Leaflet 5, Leaflet 1.9.4 e leaflet.heat 0.2.0.
- Azure App Service, Azure SQL e Azure Static Web Apps; domínio https://sentra.uno.
- xUnit/EF InMemory e node:test; ensaios adicionais com SQL Server real e Chrome.

Domain contém entidades/regras; Application contém casos de uso/DTOs/validação;
Infrastructure implementa persistência, transações e e-mail; Api aplica HTTP,
autorização e configuração. O frontend separa páginas, componentes, hooks e serviços.
Veja [arquitetura, permissões e limites](docs/arquitetura.md).

## Executar e verificar

Seguir o [guia de instalação](docs/instalacao.md) para banco local **novo**, sete
migrations, configuração privada, primeiro Admin e API/frontend. Não há senha padrão.
Testing evita seeders operacionais de Development. A aplicação não migra no startup.

Na raiz, após restaurar dependências:

```powershell
dotnet restore backend/Solution/Solution.slnx
npm.cmd ci --prefix frontend
dotnet build backend/Solution/Solution.slnx --no-restore
dotnet test backend/Tests/Tests.csproj --no-build --no-restore
node --test frontend/tests/*.test.js
npm.cmd --prefix frontend run lint
npm.cmd --prefix frontend run build
```

A demonstração utiliza `DemoData`, `DemoAdmin` e o perfil `evaluation`.
Os executáveis históricos foram removidos; a guarda de destino Azure foi preservada
somente no suporte dos testes. Veja a [revisão das ferramentas DEMO](docs/reports/remocao-ferramentas-demo-historicas.md).

## Autenticação, cargos e contas

Os cargos oficiais são Admin, OperationManager, CentralOperator,
MaintenanceTechnician e Agent. TrafficAgent, MunicipalGuard e PoliceOfficer são
categorias profissionais; o último rótulo visual é **Policial militar**.

Admin administra contas, cargos, estado e solicitações de reset. OperationManager
gerencia a operação; CentralOperator monitora e executa as ações operacionais
autorizadas; MaintenanceTechnician acessa equipamentos/manutenção; Agent consulta
seu próprio profissional pela API. As permissões são verificadas no backend.

JWT verifica usuário ativo, cargo e AuthVersion persistida. Alterar e-mail, cargo,
estado ou senha revoga sessões; reativar não restaura tokens antigos. O último
Admin ativo é protegido, inclusive sob concorrência. Cadastro público permanece
fechado em produção. O titular pode trocar a senha autenticado em
`POST /api/auth/change-password`, informando a senha atual.

Usuários é exclusivo de Admin, com paginação no servidor, busca, filtros, edição e
histórico mínimo. O ID da conta pode ser fornecido ao gestor para associar um agente
no formulário existente, sem conceder administração de usuários ao gestor.
CPF de agentes permanece mascarado após o cadastro.

## Recuperação por e-mail

Esqueci minha senha responde genericamente e envia link fixo HTTPS para sentra.uno.
O token aleatório tem 256 bits, validade de 15 minutos, hash no banco e uso único.
Nova senha, revogação e auditoria são confirmadas na mesma transação. Existem limites
por conta e origem; contas inativas não recebem/consomem links. Admin não recebe
senha, token ou link secreto. Não se envia senha permanente por e-mail.

Segredos do Resend ficam somente no backend. O padrão local é desabilitado;
LocalTest só funciona em Development/Testing e não tem caixa HTTP pública.
[Configuração privada, DNS e limitações](docs/reports/recuperacao-senha-email.md).

## Configuração e publicação

Configurações privadas: ConnectionStrings, Jwt, Cors:AllowedOrigins, bootstrap e
PasswordRecovery. Variáveis VITE são públicas; não incluir credenciais nelas.
Preservar arquivos locais do operador.

**Simulation__Enabled=false e PredictiveAutomation__Enabled=false** são os defaults
versionados e devem permanecer assim em produção. Análise manual continua disponível.
O DEMO também desabilita recuperação por e-mail.

Publicação exclusivamente de main, com concurrency por serviço:

| Serviço | Caminho | Gates mantidos |
| --- | --- | --- |
| Backend | backend/Api/Api.csproj | SENTRA_USERS_SCHEMA_VERSION e SENTRA_PASSWORD_RECOVERY_SCHEMA_VERSION |
| Frontend | frontend → dist | SENTRA_USERS_API_VERSION e SENTRA_PASSWORD_RECOVERY_API_VERSION |

Os gates de usuários exigem `20261024223405_AddUserAdministration`; os de recuperação,
`20261024234039_AddPasswordRecovery`. Secrets/identidades/URLs e recursos foram
preservados. A Sprint 08 não adiciona migration. Conferir schema antes de publicar;
a ordem continua **SQL → backend → frontend**. Não reaplicar migrations/cargas para
uma simples atualização de código.

Para baseline antiga, o [SQL revisado](docs/sql/README-query-editor.md) permite
conferência e aplicação manual no Query Editor. Não foi executado no Azure nesta
sprint. Não há commit, push ou deploy automático pelo agente.

## Demonstração de 6–8 minutos

Usar o [ambiente DEMO isolado](docs/demo/preparacao-ambiente.md) e o
[roteiro de apresentação](docs/demo/roteiro-apresentacao.md): 79 registros sintéticos,
12 equipamentos, nove agentes e cinco contas provisionadas privadamente. Carga
repetida insere zero; o aceite autorizado do cenário C gera uma única preventiva.
Não usar dados/contas reais nem executar cargas no Azure.

## Limites que importam para a avaliação

- Índices de 0–100 são prioridade heurística, não probabilidades calibradas ou ML.
  MTBF é descritivo e pode usar fallback por tipo; histórico insuficiente é explícito.
- Agendamento não é início/reparo; reforço regional não despacha agentes.
- Leituras periódicas não são snapshot único. Listas operacionais paginam no cliente;
  volumes grandes e carga de produção não foram ensaiados.
- Tiles OpenStreetMap dependem de rede/provedor. O TileLayer usa
  strict-origin-when-cross-origin; o documento usa no-referrer para proteger links.
- JWT permanece em sessionStorage. A fila de e-mail é limitada e volátil; aceite do
  provedor não garante entrega na caixa. Não há endpoint dedicado de health/readiness.
- Troca de senha própria e consulta de campo têm API, sem telas completas para Agent.
  Manutenção mostra responsável, mas o formulário atual não seleciona esse vínculo.
- Há fluxos legados de telemetria/ocorrência com múltiplos SaveChanges e cadastros
  operacionais sem revisão otimista universal. Ver detalhes no relatório final.

Documentação, procedimentos SQL e relatórios estão no [índice](docs/README.md).
