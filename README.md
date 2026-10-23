# S.E.N.T.R.A.

Plataforma de monitoramento e gestão de operações urbanas desenvolvida para o desafio
**F.O.G.O. — Ferramenta Onisciente de Gestão e Operações**, da DSIN/PROMETHEUS.
Integra inventário, monitoramento geográfico e análise preditiva explicável.

## Módulos e estado atual

| Módulo | Backend implementado | Interface React |
| --- | --- | --- |
| Inventário Inteligente da Cidade | Equipamentos e agentes: CRUD e estados; endereços; vínculo de veículos; manutenção agendada, em andamento e concluída | Gestão de equipamentos e agentes; manutenção preventiva/corretiva com listagem, filtros, detalhes, histórico, agendamento, início e conclusão |
| Centro de Operações da Cidade | Dashboard, alertas, ocorrências, telemetria e localização de agentes; consulta operacional sem executar análises preditivas | Indicadores, mapa com símbolos por tipo/estado, filtros, alertas reais, manutenção em destaque, atenção por bairro e polling configurável |
| Cidade Preditiva | Risco regional, anomalias, previsão por MTBF e recomendações com explicabilidade | Heat Map Incidents/Failures/Risk e filtros; painel de fatores, anomalias, previsões e ações de recomendações pendente |

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
  categorias apresentadas como Agente de trânsito, Guarda municipal e Policial, inclusive no mapa.

**Cartografia:** o bloqueio OpenStreetMap por ausência de Referer foi corrigido com
`strict-origin-when-cross-origin` no HTML e no TileLayer. A correção foi validada no site
publicado pelo responsável do projeto, conforme informado por ele. URL HTTPS oficial e
atribuição foram mantidas. A disponibilidade dos tiles depende do provedor e de sua política.

## Execução local

### Deploy temporário pela `finish-setup`

Os dois workflows desta branch publicam a partir de `finish-setup`: backend quando há alteração em
`backend/**` ou no seu workflow; frontend quando há alteração em `frontend/**` ou no seu workflow.
PRs não publicam. O disparo manual existente do backend também exige selecionar `finish-setup`.
Secrets, recursos e URLs foram preservados; o Static Web Apps usa `production_branch: finish-setup`.

Essa configuração só passa a valer no GitHub depois de commit/push pelo responsável. **Alterar esta
branch não desativa as definições antigas em outras branches**: a cópia remota em `main` ainda precisa
ser restringida antes de garantir exclusividade. Webhooks, Deployment Center e credencial federada
OIDC também exigem conferência autenticada. Nenhum deploy/migration foi executado nesta configuração.
Veja o [relatório e os comandos de publicação](docs/reports/configuracao-branch-finish-setup.md).

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
dotnet ef database update --project backend/Infrastructure --startup-project backend/Api
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

O modo HTTP **Demo permanece somente leitura**, salvo login; tentativas de salvar recebem uma mensagem
específica de ambiente de consulta. Ele não serve para validar os fluxos de escrita desta sprint.
O script `scripts/Start-DemoApi.ps1` citado em documentação antiga não está nesta cópia.
Os perfis normais Development podem executar seeders/simulação; o exemplo SprintLocal acima os desativa.

O [relatório de revisão](docs/reports/sprint-01-equipamentos.md) contém arquivos alterados, endpoints,
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
Há três categorias: Agente de trânsito, Guarda municipal e Policial. O CPF é enviado somente no cadastro,
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

O [relatório da Sprint 02](docs/reports/sprint-02-agentes.md) registra os endpoints, permissões, arquivos,
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
manual e não podem ser aceitas como agendamento automático. A interface preditiva continua fora do escopo.
Metadados antigos e recomendações já aceitas não são convertidos retroativamente em ordens.

O [relatório da Sprint 03](docs/reports/sprint-03-manutencoes.md) detalha diagnóstico, endpoints,
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
Veja o [relatório da Sprint 04](docs/reports/sprint-04-central-operacoes.md) para arquivos, critérios,
endpoints, evidências e roteiro de teste.

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

A validação da Sprint 04 aprovou build/lint frontend, **148 testes frontend** e **194 testes
backend**. A suíte HTTP usa controllers reais, JWT e Kestrel com persistência InMemory, sem iniciar a API
normal ou acessar conexões privadas. Modelo SQL e snapshot também são conferidos sem conexão.
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
  a visualização operacional de alertas está concluída. A interface preditiva continua pendente. Em manutenção, cancelamento,
  reagendamento, atribuição de equipe na interface e auditoria de todas as transições são evoluções futuras.
- Listagens de equipamentos, agentes e manutenções usam busca/filtros e paginação no cliente; paginação de servidor e
  detecção de edição concorrente não fazem parte desta sprint.
- Categoria de equipamento é descritiva; modelos especializados de balanças/atuadores não foram
  acrescentados. Endereços antigos ficam preservados quando uma localização é substituída.
- Vínculos de agentes abrangem somente as classes de veículos existentes; área de atuação permanente,
  gestão de contas, exclusão na interface e controle completo de concorrência ficam fora desta sprint.
- Não há clustering/spiderfy; marcadores próximos podem se sobrepor. O polling não é streaming físico de IoT
  e os endpoints separados não representam uma fotografia transacional única de toda a cidade.
- O modelo preditivo é estatístico e baseado em regras, sem garantia de ocorrência futura.
  Revisar o efeito operacional de aceitar recomendações de deslocamento: hoje não movimenta agentes.
- A janela padrão de ocorrências do calor é de sete dias. Dados DEMO antigos podem deixar de aparecer;
  a carga idempotente não renova datas já persistidas.
- Confirmar requisitos no enunciado oficial antes de ampliar tipos de equipamento ou funcionalidades opcionais.
- Concluir documentação de entrega, declaração das ferramentas de IA utilizadas e roteiro de demonstração.

O [índice de documentação](docs/README.md) contextualiza os relatórios históricos e as
mudanças posteriores. As contagens e autorizações antigas não devem ser tratadas como estado atual.
