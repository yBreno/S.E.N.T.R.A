# S.E.N.T.R.A. — frontend

React 19 + Vite 8 em JavaScript/JSX. Dashboard operacional com mapa base Leaflet/OpenStreetMap, marcadores de equipamentos/agentes e mapa de calor para ocorrências, falhas e risco regional.

## Configuração e execução

Na raiz do repositório, usando PowerShell:

```powershell
if (-not (Test-Path -LiteralPath frontend/.env.local)) {
  Copy-Item -LiteralPath frontend/.env.example -Destination frontend/.env.local
}
Set-Location frontend
npm.cmd ci
npm.cmd run dev -- --host localhost --port 5173 --strictPort
```

Se `.env.local` já existir, preserve o arquivo e ajuste apenas a URL. Acesse http://localhost:5173.

```dotenv
VITE_API_BASE_URL=http://localhost:5129
```

A configuração é obrigatória, sem `/api` no final. Para o perfil HTTPS, utilize `https://localhost:7130` com certificado local confiável. Reinicie Vite após mudar o arquivo. Nenhuma variável `VITE_*` deve conter segredos, senhas ou tokens; esses valores são públicos no bundle. `.env.local` é ignorado pelo Git.

A API precisa ser executada separadamente. Em outro terminal, na raiz, quando sua inicialização estiver autorizada:

```powershell
dotnet run --project backend/Api/Api.csproj --launch-profile http
```

Alternativa:

```powershell
dotnet run --project backend/Api/Api.csproj --launch-profile https
```

O backend requer .NET 10, banco SQL Server/LocalDB já preparado e `Jwt:Key` em configuração privada. A inicialização em Development já chama seeders e inicia o worker de simulação, podendo alterar o banco. Esta entrega não iniciou esses serviços nem executou migrations. Use uma conta já existente; não há credenciais preenchidas no formulário.

`npm.cmd` funciona no PowerShell quando a política bloqueia `npm.ps1`, sem mudar a política do sistema. A porta 5062 citada no protótipo não corresponde aos perfis locais atuais.

## Comportamento

- Token somente em memória React. Recarregar exige login; logout, expiração ou 401 encerram a sessão.
- Monitoramento disponível para Admin, OperationManager e CentralOperator. O backend mantém a autorização efetiva.
- Após login autorizado: dashboard com indicadores de `/api/dashboard`, mapa base centrado em São Paulo e atualização manual.
- Os quatro GETs existentes (dashboard, equipamentos, agentes e pontos de heatmap Incidents) compartilham uma rodada de consultas. Seus resultados detalhados ficam na seção recolhível de verificação da API.
- Equipamentos e agentes reutilizam os registros desses mesmos GETs, com controles independentes, legenda de estados e popups operacionais. Não são realizadas consultas adicionais ao alternar camadas.
- Filtros locais combinam tipo, status e bairro dos equipamentos; disponibilidade filtra somente agentes. Limpar filtros restaura as opções iniciais, sem novas consultas ou recentralização.
- Contadores separados mostram recebidos, válidos, após filtros, descartados e na camada. A quantidade filtrada não substitui o total geral da dashboard.
- A inspeção reutiliza o conteúdo seguro do popup. Seleções removidas por filtros, camadas desligadas ou respostas novas são limpas, sem restaurá-las ao limpar filtros.
- A última resposta válida é mantida durante atualização e falhas, com aviso de possível desatualização e horário da resposta válida por endpoint.
- Arrays vazios mostram mensagens sem criar marcadores fictícios. Coordenadas precisam ser números finitos, dentro dos limites geográficos; zero é válido. Strings numéricas não são convertidas automaticamente.
- Popups não exibem CPF e renderizam textos da API com JSX. Atualizações dos dados preservam centro/zoom; abrir explicitamente um popup pode ajustar a vista para torná-lo legível.
- Resultados independentes, atualização manual e erros específicos para status HTTP, rede e contrato inválido.
- Cancelamento ao desmontar/substituir consultas, compatível com StrictMode.
- React Leaflet 5.0.0, Leaflet 1.9.4 e leaflet.heat 0.2.0, fixados no manifesto e lockfile.
- Mapa responsivo com zoom nativo, navegação, atribuição visível e tiles HTTPS oficiais. Precisa de acesso à internet para carregar a cartografia.
- Equipamentos, agentes e calor podem ser ativados/desativados independentemente. O calor inicia desativado.
- Abra "Mapa de calor" para escolher a métrica e os filtros. "Aplicar filtros" consulta ou reutiliza a consulta equivalente; "Atualizar calor" solicita uma atualização explícita. Alterar campos cancela chamadas anteriores e não dispara outra automaticamente.
- Incidents permite período, horas, dia, bairro e tipo de ocorrência. Failures mantém os mesmos filtros temporais/bairro, com tipo EquipmentFailure fixo no backend. Risk permite somente bairro e instante de referência.
- O calor reaproveita a resposta padrão de Incidents já recebida pela verificação. As demais consultas usam o mesmo cliente JWT e cache em memória por parâmetros.
- Intensidade zero é válida e contabilizada sem produzir mancha. Pontos inválidos são descartados; valores positivos não recebem piso artificial. As cores são intensidade visual, não probabilidade estatística.
- Risk mostra referências regionais, não polígonos de bairros nem certeza de ocorrência. Datas usam o fuso do navegador; faixa de horas/dia usa o horário configurado no backend.
- Nenhum polling ou armazenamento persistente de credenciais.

## Verificação

Dentro de `frontend`:

```powershell
npm.cmd run build
npm.cmd run lint
node --test tests/*.test.js
```

Os testes usam transporte controlado e não substituem login real. Na etapa 6, build, lint e 70 testes passaram. O Chrome verificou pixels do plugin real, as três métricas, zero/vazio sem manchas, camadas independentes, cache, cancelamento, 401/403/rede, limpeza e layout móvel com API e tiles interceptados. O carregamento do plugin também foi conferido em um build de produção de teste. Isso não comprova dados reais do Azure nem acesso à cartografia real. Nenhum serviço Azure ou banco foi alterado.

Para estrutura dos arquivos, evidências, limitações e checklist manual, consulte [o relatório da etapa 2](../docs/integracao-api-etapa-2.md). O protótipo `heatmap.html` foi preservado.

Para as mudanças da dashboard, comportamento do mapa, evidências de navegador e próximas pendências, consulte [o relatório da etapa 3](../docs/dashboard-mapa-etapa-3.md).

Para camadas operacionais, validação dos dados e testes de marcadores, consulte [o relatório da etapa 4](../docs/marcadores-etapa-4.md).

Para filtros, contadores, inspeção e preservação da última resposta válida, consulte [o relatório da etapa 5](../docs/filtros-inspecao-etapa-5.md).

Para o mapa de calor, compatibilidade do plugin, controles, ciclo de vida e evidências, consulte [o relatório da etapa 6](../docs/heatmap-etapa-6.md).
