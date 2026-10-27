# Como executar a DEMO do S.E.N.T.R.A.

Este guia explica como executar uma demonstração completa do S.E.N.T.R.A. no seu computador, utilizando dados fictícios já preparados para apresentar as funcionalidades do projeto.

A DEMO inclui:

- **12 equipamentos urbanos**, com diferentes tipos e situações operacionais.
- **9 agentes**, distribuídos geograficamente.
- **9 ordens de manutenção**, em diferentes estágios.
- **2 recomendações preditivas pendentes**.
- **5 contas de acesso**, cada uma com um nível de permissão.
- **79 registros sintéticos**, preparados para os cenários de avaliação.

Tudo funciona em um **banco de dados local e isolado**. Não é necessário acessar o Azure, configurar o Resend ou utilizar dados reais.

## 1. Pré-requisitos

Este procedimento foi preparado para **Windows com PowerShell**.

Antes de começar, tenha instalado:

| Ferramenta | Para que serve |
| --- | --- |
| [.NET SDK 10](https://dotnet.microsoft.com/download/dotnet/10.0) | Executar o backend |
| [Node.js](https://nodejs.org/) | Executar o frontend React (utilize uma versão compatível com Vite 8) |
| [SQL Server Express LocalDB](https://learn.microsoft.com/en-us/sql/database-engine/configure-windows/sql-server-express-localdb) | Armazenar os dados localmente |
| [Git](https://git-scm.com/) | Baixar o projeto |

As portas `5180` (API) e `5173` (frontend) precisam estar disponíveis.

## 2. Baixar e preparar o projeto

Abra o PowerShell e execute:

```powershell
git clone https://github.com/yBreno/S.E.N.T.R.A.git
cd S.E.N.T.R.A
```

Se você já baixou o projeto, basta abrir o PowerShell na pasta raiz.

Agora prepare o backend e o frontend:

```powershell
dotnet restore backend/Solution/Solution.slnx
dotnet build backend/Solution/Solution.slnx --no-restore
npm.cmd ci --prefix frontend
```

Esses comandos instalam as dependências e compilam o projeto.

**Mantenha esse terminal aberto.** Vamos utilizá-lo para preparar o banco e iniciar a API.

## 3. Criar um banco de demonstração

Vamos criar um banco novo no SQL Server LocalDB. O nome será gerado automaticamente, evitando utilizar algum banco que já exista no computador.

No mesmo PowerShell, copie e execute:

```powershell
$demoDb = 'SENTRA_DEMO_Avaliacao_Ensaio_' + [Guid]::NewGuid().ToString('N').Substring(0, 12)

$demoUtc = [DateTime]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ssZ')

$env:DOTNET_ENVIRONMENT = 'Demo'
$env:ASPNETCORE_ENVIRONMENT = 'Demo'

$env:SENTRA_DEMO_CONNECTION_STRING = 'Server=(localdb)\MSSQLLocalDB;Database=' + $demoDb + ';Integrated Security=true;TrustServerCertificate=true'

$demoArgs = @('--profile', 'evaluation', '--database', $demoDb, '--confirm', 'DEMO')
```

**O que acabamos de fazer?**

- Geramos um nome exclusivo para o banco.
- Definimos a referência de data e hora da demonstração.
- Configuramos o ambiente para utilizar apenas o LocalDB.
- Preparamos os argumentos exigidos pelas ferramentas DEMO.

Agora crie o banco:

```powershell
dotnet backend/DemoData/bin/Debug/net10.0/DemoData.dll --prepare-new @demoArgs

if ($LASTEXITCODE -ne 0) {
    throw 'Não foi possível preparar o banco DEMO.'
}
```

Esse comando cria um banco novo e aplica as **sete migrations existentes**.

Ele não utiliza o Azure SQL e recusa um banco que já exista.

## 4. Carregar os dados fictícios

Com o banco criado, precisamos cadastrar os registros que serão utilizados na demonstração.

Primeiro, valide a preparação:

```powershell
dotnet backend/DemoData/bin/Debug/net10.0/DemoData.dll --validate @demoArgs --reference-utc $demoUtc

if ($LASTEXITCODE -ne 0) {
    throw 'A validação da DEMO falhou.'
}
```

Agora carregue os dados:

```powershell
dotnet backend/DemoData/bin/Debug/net10.0/DemoData.dll --apply @demoArgs --reference-utc $demoUtc

if ($LASTEXITCODE -ne 0) {
    throw 'Não foi possível carregar os dados da DEMO.'
}
```

**Resultado esperado:** 79 registros sintéticos inseridos.

Esses registros incluem equipamentos, agentes, manutenções, ocorrências e informações utilizadas pela Cidade Preditiva.

Não é necessário cadastrar nada manualmente.

**Importante:** executar a carga novamente não duplica esses registros. Porém, também não desfaz operações já realizadas durante a demonstração.

## 5. Criar as contas de acesso

Agora vamos preparar cinco usuários fictícios, cada um com uma permissão diferente.

Execute:

```powershell
dotnet backend/DemoAdmin/bin/Debug/net10.0/DemoAdmin.dll --create-evaluation-users --database $demoDb --confirm DEMO

if ($LASTEXITCODE -ne 0) {
    throw 'Não foi possível criar as contas DEMO.'
}
```

O terminal solicitará uma **senha para as contas da demonstração**.

Digite uma senha forte e siga as instruções exibidas.

Não é necessário colocar aspas na senha digitada. Ela não aparecerá na tela enquanto você a informa.

As cinco contas utilizarão a mesma senha escolhida:

| Perfil | E-mail para login |
| --- | --- |
| Administrador | `admin.s07@sentra.invalid` |
| Gerente de Operações | `operationmanager.s07@sentra.invalid` |
| Operador da Central | `centraloperator.s07@sentra.invalid` |
| Técnico de Manutenção | `maintenancetechnician.s07@sentra.invalid` |
| Agente de Campo | `agent.s07@sentra.invalid` |

**Esses e-mails são fictícios.** Não é necessário criá-los em um serviço de e-mail ou possuir acesso a alguma caixa de entrada.

Cada perfil possui permissões próprias. Ao entrar com contas diferentes, você poderá observar como os módulos disponíveis mudam conforme o usuário.

## 6. Iniciar a API

Antes de iniciar o backend, precisamos gerar uma chave privada para autenticação dos usuários da DEMO.

No mesmo terminal, execute:

```powershell
$demoKeyBytes = New-Object byte[] 32

$demoRng = [Security.Cryptography.RandomNumberGenerator]::Create()

try {
    $demoRng.GetBytes($demoKeyBytes)
    $env:SENTRA_DEMO_JWT_KEY = [Convert]::ToBase64String($demoKeyBytes)
}
finally {
    $demoRng.Dispose()
    [Array]::Clear($demoKeyBytes, 0, $demoKeyBytes.Length)
}
```

Essa chave é gerada automaticamente. Você não precisa inventar, copiar ou divulgar nenhum segredo.

Agora execute:

```powershell
./scripts/demo/Start-DemoEvaluation.ps1 -Database $demoDb
```

Pronto! A API da DEMO deverá ficar disponível em:

**http://127.0.0.1:5180**

**Deixe esse PowerShell aberto.** Ele precisa continuar executando enquanto você utiliza o sistema.

A API inicia com as alterações de dados bloqueadas, permitindo consultar os cenários preparados sem modificar os registros.

## 7. Iniciar o frontend

Abra **um segundo PowerShell**, mantendo o primeiro aberto.

Entre na pasta raiz do repositório, caso necessário.

Execute:

```powershell
$env:VITE_API_BASE_URL = 'http://127.0.0.1:5180'
$env:VITE_DEMO_MODE = 'true'

npm.cmd --prefix frontend run dev -- --host 127.0.0.1 --port 5173 --strictPort
```

Agora abra no navegador:

**http://127.0.0.1:5173**

A tela de login do S.E.N.T.R.A. deverá aparecer com a identificação:

**DEMO · DADOS SINTÉTICOS**

Entre utilizando uma das contas preparadas no passo 5 e a senha escolhida.

**Pronto! Você já pode explorar o S.E.N.T.R.A. com os dados da demonstração.**

## 8. O que explorar na DEMO?

Recomendamos entrar primeiro como Administrador ou Gerente de Operações para visualizar os principais módulos.

| Módulo | O que observar |
| --- | --- |
| Dashboard | Indicadores de equipamentos, disponibilidade e agentes |
| Equipamentos | Inventário, características e situações operacionais |
| Agentes | Profissionais cadastrados e informações de localização |
| Central de Operações | Mapa interativo, marcadores, filtros e dados geográficos |
| Manutenções | Ordens corretivas, preventivas e agendamentos |
| Cidade Preditiva | Índices de risco, justificativas e recomendações |

O cenário preditivo inclui a câmera `DEMO-S07-C-PREV`, com índice inicial de risco **85/100**.

Você também pode sair e entrar com outra conta para conferir as permissões de cada perfil. A interface mostra apenas os módulos autorizados, enquanto o backend também restringe operações protegidas.

## 9. Opcional: testar o aceite de uma recomendação

Por segurança, o modo padrão da DEMO permite apenas consultas.

Mas existe um modo especial para demonstrar o funcionamento de uma recomendação preventiva.

**Para ativá-lo:**

1. Volte ao PowerShell da API.
2. Pressione `Ctrl + C` para encerrar a execução atual.
3. Inicie novamente com o comando:

```powershell
./scripts/demo/Start-DemoEvaluation.ps1 -Database $demoDb -InteractiveAcceptance
```

A API continuará na porta `5180`.

Nesse modo, entre com a conta de Administrador ou Gerente de Operações.

O sistema permitirá aceitar **uma recomendação específica do cenário de avaliação**, criando uma ordem de manutenção preventiva vinculada.

**Resultado esperado:**

- O total de ordens passa de 9 para 10.
- As ordens agendadas passam de 2 para 3.
- As recomendações pendentes passam de 2 para 1.

As demais operações de escrita permanecem bloqueadas nesse ambiente DEMO. Não execute análises persistentes ou tente cadastrar e editar outros registros.

O aceite altera intencionalmente o banco local de demonstração. Se quiser repetir o cenário desde o início, prepare um **novo banco DEMO** seguindo os passos anteriores.

## 10. Como encerrar?

Para finalizar:

1. Pressione `Ctrl + C` no PowerShell do frontend.
2. Pressione `Ctrl + C` no PowerShell da API.
3. Feche os terminais utilizados.

O banco de demonstração não é excluído automaticamente.

**Não utilize as contas, chaves ou conexões da DEMO em produção.**

## Problemas comuns

| Problema | O que fazer |
| --- | --- |
| Falha na criação do banco | Confira se o SQL Server LocalDB está instalado. |
| `DemoData.dll` não encontrado | Execute novamente o `dotnet build` do passo 2. |
| Erro ao carregar registros | Verifique a mensagem apresentada e se a preparação do banco terminou corretamente. |
| Não consigo fazer login | Confira o e-mail fictício e a senha escolhida no passo 5. |
| Frontend não conecta à API | Confirme que a API continua aberta na porta 5180. |
| Não consigo cadastrar ou editar | É esperado: o ambiente DEMO é protegido contra alterações. |
| Módulos não aparecem | Confira as permissões do perfil utilizado. |
| Não consigo repetir o cenário preventivo | O aceite não é desfeito automaticamente; utilize um banco DEMO novo. |
| Mapa sem cartografia | Confira a conexão com a internet, necessária para carregar os tiles OpenStreetMap. |

---

## Mais informações

- [README principal](../../README.md) — apresentação e funcionalidades.
- [Instalação completa](../instalacao.md) — ambiente de desenvolvimento com banco próprio.
- [Arquitetura](../arquitetura.md) — organização e tecnologias.

**Esta demonstração utiliza exclusivamente dados sintéticos e não depende do ambiente de produção publicado no Azure.**
