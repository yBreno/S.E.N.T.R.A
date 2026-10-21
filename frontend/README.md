# Frontend S.E.N.T.R.A.

## Apresentação do S.E.N.T.R.A.

O S.E.N.T.R.A. é uma plataforma de monitoramento e gestão de operações urbanas desenvolvida
para o desafio **F.O.G.O. — Ferramenta Onisciente de Gestão e Operações**, da DSIN/PROMETHEUS.
O projeto reúne Inventário Inteligente da Cidade, Centro de Operações da Cidade e Cidade Preditiva.

Este frontend implementa o Centro de Operações: autenticação, indicadores, mapa de equipamentos
e agentes e análise geográfica por mapa de calor. O backend ASP.NET Core fornece os dados e
aplica as regras de negócio e permissões. Ter um endpoint no backend não significa que sua
respectiva tela de gestão já existe. Consulte também o [README principal](../README.md).

## Tecnologias utilizadas

Versões conferidas nas dependências instaladas durante esta atualização:

| Tecnologia | Versão | Uso |
| --- | --- | --- |
| React e React DOM | 19.3.1 | Componentes e renderização da interface |
| Vite | 8.3.4 | Servidor de desenvolvimento e build |
| React Leaflet | 5.0.0 | Integração do mapa com os componentes React |
| Leaflet | 1.9.4 | Cartografia, navegação, marcadores e popups |
| leaflet.heat | 0.2.0 | Camada de calor em canvas |
| ESLint | 10.13.0 | Análise estática de JavaScript e JSX |
| Plugin React do Vite | 6.1.2 | Integração React no Vite |

O código utiliza **JavaScript/JSX e CSS**, sem migração para TypeScript. Os testes usam
`node:test` e `node:assert/strict`. O transporte HTTP utiliza `fetch` nativo.
As versões reproduzíveis estão no `package-lock.json`; React e Vite possuem intervalos
no manifesto, enquanto as três bibliotecas de mapa estão fixadas no `package.json`.

## Estrutura do frontend

```text
frontend/
├── public/brand/                     Logos oficiais, incluindo a versão transparente
├── src/
│   ├── main.jsx                      Inicialização com React StrictMode
│   ├── App.jsx                       Login, restauração e acesso à dashboard
│   ├── App.css / index.css           Estilos da aplicação e tema global
│   ├── config/api.js                 Validação da URL da API
│   ├── pages/
│   │   └── DashboardPage.jsx         Página operacional e navegação lateral
│   ├── components/
│   │   ├── Brand.jsx / Brand.css     Logo e nome completo compartilhados
│   │   ├── LoginForm.jsx             Formulário de autenticação
│   │   ├── auth/                     Composição visual e estilos do acesso
│   │   ├── dashboard/                Menu, cabeçalho, indicadores e painéis laterais
│   │   └── map/                      Mapa, marcadores, filtros, calor e inspeção
│   ├── hooks/                        Estado e integração com serviços
│   └── services/                     HTTP, autenticação, sessão e consultas
├── tests/                            Testes automatizados com Node
├── .env.example                      Exemplo de configuração pública
├── index.html                        Entrada HTML e política de referência
├── vite.config.js                    Configuração do Vite
└── eslint.config.js                  Regras de análise estática
```

Os estilos específicos acompanham as páginas/componentes. O protótipo HTML antigo foi
removido; o mapa de calor oficial é o integrado ao React.

| Hook | Responsabilidade |
| --- | --- |
| `useAuth` | Login, logout, restauração, expiração e controle visual de acesso |
| `useEndpointVerification` | Rodada compartilhada de consultas, atualização manual e cancelamento |
| `useOperationalMarkers` | Preparação e filtragem local dos marcadores |
| `useMarkerInspection` | Seleção válida de equipamento ou agente |
| `useHeatmap` | Métrica, filtros, visibilidade e recurso de consultas de calor |

## Funcionalidades implementadas

### Identidade visual e experiência

A identidade utiliza a logo oficial **sem fundo** em `public/brand/sentra-logo.png`,
compartilhada por `Brand.jsx` no login e no topo da sidebar. Abaixo da marca aparece
**Sistema Estratégico de Navegação, Tráfego e Resposta Ativa**. A versão original com
fundo foi organizada como `public/brand/sentra-logo-background.png` e não é utilizada na interface.

| Cor | Papel na interface |
| --- | --- |
| `#111316` | Fundo principal |
| `#171B1F` | Navegação e área de apresentação do login |
| `#20252A` | Painéis operacionais |
| `#FFCC00` | Ação principal, seleção e foco |
| `#F5F5F5` | Texto principal |
| `#9CA3AF` | Texto de apoio |

O login tem duas colunas no desktop: marca e apresentação da operação à esquerda,
formulário de acesso à direita. No celular, a composição vira uma coluna com marca,
nome completo e formulário. Os campos têm 50 px de altura, rótulos visíveis, autocomplete
nativo e fonte de 16 px; carregamento e erros mantêm o tratamento da autenticação existente.
Não foram adicionados fontes externas, bibliotecas ou animações de entrada.

`auth/AuthLayout.jsx` organiza apenas a apresentação. A lógica de login, restauração,
expiração, logout e autorização permanece nos hooks e serviços existentes. Os estilos
do acesso são específicos dessa tela para preservar os controles do mapa e da dashboard.
A sidebar mantém sua versão recolhida no desktop e o menu com fechamento por teclado
no celular; quando recolhida, a descrição fica oculta e a janela da logo mostra apenas
o símbolo oficial, sem alterar o arquivo original.

Na limpeza visual, `EndpointVerification.jsx`, sem consumidores, foi removido. A análise
final dos imports a partir de `main.jsx` confirmou que os **22 componentes JSX restantes**
participam da aplicação; nenhum outro JSX foi excluído.

### Autenticação e sessão

- Login por e-mail/senha integrado ao JWT da API .NET; formulário com carregamento e erro.
- Monitoramento disponível para **Admin**, **OperationManager** e **CentralOperator**.
  Outros papéis recebem uma mensagem de acesso indisponível.
- Sessão em memória e, temporariamente para demonstração, em `sessionStorage` da aba.
  Não há uso de `localStorage`, cookies de autenticação ou refresh token.
- Após F5, o frontend verifica formato e validade da sessão e consulta `GET /api/dashboard`
  com o JWT. A dashboard só aparece depois da aceitação dessa consulta protegida.
- A validade efetiva é a menor entre `exp` do JWT e `expiresAt` da resposta de login.
  A sessão armazenada é vinculada à URL da API, evitando sua reutilização ao trocar de ambiente.
- Logout, expiração e respostas 401 dos fluxos autenticados removem a sessão local.
  Uma falha de restauração também exige novo login; cancelamentos não restauram respostas antigas.
- Somente token, validade e nome/papel de apresentação são persistidos. Senha, CPF e dados
  operacionais não são guardados nesse registro. Se o armazenamento estiver bloqueado,
  o login funciona apenas em memória e não sobrevive ao F5.

**Segurança:** `sessionStorage` pode ser lido ou adulterado por scripts em caso de XSS.
Essa persistência é uma alternativa temporária, não equivalente a cookies HttpOnly.
Decodificar claims ou verificar papéis no navegador não valida a assinatura nem concede
permissão: autenticação e autorização efetivas continuam no backend. O logout local não
revoga um JWT já emitido no servidor; ele remove a sessão deste cliente.

### Dashboard

A página mostra nome/papel do usuário e cinco indicadores baseados em `/api/dashboard`:
total cadastrado, operacionais, indisponíveis, disponibilidade e agentes ativos.
Operacionais usa `byStatus.Operational`; indisponíveis soma Malfunctioning,
UnderMaintenance e Stolen, definição também mostrada no card. São números gerais da API;
os filtros do mapa não recalculam esses totais. Nenhuma variação temporal é inventada.

Há estados de carregamento, erro e ausência de dados, além do botão **Atualizar dados**.
Durante uma atualização ou falha, a última resposta válida é preservada com aviso de possível
desatualização. A seção visual de verificação de endpoints foi removida; os serviços e
consultas compartilhadas continuam sendo utilizados pelos indicadores e pelo mapa.

A sidebar pode ser recolhida no desktop e aberta como menu no celular. Seções sem telas
implementadas são identificadas como **Em breve**. A coluna direita apresenta um estado
informativo para alertas ainda não integrados e uma rosca SVG da distribuição real de
equipamentos por estado, sem dependência de gráficos ou novas consultas.

### Mapa operacional

- `MapContainer` centrado inicialmente em São Paulo (`-23.5505`, `-46.6333`), com zoom 12.
- Navegação por arraste, controles de zoom, roda do mouse e gesto de pinça em dispositivos compatíveis.
- Tiles oficiais `https://tile.openstreetmap.org/{z}/{x}/{y}.png`, com atribuição aos contribuidores.
- Política `strict-origin-when-cross-origin` no HTML e no TileLayer para enviar a origem
  legítima nas requisições aos tiles. A correção do bloqueio por ausência de Referer está
  implementada; a validação publicada foi informada pelo responsável do projeto.
- Altura explícita e responsiva, CSS do Leaflet e observação de redimensionamento do contêiner.
- Equipamentos com ícones quadrados e agentes com ícones circulares, diferenciados por estado.
- Popups e painel de inspeção reutilizam detalhes operacionais: tipo, marca/modelo, série,
  estado e bairro do equipamento; nome, matrícula, função, disponibilidade, bairro e horário
  da localização do agente. Não exibem CPF nem oferecem edição.
- Coordenadas devem ser números finitos, latitude entre −90 e 90 e longitude entre −180 e 180.
  Zero é válido; valores nulos ou strings numéricas são descartados. Identificadores vazios
  ou duplicados também não geram marcadores.
- Textos da API são renderizados com JSX. Estados desconhecidos recebem apresentação neutra.
  `TrafficAgent` é apresentado como **Agente de trânsito**; datas seguem o fuso do navegador.

Equipamentos, agentes e calor têm controles independentes. Atualizações e filtros preservam
a instância, centro e zoom do mapa. Abrir um popup pode ajustar a vista para deixá-lo legível.
A sessão pode sobreviver ao F5, mas filtros e posição do mapa não são persistidos.

### Filtros e contadores

Os filtros de equipamentos combinam **tipo**, **status** e **bairro** por interseção.
Os oito tipos disponíveis são câmera, radar, sensor, veículo, patinete elétrico, parquímetro,
moto e veículo autônomo. Os estados são Operational, Malfunctioning, UnderMaintenance,
Deactivated, Stolen e NotInstalled, com rótulos em português na interface.

Agentes são filtrados por Available, Busy e Offline. O filtro de bairro dos equipamentos
não filtra agentes. As opções de bairro são derivadas dos dados recebidos, com normalização
de espaços, caixa e acentos. **Limpar filtros** restaura as opções iniciais.

A filtragem utiliza os arrays já carregados, sem novos GETs. Os contadores distinguem:

| Contador | Significado |
| --- | --- |
| Recebidos | Registros da resposta daquele endpoint |
| Válidos | Registros com coordenadas e identificadores aceitos |
| Após filtros | Marcadores que atendem à combinação selecionada |
| Descartados | Registros com coordenadas/identificadores inválidos ou duplicados |
| Na camada | Quantidade após filtros quando a camada está ligada; zero quando desligada |

Sem resposta válida, os contadores não inventam zeros. Arrays vazios e filtros sem resultado
produzem mensagens específicas, sem registros fictícios. Desligar uma camada preserva seus
filtros. Seleções que saem da camada visível são limpas e não reaparecem ao limpar os filtros.

### Mapa de calor

`HeatmapLayer` desenha os pontos com leaflet.heat. O calor começa desativado; o painel
**Mapa de calor** permite escolher métrica, filtros e ações de consulta.

| Métrica | Conteúdo | Filtros enviados |
| --- | --- | --- |
| Incidents | Ocorrências | Período, faixa de horas, dia da semana, bairro e tipo de ocorrência |
| Failures | Falhas de equipamentos | Período, faixa de horas, dia da semana e bairro; tipo EquipmentFailure definido pelo backend |
| Risk | Índice regional de risco | Bairro e instante de referência (`to`) |

Datas digitadas são interpretadas no fuso do navegador e enviadas em UTC. Faixas de horas
e dias da semana são interpretadas pela regra de horário do backend.

- Alterar campos cancela a consulta própria pendente e não dispara outro GET automaticamente.
  **Aplicar filtros** consulta ou reutiliza o cache; **Atualizar calor** força nova consulta.
- A resposta padrão de Incidents é compartilhada com a rodada operacional. Enquanto ela
  carrega, o recurso aguarda para não duplicar a mesma chamada.
- O cache é em memória, por consulta e sessão, limitado a oito entradas. Não persiste no F5
  e não possui renovação automática. Consultas simultâneas equivalentes são deduplicadas.
- `AbortController` e identificação da geração impedem que respostas antigas substituam
  dados recentes ou encerrem a sessão por um 401 de uma consulta já cancelada.
- Falhas preservam o último sucesso apenas da mesma consulta, com aviso e horário. Uma
  consulta nova sem resultado próprio não mostra pontos dos filtros anteriores.
- Pontos com coordenadas/intensidades inválidas são descartados. Intensidade zero é válida
  e contada, mas não produz mancha. Valores positivos são preservados sem piso artificial.
- A legenda vai de menor a maior intensidade visual. A aparência depende da intensidade,
  concentração e zoom; não representa necessariamente probabilidade estatística.
  Risk usa pontos de referência regional, não polígonos de bairros.
- Mudar métrica ou pontos atualiza a camada sem recriar o mapa. A atualização geral da
  dashboard renova Incidents padrão, não uma consulta personalizada de Failures ou Risk.

**Zoom no celular:** `heatmapPinchZoom.js` acompanha os eventos de pinça transformando o
canvas existente, sem consultar a API ou redesenhar todos os pontos por frame. O encaixe final
usa a referência do bitmap inicial; no fim, o plugin faz seu recálculo normal. O adaptador
ativa essa ponte quando há suporte a transformações 3D e remove seus listeners ao desmontar.
O acesso ao canvas interno do plugin fica isolado em `leafletHeatAdapter.js`, compatível com
a versão fixada 0.2.0. A melhoria está implementada e tem testes de geometria e limpeza;
a fluidez do gesto ainda precisa ser validada visualmente em celular real.

### Responsividade

A identidade preto/cinza/amarelo usa CSS Grid, flex e media queries para adaptar sidebar,
cabeçalho, indicadores, filtros e inspeção. Em telas menores, os grupos passam para menos
colunas e o mapa mantém altura própria. Um `ResizeObserver` atualiza suas dimensões sem
recentralizar a vista. Popups têm altura máxima e rolagem para acomodar detalhes.

### Páginas e outros recursos

A página operacional existente é `pages/DashboardPage.jsx`. O login é renderizado por `App.jsx`
com `LoginForm`; a navegação lateral leva à dashboard, ao mapa operacional ou ao calor.
Não há páginas próprias de inventário, agentes, manutenção, alertas, ocorrências ou Cidade
Preditiva completa, nem um roteador de navegação entre esses módulos.

## Integração com o backend

`config/api.js` valida `VITE_API_BASE_URL`. `services/http.js` centraliza GET e POST JSON,
`Authorization: Bearer`, `AbortSignal` e erros. O cliente usa `credentials: 'omit'` e
`cache: 'no-store'` nas requisições da API; os tiles são carregados separadamente pelo Leaflet.

| Método e rota utilizados | Finalidade |
| --- | --- |
| `POST /api/auth/login` | Envia `{ email, password }`; espera `{ token, expiresAt, user }` |
| `GET /api/dashboard` | Indicadores e validação protegida na restauração da sessão |
| `GET /api/equipments` | Array compartilhado pelos marcadores e filtros de equipamentos |
| `GET /api/agents` | Array compartilhado pelos marcadores e filtros de agentes |
| `GET /api/heatmap?metric=Incidents` | Ocorrências, inclusive consulta padrão compartilhada |
| `GET /api/heatmap?metric=Failures` | Falhas, com os filtros compatíveis da tabela anterior |
| `GET /api/heatmap?metric=Risk` | Risco regional, por bairro e referência |

Nenhuma tela atual cria ou edita registros operacionais. Os endpoints de inventário,
manutenção, risco detalhado, previsões e recomendações não são consumidos por páginas próprias.

| Situação | Tratamento |
| --- | --- |
| 400 | Mensagem de dados/filtros inválidos |
| 401 | Credenciais recusadas no login; em fluxos autenticados, encerramento da sessão |
| 403 | Acesso negado, sem logout automático das consultas operacionais; na restauração, exige novo login |
| 404 | Recurso ou rota não encontrados |
| 500 | Erro interno da API |
| Outros status de erro | Mensagem com o código HTTP |
| Rede/certificado/CORS | Erro de conexão; o navegador pode não permitir distinguir essas causas |
| Contrato incompatível | Erro de resposta, sem apresentar contagem inventada |

Corpos de erro não precisam ser JSON e não são exibidos como mensagens brutas. Erros parciais
não eliminam automaticamente os resultados válidos dos outros endpoints.

## Execução local

Use Node compatível com o Vite instalado (`^20.19.0` ou `>=22.12.0`) e npm.
A API deve estar disponível separadamente, com banco, autenticação e CORS já configurados
para a origem do frontend. O Vite não inicia API, banco, seeders ou migrations.

Os comandos abaixo são executados **na raiz do repositório**, em PowerShell:

```powershell
npm.cmd ci --prefix frontend
if (-not (Test-Path -LiteralPath frontend/.env.local)) {
  Copy-Item -LiteralPath frontend/.env.example -Destination frontend/.env.local
}
npm.cmd --prefix frontend run dev -- --host localhost --port 5173 --strictPort
```

Preserve `.env.local` se já existir. A variável precisa conter somente a URL pública base,
sem `/api` no final, credenciais, query ou fragmento. Exemplo de API local:

```dotenv
VITE_API_BASE_URL=http://localhost:5129
```

Acesse `http://localhost:5173`. Para uma API local HTTPS, use a URL do perfil correspondente
com certificado confiável. Para apontar temporariamente à API DEMO já disponível, em um
terminal dedicado, sem editar arquivos privados:

```powershell
$env:VITE_API_BASE_URL = 'http://127.0.0.1:5180'
npm.cmd --prefix frontend run dev -- --host localhost --port 5173 --strictPort
# Depois de encerrar o Vite com Ctrl+C:
Remove-Item Env:VITE_API_BASE_URL
```

A variável vale para esse processo de terminal e seus filhos. Em produção, Vite incorpora a
URL durante o build; alterar configuração exige reiniciar o servidor de desenvolvimento ou
gerar outro build. **Variáveis `VITE_*` são públicas: nunca inclua senhas, JWTs ou segredos.**
Não substitua a configuração de um ambiente pela de outro sem conferir o destino.

```powershell
npm.cmd --prefix frontend run build
npm.cmd --prefix frontend run preview -- --host localhost --port 4173 --strictPort
```

O build gera `frontend/dist`; preview permite conferir esse resultado localmente e não é
um servidor de produção. A origem do preview também precisa ser aceita pelo CORS da API.
Nenhum desses comandos publica o frontend.

## Organização do código

- Imports JS/JSX são agrupados por responsabilidade, com comentários em português:
  bibliotecas externas, componentes, hooks personalizados, serviços/utilitários e estilos.
  A posição dos estilos preserva sua ordem de carregamento.
- Funções e hooks usam indentação de dois espaços e espaçamento entre responsabilidades,
  sem divisórias de estados/renderização. Comentários técnicos explicam integrações específicas.
- CSS usa indentação de quatro espaços, uma propriedade por linha e seções com comentários simples.
- Componentes cuidam da apresentação; hooks integram estado e efeitos; serviços concentram
  transporte, normalização, persistência de sessão e cache. `App.jsx` coordena o acesso inicial.

## Funcionalidades pendentes

- Telas de listagem, cadastro, edição e gestão de equipamentos e agentes.
- Histórico e gestão de manutenção; listagens e fluxos de alertas/ocorrências.
- Painel de Cidade Preditiva com fatores e explicações de risco, anomalias, previsões e
  visualização/ações de recomendações. O calor Risk já existe e não está pendente.
- Polling automático, caso necessário ao escopo; a atualização atual é manual.
- Clustering ou spiderfy, caso necessário; equipamentos e agentes próximos ainda podem se sobrepor.
- Validar a melhoria já implementada de pinça em celular real, nas três métricas, e validar
  F5/logout/expiração com a API real. São pendências de validação, não ausência dessas implementações.
- Evoluir a persistência temporária para uma solução de sessão aprovada para uso definitivo.
  Cookies HttpOnly/Secure exigiriam suporte de backend, proteção CSRF e avaliação de SameSite/CORS.

## Testes e qualidade

Na raiz:

```powershell
npm.cmd --prefix frontend run lint
node --test frontend/tests/*.test.js
```

Não existe script `npm test` no manifesto atual. Os scripts disponíveis são `dev`, `build`,
`lint` e `preview`; a suíte é executada diretamente pelo Node.

| Arquivo de teste | Cobertura funcional |
| --- | --- |
| `services.test.js` | Configuração, HTTP, login, papéis, erros e contratos |
| `session.test.js` | Restauração, validade, logout, 401/403, cancelamento e isolamento por API |
| `markers.test.js` | Coordenadas, estados, identificadores, tradução e apresentação de UTC |
| `filters.test.js` | Combinações, limpeza, contadores, seleção e ausência de novos GETs |
| `verificationState.test.js` | Preservação e substituição da última resposta válida |
| `heatmap.test.js` | Métricas, filtros, contratos e normalização das intensidades |
| `heatmapResource.test.js` | Cache, compartilhamento, concorrência, cancelamento e erros |
| `heatmapPinchZoom.test.js` | Transformações de zoom/pan, encaixe final e limpeza de listeners |
| `dashboard.test.js` | Indicadores derivados, distribuição, vazio e estados desconhecidos |

Na execução local da refatoração visual: **build e lint aprovados; 99 testes aprovados, zero
falhas e zero ignorados**. A quantidade corresponde à suíte executada, não a uma meta fixa.
Os testes usam dados e transportes controlados; não comprovam contagens atuais do banco,
login real ou renderização física em dispositivos móveis.

A atualização de identidade também foi revisada em Chrome com API de teste isolada:
login e dashboard, logo carregada, F5, filtros locais sem GETs adicionais, camadas
independentes e as três métricas de calor. Larguras de 320, 360, 390, 768, 1024, 1280 e
1600 px foram verificadas sem rolagem horizontal e sem exceções JavaScript nos fluxos
exercitados. As capturas usam dados controlados; não representam o banco DEMO ou Azure.
O gesto de pinça e o teclado virtual ainda exigem conferência em celular real.

Para verificação manual, conferir login/F5/logout, filtros e camadas, popups, as três métricas,
zoom/arraste em desktop e pinça em celular. Os tiles devem mostrar cartografia real: uma
resposta HTTP 200 com imagem de bloqueio não comprova funcionamento. Não copie JWTs, senhas
ou cabeçalhos Authorization para evidências.

O [índice de documentação](../docs/README.md) reúne relatórios históricos com seus limites
de validação. Contagens de banco e pendências de uma etapa anterior não descrevem, por si,
o estado atual da aplicação.
