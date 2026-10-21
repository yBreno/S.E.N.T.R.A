# Etapa 5 — filtros locais e inspeção operacional

Escopo concluído: filtros locais combináveis, contadores por camada, inspeção compartilhada com popups e preservação da última resposta válida. Sem heatmap visual, polling ou dependências novas.

## Arquivos criados

- `frontend/src/components/map/operationalFilters.js`: funções puras para filtros, opções de bairro, contagens, construção das camadas e resolução da seleção.
- `frontend/src/components/map/OperationalFilters.jsx` e `.css`: filtros responsivos em português e estilos da inspeção/contadores.
- `frontend/src/components/map/OperationalRecordDetails.jsx`: detalhes seguros reutilizados pelo popup e painel de inspeção.
- `frontend/src/components/map/RecordInspection.jsx`: painel de consulta sem edição, com fechamento e indicação de dados antigos.
- `frontend/src/hooks/useMarkerInspection.js`: somente identidade da seleção no estado; registro derivado dos dados atuais.
- `frontend/src/services/verificationState.js`: combinação da rodada nova com a última resposta válida por endpoint.
- `frontend/tests/filters.test.js`: 12 testes de filtros, contadores, seleção e valores desconhecidos.
- `frontend/tests/verificationState.test.js`: 4 testes de respostas válidas, falhas parciais, falhas repetidas e respostas vazias novas.
- Este relatório.

## Arquivos modificados

- `frontend/src/hooks/useOperationalMarkers.js`: deriva camadas filtradas com useMemo sobre resultados e filtros, sem transporte de rede.
- `frontend/src/hooks/useEndpointVerification.js`: preserva dados/horário da última resposta válida em falhas, sem alterar a saída obrigatória em 401.
- `frontend/src/components/map/MapView.jsx`: mantém filtros e visibilidade como estado; compõe controles, camadas e inspeção sem recriar MapContainer.
- `frontend/src/components/map/EquipmentMarkers.jsx` e `AgentMarkers.jsx`: comunicam cliques pela identidade e usam a seleção para controlar o popup.
- `frontend/src/components/map/MapPopup.jsx`: reutiliza detalhes compartilhados e fecha o popup quando a seleção é removida.
- `frontend/src/components/map/MapLayersControl.jsx`: contadores separados, estados sem resultados, mensagens de desatualização e último horário válido.
- `frontend/src/components/map/markerData.js`: estados conhecidos também aceitam espaços nas extremidades, coerentemente com os filtros.
- `frontend/src/components/dashboard/DashboardIndicators.jsx`: mantém a última resposta válida durante atualização/falha, sinalizando isso.
- `frontend/src/components/EndpointVerification.jsx`: expõe erro da tentativa e valores anteriores identificados como antigos, com horário da resposta válida.
- `frontend/README.md`: comportamento atual e comando para executar todos os testes frontend.

`App.jsx`, autenticação, cliente HTTP, configurações privadas e dependências foram preservados nesta etapa. O diretório já continha alterações das etapas anteriores; elas não foram descartadas.

## Filtros e dados

Equipamentos: tipo, status e bairro se combinam por interseção. Agentes: disponibilidade é independente dos filtros de equipamentos. O botão "Limpar filtros" restaura todos os quatro campos, preservando a escolha de ativação/desativação de cada camada.

Todos os cálculos usam os `items` já retornados por `loadVerification`, que faz os mesmos quatro GETs com o cliente autenticado existente. Não há novo cliente, parâmetros novos no endpoint de agentes ou buscas adicionais ao alterar filtros/seleção/camadas. A atualização dos dados continua manual.

Tipos e estados conhecidos têm rótulos em português; valores ausentes/desconhecidos possuem uma opção explícita e apresentação neutra. Bairros são derivados da resposta inteira de equipamentos, incluindo registros que posteriormente sejam descartados por posição. Sua comparação ignora caixa, acentos e espaços redundantes. Os rótulos permanecem textos React seguros.

As opções de bairro não são reduzidas pelos outros filtros. Se o bairro selecionado desaparecer em uma resposta nova, o seletor mostra "Bairro selecionado (ausente na resposta atual)" e mantém o filtro visível até o usuário alterá-lo ou limpá-lo. Isso evita um filtro ativo invisível.

Os indicadores gerais de `/api/dashboard` e as contagens brutas na verificação de endpoints não recebem filtros locais. Nenhuma contagem filtrada é apresentada como total cadastrado na cidade.

## Contadores

| Contador | Regra |
| --- | --- |
| Recebidos | Comprimento do array da última resposta válida daquele endpoint |
| Válidos | Registros com coordenadas numéricas finitas nos limites geográficos e ID textual não vazio, sem duplicação de ID na camada |
| Após filtros | Subconjunto dos válidos que atende à combinação local selecionada |
| Descartados | Recebidos menos válidos; não muda só por alterar filtros |
| Na camada | Após filtros quando a camada está habilitada; zero quando desabilitada |

Os valores "Na camada" não significam uma contagem de pixels/ícones dentro do viewport atual: posições válidas podem estar fora da vista. Não há filtro por área visível. Recebidos não significa total global de cadastro além da resposta da API.

Sem uma resposta válida, os contadores apresentam `—`, e não zero inventado. Arrays válidos vazios apresentam zero e mensagem de ausência de registros. Dados válidos sem coordenadas aptas têm mensagem própria; filtros sem correspondência também têm mensagem específica.

A validação de coordenadas da etapa 4 permanece: apenas números finitos, latitude inclusiva de -90 a 90 e longitude de -180 a 180, sem coerção de nulos/strings/booleanos. Zero continua válido.

## Inspeção e seleção

O clique abre o popup existente e preenche a inspeção abaixo do mapa. Ambos reutilizam `OperationalRecordDetails` e mostram somente os campos operacionais autorizados da etapa anterior. Sem CPF, edição, HTML bruto ou expansão de propriedades da resposta.

O estado guarda somente `{ kind, id }`. O registro inspecionado é derivado da camada filtrada atual, portanto uma posição/status/detalhe novo aparece sem manter uma cópia antiga do registro na seleção.

Se o registro sair dos filtros, a camada for desativada ou uma resposta válida nova removê-lo/inutilizar suas coordenadas, a seleção é descartada. Limpar os filtros não restaura a seleção antiga. Fechar a inspeção também fecha o popup. Fechar apenas o popup permite continuar a consulta no painel.

Nenhuma alteração de filtro chama setView/fitBounds, altera a chave de MapContainer ou reinicializa o mapa. O ajuste existente ao abrir explicitamente um popup continua permitido para legibilidade; atualizações do registro aberto mantêm a vista estável.

## Atualização manual e dados antigos

Durante a atualização, o resultado anterior continua disponível com aviso de possível desatualização. A rodada nova é combinada por endpoint:

- Sucesso: substitui os dados, registra `receivedAt` e limpa o estado antigo.
- Falha com sucesso anterior: mantém a última resposta válida e seu horário, registra o erro atual e marca `stale`.
- Falha sem sucesso anterior: mostra erro, sem criar dados/contagens fictícias.
- Sucesso com array vazio: substitui dados antigos por vazio, removendo marcadores/seleções que deixaram de existir.
- 401: mantém o comportamento anterior de encerrar a sessão; dados antigos não são usados para contornar autenticação.

O horário global continua sendo o da última tentativa concluída. O horário por endpoint representa a última resposta válida, sem ser atualizado indevidamente em falhas. Dashboard, mapa, inspeção e verificação indicam a condição dos dados, sem tratar uma resposta antiga como nova.

## Validação

| Verificação | Resultado |
| --- | --- |
| `npm.cmd run build` | Aprovado |
| `npm.cmd run lint` | Aprovado, sem desabilitar regras |
| `node --test tests/*.test.js` | 45 aprovados, 0 falhas |
| Chrome | Executado com API/tiles interceptados localmente |
| Backend/Azure real | Não consultado nem alterado nesta validação |

Os 29 testes anteriores foram executados junto dos 16 novos. A cobertura adicional inclui combinações de filtros, limpeza, vazio/ausência de resposta, valores desconhecidos, bairros normalizados, registros inválidos, contadores com camada desligada, seleção válida e preservação de respostas por endpoint.

No Chrome foram verificados, com fixtures exclusivas do teste:

1. Arrays vazios sem marcadores fictícios.
2. Tipo Radar + status Com falha + bairro Mooca, com disponibilidade Offline independente para agentes.
3. Contadores recebidos/válidos/após filtros/descartados/na camada.
4. Ausência de GETs ao filtrar, limpar filtros e alternar camadas.
5. Remoção de seleção/popup ao filtrar o registro para fora e ausência de restauração automática ao limpar.
6. Opções de estados/tipos/bairros desconhecidos sem erro em execução.
7. Mesma instância e deslocamento do mapa durante filtros e atualizações.
8. Última resposta mantida em consulta artificialmente atrasada e após erro 500 parcial; aviso antigo removido após sucesso.
9. Array vazio bem-sucedido removendo registros anteriores e seleção; bairro ausente permanece visivelmente selecionado até limpeza.
10. Inspeção compartilhada, texto HTML tratado como texto, ausência de CPF, layout móvel de 390px, popups, logout e papel Agent bloqueado.
11. Ausência de exceções JavaScript no cenário e nenhum polling observado.

As capturas foram realmente inspecionadas. Os tiles de teste estão identificados como sem cartografia real. Os dados de teste não fazem parte da aplicação nem foram inseridos em nenhum banco. Artefatos locais ignorados pelo Git em `artifacts/etapa-5/`: `browser-check.mjs`, `browser-result.json`, `filters-combined.png`, `stale-response.png`, `inspection-mobile.png`, além das capturas gerais e de popups.

Não há afirmação de registros reais renderizados. Conforme o contexto informado, a API atual recebe/retorna arrays vazios; nessa situação a interface preserva o estado vazio. A configuração Azure em `.env.local` não foi modificada. A execução do teste forneceu uma URL local apenas ao processo temporário do Vite para interceptação das chamadas.

## Preparação para leaflet.heat e pendências

O mapa permanece uma instância estável, com camadas independentes e estado de filtros separado do transporte. Isso permite uma futura camada de calor sem misturar os filtros de equipamentos/agentes com a consulta de ocorrências.

Para uma próxima etapa autorizada ainda será necessário:

- Instalar e validar `leaflet.heat` com o carregamento correto da instância Leaflet no Vite.
- Criar componente de camada com criação/atualização/remoção e limpeza em StrictMode.
- Consumir/reutilizar os pontos de `/api/heatmap`, mantendo métrica, período e intensidade próprios; o resultado atual serve apenas à contagem de verificação.
- Definir controles específicos de calor e explicar filtros efetivos para Incidents, Failures e Risk conforme o backend, sem aplicar silenciosamente os filtros operacionais atuais ao risco.
- Validar com dados reais disponíveis e manter tratamento claro para arrays vazios e respostas antigas.

Nada disso foi implementado. Backend, banco, migrations, Azure, dependências e `frontend/heatmap.html` foram preservados. Etapa 6 não iniciada.
