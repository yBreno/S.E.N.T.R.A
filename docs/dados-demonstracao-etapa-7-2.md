# S.E.N.T.R.A. — Carga DEMO, Etapa 7.2

## Escopo e estado da validação

Ferramenta manual `backend/DemoData`, sem integração com o startup da API. Não cria usuários,
não modifica autenticação, não inicializa workers, não lê `appsettings`, user-secrets,
`frontend/.env.local` ou a conexão da API. Não cria banco/schema, não executa migrations,
não apaga dados e não publica recursos. Nesta versão, qualquer destino Azure/remoto é recusado.

O LocalDB está instalado. A consulta local não encontrou banco `SENTRA_DEMO_*` previamente
preparado. Por isso a carga foi testada somente no EF Core InMemory existente nos testes:
isso verifica a lógica e serviços, mas não constraints/transações SQL Server, concorrência,
HTTP, JWT ou a dashboard no navegador. Nenhuma carga foi executada no Azure.

## Análise do modelo existente

- `AppDbContext` usa SQL Server e `ConnectionStrings:DefaultConnection`; as configurações
  seguem o `WebApplication.CreateBuilder`: arquivos de configuração, overrides por ambiente,
  user-secrets em Development, variáveis de ambiente e argumentos. O ambiente vem dos
  mecanismos normais ASP.NET/.NET. Não é possível inferir o banco publicado pelo arquivo local.
- Equipamentos usam TPH na tabela `Equipments`, discriminador `Type`, serial único e FK
  obrigatória para endereço. Veículos têm placa única não nula e agente opcional. As transições
  passam por `Equipment.ChangeStatus`, respeitando a matriz de domínio.
- Agentes exigem nome, documento de 11 dígitos, matrícula única, bairro e coordenadas;
  CPF e vínculo opcional `UserId` têm índices únicos. Nenhum agente DEMO é vinculado a usuário.
- Ocorrências exigem bairro/coordenadas, severidade/tipo válidos e descrição de até 500
  caracteres. A associação ao equipamento é opcional, com FK restritiva.
- Alertas exigem equipamento, mensagem de até 500 caracteres, tipo, severidade e status.
  Falha recorrente para risco depende de alertas `Failure` por equipamento, não apenas
  de ocorrências `EquipmentFailure`.
- Equipamentos `UnderMaintenance` recebem uma manutenção aberta correspondente, com datas
  coerentes e agente opcional nulo. Não são geradas recomendações ou telemetrias adicionais.
- As migrations existentes são `InitialCreate`, `RemoveOrganization` e `AddPredictiveCity`.
  A última adiciona ocorrências, posições, recomendações e tipo dos alertas. Nenhuma foi alterada
  ou executada; a ferramenta recusa um banco com migrations pendentes.
- `PredictiveSeeder` atual é chamado somente em Development e retorna se existir qualquer
  ocorrência. Não serve como complemento idempotente de uma base parcialmente preenchida.
- `AdminSeeder` exige senha configurada, CPF válido e ausência de usuários. Em ambientes
  diferentes de Development depende de `SeedAdmin:Enabled`. Ele não cria o cadastro operacional.
  Ambos permanecem inalterados.

## Manifesto da carga v1

| Entidade | Quantidade | Características |
| --- | ---: | --- |
| Endereços | 60 | Vias fictícias, um por equipamento, coordenadas sintéticas |
| Equipamentos | 60 | Os oito tipos existentes; dez em cada um dos seis estados |
| Agentes | 20 | Três papéis e disponibilidades; nenhum usuário associado |
| Ocorrências | 100 | 25 EquipmentFailure; 75 distribuídas pelos outros tipos |
| Alertas | 15 | Failure, abertos; quatro severidades; três por equipamento-alvo em cada bairro |
| Manutenções | 10 | Abertas, correspondentes aos dez equipamentos UnderMaintenance |

Total: 265 registros. Distribuição entre Centro, Bela Vista, Pinheiros, Vila Mariana e Mooca.
Os nomes e localizações são ilustrações; não representam operações ou pessoas reais.
Há 13 agentes ativos (Available/Busy) e dez equipamentos Operational; em um banco que contenha
somente esta carga, a disponibilidade esperada é 16,67%.

As ocorrências são datadas de 75 minutos a 125 horas antes da primeira inserção e os alertas
de uma a 15 horas antes. Com as opções padrão do backend, os serviços retornam 100 pontos
Incidents, 25 Failures e cinco Risk logo após a carga. Scores são calculados pelo backend:
não são fixados como resultados esperados de produção e variam com horário/configuração.

Documentos são gerados algoritmicamente com dígitos verificadores válidos para passar pelas
validações existentes. Não foram consultados ou copiados de pessoas reais. Não existe aqui
garantia cadastral de que uma sequência gerada esteja desocupada: use exclusivamente em banco
isolado; não use esses documentos para identificação, integrações ou cruzamentos reais.
Placas e CEPs são exemplos sintáticos, sem consulta a cadastros externos.

## Identificação e idempotência

- Dataset fixo `SENTRA-DEMO-v1`; GUIDs determinísticos a partir de SHA-256 de categoria/índice.
- Seriais e matrículas levam o prefixo; referências de endereços, descrições de ocorrências/
  manutenções e mensagens de alertas identificam DEMO e a chave estável.
- A carga procura cada ID e também conflitos de serial, placa, matrícula e CPF. Identidade
  ou relacionamento incompatível causa erro antes de qualquer inserção.
- Somente registros ausentes são adicionados. Não se adotam registros externos por semelhança.
  Registros encontrados não têm datas, status ou outros campos atualizados.
- Um contexto exclusivo, sem alterações pendentes, é obrigatório. As inserções SQL usam uma
  transação Serializable e uma única chamada a SaveChanges; em falha, a transação é revertida.
  Não há retry automático. Uma disputa concorrente pode falhar/deadlockar; não execute duas
  cargas simultaneamente. Constraints e concorrência ainda exigem teste em SQL Server isolado.

**Atualidade não implica sobrescrita:** repetir a mesma carga depois de sete dias não renova
ocorrências antigas. Assim, Incidents/Failures podem passar a zero sem duplicação. Para outra
sessão de demonstração, prepare um novo banco isolado com schema autorizado. Não exclua ou
renove automaticamente os registros do banco anterior. Completar uma carga parcial posteriormente
também pode produzir datas de inserção diferentes entre registros; todos os existentes são preservados.

## Comandos manuais, a partir da raiz do repositório

Inspecionar somente o planejamento, sem conexão a banco:

```powershell
dotnet run --project backend/DemoData/DemoData.csproj -- --plan
```

Para aplicar, um responsável deve disponibilizar previamente um banco **local isolado**, com
nome `SENTRA_DEMO_<nome>`, schema atual e acesso Windows integrado na instância
`(localdb)\MSSQLLocalDB`. A criação do banco/schema é uma operação separada: não é realizada
pela ferramenta e não foi executada nesta etapa. Não aponte a API para esse banco antes de
revisar seus seeders/worker existentes, que podem gerar outros dados ao iniciar.

Somente depois de confirmar esse destino e autorizar a carga local:

```powershell
# Em um terminal dedicado à demonstração, sem configurações de produção.
$env:DOTNET_ENVIRONMENT = 'Demo'
# Se ASPNETCORE_ENVIRONMENT estiver definido, também precisa ser Demo neste terminal.
# Configure SENTRA_DEMO_CONNECTION_STRING externamente para o banco local isolado aprovado.
# Servidor permitido: (localdb)\MSSQLLocalDB; banco: SENTRA_DEMO_Stage72; autenticação Windows integrada.
# Não copie a conexão do Azure nem coloque a conexão em código ou no relatório.
dotnet run --project backend/DemoData/DemoData.csproj -- --apply --database SENTRA_DEMO_Stage72 --confirm DEMO
```

A CLI exige a sintaxe exata, ambiente Demo e confirmação DEMO; não aceita connection string,
servidor ou credenciais como argumento. A conexão vem exclusivamente da variável dedicada
`SENTRA_DEMO_CONNECTION_STRING`, sem fallback. O guard exige LocalDB, autenticação integrada
sem usuário/senha, banco com prefixo DEMO e nome igual ao informado em `--database`.
Não há connection string de execução embutida no código. Não utiliza a conexão DefaultConnection
nem overrides do Azure. O processo
termina após a carga, sem iniciar a API. Saída: apenas contagens; nenhum documento, senha,
token ou connection string. Erros retornam código não zero sem despejar mensagens do provider.
Se falhar, confira primeiro ambiente, disponibilidade do banco, schema e conflitos; não altere
as proteções para tentar apontar o comando ao banco da API.

Repetir o comando deve retornar zero inserções. Isso preserva os registros existentes e
complementa somente ausentes. Não houve publicação do utilitário nem autorização de carga remota.

## Testes e aceitação restante

```powershell
dotnet test backend/Tests/Tests.csproj --no-restore
dotnet build backend/Solution/Solution.slnx --no-restore
```

Os testes verificam quantidades, DTOs/validadores existentes, coordenadas, chaves únicas do
plano, vínculos, estados, documentos sintéticos, datas relativas, repetição com relógio posterior,
carga parcial em base não vazia, preservação de todos os campos persistidos, conflitos sem
escrita e rejeição de ambiente/destinos não autorizados. Os serviços existentes de equipamentos,
agentes, heatmap e risco são exercitados com repositórios reais e banco InMemory isolado.

Resultado nesta etapa: 72 testes backend aprovados (49 existentes e 23 novos), nenhum
ignorado ou com falha. Build da solução aprovado, sem warnings ou erros. O comando `--plan`
foi executado e retornou as contagens acima com `databaseAccessed: false`.
A restauração necessária para o novo projeto foi concluída com o cache NuGet local;
nenhuma referência de pacote foi adicionada ou atualizada.

Antes de publicar: validar persistência relacional e rollback, constraints, tradução SQL das
consultas, idempotência em contextos independentes e respostas HTTP/JWT em uma API de demonstração
isolada. Depois, confirmar marcadores e as três métricas no navegador. Nenhum desses passos
é substituído pelos testes InMemory.

Azure exigirá autorização adicional, identificação explícita de recurso/banco isolados, revisão
de configurações efetivas, credenciais de privilégio mínimo e revisão de um mecanismo de execução
remota. Esta versão recusa Azure deliberadamente; não basta mudar uma variável ou connection string.
Não habilitar Development em um App Service existente para disparar seeders.
