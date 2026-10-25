# S.E.N.T.R.A.

Plataforma de monitoramento e gestão de operações urbanas desenvolvida para o desafio
**F.O.G.O. — Ferramenta Onisciente de Gestão e Operações**, da DSIN/PROMETHEUS.
Integra inventário, monitoramento geográfico e análise preditiva explicável.

## Módulos e estado atual

| Módulo | Backend implementado | Interface React |
| --- | --- | --- |
| Inventário Inteligente da Cidade | Equipamentos e agentes: CRUD e estados; endereços; vínculo de veículos; manutenção agendada, em andamento e concluída | Gestão de equipamentos e agentes; manutenção preventiva/corretiva com listagem, filtros, detalhes, histórico, agendamento, início e conclusão |
| Centro de Operações da Cidade | Dashboard, alertas, ocorrências, telemetria e localização de agentes; consulta operacional sem executar análises preditivas | Indicadores, mapa com símbolos por tipo/estado, filtros, alertas reais, manutenção em destaque, atenção por bairro e polling configurável |
| Cidade Preditiva | Risco explicável por equipamento, exposição por bairro com cobertura de agentes, anomalias, MTBF e recomendações | Heat Map e módulo Cidade preditiva: indicadores, fatores, evidências, histórico insuficiente, prioridades e aceite/descarte por gestores |

A presença de um endpoint não significa que o fluxo correspondente já possui tela.
Fornecedores e bases operacionais têm entidades/repositórios, sem telas ou controllers próprios.

## Stack e arquitetura

- Backend: C# / ASP.NET Core .NET 10, EF Core 10, SQL Server, FluentValidation, JWT e BCrypt.
- Frontend: React 19, JavaScript/JSX, Vite 8 e CSS; React Leaflet 5.0.0, Leaflet 1.9.4 e leaflet.heat 0.2.0.
- Testes: xUnit e EF Core InMemory no backend; `node:test` no frontend.
- Publicação existente: Azure App Service, Azure SQL e Azure Static Web Apps, com workflows GitHub Actions.

O backend é um monólito modular: Domain contém entidades e regras; Application contém
casos de uso, DTOs, validadores e cálculos; Infrastructure implementa EF Core e repositórios;
Api expõe controllers, autenticação, autorização e tarefas hospedadas.
O frontend separa páginas, componentes, hooks, configuração e serviços HTTP.

```text
backend/
  Domain/                 Entidades e regras de negócio
  Application/            Serviços, contratos e motor preditivo
  Infrastructure/         Persistência, migrations e ferramentas DEMO
  Api/                    API HTTP e configuração por ambiente
  Tests/                  Testes automatizados
  DemoData*/ e DemoAdmin/  Ferramentas manuais protegidas
  Solution/Solution.slnx   Solução completa
frontend/
  src/                    Interface React
  public/                 Recursos públicos, quando necessários
  tests/                  Testes frontend
docs/reports/             Relatórios de revisão das sprints
```

## Funcionalidades concluídas

- Login/logout e expiração; JWT em memória e, temporariamente para demonstração, em
  sessionStorage. Após F5, a restauração exige validade local e aceitação pela API.
- Monitoramento para Admin, OperationManager e CentralOperator, com autorização efetiva no backend.
- Equipamentos habilitados na sidebar; busca por série/marca/modelo/categoria, filtros de tipo,
  situação, bairro e necessidade de manutenção, e paginação local de dez registros.
- Cadastro/edição com endereço completo, características pertinentes ao tipo e categoria de uso;
  detalhes completos e mudança de situação respeitando a matriz de transições.
- Consulta de equipamentos também para MaintenanceTechnician, com restauração de sessão própria;
  cadastro/edição restritos a Admin e OperationManager. Agent não acessa o inventário.
- Agentes habilitados para Admin, OperationManager e CentralOperator: busca por nome/matrícula/
  categoria/bairro, filtros de atuação e disponibilidade e paginação local de dez registros.
- Cadastro/edição de agentes por gestores; CPF imutável e mascarado após cadastro; detalhes de atuação,
  GPS, bairro, disponibilidade e vínculo de conta preservado. Categoria profissional não altera role de acesso.
- Consulta, associação, substituição e liberação de veículo/moto/veículo autônomo nos detalhes do agente;
  alteração do vínculo restrita a gestores, sem sobrescrever identificação/atributos do equipamento.
- Atualização operacional de posição/disponibilidade por Monitoring, com histórico no endpoint existente;
  retorno ao dashboard recarrega os agentes e preserva a definição de ativos (Available + Busy).
- Indicadores da API, mapa de São Paulo e camadas independentes de equipamentos, agentes e calor.
- Marcadores distintos para os oito tipos de equipamento e três categorias de agentes; cor por estado,
  sinalização de manutenção, validação de coordenadas e detalhes sem CPF no mapa.
- Filtros locais por tipo/estado de equipamento, categoria/disponibilidade de agente e bairro nas duas camadas.
- Heat Map por ocorrências, falhas e risco, com filtros próprios, cache em memória e cancelamento.
- Atualização automática da Central e atualização manual, dados anteriores sinalizados como desatualizados durante falhas,
  mensagens de erro/vazio e layout responsivo; mudanças de filtros preservam a instância do mapa.
- `locationUpdatedAt` explícito em UTC no DTO e apresentação no fuso do navegador;
  categorias apresentadas como Agente de trânsito, Guarda municipal e Policial militar, inclusive no mapa.

**Cartografia:** o bloqueio OpenStreetMap por ausência de Referer foi corrigido com
`strict-origin-when-cross-origin` no HTML e no TileLayer. A correção foi validada no site
publicado pelo responsável do projeto, conforme informado por ele. URL HTTPS oficial e
atribuição foram mantidas. A disponibilidade dos tiles depende do provedor e de sua política.

## Cargos, autorização e sessões

Os cargos de login são exclusivamente `Admin`, `OperationManager`, `CentralOperator`,
`MaintenanceTechnician` e `Agent`. As categorias profissionais `TrafficAgent`,
`MunicipalGuard` e `PoliceOfficer` pertencem ao agente, não concedem permissões e
mantêm seus nomes persistidos; o último rótulo visual é **Policial militar**.

O backend valida assinatura/expiração, usuário ativo, cargo atual e versão persistida
de autenticação. Alterar cargo, estado, senha ou e-mail revoga sessões anteriores;
reativar ou devolver o cargo antigo não restaura tokens. **Tokens legados sem
`auth_version` deixam de ser aceitos: todos precisam entrar novamente após a atualização.**

A sidebar **Usuários** é exclusiva de Admin, com paginação no servidor, busca/filtros,
cadastro, edição de nome/e-mail, cargo, ativação e histórico mínimo sem credenciais.
OperationManager não administra contas. O último Admin ativo é protegido, inclusive
contra alterações concorrentes. A vinculação profissional continua em `Agent.UserId`;
desativar a conta preserva o cadastro e os dados operacionais do agente.

`PublicRegistration:AllowAgentSelfRegistration` é explicitamente `false`; o opt-in
somente é aceito em Development/Testing. O caminho administrativo é `POST /api/users`;
o legado `/api/auth/register` por Admin reutiliza o mesmo fluxo auditado.
O primeiro Admin é provisionado por configuração privada, apenas se não houver usuários.
Há recuperação por link de e-mail, desabilitada por padrão até configurar o provedor
privadamente. Admin pode solicitar o envio; não recebe senha, token ou link secreto.
O titular pode trocar sua senha pelo endpoint autenticado
`POST /api/auth/change-password`, informando a senha atual; a operação é auditada e exige novo login.
Senhas de cadastro/login devem respeitar o limite de **72 bytes UTF-8** do BCrypt;
credenciais antigas maiores exigem revisão privada e troca segura, sem ajuste automático.
Veja endpoints, provisionamento, rollout e pendências no
[relatório de Administração de Usuários](docs/reports/administracao-usuarios.md).

**Schema obrigatório:** aplicar e conferir `20261024223405_AddUserAdministration`
e depois `20261024234039_AddPasswordRecovery` antes de publicar a API.
O [SQL incremental atual](docs/sql/20261024234039-recuperacao-senha-email.sql)
parte de `AddMaintenanceScheduling`, inclui as duas pendências, preserva contas/hashes
e não é executado pela aplicação.
Use uma janela de manutenção: schema → API → frontend, sem mistura de versões de autenticação.
Para execução manual no Azure Portal Query Editor, seguir o
[roteiro de aplicação e conferência](docs/sql/README-query-editor.md). O SQL atual
usa um lote T-SQL e uma transação para as duas migrations, sem SQLCMD/GO.

### Recuperação segura de senha por e-mail

No login, **Esqueci minha senha** solicita um link com resposta genérica. O link
HTTPS aponta exclusivamente para `https://sentra.uno/#redefinir-senha`, expira em
15 minutos e exige confirmação de nova senha. Apenas o hash do token é persistido;
o consumo único atualiza senha, AuthVersion e auditoria na mesma transação.
Contas inativas não recebem links e alterações de acesso invalidam links anteriores.

Configurar somente no backend: `PasswordRecovery__Enabled`, `PasswordRecovery__Transport=Resend`,
`PasswordRecovery__ApiKey`, `PasswordRecovery__From` e `PasswordRecovery__RateLimitKey`
(segredo aleatório compartilhado entre instâncias, com ao menos 32 caracteres).
O padrão é `Enabled=false`; o botão administrativo só é liberado quando a API
informa disponibilidade. A chave do provedor nunca entra em variáveis `VITE_*`.
O transporte `LocalTest` é exclusivo de Development/Testing, em memória, sem
endpoint de caixa de entrada; não pode iniciar em Production, Staging ou Demo.

Resend requer domínio remetente verificado e DNS adequado. Nenhum provedor, DNS,
credencial ou configuração Azure foi alterado nesta entrega. Consulte a comparação,
DNS, testes, limitações de fila/entrega e o plano de publicação no
[relatório de recuperação de senha](docs/reports/recuperacao-senha-email.md).

## Execução local

### CI/CD de produção pela `main`

Os pushes de backend e frontend usam `main`. O workflow `main_sentra-api.yml` compila
`backend/Api/Api.csproj`, gera o artefato `sentra-api` e publica o pacote `./publish`.
O frontend usa `./frontend`, saída `dist` e `production_branch: main` no Azure Static Web Apps.
Há um publicador por serviço; os arquivos atuais ficam em `.github/workflows/`.

Os filtros incluem a pasta de cada serviço e seu workflow. Alterações somente no README
da raiz ou em `docs/` não disparam publicação; documentação dentro de `frontend/` ou
`backend/` também corresponde aos filtros dessas pastas. Não há gatilho de pull request.

O backend permite `workflow_dispatch` somente em `refs/heads/main`, com condição nos
jobs de build e deploy. Cada serviço tem grupo de concorrência próprio e
`cancel-in-progress: false`, preservando publicações em andamento. A revisão RBAC
acrescentou os controles do backend e preservou secrets, recursos e URLs.
O deploy backend exige a variável de repositório `SENTRA_USERS_SCHEMA_VERSION` igual
a `20261024223405_AddUserAdministration`, a ser definida somente após conferir a migration.
O frontend exige `SENTRA_USERS_API_VERSION` com o mesmo valor, somente após validar
a nova API. Esses bloqueios foram preservados. Adicionalmente, backend exige
`SENTRA_PASSWORD_RECOVERY_SCHEMA_VERSION` e frontend exige `SENTRA_PASSWORD_RECOVERY_API_VERSION`,
ambas com `20261024234039_AddPasswordRecovery`, liberadas após schema e API,
respectivamente. Sem todas as liberações, o deploy é bloqueado. Não há migrations nos workflows.

Credenciais federadas e controles privados do Azure não foram acessados nesta revisão.
Consulte o procedimento e os limites no [relatório administrativo](docs/reports/administracao-usuarios.md).

Pré-requisitos: .NET 10 SDK, Node compatível com Vite 8 (22.12+ na linha 22, ou 20.19+ na
linha 20) e npm. A validação local utilizou Node 26.7.0 e npm 11.16.0.
SQL Server/LocalDB e banco já preparado são necessários para usar a API, mas não para os testes InMemory.

Na raiz, os comandos de build e testes estão na seção seguinte. Para iniciar o frontend,
consulte o [guia específico](frontend/README.md); a API precisa estar disponível separadamente.

### Gestão de equipamentos — Sprint 01

É necessário usar frontend e backend desta versão e um banco **local isolado** com a migration
`20261023214553_AddEquipmentCategory`. Ela adiciona somente `Category`, opcional, em `Equipments`;
preserva os oito tipos, o discriminador TPH e os registros existentes. A sprint não aplicou migrations
em bancos persistentes. A inicialização da API continua sem aplicação automática de migrations.

Configure privadamente no terminal `ConnectionStrings__DefaultConnection` apontando exclusivamente
para seu SQL Server/LocalDB de desenvolvimento, `Jwt__Key` com pelo menos 32 caracteres,
`Jwt__Issuer` e `Jwt__Audience`. O ambiente abaixo usa variáveis de ambiente, sem depender de
user-secrets do ambiente Development. Use uma conta local já provisionada com o papel adequado.

```powershell
# Na raiz, depois de conferir a conexão LOCAL e definir as variáveis privadas acima:
$env:ASPNETCORE_ENVIRONMENT = 'SprintLocal'
$env:DOTNET_ENVIRONMENT = 'SprintLocal'
$env:Simulation__Enabled = 'false'
$env:SeedAdmin__Enabled = 'false'
$env:Cors__AllowedOrigins__0 = 'http://localhost:5173'
$env:Cors__AllowedOrigins__1 = 'http://127.0.0.1:5173'

# Aplicação manual de schema exclusivamente no banco local conferido:
dotnet ef database update --project backend/Infrastructure --startup-project backend/Api --connection "$env:ConnectionStrings__DefaultConnection"
dotnet run --project backend/Api/Api.csproj --no-launch-profile -- --urls http://127.0.0.1:5129
```

Em outro terminal, sem sobrescrever `.env.local`:

```powershell
$env:VITE_API_BASE_URL = 'http://127.0.0.1:5129'
npm.cmd --prefix frontend run dev -- --host 127.0.0.1
```

Abra `http://127.0.0.1:5173/#equipamentos`. Admin/OperationManager podem cadastrar e editar;
CentralOperator/MaintenanceTechnician podem consultar e alterar situações permitidas pela API.
Reativar um equipamento desativado exige gestor. O tipo é imutável na edição. Não há exclusão na tela.

Use **Novo equipamento**, informe identificação, campos do tipo e localização completa ou existente.
O endereço informado e o equipamento são salvos na mesma unidade de trabalho. Alterar a localização
cria uma cópia, preservando endereços compartilhados por outros equipamentos. O cadastro começa como
Não instalado; a situação é alterada separadamente nos detalhes e exige confirmação.

Necessidade e próxima data são metadados do inventário, sem abrir/encerrar manutenção. Agendada/Periódica
exigem data; durante falha ou manutenção em andamento o formulário preserva o planejamento atual.
Datas são enviadas em UTC e apresentadas no fuso do navegador.

**Classificação:** `Category` descreve a finalidade: táxi/escolar/viatura em Vehicle, moto de agente
em Motorcycle, balança como Sensor de pesagem. Em Sensor, “Controlador IoT de cancela” identifica o
controlador de acesso; não cria uma classe de atuador nem um serviço de acionamento da cancela.
Os campos técnicos próprios continuam nos atributos existentes. Sugestões no formulário são rótulos,
não equipamentos ou dados operacionais fictícios.

O modo HTTP **Demo é somente leitura por padrão**, salvo login. A Sprint 07 oferece um opt-in
local restrito ao aceite preventivo do cenário C, conforme a [preparação DEMO](docs/demo/preparacao-ambiente.md).
Cadastro e edição continuam bloqueados nesse host; use o ambiente local autorizado para esses fluxos.
Os perfis normais Development podem executar seeders/simulação; o exemplo SprintLocal acima os desativa.

O relatório de revisão (histórico no Git) contém arquivos alterados, endpoints,
decisões, resultados, limitações e roteiro de conferência no navegador.

### Gestão de agentes — Sprint 02

Use a mesma execução local isolada acima, com frontend/backend atualizados. Esta sprint **não exige
nova migration**: reaproveita Agent, AgentRole, AgentAvailability, bairro/GPS e o vínculo CurrentAgentId
dos veículos. A migration de categoria da Sprint 01 continua necessária para o projeto como um todo.

Abra `http://127.0.0.1:5173/#agentes` ou escolha **Agentes** na sidebar. Admin/OperationManager podem
cadastrar e editar; CentralOperator pode consultar e atualizar posição/disponibilidade. Técnico de
manutenção não recebe acesso ao módulo de agentes. O perfil Agent mantém os endpoints próprios existentes,
sem ganhar acesso à gestão geral pela interface.

Em **Novo agente**, informe nome, CPF, matrícula, categoria profissional, disponibilidade, bairro e GPS.
Há três categorias: Agente de trânsito, Guarda municipal e Policial militar. O CPF é enviado somente no cadastro,
não pode ser editado e aparece mascarado nos detalhes. O cadastro do profissional não cria uma conta de
login; um vínculo de conta já existente é preservado na edição, sem gestão de usuários nesta sprint.

Nos detalhes, gestores podem selecionar um veículo livre e confirmar **Salvar vínculo**, trocar por outro
ou escolher **Sem veículo associado** para liberar. Somente Vehicle/Motorcycle/AutonomousVehicle possuem
esse vínculo no domínio atual; não foi criado vínculo genérico com câmeras, sensores ou patinetes.
Veículo ocupado por outro agente é recusado e a troca não altera os demais dados do equipamento.

O painel **Atualizar posição e disponibilidade** reutiliza `PUT /api/agents/{id}/location` e grava histórico.
A edição cadastral continua com o comportamento existente de atualizar a posição atual, sem gerar esse log.
O bairro representa a área atual informada, sem delimitação geográfica permanente. Disponível/Ocupado contam
como ativos; Offline não conta. Após salvar, volte ao mapa/dashboard para consultar os dados atualizados.
Os popups mostram categoria em português, bairro, GPS, disponibilidade e horário da posição, sem CPF.

O relatório da Sprint 02 (histórico no Git) registra os endpoints, permissões, arquivos,
testes, evidências de mapa e limitações. O modo HTTP Demo continua somente leitura.

### Gestão de manutenções — Sprint 03

Atualize frontend e backend juntos. Aplique manualmente a migration
`20261023224842_AddMaintenanceScheduling` **somente no banco local isolado conferido**, usando a execução
acima. Ela acrescenta natureza, data prevista, vínculo com recomendação e índices de proteção;
`InitDate` passa a aceitar nulo enquanto a ordem está apenas agendada. Ordens anteriores permanecem
corretivas, com seus estados e datas preservados. A migration interrompe se encontrar mais de uma ordem
pendente para o mesmo equipamento; esses dados exigem revisão, sem exclusão ou correção automática.

Abra `http://127.0.0.1:5173/#manutencoes` ou **Manutenções** na sidebar. Admin, OperationManager e
MaintenanceTechnician podem consultar e operar as ordens, conforme a política já existente na API.
CentralOperator e Agent não recebem acesso ao módulo. No inventário, perfis autorizados encontram
**Manutenções e histórico deste equipamento** nos detalhes; a volta ao equipamento recarrega seus dados.

1. **Agendar preventiva**: escolha um equipamento operacional, descrição, urgência e data futura.
   Periodicidade em dias é opcional. A ordem fica Agendada e o equipamento continua Operacional.
2. Abra seus detalhes, confirme e escolha **Iniciar intervenção** quando o serviço começar, inclusive
   antecipadamente. A ordem passa a Em andamento e o equipamento a Em manutenção.
3. **Concluir manutenção** registra data/custo opcionais, restaura a operação e resolve os alertas
   abertos do equipamento. Com periodicidade, calcula a próxima previsão; isso não cria outra ordem.
4. **Abrir corretiva** inicia agora e exige equipamento em mau funcionamento. Se uma falha real ocorrer
   após o agendamento preventivo, iniciar a mesma ordem a transforma em corretiva, preservando previsão e ID.

Não pode existir mais de uma ordem agendada/em andamento por equipamento. Uma ordem pendente protege
a previsão no inventário; durante a intervenção, mudanças manuais de situação são bloqueadas. Enquanto
agendada, uma falha real ainda pode ser registrada. Não há exclusão, cancelamento ou reagendamento nesta
entrega. Listagem com busca/filtros e paginação local, histórico e detalhes incluem datas, custo, natureza,
urgência e identificadores de agente/recomendação quando existentes. Horários são enviados em UTC e
apresentados no fuso do navegador; início e conclusão efetivos não aceitam datas futuras.

Aceitar `SendMaintenance` pela API agora cria uma ordem real na mesma unidade de trabalho da aceitação.
Repetir a aceitação não duplica a ordem; a análise evita nova sugestão para equipamento com ordem pendente.
Somente gestores podem aceitar recomendações. Recomendações de bairro sem equipamento exigem triagem
manual e não podem ser aceitas como agendamento automático. A Sprint 05 disponibiliza a interface preditiva.
Metadados antigos e recomendações já aceitas não são convertidos retroativamente em ordens.

O relatório da Sprint 03 (histórico no Git) detalha diagnóstico, endpoints,
permissões, arquivos, testes no SQL Server/Chrome, limitações e roteiro de revisão.

### Central de Operações — Sprint 04

Use a execução local acima com backend/frontend atualizados e as migrations anteriores. Não há nova
migration nesta sprint. Acesse `http://127.0.0.1:5173/#dashboard` como Admin, OperationManager ou
CentralOperator. O técnico permanece no inventário/manutenções; Agent não recebe acesso à Central.

A Central sincroniza dashboard, equipamentos, agentes, alertas e calor padrão sem F5. Gestores também
consultam ordens Scheduled/Open; o operador não recebe chamadas ou acesso adicional ao módulo de manutenção.
O padrão é **30 segundos após terminar cada ciclo**, com botão Atualizar. Para configurar antes de iniciar
o Vite/build, use `$env:VITE_POLL_INTERVAL_MS = '30000'` (5000–300000 ms; inválido usa 30000).
O polling é suspenso ao navegar aos módulos de gestão. Filtros e seleção do mapa são preservados durante
a sincronização; o horário mostra o último ciclo completo bem-sucedido. Falhas mantêm os últimos dados
válidos com aviso e nova tentativa automática. Este é monitoramento por consultas HTTP, sem comprovação
de integração com dispositivos IoT físicos.

Os indicadores representam **a cidade inteira**: total cadastrado; Operational; indisponíveis =
Malfunctioning + UnderMaintenance + Stolen; disponibilidade = Operational / total × 100, com duas casas
e zero para inventário vazio; agentes ativos = Available + Busy. Deactivated/NotInstalled não entram
na soma de indisponíveis dessa definição. Filtros locais alteram marcadores/contagens das camadas,
sem recalcular os indicadores globais, alertas ou consolidação por bairro.

No mapa, quadrados identificam equipamentos, círculos identificam agentes e a cor indica a situação.
Símbolos diferenciam os tipos/categorias; ! destaca manutenção imediata e ◷ previsão vencida ou nos
próximos **sete dias corridos**, calculados pela mesma regra nos painéis e marcadores. Agendamento
preventivo mantém o equipamento operacional. Coordenadas inválidas não são substituídas por posições
artificiais; os contadores das camadas informam os descartados. Popups/inspeção mostram GPS, manutenção,
endereço e características pertinentes, com navegação para os detalhes completos das sprints anteriores.

**Alertas recentes** consulta alertas abertos reais, prioriza Critical/High e mostra severidade, mensagem,
horário e equipamento. **Manutenção em destaque** separa necessidade/previsão das ordens reais; ordens e
ações ficam restritas aos perfis autorizados. **Atenção por bairro** consolida recursos, indisponibilidade,
necessidade imediata, previsões e alertas High/Critical. Selecionar um bairro filtra o mapa. Trata-se de
contagem operacional atual, sem novo índice de risco. Não há classificação confiável por órgão ou região
administrativa no cadastro atual; esses filtros não foram inventados.

A Central usa `GET /api/dashboard?includePredictive=false`, evitando calcular estatísticas históricas
a cada ciclo. O parâmetro mantém o padrão anterior quando omitido; com false, `predictive` é nulo.
O contrato de equipamento agora inclui o endereço já associado e os horários de alertas são explícitos
em UTC. Nenhuma dessas extensões exige mudança no banco.

O carregamento separado de mapa/equipamentos/agentes/manutenções eliminou o aviso de chunk de 510 kB:
o principal ficou em aproximadamente **282 kB**, e o mapa em **181 kB**, sem bibliotecas novas.
Falha ao carregar um módulo apresenta recuperação por recarregamento, preservando a sessão validada.
Veja o relatório da Sprint 04 (histórico no Git) para arquivos, critérios,
endpoints, evidências e roteiro de teste.

### Cidade Preditiva — Sprint 05

Com a API local configurada como acima e uma conta de monitoramento, abra **Cidade preditiva**
na sidebar ou `http://127.0.0.1:5173/#cidade-preditiva`. A consulta inicial é somente leitura;
**Atualizar dados** recarrega manualmente as fontes, preservando a última leitura válida em falhas.

- Equipamentos: índice heurístico de 0–100, níveis baixo/médio/alto, falhas e corretivas recentes,
  preventivas pendentes/atrasadas, indisponibilidade e sinais categóricos de telemetria; janela,
  fatores, evidências e insuficiência de histórico explícitos. Nenhum índice é probabilidade.
- Bairros: índice existente acrescido da disponibilidade dos agentes no bairro cadastrado,
  com períodos de incidentes/falhas e fatores explicados. A cobertura é declarada e atual.
- **Executar análise**, após confirmação, reutiliza a geração de recomendações e alertas; não abre ordens.
  Admin/OperationManager/CentralOperator podem analisar. Apenas Admin/OperationManager aceitam ou descartam.
  O aceite de manutenção por equipamento reutiliza o fluxo existente e impede uma segunda ordem ativa.
  Recomendações regionais exigem triagem; aceite de reforço não desloca agentes automaticamente.
- MTBF e picos por média/desvio são estimativas descritivas, sem calibração probabilística;
  amostra pequena e fallback por tipo são apresentados separadamente do índice heurístico.

Sem migration nova. Use frontend e backend desta versão juntos. Mantenha `Simulation__Enabled=false`
em uma operação real: os eventos já persistidos não têm marca de procedência e podem incluir simulação
anterior. A Sprint 06 acrescenta a coordenação SQL entre instâncias, descrita abaixo.
Decisões, limites, arquivos, endpoints e testes estão no
relatório da Sprint 05 (histórico no Git).

### Automação e Central preditiva — Sprint 06

A Central agora consulta riscos e recomendações sem executar análises. **Prevenção operacional**
mostra equipamentos de risco alto, bairros prioritários, recomendações e estado da execução.
No mapa, **P** indica risco preditivo alto; a cor continua representando a situação operacional.
Círculos tracejados indicam bairros de índice alto/crítico. Dados preditivos têm horário próprio,
atualização manual e periódica a cada cinco minutos; falhas preservam a leitura anterior com aviso.

A automação permanece **desligada por padrão**, independentemente da consulta das páginas:

```text
PredictiveAutomation__Enabled=false
PredictiveAutomation__IntervalMinutes=60
```

Intervalos aceitos: 15–1440 minutos, validados na inicialização. Alterações exigem restart.
Se habilitada conscientemente em ambiente validado, a primeira análise só ocorre após um intervalo
completo; o próximo intervalo começa depois do ciclo anterior. Falha/cancelamento não derruba a API.
O ambiente Demo proíbe automação. Nenhuma configuração Azure foi modificada ou ativada nesta sprint.

SQL Server coordena análise e geração de recomendações com bloqueio transacional compartilhado
entre processos, sem migration nova. Conflito de bloqueio retorna 409; não há fallback inseguro.
Provedor sem coordenação distribuída não inicia o worker. Todas as instâncias que gravam recomendações
precisam estar nesta versão antes de habilitá-lo; leituras não criam ordens ou aceitam recomendações.

`GET /api/predictions/execution-status` e `GET /api/dashboard/predictive` exigem Monitoring.
O estado da última execução é **em memória da instância**, perdido no restart e sem visão global
de outras réplicas. O agendamento também é local: não garante uma única execução global por intervalo
nem recupera ciclos perdidos com a aplicação suspensa. A proteção SQL impede sobreposição das escritas
participantes e reaproveita pendências, mas não remove duplicatas legadas.
Veja testes, limitações e checklist de produção no
relatório da Sprint 06 (histórico no Git).

### Configuração privada

- Frontend: `VITE_API_BASE_URL` é uma URL pública, sem `/api`, credenciais, query ou fragmento.
  Variáveis `VITE_*` entram no bundle e nunca devem conter segredos. Preserve `.env.local` existente.
- API normal: `ConnectionStrings:DefaultConnection`, `Jwt:Key`, `Jwt:Issuer` e `Jwt:Audience`
  são selecionados pela configuração .NET; guarde segredos em user-secrets locais ou variáveis privadas.
  Variáveis aninhadas usam `__`, como `Jwt__Key` e `ConnectionStrings__DefaultConnection`.
- `Cors:AllowedOrigins`, `Simulation:Enabled` e `SeedAdmin:Enabled` controlam comportamentos
  operacionais. Fora de Demo, a simulação tem padrão habilitado; não inicie apontando para banco não autorizado.
- Ferramentas DEMO são manuais e possuem verificações próprias de ambiente/destino.
  Não execute cargas, provisionamento ou migrations como parte de uma simples inicialização.

## Demonstração para avaliação — Sprint 07

O perfil manual `evaluation` reutiliza o carregador DEMO com 12 equipamentos,
nove agentes, cinco bairros e históricos relacionados: falha com resposta corretiva,
câmera operacional com risco explicado de 85/100, bairro prioritário e ordens
agendadas/em andamento/concluídas. São 79 registros operacionais e cinco contas
fictícias provisionadas separadamente. Nenhum dado é carregado ao iniciar a API.

```powershell
dotnet run --project backend/DemoData --no-build --no-restore -- --plan --profile evaluation
```

Esse comando apenas calcula o plano, sem acessar banco. A preparação exige banco
LocalDB **novo**, prefixo `SENTRA_DEMO_Avaliacao_` e confirmação explícita; Azure e
destinos remotos são recusados. O host DEMO mantém leitura por padrão. Um opt-in
local permite somente o aceite preventivo do cenário C, com os roles existentes,
gerando uma ordem sem duplicação. O frontend local com `VITE_DEMO_MODE=true` exibe
**DEMO · DADOS SINTÉTICOS**; não alterar as variáveis de produção para o ensaio.

Consulte a [preparação segura](docs/demo/preparacao-ambiente.md), o
[roteiro de 6min30](docs/demo/roteiro-apresentacao.md) e o
relatório da Sprint 07 (histórico no Git).

## Build e testes

Na raiz, após restaurar as dependências quando necessário (`npm.cmd ci --prefix frontend`
e `dotnet restore backend/Solution/Solution.slnx`):

```powershell
npm.cmd --prefix frontend run build
npm.cmd --prefix frontend run lint
node --test frontend/tests/*.test.js
dotnet build backend/Solution/Solution.slnx --no-restore
dotnet test backend/Tests/Tests.csproj --no-build --no-restore
```

A recuperação de senha foi validada com **368 testes backend e 197 frontend**,
preservando a base administrativa e acrescentando cobertura de tokens, autorização,
concorrência, limites e transporte.
A suíte HTTP usa controllers reais, JWT e Kestrel com persistência InMemory, sem iniciar a API
normal ou acessar conexões privadas. Modelo SQL e snapshot também são conferidos sem conexão.
Bancos LocalDB novos validaram as duas migrations pendentes, repetição do SQL, consumo
concorrente e rollback. O Chrome validou recuperação pública/administrativa e
responsividade; o provedor real aguarda configuração e homologação privadas.
Na Sprint 07, bancos novos exclusivos LocalDB e a API DEMO real confirmaram integridade,
carga repetida sem inserções, aceite único preservado na recarga, roles e navegação
no Chrome em 320/390/768/1440 px. Nenhum banco Azure foi acessado.
Na Sprint 06, dois processos independentes no mesmo LocalDB confirmaram bloqueio SQL, repetição sem
novas pendências e rollback em falha/cancelamento. No Chrome, a Central preservou situação operacional,
exibiu riscos por equipamento/bairro e passou por carga, vazio, erro/recuperação e permissões.
Na Sprint 05, o Chrome com API real e banco novo exclusivo no SQL Server LocalDB validou riscos,
fatores, análise repetida, aceite sem duplicação de ordem, navegação à manutenção, permissões,
carregamento, falha parcial/recuperação e larguras 320/390/768/1440 px, sem exceções JavaScript.
Na Sprint 03, bancos novos e exclusivos no SQL Server LocalDB validaram aplicação das cinco migrations,
upgrade preservando uma ordem anterior, aceite de recomendação, vínculo, unicidade real e ciclo preventivo.
Essas verificações locais não comprovam o estado do Azure. A Sprint 04 eliminou o aviso de bundle
acima de 500 kB por carregamento separado dos módulos.

No Chrome, a Sprint 04 validou o bundle compilado com API real e SQL Server LocalDB isolado:
oito tipos de marcador, três categorias de agentes, descarte de coordenada inválida, filtros combinados,
alertas, atualização sem F5, preservação de contexto, rede/recuperação automática e conclusão de ordem
refletida nos indicadores. Também foram testados permissões, larguras 320/390/768/1440 px e falha/recuperação
de um módulo carregado sob demanda. As verificações anteriores de agentes/equipamentos/manutenções seguem na suíte.

No Chrome, a Sprint 03 validou preventiva e corretiva completas com API real e LocalDB, histórico,
retorno ao equipamento, edição sem perder precisão da previsão, filtros, recuperação de rede e permissões.
Duas solicitações simultâneas produziram HTTP 201/409 e uma única ordem pendente. Foram conferidas larguras
320/390/768/1440 px, sem transbordamento horizontal da página ou exceções JavaScript não tratadas.

Uma verificação adicional no Chrome com API local de teste exercitou as três categorias de agentes,
cadastro/edição, vínculos, duplicidade, F5, rede, permissões e mapa atualizado. Três marcadores continuaram
visíveis após atualização e o indicador passou de dois para um ativo quando um agente ficou Offline.
Também houve regressão do cadastro de equipamento com UF e conferência em 320/390/768/1440 px, sem
exceções JavaScript ou transbordamento horizontal da página. Isso não homologa celulares físicos ou produção.

## Limitações e próximos passos

- Registro/gestão de ocorrências e resolução manual de alertas pela interface continuam pendentes;
  a visualização operacional de alertas e a interface preditiva estão concluídas no escopo descrito. Em manutenção, cancelamento,
  reagendamento, atribuição de equipe na interface e auditoria de todas as transições são evoluções futuras.
- Listagens de equipamentos, agentes e manutenções usam busca/filtros e paginação no cliente; paginação de servidor e
  detecção de edição concorrente não fazem parte desta sprint.
- Categoria de equipamento é descritiva; modelos especializados de balanças/atuadores não foram
  acrescentados. Endereços antigos ficam preservados quando uma localização é substituída.
- Vínculos de agentes abrangem somente as classes de veículos existentes; área de atuação permanente
  e exclusão na interface continuam fora do escopo. A gestão de contas possui módulo Admin e concorrência própria.
- Não há clustering/spiderfy; marcadores próximos podem se sobrepor. O polling não é streaming físico de IoT
  e os endpoints separados não representam uma fotografia transacional única de toda a cidade.
- Os índices preditivos são heurísticos; MTBF e anomalias de volume usam estatística descritiva,
  sem modelo calibrado, garantia de ocorrência futura ou análise numérica de payloads de sensores.
  Cobertura de agentes usa posição/disponibilidade declaradas; despacho automático não foi implementado.
  A coordenação SQL da Sprint 06 exige todos os escritores atualizados e o mesmo banco/recurso de bloqueio.
  Duplicatas legadas exigem auditoria; ordens ativas continuam protegidas pelo índice SQL existente.
- A janela padrão de ocorrências do calor é de sete dias. Dados DEMO antigos podem deixar de aparecer;
  a carga idempotente não renova datas já persistidas.
- Confirmar requisitos no enunciado oficial antes de ampliar tipos de equipamento ou funcionalidades opcionais.
- Concluir documentação final de entrega e declaração das ferramentas de IA utilizadas;
  o roteiro e a preparação DEMO estão disponíveis em `docs/demo/`.

O [índice de documentação](docs/README.md) contextualiza os relatórios históricos e as
mudanças posteriores. As contagens e autorizações antigas não devem ser tratadas como estado atual.
