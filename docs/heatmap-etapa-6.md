# Etapa 6 — mapa de calor operacional

Implementação de HeatLayer com três métricas, filtros próprios, cache em memória e atualização manual. Sem polling, alterações no backend/banco/Azure ou dados fictícios no produto.

## Arquivos criados

- `frontend/src/services/heatmapQuery.js`: opções de métricas/tipos/dias, filtros iniciais, validação e construção canônica de parâmetros.
- `frontend/src/services/heatmap.js`: consulta com o cliente HTTP existente e normalização/validação do contrato e pontos.
- `frontend/src/services/heatmapResource.js`: cache por consulta e sessão, deduplicação, cancelamento, proteção contra respostas atrasadas e preservação de sucesso anterior dos mesmos filtros.
- `frontend/src/hooks/useHeatmap.js`: integração React com o recurso, controles, dados compartilhados e encerramento de sessão existente em 401.
- `frontend/src/components/map/leafletHeatAdapter.js`: carregamento isolado do plugin legado, instância Leaflet correta e limpeza de frame pendente.
- `frontend/src/components/map/HeatmapLayer.jsx`: criação/atualização/remoção da camada dentro do MapContainer existente.
- `frontend/src/components/map/HeatmapControls.jsx` e `.css`: métricas, filtros, atualização, contadores, estados e legenda.
- `frontend/tests/heatmap.test.js`: 12 testes de conversão, contrato, parâmetros, datas/horas, transporte e compartilhamento.
- `frontend/tests/heatmapResource.test.js`: 13 testes de cache, concorrência, cancelamento, resposta atrasada, erros e preservação por consulta.
- Este relatório.

## Arquivos modificados

- `frontend/package.json` e `frontend/package-lock.json`: instalação exata de `leaflet.heat@0.2.0`.
- `frontend/src/services/verification.js`: retém a resposta normalizada de calor junto da contagem original, sem duplicar a consulta inicial de Incidents.
- `frontend/src/hooks/useEndpointVerification.js`: início dos GETs em microtask cancelável, evitando que a primeira montagem de teste do StrictMode inicie uma chamada imediatamente abortada/duplicada.
- `frontend/src/components/map/MapLayersControl.jsx`: terceiro controle independente para ligar/desligar o mapa de calor.
- `frontend/src/components/map/MapView.jsx`: composição de controles, hook e HeatmapLayer.
- `frontend/src/pages/DashboardPage.jsx`: fornece JWT atual e callback de logout à integração do mapa, sem novo sistema de autenticação.
- `frontend/tests/services.test.js`: fixtures de calor atualizadas para o contrato completo, incluindo maxIntensity.
- `frontend/README.md`: dependências, operação e checks atuais.

As alterações de etapas anteriores já existentes no diretório de trabalho foram preservadas. Nenhuma alteração no cliente HTTP, autenticação, `.env.local`, backend, migrations, SQL Server, Azure ou `frontend/heatmap.html` foi feita nesta etapa.

## Bibliotecas e integração com Vite

Versões verificadas: React/React DOM 19.3.1, React Leaflet 5.0.0, Leaflet 1.9.4 e leaflet.heat 0.2.0, com Vite 8.3.4. Apenas leaflet.heat foi instalado nesta etapa, com versão exata no manifesto e lockfile.

O plugin 0.2.0 é legado, não um componente React. Seu bundle consulta os globais `L` e `simpleheat`, inclusive em callbacks de desenho e zoom. O adaptador importa o objeto mutável de Leaflet usado pelo pacote e mantém `window.L` apontando para ele. Não restaura esse alias após o import porque o plugin volta a consultá-lo em execução. Se encontrar outro objeto Leaflet global, informa incompatibilidade em vez de substituí-lo silenciosamente.

O import dinâmico é compartilhado por uma única Promise. No caminho CommonJS, o pacote pode exportar a função simpleheat em vez de criar o global esperado pelo próprio código; o adaptador fornece esse alias quando necessário. Uma factory verifica que cada camada criada pertence ao mesmo `L.Layer`. Os aliases contêm apenas funções de biblioteca, sem JWT, dados de usuário ou pontos de consultas.

O release 0.2.0 não cancela seu `_frame` de requestAnimationFrame em onRemove. O adaptador acrescenta esse cancelamento por instância antes de delegar à remoção original. Esse é um ajuste específico da versão fixada; nenhuma cópia de node_modules ou backend foi modificada. A remoção original libera canvas e listeners.

Compatibilidade foi verificada na prática no Chrome em desenvolvimento e em um build de produção de teste, incluindo desenho e zoom. Não se presume compatibilidade automática com futuras versões principais de Leaflet/Vite.

Referências primárias: [Leaflet.heat](https://github.com/Leaflet/Leaflet.heat) e [MapContainer/useMap do React Leaflet](https://react-leaflet.js.org/docs/api-map/). O adaptador também foi baseado na inspeção do código efetivamente instalado em `node_modules/leaflet.heat`, pois o release 0.2.0 difere do código atual do repositório.

## Métricas e filtros efetivos

| Métrica | Parâmetros enviados |
| --- | --- |
| Incidents — Ocorrências | metric, from, to, fromHour, toHour, dayOfWeek, neighborhood, type |
| Failures — Falhas | metric, from, to, fromHour, toHour, dayOfWeek, neighborhood; sem type arbitrário |
| Risk — Risco regional | metric, to e neighborhood; sem from, horas, dia ou tipo |

Somente campos preenchidos e compatíveis entram na query. Failures utiliza EquipmentFailure forçado pelo backend. Valores de filtros ocultos não são enviados nem validados como se fossem efetivos na outra métrica. Os filtros de equipamentos/agentes permanecem locais e independentes.

Os inputs datetime-local usam o fuso do navegador e são convertidos para ISO UTC. Datas inexistentes, ano zero, formato inválido e período invertido são rejeitados. Quando só há início, o fim implícito é o instante atual do backend; início futuro sem fim é rejeitado no frontend.

Horas são inteiras de 0 a 23, enviadas em par. Faixa 22→2 é aceita. Dia e faixa de horas seguem o horário local configurado no backend, conforme indicado na interface. Sem período preenchido, a API define suas janelas padrão. Em Risk, to é o instante de referência; o cálculo segue as regras do serviço existente, sem prometer reconstrução de estados históricos que o backend não forneça.

Risk é identificado como índice regional, sem certeza de evento ou probabilidade estatística calibrada. Seus pontos são referências regionais, não polígonos dos bairros.

## Contrato e intensidades

O serviço valida métrica correspondente à consulta, array points, totalPoints inteiro não negativo e igual ao comprimento recebido, maxIntensity numérico finito não negativo e datas de resposta válidas ou nulas. Envelope incompatível é erro de contrato, sem números inventados.

Cada ponto precisa de latitude/longitude numéricas finitas nos limites inclusivos de -90/90 e -180/180. Zero é coordenada válida. Intensidade deve ser numérica, finita e não negativa; valores negativos são explicitamente tratados como inválidos porque não representam densidade de calor suportada. A interface informa esse descarte.

Os pontos válidos são mantidos com suas intensidades originais; não se usa Math.max(0.2, intensity) nem se converte string para número. Intensidades positivas menores que 0.2 e maiores que 1 são preservadas.

Zero permanece na quantidade de válidos e no conjunto normalizado, mas é separado antes de enviar ao plugin, evitando mancha mínima ou divisão por zero no agrupamento legado. Não é classificado como descartado. Arrays vazios e conjuntos só de zeros produzem canvas sem pixels de calor.

Contadores: recebidos, válidos, descartados, intensidade zero e pontos para desenho. A escala de desenho é no mínimo 1 e cresce se existir intensidade válida maior; os dados não são reescritos. O plugin combina intensidade/concentração e influencia a aparência com o zoom. A legenda é visual, não uma escala de probabilidade.

Opções atuais: radius 28, blur 20, minOpacity 0, maxZoom de normalização 12 e quatro faixas de cor. MaxZoom do calor é uma referência de intensidade, não um limite do zoom do mapa. Canvas fica abaixo dos marcadores e não captura cliques; atribuição OpenStreetMap permanece visível.

## Atualização, cache e concorrência

O calor começa desativado. O controle de camadas permite ativar/desativar equipamentos, agentes e calor separadamente. O painel recolhível Mapa de calor oferece:

- Aplicar filtros: reutiliza resposta equivalente bem-sucedida em memória; consulta quando ainda não existe ou está marcada como antiga.
- Atualizar calor: força nova consulta explícita, preservando dados anteriores apenas da mesma chave enquanto carrega.
- Mudança de campo: cancela a pendência antiga, troca a chave e retira os dados que pertenciam a outros filtros. Não dispara requisição automaticamente.

A chave canônica inclui somente parâmetros efetivos. Espaços externos do bairro e representações equivalentes de hora não produzem chaves distintas; trocar um tipo oculto em Failures não cria outra consulta.

A resposta inicial de Incidents já obtida na verificação é reutilizada integralmente. Enquanto essa consulta compartilhada está pendente, o calor padrão aguarda sua conclusão em vez de enviar outro GET equivalente. Os botões de atualização não permitem acúmulo de chamadas da mesma chave.

O cache é em memória, por instância/sessão e limitado a oito consultas, preservando a entrada em uso. Não há localStorage, sessionStorage, TTL automático ou polling. O horário da última resposta válida fica exposto; o cache não significa dados em tempo real.

Cada chamada própria usa AbortController e um número de sequência. Mesmo que um transporte ignore o abort e responda depois, a geração antiga não pode publicar sucesso/erro nem encerrar a sessão com um 401 atrasado. Desativar calor, mudar filtros ou desmontar cancela a chamada própria pendente.

Falha mantém a última resposta válida apenas da mesma chave, com erro, horário preservado e aviso de desatualização. Chave diferente sem resposta própria mostra ausência de consulta, nunca pontos do filtro anterior. Sucesso vazio substitui dados antigos por vazio. Erro 401 vigente chama o logout existente; 403 e rede são apresentados sem encerrar automaticamente a sessão.

Atualizar dados gerais da dashboard continua consultando o Incidents padrão da verificação; não equivale a atualizar automaticamente uma consulta customizada Failures/Risk. Para esta, use Atualizar calor.

## Ciclo de vida da camada

HeatmapLayer usa useMap para acessar a instância existente. A criação acontece após o carregamento do plugin; um sinal de limpeza impede a criação tardia após desmontagem. Atualizações usam setLatLngs/setOptions na mesma camada, sem MapContainer novo, setView ou fitBounds.

Desativar ou mudar para uma consulta sem dados remove a camada. Na desmontagem, canvas/listeners são liberados e o frame pendente é cancelado pelo adaptador. StrictMode permanece habilitado. O primeiro ciclo de verificação de seus efeitos é cancelado antes de iniciar os GETs; a montagem efetiva inicia uma única consulta padrão.

## Testes e navegador

| Check | Resultado final |
| --- | --- |
| npm.cmd run build | Aprovado, Vite 8.3.4 |
| npm.cmd run lint | Aprovado, sem desabilitar regras |
| node --test tests/*.test.js | 70 aprovados, 0 falhas |
| npm.cmd ls react-leaflet leaflet leaflet.heat | Versões exatas 5.0.0 / 1.9.4 / 0.2.0 |
| Chrome em desenvolvimento | Cenário completo aprovado, com StrictMode |
| Chrome em build de produção de teste | Carregamento, desenho e cenário funcional do plugin aprovados |

São 45 testes anteriores e 25 novos. Cobrem conversão, zero, valores inválidos, vazio, métricas, datas/horas, cliente Bearer/AbortSignal, compartilhamento, deduplicação, cache, cancelamento, respostas atrasadas, 401/403/rede e preservação por consulta, incluindo falha compartilhada após sucesso manual.

O navegador usou **o plugin real**, com API e tiles interceptados exclusivamente no teste. Foram inspecionadas capturas desktop/celular e pixels do canvas, além de verificar:

1. Desenho para Incidents, Failures e Risk e simultaneidade com marcadores.
2. Vazio/zero sem pixels de calor e contadores de inválidos corretos.
3. Três camadas independentes; alternância sem novos GETs.
4. Uma única HeatLayer ativa, sem listeners/frames/canvas retidos após desligar, trocar consulta ou sair.
5. Atualização de pontos sem recriar a camada/mapa ou alterar a vista; zoom nativo funcionando.
6. Cache e consulta inicial compartilhada sem duplicação, inclusive em desenvolvimento com StrictMode.
7. Consulta lenta substituída por outra rápida sem sobrescrever o resultado recente.
8. 403/rede com última resposta da mesma chave; 401 vigente retornando ao login.
9. Horas atravessando meia-noite e parâmetros específicos de cada métrica.
10. Layout móvel de 390px sem overflow e sem exceções JavaScript no cenário.

Artefatos locais ignorados pelo Git em `artifacts/etapa-6/`: browser-check.mjs, browser-result.json (desenvolvimento), browser-result.production.json, heat-incidents.png, heat-failures.png, heat-risk.png, heat-stale.png, heat-mobile.png e heat-controls-mobile.png.

O build de teste para produção foi gerado em diretório separado `artifacts/etapa-6/production`, com URL local fornecida apenas ao processo para interceptação. O build normal final também foi executado. A configuração `.env.local` do usuário não foi alterada.

## Limites e próxima etapa

Não houve login ou consulta autenticada real contra o Azure nesta validação. As capturas mostram dados de teste e tiles identificados como sem cartografia real, não registros existentes no ambiente de produção. Em respostas reais vazias, não haverá manchas artificiais.

O plugin 0.2.0 requer os aliases globais e o ajuste específico de frame descritos acima; futuras atualizações exigem nova validação. Densidade visual depende de concentração, escala, raio, blur e zoom. Não substitui análise estatística nem torna o score uma probabilidade calibrada.

Para uma Etapa 7 autorizada: validar as três métricas com dados reais disponíveis e conferir volume/desempenho e legibilidade com os usuários. Eventuais novas funcionalidades precisam de escopo próprio. Nenhuma preparação de dados, alteração Azure, polling, clustering, cadastro/edição ou painel preditivo completo foi implementado.

Etapa 6 concluída. Etapa 7 não iniciada; continuidade depende de aprovação.
