# S.E.N.T.R.A. — Etapa 7.4: preparação da API HTTP DEMO

## Limite desta execução

Foram alterados código, configuração sem segredos, testes e documentação. A API não foi
iniciada. Não houve conexão ao Azure, execução de seeders/migrations, conexão ao SQL Server
ou criação de usuário persistente. Todos os testes de persistência desta etapa usam bancos
InMemory novos, com contas estritamente fictícias de teste. As evidências SQL da Etapa 7.3
continuam sendo distintas dos testes desta preparação.

O banco `SENTRA_DEMO_Stage73` conserva os 265 registros e zero usuários conforme o estado
validado na etapa anterior; nenhuma rotina desta etapa foi executada contra esse banco.

## Inspeção do startup existente

`WebApplication.CreateBuilder` seleciona configuração e ambiente pelos mecanismos .NET:
appsettings gerais, arquivo do ambiente, user-secrets automáticos em Development, variáveis
de ambiente e argumentos. Os perfis existentes em launchSettings selecionam Development;
por isso a execução futura usa obrigatoriamente `--no-launch-profile` e ambiente Demo explícito.

- `AddApplication` registra serviços, validadores, BCryptPasswordHasher e JwtTokenService.
- `AddInfrastructure` registra AppDbContext SQL Server e repositórios a partir de
  `ConnectionStrings:DefaultConnection`; a simples inscrição não abre conexão.
- O startup original registra TelemetrySimulationWorker em todos os ambientes. O padrão
  Simulation:Enabled é true; o ciclo começa após cinco segundos, grava telemetria, pode
  mudar MaintenanceNeed e atualiza localização/histórico de agentes através dos serviços.
- Em Development, o startup chama AdminSeeder e PredictiveSeeder. O primeiro executa
  SaveChanges para o primeiro usuário quando configurado. O segundo insere dados operacionais,
  executa SaveChanges e análise preditiva, que também pode gravar recomendações/alertas.
- Fora de Development, AdminSeeder ainda pode ser chamado se SeedAdmin:Enabled for true.
- Não havia chamada a Migrate/EnsureCreated no Program.cs. Isso permanece assim.
- Login valida a senha pelo hash, consulta Users e emite JWT; não executa SaveChanges.
- Endpoints de consultas operacionais usam serviços de leitura. Rotas de criação/edição,
  análise preditiva manual e telemetria possuem operações de persistência; não são necessárias
  para esta demonstração HTTP e ficam bloqueadas apenas no host Demo.

## Configuração DEMO preparada

| Item | Configuração |
| --- | --- |
| Ambiente | Demo; IsDevelopment e IsProduction são false |
| Banco único | SENTRA_DEMO_Stage73 |
| Instância única | (localdb)\MSSQLLocalDB |
| Conexão | SENTRA_DEMO_CONNECTION_STRING externa, Windows integrada, sem fallback |
| HTTP | http://127.0.0.1:5180, somente loopback IPv4 |
| CORS | http://localhost:5173 e http://127.0.0.1:5173 |
| Simulation | false; worker nem sequer registrado no host Demo |
| SeedAdmin | false; nenhum seeder chamado no host Demo |
| Migrations | nunca executadas automaticamente |
| JWT | chave aleatória exclusiva; issuer sentra-demo-local; audience sentra-demo-react-local; 30 minutos |
| Escritas HTTP | bloqueadas com 405, exceto POST /api/auth/login, que é somente leitura no banco |
| Persistência HTTP | SaveChanges e SaveChangesAsync bloqueados por interceptor exclusivo do host Demo |

`appsettings.Demo.json` contém somente flags e URL local, sem connection string, senha ou
chave JWT. `DemoApiConfiguration.Prepare` valida antes da inscrição de Infrastructure:
recusa conexão ausente, remota, outro banco, autenticação SQL ou nome diferente do confirmado.
Não usa a conexão geral como fallback. Depois aplica a conexão e JWT dedicados como provider
final de configuração. Simulation ou SeedAdmin habilitados causam falha antecipada.
Variáveis dedicadas DEMO em outro ambiente também causam recusa, evitando fallback acidental.

São recusados URLs diferentes, listeners adicionais configurados em Kestrel e HTTP_PORTS/
HTTPS_PORTS. A URL é fixada em loopback. Somente neste modo não há redirecionamento HTTPS:
trata-se de HTTP local, sem publicar listener na rede. CORS não permite origem arbitrária.

O modo Demo é uma ramificação explícita. Sem sua ativação/variáveis dedicadas, o comportamento
dos outros ambientes permanece igual ao anterior. Nada foi publicado ou configurado no Azure.

## Contratos e autorização

Controllers, DTOs, papéis e regras de autorização existentes não foram alterados.
POST /api/auth/login mantém a resposta `{ token, expiresAt, user }`. GETs continuam exigindo
JWT e os papéis existentes. A API Demo tem uma restrição adicional intencional de somente
leitura: POST de cadastro/alteração, PUT, PATCH e DELETE são recusados antes dos controllers.
Isso não modifica os endpoints nos ambientes normais.

O interceptor protege os caminhos SaveChanges usados pelos serviços existentes. Não representa
uma alteração das permissões SQL do usuário Windows nem um mecanismo genérico de bloqueio
de SQL bruto. Não existem chamadas de escrita SQL bruta nas consultas HTTP auditadas.
O provisionador separado não usa esse interceptor, pois sua única escrita futura exige
autorização explícita. Nenhuma chave, senha, hash ou JWT é impresso pelos novos comandos.

## Admin DEMO: comando preparado, não executado

`backend/DemoAdmin` é uma CLI separada, sem referência à API e sem execução no startup.
Ela exige ambiente Demo, confirmação exata e conexão dedicada validada para Stage73.
Confirma também DB_NAME e IsLocalDB pelo SQL Server, sem aplicar migrations.

Conta planejada: **DEMO Administrador**, login **admin.demo@sentra.invalid**, papel **Admin**.
Documento gerado algoritmicamente para atender à validação existente, sem consulta a pessoas
reais. Uma sequência sintética não tem garantia cadastral de estar desocupada; seu uso fica
restrito a este banco isolado, sem integrações externas.

A senha será fornecida pelo próprio usuário, preferencialmente no prompt sem eco e com
confirmação. A alternativa é a variável privada temporária SENTRA_DEMO_ADMIN_PASSWORD;
não colocar o valor no comando, histórico, relatório ou arquivos versionados. Se usar a
alternativa, remova também a variável do terminal pai após o comando; o filho só consegue
limpar sua própria cópia. Não reutilizar senhas do Azure.

A validação reutiliza RegisterDtoValidator: ao menos oito caracteres, maiúscula, minúscula,
número e caractere especial. Há limite adicional de 72 bytes UTF-8 para evitar truncamento
de senha no BCrypt. O hash usa BCryptPasswordHasher existente, custo 11, com salt automático.

Uma transação Serializable exige Users vazio. A única inserção prevista é uma linha User
com papel Admin. Se qualquer usuário já existir, o comando recusa: não duplica, não promove,
não redefine senha nem sobrescreve registros. Não associa os agentes existentes a essa conta.
Não registra conta por HTTP nem altera regras de elevação de papel do AuthService.

## Comandos futuros exatos — não executados nesta etapa

Na raiz do repositório, em terminal dedicado, configure **privadamente**
SENTRA_DEMO_CONNECTION_STRING para o destino aprovado acima, com autenticação Windows
integrada. Não copie a conexão da API atual. Nenhum comando usa fallback de produção.

Após autorização para a única inserção de usuário:

```powershell
$env:DOTNET_ENVIRONMENT = 'Demo'
$env:ASPNETCORE_ENVIRONMENT = 'Demo'
dotnet run --project backend/DemoAdmin/DemoAdmin.csproj --no-build --no-launch-profile -- --create-admin --confirm SENTRA_DEMO_Stage73
```

O terminal pedirá a senha sem exibi-la. Não execute AdminSeeder, /api/auth/register ou
scripts de hash temporários para este bootstrap.

Após autorização para iniciar o listener HTTP local:

```powershell
& .\scripts\Start-DemoApi.ps1 -ConfirmStart
```

Se a política de execução bloquear o script, interrompa e informe o bloqueio. Não altere
nem desabilite a política de execução para facilitar esta etapa.

O launcher gera 32 bytes aleatórios com CSPRNG e entrega a chave JWT em variável de ambiente
apenas ao processo filho. Não imprime nem grava a chave. Usa --no-launch-profile e fixa
ambiente/URL Demo; ao terminar, restaura o ambiente do processo. Reiniciar gera outra chave
e exige novo login. Não usa user-secrets ou chaves do Azure.

O comando .NET que o launcher executará é:

```powershell
# Dentro de backend/Api, com ambiente e chave temporária preparados pelo launcher.
dotnet run --project Api.csproj --no-build --no-launch-profile -- --environment Demo --urls http://127.0.0.1:5180
```

Para testar React futuramente sem editar frontend/.env.local, poderá ser usado um override
somente no processo Vite: `VITE_API_BASE_URL=http://127.0.0.1:5180`. Isso não foi executado.
Nenhum frontend será direcionado automaticamente para este host nesta etapa.

## Testes e limitações

Build da solução aprovado, sem warnings ou erros. **112 testes backend aprovados**,
zero falhas e zero ignorados: 72 anteriores e 40 novos casos desta preparação.
Sintaxe do launcher verificada pelo parser PowerShell sem execução do script.

Os testes não chamam WebApplication.Run, Host.Start, LocalDB, provisionador CLI ou launcher.
Exercitam configuração pura, inscrição de serviços sem abrir conexão, contextos HTTP
simulados e bancos InMemory isolados. Verificam rejeição de destino/ambiente/listener
indevidos, ausência do worker, exclusão dos seeders, bloqueios de persistência e métodos,
provisionamento fictício único, preservação dos 265 registros de fixture e login existente
sem SaveChanges. Tokens de teste ficam somente na memória, com rejeição de chave diferente.

Antes de qualquer execução real, solicitar aprovação explícita para:

1. Uma inserção de Admin em Users no banco Stage73, com senha escolhida privadamente.
2. Iniciar a API em loopback com as proteções Demo e realizar login/GETs autenticados.

Após autorização, conferir novamente destino, zero usuários e fingerprint operacional;
criar a conta uma vez; iniciar a API; confirmar contagens, 401/403 conforme aplicável, CORS e
preservação dos registros. Não iniciar migrations, carga DEMO, seeders ou simulação novamente.
