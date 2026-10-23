# Sprint 01 — Gestão de equipamentos

Revisão de 23/10/2026. Escopo: experiência de equipamentos no frontend e ajustes necessários nos
contratos existentes. Não houve alteração de Azure/deploy, banco de produção, commit ou push.

## Resultado

| Funcionalidade | Estado | Entrega |
| --- | --- | --- |
| Navegação Equipamentos | Completa | Sidebar habilitada, layout/header existentes, acesso direto por `#equipamentos` e F5 |
| Listagem, busca e filtros | Completa no escopo | Busca em série/marca/modelo/categoria/bairro; filtros de tipo/situação/bairro/necessidade; paginação local de dez registros |
| Cadastro | Completa | Oito tipos existentes, identificação, atributos pertinentes, categoria de uso e localização completa ou existente |
| Edição | Completa | Dados e planejamento editáveis para gestores; tipo imutável e vínculo de agente existente preservado |
| Detalhes completos | Completa | Identificação, categoria, situação, planejamento, atributos do tipo, endereço/referência/GPS e vínculo atual quando houver |
| Situação operacional | Completa para inventário | PATCH existente, opções por estado/perfil, confirmação explícita e regras efetivas no servidor |
| Necessidade/próxima data | Completa como metadados | Agendada/Periódica exigem data; falha e manutenção em andamento protegem o planejamento |
| Tipos do enunciado | Parcial por classificação | Categorias descritivas usando os tipos existentes; não cria subclasses ou comandos próprios de atuadores |
| Permissões | Completa no escopo | Gestores criam/editam; Staff consulta/altera situações permitidas; Agent e anônimo são recusados pela API |
| Carregamento/erro/vazio/sucesso | Completa | Feedback explícito, validação por campo, conflitos 409, modo Demo 405 e tratamento 401/403/rede |
| Integração SQL Server persistente | Pendente de homologação local | Modelo/migration conferidos sem conexão; execução funcional usou InMemory |

Não foi implementada exclusão na interface. Agentes, ordens de manutenção e cidade preditiva não
ganharam novas funcionalidades nesta sprint. O fluxo corretivo/preventivo de abertura/encerramento
de manutenção e a matriz existente foram preservados.

## Endpoints utilizados

| Método/rota | Uso |
| --- | --- |
| `POST /api/auth/login` | Autenticação existente |
| `GET /api/equipments` | Carregar inventário; também validar restauração de sessão do técnico |
| `GET /api/equipments/{id}` | Dados atuais para detalhes/edição |
| `POST /api/equipments` | Cadastrar equipamento, opcionalmente com endereço completo no mesmo corpo |
| `PUT /api/equipments/{id}` | Editar identificação, atributos, categoria, planejamento e localização |
| `PATCH /api/equipments/{id}/status` | Situação operacional e motivo opcional |
| `GET /api/addresses` | Escolher localização existente |
| `GET /api/addresses/{id}` | Consultar endereço completo nos detalhes |

As rotas de endereço de escrita continuam existentes, mas a tela não as chama para criar uma
localização separadamente. O GET de equipamentos mantém seus filtros de servidor anteriores;
a busca/filtros da nova tela são aplicados localmente sobre o inventário carregado.

## Decisões técnicas

1. **Reutilização:** controllers, serviços, repositórios, DTOs e regras de situação existentes foram
   estendidos. A divisão Domain/Application/Infrastructure/Api foi mantida; nenhuma biblioteca de UI
   ou nova dependência de runtime foi adicionada.
2. **Categoria separada do tipo:** `Category`, texto opcional com até 80 caracteres, descreve a
   finalidade sem alterar EquipmentType ou o discriminador TPH. Exemplos: Vehicle + Transporte escolar,
   Motorcycle + Moto de agente de trânsito, Sensor + Balança de caminhão com pesagem/kg. Para cancelas,
   o cadastro representa o controlador IoT de acesso em Sensor com essa categoria explícita. Isso
   não equivale a criar um domínio especializado de cancela, controlar abertura ou modelar atuadores.
3. **Migration aditiva:** `20261023214553_AddEquipmentCategory` adiciona somente uma coluna nullable
   em Equipments. Nenhuma tabela/tipo existente foi substituído. Designer/snapshot foram gerados pelo
   EF com uma factory temporária de scaffolding, já removida, sem iniciar a API ou abrir conexão.
   A migration não foi aplicada a nenhum banco persistente. Seu Down padrão remove a coluna e não
   deve ser executado como parte da revisão. A API não aplica migrations automaticamente.
4. **Endereço e equipamento na mesma gravação:** o contrato aceita **AddressId ou Address**, nunca
   ambos. Address é validado pelo validador já existente. O serviço cria a localização e o equipamento
   com o único SaveChanges do CRUD. No provider relacional, essa gravação usa a transação padrão do EF.
   Falha de validação/duplicidade ocorre antes de adicionar endereço. A atomicidade SQL real ainda
   requer homologação com esse provider; InMemory não simula transações relacionais.
5. **Endereços compartilhados:** editar a localização cria uma nova referência, sem modificar o
   endereço de outros equipamentos. Também é possível selecionar um endereço já existente. Endereços
   antigos ficam preservados; não há limpeza/exclusão automática nesta sprint.
6. **Planejamento do inventário:** campos opcionais MaintenanceNeed/NextMaintenanceDate utilizam
   colunas que já existiam. Omitir planejamento preserva valores anteriores na edição. None/Immediate
   não recebem data; Scheduled/Periodic exigem data. Equipamentos em falha não podem ter a necessidade
   imediata ocultada e equipamentos em manutenção não podem ter o planejamento alterado pelo CRUD.
   Esses campos não abrem nem encerram manutenção. A próxima data é normalizada em UTC no DTO de saída.
7. **Situação separada da edição:** novo cadastro inicia NotInstalled. Alterações usam a rota PATCH
   existente para preservar alertas/regras/permissões. Entrar/sair de manutenção por ordens continua
   fora da tela. Furtado não possui transições de saída; a alteração exige confirmação na interface.
8. **Roles existentes:** Admin/OperationManager criam/editam; CentralOperator e MaintenanceTechnician
   consultam e usam o PATCH conforme regras Staff. Só gestores reativam Deactivated. O técnico entra
   diretamente no inventário e restaura sessão por GET de equipamentos, sem ganhar acesso ao dashboard
   ou mapa. JWT/autorizações no servidor permanecem responsáveis por permitir ou negar operações.
9. **Tratamento de erro:** PUT/PATCH foram acrescentados ao transporte HTTP. Somente formulários opt-in
   extraem chaves de ValidationProblemDetails; não reproduzem mensagens, HTML ou detalhes arbitrários
   do servidor. A interface apresenta mensagens locais de campo e conflito. O middleware foi corrigido
   para preservar Errors ao serializar ValidationProblemDetails, comportamento comprovado por HTTP.
10. **Atualização e cancelamento:** leituras têm AbortController e não publicam resultados cancelados;
    envio bloqueia repetição/navegação interna enquanto pendente. Retornar ao dashboard consulta a API
    novamente. Falha de rede em escrita pede conferir a lista antes de reenviar, pois o cliente pode
    não ter recebido a confirmação de uma gravação já concluída.
11. **Visual:** cores, tipografia, sidebar e header existentes foram reutilizados. Formulários mudam
    por tipo, usam rótulos e erros acessíveis. A tabela tem rolagem própria em larguras pequenas;
    foi corrigida a contenção de conteúdo acessível que causava transbordamento da página em 768 px.

## Arquivos alterados/adicionados

| Área | Arquivos |
| --- | --- |
| Domínio | `backend/Domain/Entities/Equipments/Equipment.cs` |
| Contratos/mapeamento | `backend/Application/DTOs/Equipment/EquipmentDtos.cs`; `backend/Application/Mappings/Mappings.cs` |
| Regras de aplicação | `backend/Application/Services/EquipmentService.cs`; `backend/Application/Validators/EquipmentValidators.cs`; `backend/Application/Validators/AddressValidators.cs` |
| Erros HTTP | `backend/Api/Middleware/ExceptionHandlingMiddleware.cs` |
| Persistência | `backend/Infrastructure/Data/Configurations/EquipmentConfiguration.cs`; `backend/Infrastructure/Data/Migrations/20261023214553_AddEquipmentCategory.cs`; `.Designer.cs` correspondente; `backend/Infrastructure/Data/Migrations/AppDbContextModelSnapshot.cs` |
| Testes backend | `backend/Tests/EquipmentInventoryTests.cs`; `backend/Tests/EquipmentHttpTests.cs`; `backend/Tests/Support/EquipmentTestHost.cs` |
| Tela/componentes | `frontend/src/pages/EquipmentPage.jsx`; `frontend/src/pages/EquipmentPage.css`; `frontend/src/components/equipment/EquipmentForm.jsx`; `EquipmentDetails.jsx`; `equipmentModel.js` na mesma pasta |
| Navegação/layout | `frontend/src/App.jsx`; `frontend/src/pages/DashboardPage.jsx`; `frontend/src/components/dashboard/DashboardSidebar.jsx`; `DashboardHeader.jsx`; `dashboardData.js` na mesma pasta |
| Serviços/hooks | `frontend/src/services/equipments.js`; `http.js`; `auth.js`; `session.js` na mesma pasta; `frontend/src/hooks/useAuth.js`; `frontend/src/hooks/useEndpointVerification.js` |
| Testes frontend | `frontend/tests/equipments.test.js` |
| Entrega | `README.md`; `docs/reports/sprint-01-equipamentos.md` |

Artefatos locais de verificação ficam em `artifacts/sprint-01/`, ignorados pelo Git. O host auxiliar,
os dados sintéticos de teste e a automação de navegador não integram o fluxo real da aplicação.
Não foram alterados workflows, arquivos de configuração privada, configurações Azure ou dependências.

## Testes e evidências

| Verificação | Resultado |
| --- | --- |
| Suíte backend | **142 aprovados**, zero falhas/ignorados: 120 anteriores + 22 novos casos |
| Suíte frontend | **115 aprovados**, zero falhas/ignorados: 104 anteriores + 11 novos casos |
| Build frontend | Aprovado |
| Lint frontend | Aprovado |
| Build da solução | Aprovado, sem avisos ou erros |
| Modelo/migration | Snapshot compatível com o modelo SQL; script de criação contém Category nullable; sem conexão |
| Chrome com API local | Cadastro, detalhes, edição, situação, busca, troca de tipo, conflito 409, F5, papéis operador/técnico, retorno ao dashboard atualizado e recuperação de falha de rede aprovados |
| Layout no Chrome | 320, 390, 768 e 1440 px sem transbordamento horizontal da página; zero exceções JavaScript nos cenários exercitados |

Os testes HTTP iniciam Kestrel em porta loopback dinâmica, usam os controllers reais, middleware de
erros, validação e autenticação JWT com chave exclusiva do teste, e persistem em EF InMemory. Verificam
401 de anônimo, 403 de Agent, leitura de Staff, bloqueio de POST/PUT de não gestores, criação/edição
por Admin/OperationManager e bloqueio de reativação por operador/técnico. Também conferem filtros GET,
PATCH, duplicidade 409 e validação 400 com erros por campo. Não há dependência de segredo ou banco real.

Os testes de serviço verificam endereços compartilhados, ausência de endereço novo após falha de
validação/duplicidade, preservação de planejamento de clientes antigos, datas obrigatórias e proteção
do planejamento durante falha. Os testes frontend verificam payloads por tipo, campos irrelevantes
omitidos, validação, preservação de agente/tipo, filtros combinados, transporte, permissões e F5 do técnico.

A verificação no Chrome usou autenticação e endpoints reais de um host isolado com InMemory e usuários
sintéticos. A URL da API foi sobrescrita somente no processo Vite de teste. Não foram alterados `.env.local`
ou contas reais. `artifacts/sprint-01/browser-results.json` registra os resultados sem credenciais;
há capturas locais de lista desktop e formulário mobile. Isso não comprova persistência SQL, desempenho
com inventário grande ou funcionamento em celular físico.

Para repetir as verificações automatizadas, na raiz:

```powershell
dotnet build backend/Solution/Solution.slnx --no-restore
dotnet test backend/Tests/Tests.csproj --no-build --no-restore
npm.cmd --prefix frontend run build
npm.cmd --prefix frontend run lint
node --test frontend/tests/*.test.js
```

## Conferência objetiva no navegador

1. Siga a seção **Gestão de equipamentos — Sprint 01** do README: API desta versão, banco local
   isolado, migration aplicada manualmente nesse destino e conta local existente. Não use produção.
2. Inicie o frontend com `VITE_API_BASE_URL` apontando para essa API e abra
   `http://127.0.0.1:5173/#equipamentos`.
3. Entre como Admin ou OperationManager. Confira Equipamentos habilitado, carregamento, contagens e
   vazio quando aplicável. Pesquise por série/marca/modelo/categoria e combine filtros; limpe-os.
4. Em **Novo equipamento**, alterne os oito tipos. Campos específicos devem aparecer apenas quando
   pertinentes. Cadastre câmera com visão noturna/resolução/cor e localização completa; confira detalhes.
5. Cadastre Sensor de pesagem com categoria Balança de caminhão e unidade kg; represente o controlador
   de cancela explicitamente como controlador IoT; cadastre Vehicle com categoria Transporte escolar e
   placa válida. Categoria não deve alterar o tipo técnico.
6. Edite identificação, características e planejamento. Agendada/Periódica exigem próxima data. Confira
   que o tipo está bloqueado na edição e que um vínculo de agente existente é preservado.
7. Edite localização usando **Informar outra localização**; confira endereço completo. Se outro
   equipamento usava o endereço anterior, ele deve continuar com aquele endereço.
8. Nos detalhes, altere NotInstalled para Operational com confirmação. Verifique listagem e, para
   papéis de monitoramento, retorne ao mapa/dashboard para conferir os dados atualizados.
9. Tente cadastrar série/placa duplicada e campos inválidos: o formulário deve preservar os dados e
   mostrar mensagens compreensíveis. Interrompa a rede e atualize para conferir falha e recuperação.
10. Entre como CentralOperator e MaintenanceTechnician: não deve haver Novo/Editar; detalhes e mudanças
    de situação devem seguir as permissões atuais. Técnico deve permanecer no inventário após F5 e
    não ganhar acesso ao dashboard/mapa. Agent deve ser recusado.
11. Em um equipamento Deactivated, operador/técnico não podem reativar; gestor pode. Abrir/encerrar
    manutenção continua fora da tela. Evite usar Furtado em registros que precisem voltar à operação,
    pois a matriz atual não permite saída desse estado.
12. Confira formulário, foco/teclado e rolagem da tabela em desktop e celular. A tabela pode rolar
    horizontalmente dentro de seu contêiner; a página deve permanecer contida na largura da tela.

## Pendências e limites

- Homologar persistência e gravação relacional no SQL Server local após aplicar a migration nesse
  destino. Nenhuma aprovação de produção é inferida da validação InMemory.
- Há paginação somente no cliente e o GET carrega o inventário completo; otimização para grandes
  volumes e resolução de edições concorrentes ficam para uma etapa própria.
- O módulo representa categorias adicionais de forma descritiva. Funcionalidades específicas de
  balanças/atuadores, campos especializados adicionais e automação física de cancelas não foram criados.
- Endereços antigos são preservados após substituição; não há limpeza destrutiva automática.
- Modo HTTP Demo continua bloqueando gravações e não serve para homologar cadastro/edição.
- A ferramenta histórica `DemoData.Validation` está vinculada ao manifesto original de três migrations;
  ela não foi adaptada nem executada para preparar o schema desta sprint. Use o procedimento local
  atualizado do README para a versão atual.
- Agentes, ordens de manutenção, alertas e cidade preditiva permanecem fora da sprint; a restrição
  já existente para abrir manutenção preventiva não foi alterada neste trabalho.
