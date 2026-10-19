# F.O.G.O. — Ferramenta Onisciente de Gestão e Operações

Plataforma integrada para apoiar a gestão de mobilidade, trânsito e segurança urbana (**Desafio PROMETHEUS**), reunindo **Inventário Inteligente da Cidade**, **Centro de Operações da Cidade** e **Cidade Preditiva** em uma única solução.

---

## Status do Projeto (Cruzamento com o Documento de Contexto)

### ✅ Feitos

- **Arquitetura em Monólito Modular (.NET 10 + EF Core + SQL Server):** Separação limpa nas camadas `Domain`, `Application`, `Infrastructure`, `Api` e `Tests` com migrations (`InitialCreate`, `RemoveOrganization`, `AddPredictiveCity`) aplicadas no banco `FogoDb`.
- **Autenticação JWT, Papéis e LGPD:** Login/registro com BCrypt, proteção por papéis (`Admin`, `OperationManager`, `CentralOperator`, `MaintenanceTechnician`, `Agent`) e mascaramento de CPF nos retornos.
- **3.1 Inventário Inteligente de Equipamentos (TPH):** Cadastro, consulta, atualização e exclusão segura de câmeras, radares, sensores, veículos, patinetes, parquímetros, motos e veículos autônomos com atributos específicos por tipo.
- **3.1 Localização e Endereços (RD-01):** Entidade e endpoints de endereço exigindo CEP, rua, cidade/UF, bairro obrigatório e validação de coordenadas GPS (`Latitude` e `Longitude`).
- **3.1 Status Operacional e Matriz de Transição (RD-02):** Máquina de estados validada (`NotInstalled`, `Operational`, `Malfunctioning`, `UnderMaintenance`, `Deactivated`, `Stolen`) impedindo transições inválidas.
- **3.1 Gestão de Manutenções:** Abertura e fechamento de manutenções com urgência, periodicidade, custo, cálculo automático de `NextMaintenanceDate` e restauração automática do equipamento para `Operational`.
- **3.1 Cadastro e Disponibilidade de Agentes:** CRUD de agentes de trânsito, guardas e policiais com controle de disponibilidade (`Available`, `Busy`, `Offline`) e atualização de posição GPS.
- **3.2 API do Centro de Operações (Dashboard, Filtros e Alertas):** Endpoints de dashboard consolidado (`/api/dashboard`), filtros de equipamentos por tipo/status/bairro (`/api/equipments`) e gestão de alertas (`/api/alerts`).
- **3.2 Telemetria IoT e Simulação em Tempo Real:** Endpoints de telemetria (`/api/telemetry`) e worker em background (`TelemetrySimulationWorker`) simulando pulsos de sensores e movimentação GPS de agentes.
- **3.3 Base Histórica e Dados Sintéticos de Demonstração:** Entidade `Incident` e `PredictiveSeeder` populando 6 semanas de ocorrências, falhas e manutenções em 5 bairros (`Bela Vista`, `Centro`, `Pinheiros`, `Vila Mariana`, `Mooca`).
- **3.3 Índice de Risco Regional Transparente:** Cálculo auditável de 0 a 100 por bairro (`/api/risk`) ponderando densidade de ocorrências (40%), falhas recorrentes (30%), horário de pico (20%) e indisponibilidade (10%).
- **3.3 Detecção de Anomalias e Previsão Futura (MTBF):** Rotinas estatísticas (`/api/predictions/anomalies` via Z-Score $\mu + 2\sigma$ e falhas repetidas; `/api/predictions/failures` via desgaste $\ge 85\%$ do MTBF).
- **3.3 & 5. API de Mapa de Calor (Heatmap) + Protótipo Leaflet:** Endpoint `/api/heatmap` com filtros por métrica (`Incidents`, `Failures`, `Risk`), período, hora, dia da semana, bairro e tipo, validado em `frontend/heatmap.html`.
- **4. Fluxo Integrado (Evento $\rightarrow$ Status $\rightarrow$ Alerta $\rightarrow$ Risco $\rightarrow$ Recomendação):** O registro de ocorrência (`POST /api/incidents`) atualiza o equipamento, abre alerta, recalcula o risco do bairro e gera recomendações (`DeployAgent` / `SendMaintenance`) com JSON de explicabilidade.
- **6 & 12. Testes Automatizados (xUnit):** Suíte com 49 testes passando (`RulesTests.cs` e `PredictiveCityTests.cs`) cobrindo transições de status, validações, LGPD, ciclo de manutenção e motor preditivo.

---

### ❌ Não feitos (Pendências até a entrega)

- **6. Aplicação Frontend em React + TypeScript:** Inicializar o projeto SPA em `frontend/` (atualmente só existe a página estática de validação `frontend/heatmap.html`) consumindo a API com autenticação JWT.
- **3.1 Telas de Inventário e Manutenção no Frontend:** Criar as interfaces de listagem, cadastro, edição, troca de status de equipamentos/agentes e abertura/encerramento de manutenções.
- **3.2 Tela do Dashboard e Centro de Operações no Frontend:** Construir o painel visual com cards de disponibilidade, contadores de equipamentos por status, agentes ativos, feed de alertas e resumo regional.
- **3.2 Mapa Operacional Interativo com Marcadores no Frontend:** Renderizar no mapa Leaflet os marcadores distintos (cores/ícones por categoria e status) de equipamentos e agentes, com filtros laterais e drawer/modal de detalhes ao clicar.
- **3.3 & 5. Integração do Heatmap e Explicabilidade no Frontend React:** Incorporar a camada de mapa de calor, os cards de índice de risco, anomalias, MTBF, botões de aceitar/descartar recomendações e um aviso visual claro identificando dados simulados.
- **3.1 Tipos Extras de Equipamento do Enunciado (se exigidos literalmente):** Caso o enunciado oficial cobre todos os exemplos da seção 3.1, adicionar no enum/TPH os subtipos restantes (`TruckScale`/balança, `Gate`/portão, `Taxi`, `SchoolVehicle`).
- **12. Documentação Final e Declaração de IA no README:** Completar este `README.md` com instruções finais do frontend React, limitações conhecidas do modelo baseado em regras e declaração das ferramentas de IA utilizadas.
- **10 & 12. Pacote Final de Entrega ($\le$ 50 MB) e Vídeo de Demonstração:** Gravar o vídeo curto seguindo o roteiro da seção 10 e gerar o pacote executável (ou arquivo `.txt` com link público do GitHub/Drive).

---

## Como Executar o Backend Atual

### Pré-requisitos
- **.NET 10 SDK**
- **SQL Server LocalDB** (`(localdb)\mssqllocaldb`)

### 1. Configurar `user-secrets` (apenas na primeira vez)
```powershell
cd backend
dotnet user-secrets set "Jwt:Key" "ChaveSecretaSuperSeguraComMaisDe32Caracteres2026!" --project Api
dotnet user-secrets set "SeedAdmin:Email" "admin@fogo.local" --project Api
dotnet user-secrets set "SeedAdmin:Password" "Admin@12345" --project Api
dotnet user-secrets set "SeedAdmin:Cpf" "52998224725" --project Api
```

### 2. Aplicar as Migrations no Banco (`FogoDb`)
```powershell
cd backend
dotnet ef database update -p Infrastructure -s Api
```

### 3. Executar os Testes Automatizados (xUnit)
```powershell
cd backend
dotnet test Tests/Tests.csproj
```

### 4. Subir a API e Testar o Protótipo do Heatmap
```powershell
cd backend
dotnet run --project Api/Api.csproj
```
- **Swagger UI:** acesse `http://localhost:5062/swagger` (ou a porta exibida no terminal).
- **Protótipo do Mapa de Calor e Explicabilidade:** abra o arquivo `frontend/heatmap.html` no navegador, informe a URL da API e clique em **Autenticar & Carregar**.
