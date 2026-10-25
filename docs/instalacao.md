# Instalação e execução do S.E.N.T.R.A.

Este guia explica como executar o S.E.N.T.R.A. no seu computador, desde a instalação das dependências até o primeiro login.

Você não precisa configurar o Azure nem ter uma conta no Resend para executar o projeto localmente.

**Quer conhecer o sistema com dados já cadastrados?** Consulte o [guia da DEMO](demo/preparacao-ambiente.md), que utiliza um banco isolado com informações fictícias.

## 1. O que preciso instalar?

Antes de começar, instale estas ferramentas:

| Ferramenta | Para que serve |
| --- | --- |
| [.NET SDK 10](https://dotnet.microsoft.com/download/dotnet/10.0) | Executar o backend |
| [Node.js](https://nodejs.org/) | Executar o frontend React (utilize uma versão compatível com Vite 8) |
| [SQL Server Express LocalDB](https://learn.microsoft.com/en-us/sql/database-engine/configure-windows/sql-server-express-localdb) | Armazenar os dados localmente |
| [Git](https://git-scm.com/) | Baixar o projeto |

Este tutorial utiliza **Windows e PowerShell**.

Também precisaremos do Entity Framework CLI. Abra o PowerShell e execute:

```powershell
dotnet tool install --global dotnet-ef --version "10.*"
```

Se a ferramenta já estiver instalada, não é necessário instalá-la novamente. Verifique com `dotnet ef --version`.

## 2. Baixar o projeto

Abra o PowerShell e execute:

```powershell
git clone https://github.com/yBreno/S.E.N.T.R.A.git
cd S.E.N.T.R.A
```

Agora instale as dependências do backend e do frontend:

```powershell
dotnet restore backend/Solution/Solution.slnx
dotnet build backend/Solution/Solution.slnx
npm.cmd ci --prefix frontend
```

Pronto! O código está preparado.

## 3. Criar o banco de dados

O S.E.N.T.R.A. utiliza SQL Server.

Para facilitar, vamos criar automaticamente um banco novo no LocalDB, sem modificar outros bancos existentes no computador.

**No mesmo PowerShell**, execute:

```powershell
$database = 'SENTRA_Local_' + [Guid]::NewGuid().ToString('N')

$connection = 'Server=(localdb)\MSSQLLocalDB;Database=' + $database + ';Integrated Security=true;TrustServerCertificate=true'

dotnet ef database update --project backend/Infrastructure --startup-project backend/Api --connection $connection
```

Esse último comando cria a estrutura do banco utilizando as sete migrations existentes no projeto.

Não é necessário criar tabelas manualmente nem gerar novas migrations.

**Importante:** mantenha esse terminal aberto. Vamos utilizar a mesma conexão no próximo passo.

## 4. Configurar e iniciar o backend

Ainda no mesmo PowerShell, copie este bloco:

```powershell
$env:DOTNET_ENVIRONMENT = 'Testing'
$env:ASPNETCORE_ENVIRONMENT = 'Testing'

$env:ConnectionStrings__DefaultConnection = $connection

$env:Jwt__Issuer = 'sentra-local'
$env:Jwt__Audience = 'sentra-react-local'

$bytes = New-Object byte[] 32
$rng = [Security.Cryptography.RandomNumberGenerator]::Create()
$rng.GetBytes($bytes)
$rng.Dispose()

$env:Jwt__Key = [Convert]::ToBase64String($bytes)

$env:Cors__AllowedOrigins__0 = 'http://127.0.0.1:5173'

$env:Simulation__Enabled = 'false'
$env:PredictiveAutomation__Enabled = 'false'
$env:PasswordRecovery__Enabled = 'false'
$env:PublicRegistration__AllowAgentSelfRegistration = 'false'
```

Essas configurações conectam a API ao banco local, criam uma chave de autenticação para este ambiente e desabilitam integrações automáticas desnecessárias.

Não é preciso editar arquivos de configuração.

### Criar o primeiro administrador

Como o banco está vazio, precisamos criar uma conta para entrar no sistema.

No mesmo terminal, execute:

```powershell
$env:SeedAdmin__Enabled = 'true'

$env:SeedAdmin__Name = Read-Host 'Nome do administrador'
$env:SeedAdmin__Email = Read-Host 'E-mail do administrador'
$env:SeedAdmin__Cpf = Read-Host 'CPF valido do administrador'
```

Agora defina a senha:

```powershell
$senha = Read-Host 'Crie uma senha forte' -AsSecureString
$ptr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($senha)

try {
    $env:SeedAdmin__Password = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($ptr)
}
finally {
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($ptr)
}
```

A senha precisa ter pelo menos oito caracteres, incluindo letra maiúscula, minúscula, número e caractere especial, respeitando o limite de 72 bytes do BCrypt.

**Guarde o e-mail e a senha que você escolheu.** Eles serão utilizados no login.

### Iniciar a API

Execute:

```powershell
dotnet run --project backend/Api/Api.csproj --no-build --no-restore --no-launch-profile -- --urls http://127.0.0.1:5129
```

Na primeira inicialização, a aplicação provisiona o administrador no banco vazio.

Após confirmar a criação da conta, pressione `Ctrl + C` para interromper a API.

Por segurança, desative a criação automática do administrador e remova os dados temporários:

```powershell
$env:SeedAdmin__Enabled = 'false'

'SeedAdmin__Password','SeedAdmin__Cpf','SeedAdmin__Email','SeedAdmin__Name' |
    ForEach-Object { Remove-Item "Env:$_" -ErrorAction SilentlyContinue }

$senha = $null
```

Agora inicie novamente:

```powershell
dotnet run --project backend/Api/Api.csproj --no-build --no-restore --no-launch-profile -- --urls http://127.0.0.1:5129
```

**Deixe esse terminal aberto.** Ele está executando o backend.

## 5. Iniciar o frontend

Abra **um segundo PowerShell**, sem fechar o primeiro.

Entre na pasta do projeto, caso necessário, e execute na raiz:

```powershell
$env:VITE_API_BASE_URL = 'http://127.0.0.1:5129'
$env:VITE_DEMO_MODE = 'false'

npm.cmd --prefix frontend run dev -- --host 127.0.0.1 --port 5173 --strictPort
```

Agora abra o navegador:

**http://127.0.0.1:5173**

A tela de login do S.E.N.T.R.A. deverá aparecer.

Utilize o e-mail e a senha do administrador criados no passo anterior.

**Pronto! O sistema está rodando localmente.**

Como o banco é novo, o inventário estará inicialmente vazio. Você poderá cadastrar equipamentos, agentes e outras informações utilizando a interface.

## 6. Como executar os testes?

Se quiser verificar o funcionamento do projeto, abra outro terminal na raiz do repositório e execute:

**Backend:**

```powershell
dotnet test backend/Tests/Tests.csproj
```

**Frontend:**

```powershell
node --test frontend/tests/*.test.js
npm.cmd --prefix frontend run lint
npm.cmd --prefix frontend run build
```

Na validação realizada em 25/10/2026, o projeto passou em **591 testes automatizados**, sendo 374 no backend e 217 no frontend.

## 7. Problemas comuns

| Problema | O que verificar |
| --- | --- |
| Erro ao criar o banco | Confira se o SQL Server LocalDB está instalado e se o comando `dotnet ef` funciona. |
| Frontend não abre | Confira se o segundo terminal está executando o Vite na porta 5173. |
| Frontend abre, mas não conecta | Verifique se a API está em execução na porta 5129. |
| Não consigo fazer login | Utilize a conta criada no passo 4 e confirme que o primeiro provisionamento foi concluído. |
| Algumas telas não aparecem | O S.E.N.T.R.A. exibe os módulos conforme as permissões da conta. |
| Recuperação de senha indisponível | O envio de e-mails está intencionalmente desativado neste ambiente local. |

## 8. Encerrar a aplicação

Para desligar o S.E.N.T.R.A., pressione `Ctrl + C` nos terminais do backend e frontend.

O banco local continuará salvo para a próxima execução. Se fechar o PowerShell, precisará configurar novamente as variáveis de ambiente e informar a conexão com o mesmo banco para reutilizá-lo. **Não crie um novo banco se quiser conservar os dados anteriores.**

---

## Quer conhecer mais sobre o projeto?

- [README principal](../README.md) — funcionalidades e tecnologias.
- [Arquitetura do S.E.N.T.R.A.](arquitetura.md) — organização técnica do sistema.
- [Guia da DEMO](demo/preparacao-ambiente.md) — ambiente isolado com dados fictícios.
- [Frontend](../frontend/README.md) — configuração e desenvolvimento da interface.
