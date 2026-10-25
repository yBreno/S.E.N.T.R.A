# Preparação do ambiente DEMO — Sprint 07

Este procedimento é manual e exclusivo para um ambiente local de demonstração
separado e autorizado. A aplicação normal, o Azure e os bancos já existentes não
participam da preparação. Não usar `DemoData.Azure` nem o roteiro histórico Stage73
para executar esta sprint.

## Pré-requisitos e destino

Windows com SQL Server LocalDB `(localdb)\MSSQLLocalDB`, SDK .NET 10, Node compatível
com o frontend e dependências restauradas. Execute na raiz do repositório, em
terminais PowerShell próprios para a demonstração. Confirme que as portas 5180 e
5173 estão livres. Não interrompa processos de terceiros para liberá-las.

```powershell
dotnet restore backend/Solution/Solution.slnx
dotnet build backend/Solution/Solution.slnx --no-restore
npm.cmd ci --prefix frontend

# Somente memória: não lê configuração privada nem conecta ao SQL.
dotnet backend/DemoData/bin/Debug/net10.0/DemoData.dll --plan --profile evaluation
```

O plano retorna `synthetic: true`, `databaseAccessed: false` e 79 registros.
O perfil `legacy`, mantido por compatibilidade, continua sendo o padrão da ferramenta;
para esta sprint sempre informe `--profile evaluation`.

Escolha um nome **novo** para cada ensaio que precise começar com recomendações
pendentes. O código recusa destinos remotos, Azure, autenticação SQL, arquivo anexado,
failover e nomes que não correspondam ao banco confirmado. A avaliação exige o
prefixo `SENTRA_DEMO_Avaliacao_`; não há opção para desativar essa proteção.

```powershell
$demoDb = 'SENTRA_DEMO_Avaliacao_Ensaio_' + [Guid]::NewGuid().ToString('N').Substring(0, 12)
$demoUtc = [DateTime]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ssZ')
$env:DOTNET_ENVIRONMENT = 'Demo'
$env:ASPNETCORE_ENVIRONMENT = 'Demo'
$env:SENTRA_DEMO_CONNECTION_STRING = 'Server=(localdb)\MSSQLLocalDB;Database=' + $demoDb + ';Integrated Security=true;TrustServerCertificate=true'
$demoArgs = @('--profile', 'evaluation', '--database', $demoDb, '--confirm', 'DEMO')

# Cria SOMENTE um banco novo LocalDB e aplica as sete migrations existentes.
# Se o banco já existir, interrompe sem adotá-lo ou atualizar seu schema.
dotnet backend/DemoData/bin/Debug/net10.0/DemoData.dll --prepare-new @demoArgs
if ($LASTEXITCODE -ne 0) { throw 'Preparação interrompida; não prossiga.' }

# Prévia de faltantes: leitura; não insere registros.
dotnet backend/DemoData/bin/Debug/net10.0/DemoData.dll --validate @demoArgs --reference-utc $demoUtc
if ($LASTEXITCODE -ne 0) { throw 'Validação interrompida; não prossiga.' }

# Carga explícita e transacional: resultado esperado 79 inserções.
dotnet backend/DemoData/bin/Debug/net10.0/DemoData.dll --apply @demoArgs --reference-utc $demoUtc
if ($LASTEXITCODE -ne 0) { throw 'Carga interrompida; revise o destino e os conflitos.' }

# Repetição: resultado esperado zero inserções.
dotnet backend/DemoData/bin/Debug/net10.0/DemoData.dll --apply @demoArgs --reference-utc $demoUtc
```

Não troque a conexão por uma string copiada da aplicação publicada. A ferramenta de
carga não cria schema: essa responsabilidade é somente de `--prepare-new`, que
recusa um banco existente. Não foi criada migration para dados fictícios.

Registre privadamente o nome e a referência UTC do ensaio. Aceita-se referência UTC
explícita com `Z` ou `+00:00`; carga com referência futura acima de cinco minutos é
recusada. Prepare próximo à apresentação, preferencialmente na mesma hora: as
consultas da aplicação usam o relógio real e janelas móveis de sete/trinta dias.

## Contas fictícias e API

O provisionamento utiliza o mecanismo manual existente. Ele pede uma senha privada
no terminal; não há senha padrão publicada. O operador também pode fornecer
`SENTRA_DEMO_ADMIN_PASSWORD` privadamente ao processo. A ferramenta não exibe senha,
hash, documento ou token. Repetir o comando preserva hashes existentes.

```powershell
dotnet backend/DemoAdmin/bin/Debug/net10.0/DemoAdmin.dll --create-evaluation-users --database $demoDb --confirm DEMO
if ($LASTEXITCODE -ne 0) { throw 'Provisionamento interrompido.' }
```

São cinco contas locais, com a mesma senha fornecida para este ensaio:

| Login fictício | Permissão relevante existente |
| --- | --- |
| `admin.s07@sentra.invalid` | Visão completa e aceite preventivo |
| `operationmanager.s07@sentra.invalid` | Visão completa e aceite preventivo |
| `centraloperator.s07@sentra.invalid` | Central, agentes e análises; sem ordens ou aceite |
| `maintenancetechnician.s07@sentra.invalid` | Inventário, ordens e risco de equipamento; sem Central ou aceite |
| `agent.s07@sentra.invalid` | Sem acesso ao inventário interno; demonstra a restrição de acesso |

São contas de avaliação separadas dos nove profissionais do mapa, sem `UserId`
vinculado. Os documentos de ambos são sequências repetidas de onze dígitos,
**inválidas como CPF**. Somente os provisionadores do perfil sintético admitem essas
sentinelas; cadastro e autenticação normais mantêm seus validators e roles.
Não há telefones nem e-mails pessoais. As ruas/CEP são fictícios; coordenadas
ilustrativas ficam na região de São Paulo, sem afirmar instalações reais.

Gere uma chave JWT exclusiva para a sessão, sem imprimir seu valor:

```powershell
$demoKeyBytes = New-Object byte[] 32
$demoRng = [Security.Cryptography.RandomNumberGenerator]::Create()
try {
    $demoRng.GetBytes($demoKeyBytes)
    $env:SENTRA_DEMO_JWT_KEY = [Convert]::ToBase64String($demoKeyBytes)
} finally {
    $demoRng.Dispose()
    [Array]::Clear($demoKeyBytes, 0, $demoKeyBytes.Length)
}

# Padrão: somente leitura e login.
./scripts/demo/Start-DemoEvaluation.ps1 -Database $demoDb

# Para o roteiro com aceite, encerre a execução anterior e use:
./scripts/demo/Start-DemoEvaluation.ps1 -Database $demoDb -InteractiveAcceptance
```

Execute apenas uma das opções por vez. O script usa os binários já compilados;
recompile antes de iniciar caso altere código. A API fica em
`http://127.0.0.1:5180`, ambiente `Demo`, com conexão construída exclusivamente para
LocalDB. Ele não carrega dados, migra schema, cria usuários ou inicia simulação,
seeders e automação preditiva. As variáveis configuradas pelo script são restauradas
ao encerrá-lo. Para remover as variáveis definidas manualmente, feche o terminal dedicado.

Com `-InteractiveAcceptance`, somente o POST abaixo atravessa o bloqueio de escritas,
além do login; continua exigindo JWT e `Admin`/`OperationManager`:

```text
POST /api/recommendations/df1f8c49-d255-9661-24ff-d2ce69f4d784/accept
```

Um interceptor limita a persistência à recomendação C, ao planejamento de seu
equipamento e à nova ordem vinculada. O endpoint e o serviço de aceite já existiam.
Outros aceites, descarte, análise persistida, cadastro, edição, início e conclusão
de ordens retornam 405 neste host DEMO. O roteiro mostra os registros previamente
preparados para esses estados. Não clicar em **Executar análise** durante a apresentação.

## Frontend e conferência

Em outro terminal dedicado, sem editar `.env.local` nem arquivos de produção:

```powershell
$env:VITE_API_BASE_URL = 'http://127.0.0.1:5180'
$env:VITE_DEMO_MODE = 'true'
npm.cmd --prefix frontend run dev -- --host 127.0.0.1 --port 5173 --strictPort
```

Abra `http://127.0.0.1:5173`. A faixa **DEMO · DADOS SINTÉTICOS** deve aparecer já
no login e permanecer nas páginas internas. Essa flag aceita somente API HTTP
loopback na porta 5180; destino externo é recusado. Variáveis `VITE_*` são públicas
e não devem receber segredos. Essa configuração não deve ser enviada ao deploy.

Antes de apresentar: conferir 12 equipamentos, sete operacionais, três
indisponíveis, 58,33% de disponibilidade e sete agentes ativos. Verificar nove
agentes, nove ordens (duas agendadas, duas em andamento, cinco concluídas), duas
recomendações pendentes e câmera `DEMO-S07-C-PREV` operacional com índice 85.
Seguir o [roteiro de 6min30](roteiro-apresentacao.md).

Aceitar a recomendação é uma alteração deliberada no banco DEMO: o total passa
a dez ordens, com três agendadas, e resta uma recomendação pendente. Repetir o aceite
reutiliza a mesma ordem. Repetir a carga insere zero e **não desfaz o aceite, não
reescreve datas e não reinicia o ensaio**. Para repetir a história desde o início,
prepare outro banco novo explicitamente autorizado. Não há limpeza automática.

Ao terminar, encerre a API e o frontend com Ctrl+C e feche os terminais dedicados.
Não publique nem reutilize contas, chave ou conexão deste ensaio em produção.
