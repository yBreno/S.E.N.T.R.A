# Revisão de arquitetura e RBAC — Etapa 02

Revisão local em `main`, 24/10/2026, sobre a base das Sprints 01–07. Alterações não
commitadas da Etapa 01 foram preservadas. Não houve commit, push, deploy, migração,
carga, acesso ao Azure ou alteração em banco de produção. Os testes com escrita
usam exclusivamente EF InMemory, em bancos isolados por teste.

## Arquitetura e cargos

- Domain mantém entidades/regras e não depende de EF/HTTP. `User` agora rejeita
  enum de cargo inexistente na criação e troca. Nenhum campo ou enum persistido mudou.
- Application mantém casos de uso, DTOs, validators e motor preditivo. O contrato
  `IUserSessionValidator` permite à API validar a sessão sem conhecer EF ou ler
  CPF/hash. Hash e validação de senha compartilham o limite real do BCrypt.
- Infrastructure implementa a consulta de existência/cargo via `AnyAsync`, sem
  carregar dados pessoais. Normalização de e-mail foi alinhada à factory do domínio.
- Api mantém autorização por atributos/grupos e passa a compor autenticação em
  `AddSentraJwt`. Predicados repetidos de Monitoring/MaintenanceOnly foram extraídos,
  preservando inclusive a precedência quando há múltiplos roles em principals internos.
- Frontend mantém páginas, hooks, serviços e componentes. Regras/rótulos dos cargos
  saíram do serviço HTTP de login para `services/authorization.js`; exports antigos
  foram preservados. Login e restauração recusam cargos fora do contrato.

Os cinco cargos oficiais, sem renomeação ou alteração de valores, são:
`Admin=0`, `OperationManager=1`, `CentralOperator=2`, `MaintenanceTechnician=3`, `Agent=4`.
No SQL, o EF usa nomes em string; testes conferem conversão e snapshot sem abrir conexão.

`AgentRole` é **categoria profissional**, não autorização: `TrafficAgent`,
`MunicipalGuard`, `PoliceOfficer`. Mantidos nomes/valores SQL/JSON. Rótulos:
Agente de trânsito, Guarda municipal e **Policial militar**. A categoria nunca
entra no claim `role` de login e não concede permissões.

Foram examinados serviços, mapeamentos, validators, DI, controllers, workers,
seeders/DEMO, testes e os imports/permissões do frontend. Não foi encontrada
necessidade de reescrever os módulos. Os métodos de criação/mapeamento de oito
tipos e o plano DEMO são extensos por contrato/fixture. Manutenção e recomendações
concentram transações e regras já cobertas por testes; fragmentá-las sem ganho
concreto aumentaria risco. Validação no frontend auxilia UX; a do backend continua
autoritativa. `Service`/`IService`, repositórios de bases/fornecedores, cálculos,
workers e adaptadores de mapa têm referências reais e foram preservados.

## Matriz de permissões atual, após as correções

A = Admin; G = OperationManager; C = CentralOperator; T = MaintenanceTechnician;
Ag = Agent. **S** permite; **—** nega. Todas as permissões são verificadas no
backend; a visibilidade de botões não é uma barreira de autorização.

| Operação/rota | A | G | C | T | Ag |
| --- | --- | --- | --- | --- | --- |
| Consultar equipamentos/endereços e alertas | S | S | S | S | — |
| Cadastrar/editar/excluir equipamentos e endereços | S | S | — | — | — |
| Alterar situação de equipamento | S | S | S | S | — |
| Reativar equipamento desativado | S | S | — | — | — |
| Central, mapa/calor, ocorrências, risco regional e anomalias | S | S | S | — | — |
| Consultar agentes, veículo e histórico GPS | S | S | S | — | — |
| Cadastrar/editar/excluir agentes; associar veículo | S | S | — | — | — |
| Atualizar GPS/disponibilidade de qualquer agente | S | S | S | — | — |
| Consultar próprio vínculo (`GET /api/agents/me`) | próprio | próprio | próprio | próprio | próprio |
| Atualizar GPS do próprio vínculo, sem Monitoring | próprio | próprio | próprio | próprio¹ | próprio¹ |
| Consultar/agendar/abrir/iniciar/concluir manutenção | S | S | — | S | — |
| Consultar risco de equipamento/MTBF e telemetria; registrar telemetria | S | S | S | S | — |
| Registrar incidente, resolver alerta e executar análise persistida | S | S | S | — | — |
| Consultar recomendações | todas | todas | todas | manutenção | — |
| Aceitar/descartar recomendação | S | S | — | — | — |
| Criar conta com cargo elevado (`POST /api/auth/register`) | S | — | — | — | — |
| Listar/editar/desativar contas, mudar cargo/senha por API administrativa | ausente | ausente | ausente | ausente | ausente |

¹ O serviço exige `Agent.UserId == sub`; o endpoint atual aceita qualquer cargo
com esse vínculo. Não permite consultar/alterar outros agentes. Sem vínculo,
`/agents/me` retorna 404. Agent não possui tela interna de consulta própria: após
login, o frontend informa a restrição de inventário. Categorias e vínculo da conta
não alteram o cargo do usuário.

Login e registro são as únicas actions explicitamente anônimas. Registro público
continua criando **somente Agent**, inclusive com Role omitido; não é uma API de
administração geral. OperationManager não pode criar Admin, mesmo chamando HTTP
diretamente. Não há endpoint público para trocar cargo ou senha de outra conta.
Swagger/UI continuam públicos, como antes, e não são actions de negócio.

O host DEMO aplica uma restrição adicional: leitura/login por padrão e, com opt-in,
um único aceite preventivo C por A/G. Os cargos não contornam os demais bloqueios 405.

## Matriz desejada para a Administração de Usuários

| Capacidade | Admin | OperationManager | CentralOperator | MaintenanceTechnician | Agent |
| --- | --- | --- | --- | --- | --- |
| Gerir contas de terceiros, cargos, credenciais e acessos | Exclusivo | Não | Não | Não | Não |
| Gestão operacional de inventário, agentes e recomendações | Sim | Sim | Somente ações operacionais autorizadas | Somente manutenção/situações permitidas | Não |
| Monitoramento da cidade e análise regional | Sim | Sim | Sim | Não, salvo decisão explícita futura | Não |
| Operações de manutenção | Sim | Sim | Consulta de necessidades operacionais | Sim | Não |
| Consulta do próprio perfil | Sim, quando vinculado | Sim, quando vinculado | Sim, quando vinculado | Sim, quando vinculado | Limitada ao próprio vínculo |
| Desativação e revogação de sessão de terceiros | Exclusivo | Não | Não | Não | Não |

A matriz desejada não significa que a API administrativa já foi implementada.
Pendências de produto/compatibilidade: decidir o encerramento do auto-cadastro
Agent; explicitar se Agent deve continuar enviando somente seu próprio GPS ou ser
estritamente leitor; definir quem administra a associação `Agent.UserId` (hoje
Managers, como parte do cadastro profissional). Essas operações existentes não
foram silenciosamente removidas nesta revisão. Criar usuários elevados permanece
exclusivo de Admin atual; não há ampliação de acesso para facilitar navegação.

## Inconsistências comprovadas e correções

1. **JWT com cargo antigo:** antes, assinatura válida bastava até expirar. Agora,
   após validação criptográfica, `CurrentUserJwtEvents` exige um `sub` GUID e um
   único cargo canônico, e confronta usuário/cargo no banco. Usuário removido ou
   cargo divergente retorna 401. Consulta não grava nem carrega senha/CPF.
2. **BCrypt truncava após 72 bytes:** um teste reproduziu aceitação de senha com
   sufixo diferente. Cadastro/login limitam bytes UTF-8; hasher recusa hash acima
   do limite e `Verify` retorna falso para entrada longa. Erros HTTP 400 não
   reproduzem a senha. Regras existentes de força permanecem no cadastro.
3. **E-mail dependente da cultura:** `ToLower()` falhava com `I` em cultura turca,
   divergindo de `User.Create`. Repositório usa `ToLowerInvariant()`; regressão
   reproduzida antes da correção e coberta por teste.
4. **Cargos espalhados:** constantes Api usam `nameof(UserRole...)`; frontend tem
   fonte única de permissões/rótulos e aceita somente os cinco cargos. Categoria
   profissional passou a exibir Policial militar sem mudar `PoliceOfficer` persistido.
5. **Identidade inválida podia virar 500:** `GetUserId` agora lança a exceção de
   autenticação mapeada a 401; o evento JWT barra identidade inválida antes da action.
6. **Deploy manual fora da main e sobreposição:** backend recebeu condição de
   branch/evento nos jobs build/deploy e `concurrency: sentra-api-production`,
   `cancel-in-progress: false`. Não se cancelam deploys em andamento.

Não foi demonstrado cadastro público como Admin na versão inicial: o serviço já
negava elevação a não Admin. Acrescentados testes HTTP para confirmar essa proteção,
a negativa a gestores e o comportamento com JWT de Admin cujo cargo foi alterado.
DTOs de usuário/agente continuam sem PasswordHash/CPF bruto nas respostas.

## CI/CD e ferramentas históricas

Actionlint aprovado. Verificadas 15 combinações de eventos/branches, condições de
ambos os jobs, caminhos `backend/Api/Api.csproj`, artefato/pacote e `frontend` → `dist`.
Comparação com Git confirma que o workflow backend só ganhou condições/concurrency:
secrets, identidades, recursos, URLs e passos de build/publicação são iguais.
O workflow Static Web Apps permaneceu byte a byte igual, com `production_branch: main`.
Há um publicador por serviço. Nenhum workflow ou controle remoto foi disparado.
Condições se aplicam a esta versão dos arquivos; cópias antigas em outras branches,
execuções anteriores e a federação OIDC no Azure não foram inspecionadas remotamente.

| Arquivo/ferramenta | Evidência e decisão |
| --- | --- |
| `DemoData.Validation` | Está na solução completa; manifesto histórico de três migrations diverge das cinco atuais. Não é caminho da Sprint 07; preservar como ferramenta histórica, sem executar ou reescrever silenciosamente |
| `DemoData.Azure` | Projeto referenciado diretamente pelos testes `AzureDemoTargetGuardTests`; mantém validações de destino. Preservado, não executado |
| `backend/Api/Api.slnx` | Solução alternativa válida para IDE, com Api/camadas/testes; solução completa documentada continua `backend/Solution/Solution.slnx`. Preservada |
| `backend/Api/Api.http` | Coleção manual de rotas existentes, sem integração no runtime; exemplos de desenvolvimento não substituem credenciais privadas. Preservada e não executada |

## Validação e evidências

Base da Etapa 01: 267 testes backend e 166 frontend. Os testes de regressão novos
reproduziram os problemas BCrypt/cultura antes da correção e passaram depois.

| Verificação | Resultado |
| --- | --- |
| Build completo .NET | Aprovado, zero avisos/erros |
| Pacote local Release da API | `dotnet publish backend/Api/Api.csproj --configuration Release --no-restore --output artifacts/revisao-rbac/api-publicada`; Api.dll conferida, sem deploy |
| Backend completo | **304 aprovados**, zero falhas/ignorados; 37 casos adicionais |
| Frontend | **176 aprovados**, zero falhas; dez casos adicionais |
| ESLint e build Vite | Aprovados |
| Actionlint 1.7.12 | Aprovado |
| Contrato SQL | Cinco enums de login, três categorias, conversão string e snapshot sem mudanças; sem conexão SQL |
| Rotas/actions | Reflexão confere autorização em todas as actions, exceto login/register; cargos dos atributos pertencem ao enum oficial |
| JWT real + Kestrel + InMemory | Matriz de consultas/escritas por cargo, tokens com cargo antigo/usuário removido, subject inválido, cargo profissional, múltiplos cargos, assinatura/issuer/audience/expiração inválidos |
| Compatibilidade pública | Auto-cadastro Agent, login, cargos elevados só por Admin, CPF/hash não expostos, rejeição de cargo legado inválido e erro de senha sem valor sensível |
| DEMO com nova autenticação | Login de gestor persistido, consultas, aceite C repetido com uma ordem, e bloqueio das demais escritas |

A suíte de segurança usa `AddSentraJwt`, o mesmo registro da API, com usuários
persistidos por teste. Os hosts antigos de testes de controllers mantêm seus
principals sintéticos por padrão; isso não é opção de configuração da aplicação.
Não houve inicialização do Program normal, seed, carga, teste com escrita SQL ou
homologação no Azure. Os testes existentes da DEMO/preditivo/mapa continuam aprovados.
Logs/resultados locais em `artifacts/revisao-rbac/`, ignorados pelo Git.

## Riscos SQL/JWT e pendências de Administração

- **Sem migration necessária:** nomes/valores dos enums, modelo, snapshot e schema
  permaneceram iguais. A nova consulta exige disponibilidade do banco em cada
  autenticação; falha de consulta não libera acesso nem usa cargo em cache. Uma
  requisição já autorizada não é interrompida se o cargo mudar durante sua execução.
- **JWTs existentes:** mesmo formato, claims, assinatura, issuer/audience, expiração
  e tolerância de 30 s. Tokens de usuários reais com cargo atual permanecem válidos;
  tokens antigos sem usuário correspondente passam a ser recusados. Instâncias
  precisam estar todas atualizadas para aplicar a mesma verificação.
- **Revogação definitiva ainda ausente:** não há IsActive, security stamp/version ou
  endpoint de desativação. Troca de senha não revoga JWT já emitido; devolver o cargo
  anterior pode tornar um token antigo ainda não expirado novamente compatível.
  Implementar versão persistida de sessão e estado ativo, validando login/JWT, na
  etapa de Administração, com migration aditiva revisada e rollout definido.
- **Senhas longas legadas:** o hash BCrypt não revela se a origem excedia 72 bytes.
  Senhas maiores agora são recusadas; prefixos de hashes legados não podem ser
  diferenciados retrospectivamente. Planejar troca segura de credenciais afetadas;
  nenhuma conta foi examinada, redefinida ou alterada nesta etapa.
- **Fluxos administrativos pendentes:** listar/criar/editar/desativar contas, trocar
  cargo, gerir credenciais e auditar essas ações, com autorização Admin no backend,
  proteção do último Admin, concorrência, sessões e minimização de dados. A tela
  não deve ser usada como barreira de segurança. Não há recuperação por e-mail.
- Manter respostas genéricas no login, revisar rate limiting e a política do
  registro público na implementação administrativa. A persistência temporária do
  JWT em sessionStorage continua sujeita ao risco de XSS; não houve reescrita da sessão.

## Arquivos desta etapa

Criados:

- `backend/Api/Security/CurrentUserJwtEvents.cs`
- `backend/Api/Security/JwtAuthentication.cs`
- `backend/Application/Interfaces/Security/IUserSessionValidator.cs`
- `backend/Application/Services/UserSessionValidator.cs`
- `backend/Application/Security/PasswordLimits.cs`
- `backend/Tests/RbacSecurityTests.cs`
- `backend/Tests/AuthenticationCompatibilityTests.cs`
- `frontend/src/services/authorization.js`
- `frontend/tests/authorization.test.js`
- `docs/reports/revisao-arquitetura-rbac.md`

Modificados:

- `.github/workflows/main_sentra-api.yml`
- `backend/Api/Program.cs` (encoding original preservado)
- `backend/Api/Security/Roles.cs`
- `backend/Api/Security/ClaimsPrincipalExtensions.cs`
- `backend/Api/Controllers/AgentsController.cs`
- `backend/Api/Controllers/RecommendationsController.cs`
- `backend/Api/Demo/DemoInteractionSettings.cs`
- `backend/Application/DependencyInjection.cs`
- `backend/Application/Services/AuthService.cs`
- `backend/Application/Security/BCryptPasswordHasher.cs`
- `backend/Application/Validators/AuthValidators.cs`
- `backend/Domain/Entities/User.cs`
- `backend/Domain/IRepositories/IUserRepository.cs`
- `backend/Infrastructure/Repositories/UserRepository.cs`
- `backend/Tests/Support/EquipmentTestHost.cs`
- `frontend/src/services/auth.js`
- `frontend/src/services/session.js`
- `frontend/src/components/dashboard/dashboardData.js`
- `frontend/src/components/map/markerData.js`
- `frontend/tests/agents.test.js`
- `frontend/tests/markers.test.js`
- `README.md`
- `docs/README.md`

Nenhuma exclusão nova. As exclusões de `GerarHashTemp` e alterações em `.gitignore`,
`frontend/README.md` e relatório de limpeza pertencem à Etapa 01, já presentes no
início e preservadas. O relatório anterior é histórico: sua pendência de CI/CD foi
corrigida nesta etapa, sem modificar seu registro de auditoria.
