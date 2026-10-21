# S.E.N.T.R.A. — Etapa 7.6: validação visual com API e SQL reais

## Execução e isolamento

- Frontend efetivamente utilizado: **http://localhost:5173**.
- API efetivamente utilizada: **http://127.0.0.1:5180**.
- A API já estava ativa: GET sem JWT retornou 401. Não foi reiniciada.
- Vite iniciado com `VITE_API_BASE_URL` definido somente no processo, host localhost,
  porta 5173 e strictPort. O módulo servido confirmou a URL DEMO antes do login.
- Listener Vite confirmado apenas em `::1:5173`; API apenas em `127.0.0.1:5180`.
- SHA-256 de frontend/.env.local idêntico antes/depois. O arquivo não foi editado.
- Nenhum acesso ao Azure, SQL direto, seeders, migrations, DemoData, DemoAdmin ou criação
  de conta. Backend, dados, autenticação e CORS não foram modificados.
- Login efetuado pelo próprio usuário no Chrome visível, em perfil de teste separado,
  sem salvamento de senhas. Não foram lidos inputs de login, estado React de autenticação,
  cookies, JWT, cabeçalhos Authorization ou corpos de respostas de autenticação.

Os testes visuais usaram DOM, cliques reais de ponteiro, seletores de filtros, captura de
canvas de calor e Resource Timing apenas dos endpoints operacionais. Não foram habilitados
interceptadores Fetch/Network ou fixtures. Nenhum dado operacional foi fabricado no navegador.

## Resultados reais na interface

Execução automatizada concluída em **20/10/2026 às 20:53:06, UTC−03:00**. As telas foram
também abertas e inspecionadas visualmente, incluindo as três métricas, popups e celular.

| Consulta | HTTP observado | Resultado |
| --- | ---: | --- |
| GET /api/dashboard | 200 | 60 equipamentos, 13 agentes ativos, disponibilidade 16,67% |
| GET /api/equipments | 200 | 60 recebidos, 60 válidos, zero descartados, 60 marcadores renderizados |
| GET /api/agents | 200 | 20 recebidos, 20 válidos, zero descartados, 20 marcadores renderizados |
| GET /api/heatmap?metric=Incidents | 200 | 100 recebidos/válidos/desenháveis |
| GET /api/heatmap?metric=Failures | 200 | 25 recebidos/válidos/desenháveis |
| GET /api/heatmap?metric=Risk | 200 | 5 recebidos/válidos/desenháveis |

As três métricas renderizaram manchas no canvas real Leaflet. Houve zero pontos descartados
ou com intensidade zero nas consultas observadas. As imagens usam dados persistidos na API
DEMO/SQL, não as fixtures dos testes de etapas anteriores.

## Filtros verificados

- Oito tipos: Camera, Radar, Sensor e Vehicle com oito equipamentos cada; ElectricalScooter,
  ParkingMeter, Motorcycle e AutonomousVehicle com sete cada.
- Seis estados: dez equipamentos em cada estado.
- Cinco bairros: 12 equipamentos por bairro.
- Combinação Camera + NotInstalled + Centro: um equipamento.
- Disponibilidade: Offline=7, Available=7 e Busy=6. A disponibilidade dos agentes também foi
  combinada com os filtros de equipamentos, sem alterar indevidamente a outra camada.
- Limpar filtros restaurou 60 equipamentos e 20 agentes.
- Totais recebidos/válidos foram preservados; somente os contadores filtrados/na camada mudaram.
- **Nenhum filtro operacional disparou novos GETs.**

## Mapa, interação e responsividade

- Equipamentos, agentes e mapa de calor ligados/desligados independentemente.
- Marcadores quadrados e circulares, com cores distintas para estados/disponibilidades.
- Popups e painel de inspeção funcionaram para equipamento e agente; rótulos operacionais
  existentes foram conferidos, sem exibição de CPF.
- Seleção e popup foram limpos quando o marcador deixou de corresponder ao filtro.
- Legenda textual disponível e controles nativos de zoom funcionais.
- Elemento e identificador da instância Leaflet preservados. A transformação da vista ficou
  estável durante filtros, trocas de métricas e atualização manual dos indicadores.
- Desktop em viewport 1440×1100; celular em 390×844.
- No celular, documento com largura 390 px sem overflow horizontal. Mapa com largura 364 px
  e altura aproximada de 464 px; mesma instância preservada. Controles de calor utilizáveis.

Tiles HTTPS oficiais do OpenStreetMap foram carregados sem substituição. Na conferência final,
os 24 tiles presentes estavam carregados, e a atribuição permaneceu visível. Não houve falha
de cartografia no resultado final. Não foram realizados downloads em massa ou pré-carregamento.

## Requisições e erros JavaScript

- Inicialização autenticada: quatro GETs compartilhados (dashboard, equipamentos, agentes, Incidents).
- Ativação do calor Incidents reutilizou a consulta existente.
- Failures e Risk: um GET adicional por métrica.
- Filtros, camadas e responsividade: zero GETs adicionais.
- Atualização manual explícita: quatro GETs adicionais, sem recriar o mapa.
- Total observado: dez GETs, todos HTTP 200. Nenhum host inesperado de API.
- Contagem continuou em dez numa conferência posterior; nenhum polling foi observado.
- Zero exceções JavaScript não tratadas/rejeições de Promise observadas. Esse monitoramento
  não captura valores de console nem dados privados de autenticação.

## Build, lint e testes

Executados no frontend com override de API somente no processo:

```powershell
$env:VITE_API_BASE_URL = 'http://127.0.0.1:5180'
npm.cmd run build
npm.cmd run lint
$tests = @(rg --files tests -g '*.test.js')
node --test $tests
```

Build aprovado; lint aprovado; **70 testes frontend aprovados**, zero falhas/ignorados.
O build é local; nenhum artefato foi publicado. frontend/heatmap.html foi preservado.

## Problemas e correções necessárias

1. **Horário no popup de agente:** a captura mostra localização atualizada em 20/10/2026,
   23:23:39, enquanto o navegador usa America/Sao_Paulo e o horário da consulta era cerca de
   20:53. Isso precisa ser corrigido antes de confiar no indicador de atualidade.
   `formatLocationUpdatedAt` usa `new Date(value)` diretamente; o DTO/backend usa DateTime,
   com atribuição DateTime.UtcNow e mapeamento sem normalização explícita. A causa provável
   é perda/ausência de offset na serialização após leitura SQL, interpretada como hora local.
   O corpo autenticado bruto não foi capturado nesta etapa, portanto essa causa é uma inferência
   fundamentada no código e na tela, não uma confirmação do JSON. Recomenda-se revisar o
   contrato UTC/offset e adicionar teste de fuso; não alterar datas já persistidas.
2. **Sobreposição de ícones:** há 80 marcadores renderizados, mas coordenadas DEMO próximas
   produzem sobreposição no zoom geral. Não são 80 ícones individualmente distinguíveis nessa
   escala. Zoom, filtros e alternância de camadas permitem inspeção. Não foi implementado
   agrupamento/spiderfy ou alterada a distribuição dos dados.
3. **Função do agente:** o popup ainda exibe o enum TrafficAgent em inglês. É uma melhoria
   de apresentação possível, sem impacto sobre contagem/posicionamento.

Nenhuma correção foi aplicada ao produto nesta etapa. Não foram modificados frontend/src,
backend, banco, configurações privadas ou dependências. Roteiros/evidências foram criados
somente em artifacts/etapa-7-6 (ignorado pelo Git), além deste relatório.

## Evidências sem credenciais

Resultados estruturados: `artifacts/etapa-7-6/browser-real-result.json`.

Capturas reais:

- `dashboard-desktop-real.png`
- `filtros-combinados-real.png`
- `popup-equipment-real.png`
- `popup-agent-real.png`
- `calor-incidents-real.png`
- `calor-failures-real.png`
- `calor-risk-real.png`
- `dashboard-mobile-real.png`
- `mapa-mobile-real.png`
- `controles-mobile-real.png`

Não foram capturadas telas de login ou campos de senha. As capturas contêm apenas dados
operacionais fictícios e a conta DEMO já autorizada. Frontend e API permanecem disponíveis
nos endereços locais indicados. A próxima etapa não foi iniciada.
