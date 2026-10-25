# Administração de Usuários — Etapa 03

Implementação local em `main`, 24/10/2026, sobre `a1f4544`. O Git estava limpo.
O relatório RBAC solicitado havia sido removido nesse commit; foi lido integralmente
no histórico (`fbb198b:docs/reports/revisao-arquitetura-rbac.md`), sem restaurar os
arquivos que o responsável removeu. Não houve commit, push, deploy, execução de
migration no Azure ou qualquer acesso ao banco de produção.

## Funcionalidades implementadas

- Módulo **Usuários**, exclusivo de Admin, com paginação no servidor, busca por
  nome/e-mail, filtros de cargo/estado, cadastro dos cinco cargos, detalhes e edição
  de nome/e-mail com unicidade. Não há exclusão física de contas.
- Alteração de cargo, desativação e reativação com confirmação; consultas e escritas
  protegidas no backend. OperationManager e os demais cargos recebem 403, mesmo
  acessando diretamente as rotas. Anônimo recebe 401 nos endpoints administrativos.
- `IsActive`, `AuthVersion` e `Revision` persistidos. Tokens verificam assinatura,
  expiração, usuário ativo, cargo e versão em toda autenticação. Alterar cargo,
  estado, senha ou e-mail incrementa a versão; reativar/restaurar cargo não recupera
  tokens antigos. Alterar somente nome incrementa revisão, preservando a sessão.
- Proteção do último Admin **ativo**, tanto no rebaixamento quanto na desativação.
  Alterações simultâneas são serializadas por bloqueio transacional SQL e revisões
  otimistas evitam sobrescritas. O ator é revalidado dentro do bloqueio, incluindo
  estado, cargo Admin e versão da sessão, antes de qualquer mutação administrativa.
- Histórico mínimo paginado, gravado na mesma transação: criação, perfil, cargo,
  desativação/reativação, bootstrap e troca de senha pelo titular. Guarda IDs, instante
  UTC, ação e cargos anterior/novo; não guarda CPF, nomes/e-mails anteriores, senha,
  hash ou token. Não se fabricou histórico retroativo para contas existentes.
- Cadastro público fechado por padrão. Em produção não é permitido habilitá-lo;
  opt-in só é aceito em Development/Testing. Legado `/auth/register` por Admin atual
  reutiliza o novo cadastro auditado. Provisionamento inicial e ferramentas DEMO
  permanecem fora desse auto-cadastro.
- Ação **Enviar redefinição de senha** explicitamente desabilitada, sem handler ou
  endpoint fictício. Não houve integração de e-mail nem reset que exponha senha/token
  ao administrador. O titular possui troca autenticada com senha atual, auditada,
  sem escolher outro usuário e sem receber novo token automaticamente.
- Sidebar definitiva: Dashboard, Equipamentos, Agentes, Manutenções, Mapa operacional,
  Mapa de calor, Cidade preditiva, Usuários (Admin) e Sair, conforme os roles atuais.
  Foram retirados apenas os atalhos Em breve de alertas e relatórios; os painéis
  reais de alertas no dashboard/mapa foram preservados. Não foi criada tela de relatórios.

`UserRole` continua exclusivamente Admin, OperationManager, CentralOperator,
MaintenanceTechnician e Agent. `AgentRole` continua TrafficAgent, MunicipalGuard e
PoliceOfficer; rótulos profissionais não concedem login/cargo. Nenhum enum foi renomeado.

## Vínculo com profissionais de campo

`Agent.UserId` é uma FK opcional para User, com índice único filtrado e delete restrito.
Os endpoints existentes de criação/edição de agentes aceitam essa associação pelos
gestores autorizados. A administração exibe o identificador do profissional vinculado;
não altera nem cria vínculos automaticamente e não transforma categoria profissional
em cargo. Desativar a conta revoga acesso, preservando o profissional, GPS, categoria,
disponibilidade operacional e histórico. Não foi ampliado o acesso de gestores a contas.

## Endpoints e permissões

| Método/rota | Comportamento | Permissão |
| --- | --- | --- |
| `GET /api/users?search=&role=&isActive=&page=1&pageSize=20` | Busca/paginação, máximo 100 por página | Admin |
| `POST /api/users` | Nome, e-mail, CPF válido, senha inicial privada e cargo obrigatório | Admin atual revalidado no bloqueio |
| `GET /api/users/{id}` | Dados da conta e AgentId opcional, sem credenciais/CPF | Admin |
| `PUT /api/users/{id}` | Nome/e-mail e Revision esperada | Admin atual |
| `PUT /api/users/{id}/role` | Role e Revision obrigatórias | Admin atual |
| `PUT /api/users/{id}/status` | IsActive e Revision obrigatórias | Admin atual |
| `GET /api/users/{id}/history?page=1&pageSize=20` | Histórico mínimo paginado | Admin |
| `POST /api/auth/change-password` | CurrentPassword/NewPassword; somente o titular do JWT | Autenticado e versão atual; sem alvo arbitrário |
| `POST /api/auth/register` | Admin: delega ao cadastro auditado; público: fechado por padrão | Admin ou opt-in Agent apenas em Dev/Testing |
| `POST /api/auth/login` | Recusa inativos/cargo inválido; emite JWT com auth_version | Credenciais válidas |

Escritas administrativas retornam conta atual e `reauthenticate`; alteração do
próprio acesso encerra a sessão no frontend. Nome/e-mail/cargo/estado/Revision são
as informações usadas pelo módulo; DTOs administrativos não incluem hash, token,
CPF ou AuthVersion. Senha inicial é entrada privada do cadastro, nunca resposta.
A revisão divergente ou duplicidade retorna 409; último Admin/campos inválidos,
400; sessão revogada, 401. O formulário usa mensagens locais compreensíveis e não
reproduz corpos de erro potencialmente sensíveis.

## Schema e impacto sobre contas existentes

Migration: **20261024223405_AddUserAdministration**. Incremento aditivo:

- Users.IsActive bit NOT NULL, padrão true;
- Users.AuthVersion bigint NOT NULL, padrão 1;
- Users.Revision bigint NOT NULL, padrão 1 e concurrency token;
- checks positivos para AuthVersion/Revision;
- tabela UserAuditEvents com FKs restritivas e índices de consulta.

IDs, nomes, e-mails, CPF, roles e hashes existentes são preservados. Todas as contas
preexistentes começam ativas, porque o schema anterior não possuía desativação.
O preflight recusa cargos legados desconhecidos e bancos com contas mas sem Admin;
não escolhe nem promove alguém automaticamente. Essa situação exige revisão privada
pelo proprietário/DBA antes da migration. Banco vazio é aceito para bootstrap seguro.

**Todos os JWTs antigos sem auth_version serão recusados.** Usuários precisam fazer
novo login, mesmo que não tenham mudado de cargo. Tokens novos são permanentemente
incompatíveis com versões anteriores do usuário após qualquer revogação. O formato
externo da resposta de login foi preservado; o claim adicional é obrigatório.

O SQL incremental idempotente está em
[`docs/sql/20261024223405-administracao-usuarios.sql`](../sql/20261024223405-administracao-usuarios.sql).
Foi gerado pelo EF, revisado e executado somente em LocalDB novo. Inclui baseline
obrigatória `20261023224842_AddMaintenanceScheduling`, `XACT_ABORT` e `:ON ERROR EXIT`.
Executar com SQLCMD e `-b`, ou SSMS em SQLCMD Mode, interrompendo ao primeiro erro.
Não usar um cliente que continue enviando batches após falha. Não executar Down:
essa reversão remove colunas/histórico e não é um procedimento seguro de rollback.

A factory design-time usa conexão LocalDB somente para metadados, sem iniciar API,
seeders ou ler secrets. Geração de migration/SQL não acessa bancos. Para atualizar
um banco local autorizado via `dotnet ef database update`, usar conexão explícita
`--connection` conferida pelo operador; não confiar na conexão de metadados da factory.

## Ordem segura de atualização do Azure SQL e publicação

**Procedimento futuro, manual e sujeito à autorização do responsável. Nada abaixo
foi executado no Azure nesta etapa. A API nova não funciona com o schema antigo.**

1. Revisar o diff, migration/SQL e artefatos. Confirmar recurso/banco corretos,
   cinco migrations anteriores e recuperação/backup/PITR, com teste em homologação.
   Inventariar contas/Admin existentes privadamente, sem exportar credenciais ou
   dados pessoais para logs. Planejar janela para locks de alteração de schema.
2. Manter **ausentes** as variáveis de liberação abaixo. Push pode compilar a API,
   mas os jobs de deploy não avançam sem confirmação explícita da etapa correta.
3. Entrar em manutenção, bloquear tráfego/escritas e retirar todas as instâncias
   antigas de autenticação/administração. Não misturar código antigo e novo:
   o código antigo não verifica IsActive/AuthVersion e não participa dos novos locks.
4. O operador autorizado aplica o SQL incremental no banco conferido e interrompe
   ao primeiro erro. Conferir History com a migration nova, as três colunas/checks,
   a tabela/FKs de auditoria, contas/hashes preservados e pelo menos um Admin ativo
   em ambientes que já tinham usuários. Não há carga DEMO nem reset de senhas.
5. Só então definir a variável de repositório GitHub **SENTRA_USERS_SCHEMA_VERSION**
   com o valor exato **20261024223405_AddUserAdministration**. Revisar/executar o
   workflow backend a partir de main. O build não migra banco; o job deploy exige
   essa declaração do operador. Secrets, identidades, recursos, caminhos e URLs
   Azure existentes foram preservados.
6. Conferir todas as instâncias novas, login Admin, recusa de tokens antigos/inativos,
   último Admin, histórico e acesso negado a gestores. Confirmar auto-cadastro
   fechado. Não reabrir tráfego com réplicas antigas.
7. Somente após essa verificação definir **SENTRA_USERS_API_VERSION** com o mesmo
   valor, liberando o workflow frontend em main. Conferir `/index.html`, login,
   sidebar e fluxo Usuários; encerrar manutenção e comunicar necessidade de novo login.

Se a atualização falhar, manter tráfego/deploys bloqueados e aplicar a estratégia
revisada de recuperação. A migração é expandida antes do código; não há fallback
inseguro para autenticar sem versão ou ignorar colunas. Reverter para API antiga
após começar a desativar/revogar contas reabre acesso a tokens que ela não sabe
recusar. Não reverter código isoladamente nem apagar auditoria automaticamente.
As variáveis são gates operacionais, não uma inspeção remota automática do schema;
se forem declaradas incorretamente, não garantem compatibilidade. Outras branches,
execuções antigas e federação privada Azure não foram alteradas remotamente.

## Primeiro administrador e execução local

Em ambiente novo autorizado, preparar o schema atual **antes** de iniciar a API.
Com tabela Users vazia, configurar privadamente:

| Configuração | Finalidade |
| --- | --- |
| `ConnectionStrings:DefaultConnection`, `Jwt:Key/Issuer/Audience` | Destino e assinatura privados corretos |
| `SeedAdmin:Enabled=true` | Autorização explícita do bootstrap fora de Development |
| `SeedAdmin:Name/Email/Cpf/Password` | Dados privados válidos e senha forte, máximo 72 bytes UTF-8 |
| `PublicRegistration:AllowAgentSelfRegistration=false` | Manter registro público fechado |
| `Simulation:Enabled=false` no ensaio local | Evitar simulação/carga incidental |

Não há senha padrão. Usar provider de secrets/variáveis temporárias autorizado,
sem colocar valores no Git ou na linha de comando compartilhada. O bootstrap usa
transação/bloqueio de administração, valida DTO/senha, cria apenas o primeiro Admin
e grava InitialProvisioning com ator sistema. Duas inicializações concorrentes
não criam dois administradores. Logs omitem e-mail, CPF, senha e hash. Se já existir
qualquer usuário, não cria, promove ou redefine contas. Depois de provisionar,
desativar SeedAdmin:Enabled e remover a senha temporária do bootstrap.

Entrar com essa conta e usar **Usuários → Novo usuário**. Para testar: criar conta,
consultar detalhes, editar nome/e-mail, alterar cargo e confirmar; desativar/reativar
e observar histórico. Repetir envio com Revision antiga deve retornar 409.
Tentar rebaixar/desativar o último Admin deve retornar 400. OperationManager não
vê Usuários e chamadas diretas retornam 403. Alterar o próprio acesso encerra a sessão.

Para a senha do próprio titular, o cliente autenticado envia
`POST /api/auth/change-password` com CurrentPassword/NewPassword, recebe 204 e deve
apagar o JWT e autenticar novamente. É troca própria, não reset de outra conta.
Não há interface de senha própria para os demais cargos nesta etapa; API documentada.

Os passos manuais DEMO seguem `docs/demo/preparacao-ambiente.md`: o manifesto do
banco novo passou a seis migrations; loaders não migram bancos existentes nem
resetam usuários. Contas DEMO continuam sintéticas e provisionadas pelo CLI. No
host DEMO, administração é somente consulta; o opt-in permite apenas o aceite C.

## Testes e evidências

| Verificação | Resultado |
| --- | --- |
| Suíte backend completa | **335 aprovados**, 0 falhas/ignorados; base 304 preservada |
| Suíte frontend completa | **186 aprovados**, 0 falhas; base 176 preservada |
| Build solução .NET | Aprovado, 0 avisos/erros |
| Build Vite e ESLint | Aprovados |
| Actionlint | Aprovado; validação adicional de gates/branches, secrets e caminhos |
| SQL Server real isolado | Upgrade preservou usuário/hash/CPF/ID; defaults ativos/versão/revisão; concorrência SQL, FK de auditoria, revisão otimista e rollback de usuário+evento conferidos |
| SQL entregue | Aplicação e repetição em banco novo aprovadas; bancos novos com Agent sem Admin e com SuperAdmin recusados sem alteração parcial ou perda de dados |
| CLI DEMO preservado | Banco novo preparado com seis migrations; primeira carga com 79 registros, repetição com zero novos registros e provisionamento das cinco contas sintéticas aprovados |
| Chrome com bundle e SQL | Paginação, filtros cargo/estado, criação, edição, cargo, ativação, histórico, erro/recuperação, 409, último Admin e logout próprio conferidos |
| Responsividade | 320/390/768/1440 px, sem overflow horizontal ou exceções JavaScript no ensaio |

Foram usados **cinco bancos novos LocalDB**, com nomes exclusivos, preservados ao
final. Nenhum banco preexistente ou Azure foi utilizado. A suíte padrão usa
InMemory; o bloqueio foi conferido separadamente em conexões SQL independentes; não
houve ensaio com réplicas Azure. Registros e credenciais desses ensaios são sintéticos e locais; não são
dados mockados na aplicação normal. Não houve alteração de usuário real.

Resultados/capturas locais ignorados em `artifacts/administracao-usuarios/`:
`backend-testes.txt`, `frontend-testes.txt`, `browser-resultados.json`,
`browser-filtros.json`, `sql-resultados.json`, `sql-migration-resultados.json`, `demo-resultados.json`,
`usuarios-lista.png`, `usuarios-detalhes-historico.png`, `usuarios-mobile.png` e
`usuarios-protecao-ultimo-admin.png`. EF CLI 10.0.10 emitiu aviso de versão inferior
ao runtime 10.0.13; geração, snapshot, script e execução SQL foram validados sem erro.

## Riscos remanescentes e próxima etapa de e-mail

- Gates de deploy devem ser liberados somente após verificação real; não foi feita
  homologação de Azure, configuração de secrets, tráfego ou réplicas nesta etapa.
- A autenticação depende de leitura atual do banco e falha fechada se indisponível.
  Uma requisição já autorizada pode terminar após uma revogação; as mutações
  administrativas revalidam o ator dentro do lock para evitar essa janela de escrita.
- Escritas externas/versões antigas que ignorem Domain/Revision/lock não têm as
  mesmas garantias. Todas as instâncias e futuros caminhos de credenciais precisam
  usar o fluxo transacional e incrementar AuthVersion.
- Histórico é mínimo, não trilha inviolável contra DBA: não inclui valores pessoais
  anteriores nem auditoria retroativa. Novo cadastro usa senha inicial informada
  privadamente pelo Admin; não existe convite por e-mail ou exigência persistida
  de troca no primeiro acesso. Planejar esse ciclo na próxima etapa.
- Preservados os limites BCrypt da Etapa 02 e os hashes antigos; credenciais legadas
  maiores que 72 bytes exigem troca segura, sem reparo automático nesta entrega.
- Próxima etapa: provider de e-mail configurado privadamente, token aleatório com
  apenas hash persistido, expiração, uso único, proteção contra enumeração/abuso,
  rate limiting, confirmação pelo titular, auditoria e incremento de AuthVersion
  na conclusão. Não ativar o botão de envio antes do fluxo completo e testado.
- JWT continua temporariamente em sessionStorage, com os riscos de XSS já conhecidos.
  Nome alterado do próprio usuário aparece imediatamente na lista/detalhes; o nome
  de apresentação da sessão é atualizado no próximo login.

## Arquivos alterados/criados

Nenhum arquivo foi removido nesta etapa. Lista completa para revisão:

- `.github/workflows/azure-static-web-apps-agreeable-desert-08bba710f.yml`
- `.github/workflows/main_sentra-api.yml`
- `README.md`
- `backend/Api/Controllers/AuthController.cs`
- `backend/Api/Controllers/UsersController.cs`
- `backend/Api/Program.cs`
- `backend/Api/Security/ClaimsPrincipalExtensions.cs`
- `backend/Api/Security/CurrentUserJwtEvents.cs`
- `backend/Api/Security/PublicRegistrationSettings.cs`
- `backend/Api/Seed/AdminSeeder.cs`
- `backend/Api/appsettings.json`
- `backend/Application/DTOs/Auth/UserAdministrationDtos.cs`
- `backend/Application/DependencyInjection.cs`
- `backend/Application/Interfaces/IServices/IUserAdministrationCoordinator.cs`
- `backend/Application/Interfaces/IServices/IUserAdministrationService.cs`
- `backend/Application/Interfaces/Security/IUserSessionValidator.cs`
- `backend/Application/Security/JwtTokenService.cs`
- `backend/Application/Services/AuthService.cs`
- `backend/Application/Services/UserAdministrationService.cs`
- `backend/Application/Services/UserSessionValidator.cs`
- `backend/Application/Validators/AuthValidators.cs`
- `backend/Application/Validators/UserAdministrationValidators.cs`
- `backend/Domain/Entities/User.cs`
- `backend/Domain/Entities/UserAuditEvent.cs`
- `backend/Domain/IRepositories/IUserAuditRepository.cs`
- `backend/Domain/IRepositories/IUserRepository.cs`
- `backend/Infrastructure/Data/AppDbContext.cs`
- `backend/Infrastructure/Data/AppDbContextDesignTimeFactory.cs`
- `backend/Infrastructure/Data/Configurations/UserAuditEventConfiguration.cs`
- `backend/Infrastructure/Data/Configurations/UserConfiguration.cs`
- `backend/Infrastructure/Data/Migrations/20261024223405_AddUserAdministration.Designer.cs`
- `backend/Infrastructure/Data/Migrations/20261024223405_AddUserAdministration.cs`
- `backend/Infrastructure/Data/Migrations/AppDbContextModelSnapshot.cs`
- `backend/Infrastructure/Data/SqlUserAdministrationCoordinator.cs`
- `backend/Infrastructure/Demo/DemoEvaluationSchema.cs`
- `backend/Infrastructure/DependencyInjection.cs`
- `backend/Infrastructure/Repositories/UserAuditRepository.cs`
- `backend/Infrastructure/Repositories/UserRepository.cs`
- `backend/Tests/RbacSecurityTests.cs`
- `backend/Tests/Support/EquipmentTestHost.cs`
- `backend/Tests/Support/InMemoryUserAdministrationCoordinator.cs`
- `backend/Tests/UserAdministrationTests.cs`
- `backend/Tests/UserBootstrapTests.cs`
- `docs/README.md`
- `docs/demo/preparacao-ambiente.md`
- `docs/reports/administracao-usuarios.md`
- `docs/sql/20261024223405-administracao-usuarios.sql`
- `frontend/README.md`
- `frontend/src/components/dashboard/DashboardSidebar.jsx`
- `frontend/src/components/users/UserForm.jsx`
- `frontend/src/components/users/userModel.js`
- `frontend/src/pages/DashboardPage.jsx`
- `frontend/src/pages/UserPage.jsx`
- `frontend/src/services/auth.js`
- `frontend/src/services/authorization.js`
- `frontend/src/services/session.js`
- `frontend/src/services/users.js`
- `frontend/tests/users.test.js`
