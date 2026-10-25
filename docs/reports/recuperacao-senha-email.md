# Recuperação segura de senha por e-mail — Etapa 04

Implementação local em `main`, 24/10/2026. O relatório e as alterações locais da
Etapa 03 foram lidos e preservados. A migration `AddUserAdministration` e seu SQL
permanecem intactos. Nenhum commit, push, deploy, DNS, serviço externo de e-mail,
Azure ou banco preexistente foi alterado. Os ensaios SQL desta etapa utilizaram
somente dois bancos LocalDB **novos**, com dados sintéticos.

## Fluxo implementado

1. Login oferece **Esqueci minha senha**, com solicitação por e-mail.
2. Para e-mails válidos, a resposta pública é sempre 202 com a mesma mensagem,
   inclusive para contas inexistentes/inativas, limites atingidos, fila cheia e
   falhas internas de enfileiramento. Validação sintática inválida retorna 400.
   A indisponibilidade global da funcionalidade também retorna 400, igualmente
   para todos; a tela consulta a disponibilidade antes de permitir envio.
3. A solicitação não consulta Users e não aguarda rede do provedor. Uma fila
   limitada a 100 solicitações, em memória, é processada por worker com escopo
   próprio. A conta é consultada somente nesse processamento assíncrono.
4. Para conta ativa e cargo conhecido, gera-se segredo CSPRNG de 32 bytes,
   codificado em base64url de 43 caracteres. Somente SHA-256 hexadecimal é salvo
   em `PasswordResetTokens`, junto com UserId, AuthVersion e expiração de 15 minutos.
   Existe no máximo um registro por conta; nova emissão substitui o hash anterior.
5. O transporte envia texto com link fixo para
   `https://sentra.uno/#redefinir-senha?token=<segredo>`. Não se deriva destino de
   Host, Origin, corpo, callback ou query fornecida pelo solicitante.
6. O navegador lê o segredo do fragmento e remove-o da URL com replaceState,
   mantendo-o somente em memória durante o formulário. Não usa localStorage,
   sessionStorage, query HTTP, logs, analytics nem GET que consuma o token.
   Recarregar a página já limpa exige reabrir o link original ou solicitar outro.
7. A tela exige nova senha e confirmação. O POST transmite o segredo no corpo,
   respeitando o mesmo conjunto de requisitos e os **72 bytes UTF-8** do BCrypt.
8. Sob o mesmo bloqueio SQL da administração, verifica conta ativa, cargo, versão,
   expiração, entrega confirmada pelo provedor e token não invalidado. A troca do
   hash BCrypt, incremento de AuthVersion/Revision, invalidação do token e auditoria
   são persistidos na mesma transação. Apenas um consumo concorrente vence.
9. A resposta é 204, sem token de login. A tela confirma e retorna ao login;
   todas as sessões anteriores passam a ser recusadas. O cadastro público continua
   fechado por padrão e o provisionamento inicial não foi alterado.

Solicitar link **não altera a senha nem revoga o JWT atual**. Alterações de e-mail,
cargo, estado e senha invalidam links antigos por AuthVersion, mesmo após
reativação/restauração dos valores. Conta inativa nunca recebe/consome link; Admin
deve reativá-la explicitamente pelo fluxo administrativo já auditado.

## Reset administrativo e auditoria

**Usuários → Detalhes → Enviar redefinição de senha** exige Admin tanto na interface
quanto no endpoint. Há confirmação explícita. E-mail/URL/senha não são parâmetros
dessa operação: somente o endereço cadastrado é utilizado. O ator é revalidado no
bloqueio transacional, com estado, cargo e versão de autenticação atuais.

O botão permanece desabilitado por padrão, sem configuração privada válida, no
DEMO ou para conta inativa. Em ambiente habilitado, informa **solicitação registrada**,
sem afirmar entrega na caixa de entrada ou alteração imediata de senha. OperationManager,
CentralOperator, MaintenanceTechnician e Agent recebem 403; anônimo recebe 401.

Eventos mínimos, sem senha, hash, segredo, link ou e-mail:

| Ação | Ator | Significado |
| --- | --- | --- |
| PasswordResetRequested | ID do Admin | Solicitação administrativa autorizada registrada |
| PasswordRecoveryRequested | Sistema | Emissão pública para conta elegível |
| PasswordResetCompleted | Sistema | Titular confirmou pela capacidade secreta válida |
| PasswordResetDeliveryFailed | Sistema | Transporte não confirmou o envio |

Eventos usam UserId/ActorUserId e instante UTC. A solicitação administrativa fica
auditada mesmo se o enfileiramento subsequente falhar; a interface recebe conflito
nessa situação e não declara sucesso. A conclusão efetiva e sua auditoria são atômicas.
O histórico existente foi reutilizado, sem armazenar valores pessoais antigos ou
fabricar eventos retroativos. UserRole e AgentRole permanecem distintos e inalterados.

## Provedor escolhido e comparação

Comparação consultada em fontes oficiais em 24/10/2026; preços em USD sujeitos a
mudança e não constituem contratação. As observações de simplicidade são decisões
de engenharia para este repositório, não garantias comerciais de entregabilidade.

| Opção | Custo e integração | Domínio e entregabilidade |
| --- | --- | --- |
| **Resend, escolhido** | Plano gratuito: 3.000/mês, 100/dia; Pro: US$20/mês. Um POST HTTPS com HttpClient evita SDK adicional | Domínio próprio verificado, SPF/DKIM/DMARC, supressões e configuração de tracking |
| Azure Communication Services Email | Cobrança por mensagem e volume transferido; integra o ecossistema Azure, mas requer recursos Email/Communication conectados | Domínio próprio verificado e gestão de autenticação/DNS no Azure |
| SendGrid | Trial de 60 dias, 100/dia; Essentials a partir de US$19,95/mês; API/SMTP | Autenticação de domínio e ferramentas de entregabilidade; mais configuração de conta para esta entrega |

Fontes: [preços Resend](https://resend.com/pricing),
[API Resend](https://resend.com/docs/api-reference/emails/send-email),
[domínios Resend](https://resend.com/docs/dashboard/domains/introduction),
[cobrança ACS](https://learn.microsoft.com/en-us/azure/communication-services/concepts/email-pricing),
[conexão de domínio ACS](https://learn.microsoft.com/en-us/azure/communication-services/quickstarts/email/connect-email-communication-resource),
[preços SendGrid](https://www.twilio.com/en-us/products/email-api/pricing) e
[autenticação SendGrid](https://www.twilio.com/docs/sendgrid/ui/account-and-settings/how-to-set-up-domain-authentication).

Application define `IPasswordRecoveryEmailSender`; Infrastructure implementa Resend.
O envio usa endpoint HTTPS fixo, timeout de oito segundos, sem seguir redirects,
sem ler/logar resposta privada e sem retries automáticos. Não foi adicionado pacote.
O worker fica em Api; entidades/contratos permanecem em Domain/Application.

## Configurações privadas do backend e DNS

O arquivo versionado contém somente `Enabled=false` e `Transport=Resend`. Para
habilitar futuramente, o operador deve configurar no App Service/Key Vault ou outro
provider privado autorizado, sem colocar valores em comandos compartilhados:

| Chave de ambiente | Valor/finalidade |
| --- | --- |
| PasswordRecovery__Enabled | true somente após schema, domínio e teste de envio revisados |
| PasswordRecovery__Transport | Resend em produção |
| PasswordRecovery__ApiKey | API key privada com permissão restrita de envio ao domínio necessário |
| PasswordRecovery__From | Um endereço simples no domínio próprio verificado, sem nome de exibição; exemplo: acesso@sentra.uno |
| PasswordRecovery__RateLimitKey | Segredo independente gerado com CSPRNG, ao menos 32 caracteres, igual em todas as instâncias |
| Cors__AllowedOrigins__N | Conferir que a configuração privada efetiva inclui exatamente https://sentra.uno, preservando as demais origens autorizadas |

Não reutilizar Jwt:Key para rate limiting. Rotacionar o segredo de limites reinicia
as chaves de contagem, não libera tokens já consumidos. As opções são validadas no
startup: provedor desconhecido, configuração habilitada incompleta e transporte
local fora de Development/Testing impedem inicialização. Demo não permite habilitar.
`LocalTestPasswordRecoverySender` mantém no máximo 20 mensagens sintéticas em memória,
sem arquivo de e-mail, console, endpoint de caixa de entrada ou envio externo.
Seu construtor também recusa Production/Staging/Demo, mesmo fora da validação de opções.

Preparação futura do domínio, **sem alteração DNS nesta etapa**:

- Escolher domínio/subdomínio remetente próprio, isolando reputação quando adequado;
  verificar individualmente o domínio escolhido no Resend.
- Copiar exatamente os registros fornecidos no painel: DKIM e SPF, com tipos
  TXT/MX/CNAME conforme o domínio/região. Não inventar seletores ou valores de chave.
- Publicar o MX de retorno/feedback indicado pelo provedor sem substituir os MX
  de recebimento corporativo da raiz. Conferir o Return-Path específico.
- Configurar DMARC e alinhamento com os envios legítimos existentes. Revisar SPF
  existente e seus limites em vez de criar registros conflitantes.
- Manter registros CNAME de verificação sem proxy. Aguardar validação no painel.
- Desabilitar **click/open tracking** para os e-mails de recuperação; impedir que
  um serviço de tracking registre/reescreva o link. Considerar TLS obrigatório e
  revisar retenção/acessos ao painel, pois o provedor processa destinatário e conteúdo.
- Validar recebimento, spam e cabeçalhos SPF/DKIM/DMARC em caixas autorizadas de
  homologação antes de habilitar em produção. API 2xx não garante inbox.

Procedimento oficial: [adicionar/verificar domínio](https://resend.com/docs/add-a-domain).
Recomendação de tracking para recuperação:
[domínios e tracking](https://resend.com/docs/dashboard/domains/introduction).
Nenhuma chave privada ou configuração de remetente é enviada ao frontend.
O appsettings versionado mantém somente origens locais de CORS, como antes desta
etapa. A configuração efetiva de produção não foi acessada: conferir o override
privado para sentra.uno e o domínio customizado/HTTPS do Static Web Apps durante
a homologação. Não liberar qualquer origem em produção para contornar falhas.

## Endpoints, respostas e proteção contra abuso

| Método/rota | Acesso | Resultado |
| --- | --- | --- |
| GET /api/auth/password-recovery | Público | enabled booleano, sem detalhes privados |
| POST /api/auth/password-recovery/request | Público | Email; 202 genérico para e-mails válidos, inclusive limite/fila/falha; 400 sintático/globalmente desabilitado |
| POST /api/auth/password-recovery/confirm | Público com token secreto | Token/NewPassword/ConfirmPassword; 204, ou 400 inválido/expirado/reutilizado, ou 429 |
| POST /api/auth/password-recovery/users/{userId} | Admin | 202, sem segredo; 400 conta inativa/feature desabilitada, 401/403, 404, 409 fila ocupada ou 429 |

As respostas normais do controller usam no-store; erros não carregam credenciais.
O frontend descarta mensagens
privadas de erro/retorno e apresenta texto local. A comparação de senhas tem mensagem
fixa, sem repetir o valor comparado; os DTOs também não revelam campos no ToString.
A página usa referrer policy
no-referrer; o fragmento não integra requisições HTTP. O backend não possui rota GET
de consumo que scanners de e-mail possam acionar automaticamente.

Limites persistidos e compartilhados no SQL, sob o bloqueio de administração:

- Até 3 solicitações por e-mail em janela de 15 minutos, compartilhadas entre
  fluxo público e administrativo, inclusive para e-mail inexistente.
- Após resolver a conta no worker, até 3 emissões por UserId na mesma duração,
  independentemente de variações equivalentes de endereço na collation SQL.
- Até 20 solicitações por origem em 15 minutos. O limite por origem é conferido
  antes de criar chave por conta, reduzindo crescimento sob abuso.
- Até 20 confirmações por origem em 15 minutos, contando tokens/senhas inválidos;
  a contagem é confirmada em transação anterior à validação/consumo.
- As chaves são HMAC-SHA256 com segredo privado e separação por finalidade.
  Não se persiste IP nem e-mail na tabela de limites. Limpeza remove até 200
  contadores com expiração anterior a um dia a cada operação.

Origem é `RemoteIpAddress` normalizado, sem confiar no X-Forwarded-For enviado
livremente pelo cliente. A topologia real do App Service/proxy deve ser conferida
em homologação: se a origem observada for um proxy compartilhado, o limite será
conservador e compartilhado. Só configurar encaminhamento com proxies confiáveis
conhecidos após revisão; não aceitar headers arbitrários para contornar os limites.

## Migration, SQL e compatibilidade

Nova migration **20261024234039_AddPasswordRecovery**, aditiva, posterior à preservada
**20261024223405_AddUserAdministration**:

- `PasswordResetTokens`: FK restritiva Users, índice único por UserId e por hash,
  hash varchar(64), AuthVersion positivo, expiração, Delivered e InvalidatedAt.
- `PasswordRecoveryLimits`: chave HMAC varchar(64), Count e ExpiresAt indexada.
- Não altera hashes BCrypt, cargos, CPF, identidades ou dados operacionais existentes.
  Nenhum token válido ou histórico artificial é criado pela migration.

O [SQL incremental revisado](../sql/20261024234039-recuperacao-senha-email.sql) começa
em `20261023224842_AddMaintenanceScheduling`, inclui as **duas** migrations pendentes
na ordem correta e é idempotente. Exige baseline registrada, usa `XACT_ABORT` e
`:ON ERROR EXIT`. Operador deve usar SQLCMD com `-b` ou SSMS em SQLCMD Mode, com
parada ao primeiro erro. Um erro na segunda migration pode deixar a primeira já
confirmada: conferir History e reaplicar o script idempotente depois da correção;
não reverter destrutivamente para "zerar" o banco.

O SQL anterior da Etapa 03 não foi alterado. Não executar Down: ele remove dados
de recuperação/auditoria. Geração usou factory design-time, sem iniciar a API ou ler
secrets. EF CLI 10.0.10 avisou que o runtime é 10.0.13; script, snapshot e execução
SQL real foram conferidos. Não se aplicou migration em banco antigo ou Azure.

O código atual exige o schema novo para recuperação. Publicação segura continua
**schema → API → frontend**; não há fallback para autenticação sem AuthVersion.
O impacto inicial sobre usuários/JWTs da Etapa 03 continua: contas preexistentes
recebem ativo/versão/revisão padrão e tokens legados sem auth_version exigem login.

## Plano ordenado de atualização do Azure

Nada deste procedimento foi executado remotamente:

1. Revisar diff/SQL e ensaiar homologação. Confirmar banco/baseline, recuperação
   testada/backup/PITR e Admin válido. Planejar manutenção sem réplicas antigas de
   autenticação; a API anterior à Etapa 03 ignora revogação e estado ativo.
2. Manter as novas variáveis de liberação ausentes. Não habilitar PasswordRecovery
   na produção antiga nem misturar versões de API que ignoram as regras novas.
3. Responsável autorizado aplica o SQL incremental no destino conferido, com
   parada ao primeiro erro. Conferir sete migrations, FKs/índices, tabelas novas,
   hashes/contas preservados e pelo menos um Admin ativo quando já havia usuários.
4. Configurar privadamente Resend/From/RateLimitKey; preparar domínio/DNS e segredos
   fora do Git. Validar transporte em homologação autorizada. Em produção manter
   `Enabled=false` até a ativação e o ensaio controlado aprovados pelo responsável.
5. Preservar `SENTRA_USERS_SCHEMA_VERSION=20261024223405_AddUserAdministration`.
   Após confirmar o schema novo, declarar também
   `SENTRA_PASSWORD_RECOVERY_SCHEMA_VERSION=20261024234039_AddPasswordRecovery`.
   Só então publicar o backend de main. Não há migration no workflow.
6. Validar API nova, bootstrap apenas em banco vazio quando pertinente, login,
   JWTs revogados, Admin exclusivo, limites e os fluxos existentes. A ativação
   controlada do envio depende das configurações/DNS e das caixas de teste privadas.
7. Preservar `SENTRA_USERS_API_VERSION=20261024223405_AddUserAdministration`.
   Após validar API, declarar
   `SENTRA_PASSWORD_RECOVERY_API_VERSION=20261024234039_AddPasswordRecovery` e
   publicar frontend de main. O frontend não tem chave do provedor.
8. Conferir sentra.uno HTTPS, link recebido, redefinição única, login novo e rejeição
   do JWT antigo; conferir spam/cabeçalhos e auditoria sem credenciais. Reabrir
   tráfego somente com todas as instâncias atualizadas e regras verificadas.

Workflows mantêm os bloqueios anteriores e acrescentam condições **AND**, além de
main/eventos/concurrency/secrets/recursos/URLs/caminhos existentes. Variáveis são
declarações operacionais do responsável, não verificação remota automática do SQL.
Não foram definidas no GitHub. Para incidente de envio, desabilitar o recurso e
investigar privadamente; para falha de rollout, manter manutenção e seguir plano
revisado de recuperação. Não fazer rollback isolado para API que aceita JWT legado,
nem apagar tabelas/histórico. Tokens já emitidos expiram em 15 minutos e não podem
ser consumidos enquanto o recurso estiver desabilitado.

## Testes e revisão

Resultados finais da suíte completa:
Os 33 novos testes backend cobrem público existente/inexistente/inativo, hash,
expiração, repetição, substituição, concorrência, mudança de acesso, nova senha,
BCrypt UTF-8, entrega falha, limites por conta/origem, falsificação de forwarding,
Admin exclusivo, endereço cadastrado, auditoria e transporte/configuração segura,
incluindo ausência de senha/token/e-mail em mensagens de validação e ToString.
Contadores antigos foram testados na limpeza/reutilização da mesma transação para
garantir que a primeira solicitação da nova janela também seja contabilizada.
O inventário de actions públicas passou a permitir explicitamente as três actions
de recuperação, mantendo a exigência de autorização em todas as demais.

| Verificação | Resultado |
| --- | --- |
| Backend | **368 aprovados**, zero falhas/ignorados; base de 335 preservada |
| Frontend | **197 aprovados**, base de 186 preservada |
| Build solução .NET | Aprovado, zero avisos/erros após restauração autorizada das dependências |
| ESLint e build Vite | Aprovados |
| Actionlint | Aprovado; proteções anteriores preservadas, duas condições adicionais e 72 combinações de branch/evento/liberação conferidas |
| SQL real | Duas migrations aplicadas a baseline em bancos novos e script repetido; sete migrations, snapshot, conta/hash preservados, consumo concorrente único, rollback conjunto e reutilização de contadores antigos aprovados |
| Chrome + SQL + transporte local | Solicitação, resposta genérica, confirmação, rejeição de reutilização, login novo, envio administrativo confirmado, histórico e segredo ausente de URL HTTP/storage aprovados |
| Responsividade e rede | Solicitação e redefinição em 320/390/768/1440 px sem overflow; erro de rede tratado, senhas limpas; sem exceções JavaScript |

Evidências locais ignoradas em `artifacts/recuperacao-senha-email/`: logs de
build/testes/lint, `sql-resultados.json`, `browser-resultados.json`,
`browser-layout-resultados.json` e capturas de tela. O transporte testado no
navegador foi o local; o contrato HTTP Resend foi testado com handler controlado,
sem contato com o provedor. Nenhuma senha ou token real foi produzido/exportado
para o relatório. O acesso do navegador à mensagem sintética do ensaio SQL usou
IPC privado do harness, sem endpoint da aplicação nem arquivo de token.

## Limitações e riscos remanescentes

- **Configuração externa pendente:** chaves, domínio/DNS, configuração App Service,
  envio real e entregabilidade não foram alterados/testados. O recurso permanece
  desligado por padrão. Só habilitar após homologação privada controlada.
- Fila em memória tem limite e não é durável; reinício/saturação pode perder
  solicitações. O usuário recebe orientação genérica para aguardar e solicitar
  novamente. Não se persiste segredo de envio em outbox; uma fila durável com
  criptografia/retentativa seria evolução separada, se o volume exigir.
- O provedor pode aceitar e depois rejeitar/quarentenar. Não há webhook de bounce
  ou confirmação de inbox nesta etapa. Não se afirma entrega final pelo status 202.
- O token só é marcado Delivered após aceite do transporte. Falha/timeout o
  invalida; interrupção entre envio e confirmação deixa-o não utilizável. Reemitir
  substitui o anterior. Link nunca foi comprovante de identidade fora da conta
  destinatária cadastrada, cuja segurança continua essencial.
- Limitação SQL é compartilhada, mas usa um bloqueio global administrativo curto;
  volume elevado requer medição e proteção de borda. O envio de rede ocorre fora
  do bloqueio. Muitos IPs/contas ainda podem esgotar a cota do provedor.
- Contadores HMAC evitam dados pessoais diretos; proteger o segredo contra ataques
  de dicionário, conferir retenção/limpeza sob abuso e revisar proxies confiáveis.
- Uma requisição já autorizada pode concluir durante uma revogação; mutações de
  conta e consumo revalidam estado/versão sob lock. Escritas externas que ignorem
  esse contrato não têm as mesmas garantias.
- JWT permanece em sessionStorage como antes. Segredo do link transita pelo
  provedor e pelo cliente de e-mail; revisar acesso/retenção/tracking externos.
  Não adicionar analytics de URL/corpo ou logs sensíveis de EF/HTTP nesta rota.
- Não houve migração para Identity, redesenho de login/sidebar, troca de BCrypt,
  senha temporária, autoelevação pública ou ampliação dos cinco cargos.

## Instruções objetivas de teste manual

1. Em ambiente **novo local ou homologação explicitamente autorizado**, preparar
   schema atual conforme README; manter Simulation desabilitada. Não usar conexões
   Azure copiadas para ensaios. Provisionar Admin pelo mecanismo privado existente.
2. Para testar Resend, configurar somente no backend chave/remetente/segredo de
   limites e domínio verificado; usar caixa própria autorizada. LocalTest é para
   testes por DI/debugger, sem caixa HTTP/console pública e sem entrega externa.
3. Abrir login → Esqueci minha senha. Testar e-mails existente, inexistente e
   inativo; mensagem deve ser idêntica. Conta inativa/inexistente não recebe link.
4. Abrir link do e-mail. Conferir HTTPS sentra.uno e remoção do token da barra de
   endereço; não compartilhar link/captura de network com corpo. Confirmar senha
   forte e confirmação iguais. Aguardar mensagem e voltar ao login.
5. Senha antiga deve falhar; senha nova funciona. JWT anterior deve retornar 401.
   Reabrir link consumido/expirado/substituído deve falhar sem alterar a senha.
6. Como Admin, abrir Usuários → conta ativa → Enviar redefinição → Confirmar envio.
   Conferir solicitação/histórico; Admin não recebe link/segredo. Até confirmação
   pelo titular, a senha antiga continua válida. Gestor não tem botão/permissão.
7. Solicitar repetidamente: público preserva mensagem; administrativo/confirmar
   podem retornar 429. Não alterar código/proxy para contornar os limites.
8. Conferir 320/390/768/1440 px e falha de rede. Sem configuração habilitada, telas
   informam indisponibilidade e o botão administrativo permanece desabilitado.

## Arquivos criados/modificados nesta etapa

Lista abaixo complementa as alterações locais da Etapa 03, que permanecem sem commit.
Nenhum arquivo do usuário foi removido. Ferramentas/evidências em artifacts são
locais e ignoradas pelo Git.

Criados:

- `backend/Api/Controllers/PasswordRecoveryController.cs`
- `backend/Api/Security/PasswordRecoveryRegistration.cs`
- `backend/Api/Workers/PasswordRecoveryWorker.cs`
- `backend/Application/DTOs/Auth/PasswordRecoveryDtos.cs`
- `backend/Application/Interfaces/IServices/IPasswordRecoveryDelivery.cs`
- `backend/Application/Interfaces/IServices/IPasswordRecoveryService.cs`
- `backend/Application/Security/PasswordRecoverySettings.cs`
- `backend/Application/Services/PasswordRecoveryService.cs`
- `backend/Application/Validators/PasswordRecoveryValidators.cs`
- `backend/Domain/Entities/PasswordRecoveryLimit.cs`
- `backend/Domain/Entities/PasswordResetToken.cs`
- `backend/Domain/Exceptions/RateLimitException.cs`
- `backend/Domain/IRepositories/IPasswordRecoveryRepository.cs`
- `backend/Infrastructure/Data/Configurations/PasswordRecoveryConfiguration.cs`
- `backend/Infrastructure/Data/Migrations/20261024234039_AddPasswordRecovery.cs`
- `backend/Infrastructure/Data/Migrations/20261024234039_AddPasswordRecovery.Designer.cs`
- `backend/Infrastructure/Email/LocalTestPasswordRecoverySender.cs`
- `backend/Infrastructure/Email/PasswordRecoveryQueue.cs`
- `backend/Infrastructure/Email/ResendPasswordRecoverySender.cs`
- `backend/Infrastructure/Repositories/PasswordRecoveryRepository.cs`
- `backend/Tests/PasswordRecoveryTests.cs`
- `docs/reports/recuperacao-senha-email.md`
- `docs/sql/20261024234039-recuperacao-senha-email.sql`
- `frontend/src/components/auth/PasswordRecoveryForm.jsx`
- `frontend/src/services/passwordRecovery.js`
- `frontend/tests/passwordRecovery.test.js`

Modificados:

- `.github/workflows/azure-static-web-apps-agreeable-desert-08bba710f.yml`
- `.github/workflows/main_sentra-api.yml`
- `README.md`
- `backend/Api/Middleware/ExceptionHandlingMiddleware.cs`
- `backend/Api/Program.cs`
- `backend/Api/appsettings.json`
- `backend/Application/DependencyInjection.cs`
- `backend/Infrastructure/Data/AppDbContext.cs`
- `backend/Infrastructure/Data/Migrations/AppDbContextModelSnapshot.cs`
- `backend/Infrastructure/Demo/DemoEvaluationSchema.cs`
- `backend/Infrastructure/DependencyInjection.cs`
- `backend/Tests/AuthenticationCompatibilityTests.cs`
- `backend/Tests/Support/EquipmentTestHost.cs`
- `docs/README.md`
- `docs/demo/preparacao-ambiente.md`
- `frontend/README.md`
- `frontend/index.html`
- `frontend/src/App.jsx`
- `frontend/src/components/LoginForm.jsx`
- `frontend/src/components/auth/AuthLayout.css`
- `frontend/src/pages/UserPage.jsx`
- `frontend/src/services/users.js`
