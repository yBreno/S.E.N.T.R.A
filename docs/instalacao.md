# Instalação e configuração

## Pré-requisitos

- .NET 10 SDK e ferramenta `dotnet-ef` compatível com o EF Core 10 do projeto.
- Node compatível com Vite 8: 22.12+ na linha 22, ou outra versão suportada pelo Vite.
- SQL Server/LocalDB para executar a aplicação; testes padrão usam InMemory.
- Dependências restauradas a partir dos projetos e de `frontend/package-lock.json`.

Na raiz do repositório, em PowerShell:

```powershell
dotnet restore backend/Solution/Solution.slnx
dotnet build backend/Solution/Solution.slnx --no-restore
npm.cmd ci --prefix frontend
```

## Banco local novo

Utilizar um destino local exclusivo. O exemplo gera um nome novo; conferir a
conexão antes da execução. Não copiar connection strings do Azure para ensaios.
Não reutilizar um nome de banco do usuário para esta preparação.

```powershell
$sentraLocalDb = 'SENTRA_Dev_' + [Guid]::NewGuid().ToString('N')
$sentraLocalConnection = 'Server=(localdb)\MSSQLLocalDB;Database=' + $sentraLocalDb + ';Integrated Security=true;TrustServerCertificate=true'
dotnet ef database update --project backend/Infrastructure --startup-project backend/Api --connection $sentraLocalConnection
```

São sete migrations oficiais. A factory design-time não inicia a API, seeders ou
workers. A conexão explícita é obrigatória neste roteiro: o destino de metadados
da factory não é o banco a ser usado pela aplicação.

Para banco antigo, revisar sua baseline e usar o [roteiro de SQL](sql/README-query-editor.md).
O incremento fornecido começa em AddMaintenanceScheduling; ele não substitui a
criação de banco vazio com as sete migrations. A API não migra schema no startup.

## Backend e primeiro Admin

Usar um terminal exclusivo e configuração privada de processo. `Testing` permite
um ensaio sem ler user-secrets de Development e sem iniciar os seeders operacionais
de Development. Os defaults de simulação e automação são desabilitados.

```powershell
$env:DOTNET_ENVIRONMENT = 'Testing'
$env:ASPNETCORE_ENVIRONMENT = 'Testing'
$env:ConnectionStrings__DefaultConnection = $sentraLocalConnection
$env:Jwt__Issuer = 'sentra-local'
$env:Jwt__Audience = 'sentra-react-local'
$sentraKeyBytes = New-Object byte[] 32
$sentraRng = [Security.Cryptography.RandomNumberGenerator]::Create()
$sentraRng.GetBytes($sentraKeyBytes)
$sentraRng.Dispose()
$env:Jwt__Key = [Convert]::ToBase64String($sentraKeyBytes)
$env:Cors__AllowedOrigins__0 = 'http://127.0.0.1:5173'
$env:Simulation__Enabled = 'false'
$env:PredictiveAutomation__Enabled = 'false'
$env:PasswordRecovery__Enabled = 'false'
$env:PublicRegistration__AllowAgentSelfRegistration = 'false'
```

Com Users vazio, definir privadamente `SeedAdmin__Enabled=true`, `SeedAdmin__Name`,
`SeedAdmin__Email`, `SeedAdmin__Cpf` válido e `SeedAdmin__Password` forte. Não há
credencial administrativa padrão. A senha segue os requisitos de complexidade e
o máximo de 72 bytes UTF-8 do BCrypt. Não colocar valores em arquivos versionados,
capturas ou comandos compartilhados.

Exemplo de entrada privada da senha no terminal, sem colocá-la no histórico:

```powershell
$sentraBootstrapSecret = Read-Host 'Senha inicial privada' -AsSecureString
$sentraBootstrapPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($sentraBootstrapSecret)
try { $env:SeedAdmin__Password = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($sentraBootstrapPointer) }
finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($sentraBootstrapPointer) }
```

Iniciar após configurar os demais campos:

```powershell
dotnet run --project backend/Api/Api.csproj --no-build --no-restore --no-launch-profile -- --urls http://127.0.0.1:5129
```

O bootstrap cria somente o primeiro Admin, com auditoria e bloqueio transacional.
Se já existir qualquer usuário, não promove, cria outra conta nem redefine senhas.
Confirmar o log de provisionamento, encerrar a API e remover a senha temporária do
ambiente antes de reiniciar:

```powershell
$env:SeedAdmin__Enabled = 'false'
Remove-Item Env:SeedAdmin__Password -ErrorAction SilentlyContinue
```

Em seguida, criar contas pelo módulo **Usuários**, exclusivo de Admin. Alterar cargo,
estado, senha ou e-mail exige novo login do titular; reativação não restaura JWT antigo.
Conta de login e categoria profissional são conceitos distintos.

## Frontend

Em outro terminal:

```powershell
$env:VITE_API_BASE_URL = 'http://127.0.0.1:5129'
$env:VITE_DEMO_MODE = 'false'
npm.cmd --prefix frontend run dev -- --host 127.0.0.1 --port 5173 --strictPort
```

Abrir `http://127.0.0.1:5173`. A URL da API não deve incluir `/api`, segredo, query
ou fragmento. Preservar qualquer `.env.local` existente; variáveis de processo
permitem ensaiar outra URL sem modificá-lo. Variáveis `VITE_*` são públicas.

## Variáveis e configurações

| Configuração | Uso |
| --- | --- |
| ConnectionStrings__DefaultConnection | SQL privado do ambiente correto |
| Jwt__Key / Jwt__Issuer / Jwt__Audience | Assinatura e destinatário; chave aleatória privada, mínimo 32 caracteres |
| Cors__AllowedOrigins__N | Lista explícita; produção deve incluir https://sentra.uno |
| Simulation__Enabled=false | Evitar gravação de telemetria/GPS simulados |
| PredictiveAutomation__Enabled=false | Manter somente análise manual |
| SeedAdmin__Enabled | Bootstrap temporário e explícito em banco vazio |
| PublicRegistration__AllowAgentSelfRegistration=false | Sem autoelevação/cadastro público em produção |
| PasswordRecovery__Enabled / Transport / ApiKey / From / RateLimitKey | Resend e limites privados do backend; [detalhes e DNS](reports/recuperacao-senha-email.md) |
| VITE_API_BASE_URL / VITE_DEMO_MODE | URL pública da API e identificação do frontend DEMO |

`LocalTest` de e-mail só é aceito em Development/Testing, em memória e sem caixa
HTTP pública. Testes recuperam a mensagem por DI; não se imprime token no console.
Os links reais têm destino fixo HTTPS sentra.uno. Não usar e-mail real para validar
um banco local nem colocar chave do provedor no navegador.

## Qualidade, DEMO e publicação

```powershell
dotnet build backend/Solution/Solution.slnx --no-restore
dotnet test backend/Tests/Tests.csproj --no-build --no-restore
node --test frontend/tests/*.test.js
npm.cmd --prefix frontend run lint
npm.cmd --prefix frontend run build
```

Para apresentar, utilizar o [DEMO isolado](demo/preparacao-ambiente.md) e o
[roteiro de 6–8 minutos](demo/roteiro-apresentacao.md).
Use somente `DemoData`, `DemoAdmin` e o inicializador evaluation atual.

Produção publica exclusivamente de main, com gates de schema/API e concurrency.
As sete migrations já aplicadas foram informadas pelo responsável; a Sprint 08
não exige migration nova. Confirmar schema/configuração no procedimento autorizado
antes de publicar. Não reaplicar SQL ou recriar dados apenas para atualizar código.

Não executar automaticamente commits, deploys, bootstrap ou cargas em produção.
