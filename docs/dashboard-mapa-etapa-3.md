# Etapa 3 — dashboard operacional e mapa base

Data: 20/10/2026, referência America/Sao_Paulo.

Dashboard inicial em preto, cinza escuro e amarelo, com indicadores reais quando retornados pela API e mapa base Leaflet independente da disponibilidade desses indicadores. Nenhum marcador, heatmap visual, filtro geográfico ou polling foi implementado.

## Dependências

Instaladas com versões exatas:

- `react-leaflet@5.0.0`
- `leaflet@1.9.4`

Comando: `npm.cmd install --save-exact react-leaflet@5.0.0 leaflet@1.9.4 --no-audit --no-fund`.

`package.json` e `package-lock.json` foram atualizados. A dependência transitiva instalada é `@react-leaflet/core@3.0.0`, usando a mesma instalação de Leaflet 1.9.4. Não foi instalado `leaflet.heat`.

## Arquivos criados nesta etapa

- `frontend/src/pages/DashboardPage.jsx`: layout da dashboard, sidebar, cabeçalho, usuário/logout, atualização manual, indicadores, mapa e verificação recolhível.
- `frontend/src/pages/DashboardPage.css`: layout e responsividade da dashboard.
- `frontend/src/components/dashboard/DashboardIndicators.jsx`: total de equipamentos, disponibilidade e agentes ativos, provenientes da resposta de `/api/dashboard`; loading, erros e ausência de equipamentos.
- `frontend/src/components/map/MapView.jsx`: MapContainer, TileLayer, estados dos tiles e observação de redimensionamento.
- `frontend/src/components/map/MapView.css`: dimensões explícitas, altura responsiva e apresentação dos controles/atribuição.
- Este relatório.

## Arquivos modificados nesta etapa

- `frontend/package.json` e `frontend/package-lock.json`: instalação das versões autorizadas.
- `frontend/src/App.jsx`: dashboard como destino do login autorizado, preservando login e bloqueio de papéis.
- `frontend/src/components/EndpointVerification.jsx`: apresentação dos quatro resultados recebidos da dashboard, sem criar uma segunda rodada de consultas ou outro hook de dados.
- `frontend/src/App.css` e `frontend/src/index.css`: cores, tipografia, formulários e estilos compartilhados.
- `frontend/README.md`: estado atual, dependências e validação.

Os arquivos da etapa 2 já estavam presentes no diretório de trabalho. Não foram descartadas essas alterações anteriores. Serviços HTTP, hooks de autenticação/consultas, seus testes e o ponto de entrada com StrictMode foram reutilizados sem alterações nesta etapa.

## Composição e dados

```text
App
├── LoginForm / mensagem de papel sem acesso
└── DashboardPage
    ├── Sidebar e cabeçalho com logout
    ├── useEndpointVerification existente
    ├── DashboardIndicators
    ├── MapView
    │   └── MapContainer
    │       ├── TileLayer
    │       └── MapResizeObserver
    └── details → EndpointVerification
```

A dashboard compartilha a rodada existente de GETs de `/api/dashboard`, `/api/equipments`, `/api/agents` e `/api/heatmap?metric=Incidents`. A seção principal mostra somente os três indicadores do dashboard; a seção de verificação detalhada permanece acessível pela sidebar ou pelo elemento recolhível. Atualizar indicadores reconsulta os quatro recursos manualmente, sem polling e sem duplicar hooks/cliente HTTP.

Durante carregamento e em falhas, indicadores são apresentados como `—`, sem números de demonstração. Uma resposta válida com total zero apresenta mensagem de ausência de equipamentos. Falhas de outros endpoints continuam visíveis na seção de verificação. O comportamento preexistente de encerrar a sessão em 401 foi preservado, assim como token em memória, expiração e papéis de monitoramento.

## Mapa e política de tiles

- Centro inicial: `[-23.5505, -46.6333]`; zoom inicial: 12.
- Controles nativos de zoom, arraste, teclado e roda do mouse.
- CSS oficial do Leaflet importado no componente.
- Tiles: `https://tile.openstreetmap.org/{z}/{x}/{y}.png`.
- Atribuição visível com link de copyright do OpenStreetMap.
- Nenhum token da API é enviado aos servidores de tiles. O TileLayer não utiliza o cliente HTTP da aplicação.
- Sem prefetch, downloads offline ou recursos de pré-carregamento; `keepBuffer={0}` e `updateWhenIdle` reduzem requisições fora da área em uso. O navegador mantém a política normal de cache dos tiles, sem parâmetros de quebra de cache.
- Contêiner com altura explícita e responsiva; ResizeObserver executa `invalidateSize({ pan: false })` após mudanças de tamanho e limpa observer/frame ao desmontar.
- MapContainer não tem chave baseada em indicadores, não depende do estado de carregamento da API e permanece montado durante as atualizações manuais. Alterar largura/altura não cria outra instância.
- Mensagens separadas para carregamento e falha dos tiles, sem ocultar a atribuição. A disponibilidade real do mapa base depende da rede e do provedor.

Referências consultadas: [MapContainer e hooks do React Leaflet](https://react-leaflet.js.org/docs/api-map/) e [política de tiles do OpenStreetMap](https://operations.osmfoundation.org/policies/tiles/).

## Verificações

| Verificação | Resultado |
| --- | --- |
| `npm.cmd run build` | Aprovado, Vite 8.3.4 |
| `npm.cmd run lint` | Aprovado, sem desabilitar regras |
| `node --test tests/services.test.js` | 19 aprovados, 0 falhas |
| `npm.cmd ls react-leaflet leaflet leaflet.heat` | React Leaflet 5.0.0 e Leaflet 1.9.4; sem leaflet.heat |
| Navegador | Chrome headless instalado, via Chrome DevTools Protocol |
| Integração autenticada com backend real | Não validada nesta etapa |
| Cartografia real do OpenStreetMap | Não validada no navegador nesta etapa |

O navegador foi realmente executado, e as capturas desktop e celular foram inspecionadas. O teste usou interceptação local das respostas da API e dos tiles antes de carregar a página. Os tiles de teste estão explicitamente identificados como sem cartografia real. Nenhuma navegação automatizada foi realizada contra os servidores de tiles públicos, e nenhuma credencial real ou alteração no banco foi necessária. Esses dados controlados existem somente no teste e não são parte da aplicação.

Com essas respostas controladas, foram verificados:

1. Login pela interface e entrada na dashboard.
2. Contêiner do mapa visível, tiles renderizados e atribuição presente.
3. Zoom + de 12 para 13 e retorno com zoom −.
4. Mesmo elemento e identificador Leaflet após atualizar os indicadores.
5. Quatro endpoints acessíveis na verificação recolhível.
6. Resposta 500 no dashboard sem indicadores inventados, mantendo o mapa.
7. Layout móvel em 390px, sem overflow horizontal e com mapa redimensionado.
8. Ausência de requisições periódicas durante a observação do teste.
9. Mensagem de falha ao simular indisponibilidade dos tiles.
10. Logout, bloqueio do papel Agent sem GETs protegidos e retorno ao login após recarregar.
11. Ausência de exceções JavaScript em execução durante o cenário.

Artefatos locais ignorados pelo Git, em `artifacts/etapa-3/`: `browser-check.mjs`, `browser-result.json`, `dashboard-desktop.png` e `dashboard-mobile.png`. O script de navegador não adicionou dependências ao projeto e não é teste contra serviços reais. A captura desktop usa viewport 1440px; a captura móvel usa 390px.

## Limitações e execução

A API local permaneceu inacessível nas sondagens de `/api/dashboard` em HTTP 5129 e HTTPS 7130; não houve login real. O endereço 5062 também foi sondado e recusou conexão. As sondagens não iniciaram serviços. Backend, banco SQL Server, migrations e Azure não foram modificados; `frontend/heatmap.html` foi preservado.

Para executar o frontend com configuração local válida, na pasta `frontend`:

```powershell
npm.cmd ci
npm.cmd run dev -- --host localhost --port 5173 --strictPort
```

É necessário `.env.local` com `VITE_API_BASE_URL=http://localhost:5129` ou a URL HTTPS do perfil utilizado. Copiar `.env.example` somente se não houver configuração local existente. Login e indicadores exigem API acessível e conta autorizada. As instruções de backend e os efeitos de inicialização existentes estão no README; nenhum seeder ou worker foi iniciado nesta entrega.

## Pendências para uma próxima etapa autorizada

- Validar login e respostas geográficas reais com API disponível.
- Adicionar camadas independentes de equipamentos e agentes, usando os DTOs existentes e validando coordenadas anuláveis, finitas e dentro dos limites geográficos.
- Diferenciar categoria/status e disponibilidade, com legenda e detalhes acessíveis.
- Reaproveitar a instância atual do mapa e evitar duplicação de requisições ao definir o fluxo de dados das camadas.
- Não tratar o centro inicial de São Paulo como posição de equipamento ou agente.

Nenhuma dessas pendências foi implementada. Etapa 3 concluída; Etapa 4 não iniciada.
