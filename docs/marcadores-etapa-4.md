# Etapa 4 — marcadores operacionais

Implementação de duas camadas independentes para equipamentos e agentes, alimentadas somente pelos resultados da API. Não foram instaladas bibliotecas nem implementados heatmap visual, polling ou filtros geográficos.

## Arquivos criados

- `frontend/src/components/map/EquipmentMarkers.jsx`: ícones quadrados de equipamentos, estados e popup por registro válido.
- `frontend/src/components/map/AgentMarkers.jsx`: ícones circulares de agentes, disponibilidade e popup por registro válido.
- `frontend/src/components/map/MapPopup.jsx`: conteúdo JSX seguro com os campos operacionais solicitados e ajuste de abertura no celular.
- `frontend/src/components/map/MapLayersControl.jsx`: checkboxes independentes, mensagens de carregamento/erro/vazio, contagens de posições válidas/descartadas e legenda recolhível.
- `frontend/src/components/map/markerData.js`: normalização de coordenadas, IDs, estados e apresentação de texto/data.
- `frontend/src/components/map/markerIcons.js`: ícones Leaflet com SVG estático local e cache por categoria/estado.
- `frontend/src/components/map/Markers.css`: apresentação de ícones, controles, legenda e popups.
- `frontend/src/hooks/useOperationalMarkers.js`: preparação das camadas a partir dos resultados existentes, sem rede própria.
- `frontend/tests/markers.test.js`: 9 testes de coordenadas, estados, registros inválidos, textos e datas.
- Este relatório.

## Arquivos modificados

- `frontend/src/services/verification.js`: mantém a contagem em `data` e acrescenta `items` de equipamentos/agentes com uma lista explícita de campos operacionais. O formato usado pela verificação anterior foi preservado.
- `frontend/src/hooks/useEndpointVerification.js`: preserva a última resposta durante a atualização manual, com estado de carregamento explícito, evitando remover todos os marcadores enquanto os GETs estão pendentes.
- `frontend/src/components/map/MapView.jsx`: incorpora as camadas e controles sem substituir MapContainer.
- `frontend/src/pages/DashboardPage.jsx`: fornece o resultado compartilhado ao mapa; botão renomeado para "Atualizar dados".
- `frontend/tests/services.test.js`: acrescenta teste de compartilhamento dos registros, quatro GETs únicos e exclusão de dados pessoais desnecessários.
- `frontend/README.md`: comportamento, comandos e referência deste relatório; cópia do modelo de ambiente condicionada à ausência de `.env.local`, preservando a configuração Azure existente.

## Consulta e reutilização

O fluxo continua sendo `DashboardPage → useEndpointVerification → loadVerification → cliente HTTP existente`.

Os GETs de `/api/equipments` e `/api/agents` recebem o JWT atual pelo cabeçalho Bearer do mesmo cliente. Não foram criados outro cliente ou autenticação paralela. A rodada existente de quatro GETs também mantém dashboard e heatmap Incidents para a verificação de endpoints, sem desenhar heatmap.

Além da contagem já utilizada, os resultados de equipamentos/agentes preservam os campos permitidos em `items`. O mapa usa esses registros; clicar em marcador ou ativar/desativar uma camada não dispara nenhuma consulta. A atualização permanece exclusivamente manual. Cancelamento, expiração, logout e autorização visual da etapa anterior continuam em vigor.

Ao atualizar, os marcadores da última resposta permanecem no mapa com a mensagem "Atualizando; exibindo a última consulta". Quando a nova rodada termina, cada camada utiliza sua nova resposta; falhas são exibidas, sem converter indisponibilidade em um array vazio fictício.

## Coordenadas e estados

- Latitude e longitude devem ser valores JavaScript do tipo `number`, finitos e nos limites inclusivos `[-90, 90]` e `[-180, 180]`.
- Zero é aceito em qualquer eixo, inclusive `[0, 0]`.
- `null`, ausência, strings (inclusive numéricas), booleanos, NaN e infinito são rejeitados sem coerção. Isso evita transformar ausência em zero.
- Registros sem ID textual não vazio ou com ID duplicado na mesma camada também são ignorados. A mensagem informa o total descartado por coordenadas/identificador.
- Nenhum equipamento/agente recebe o centro de São Paulo como posição substituta. Posições válidas fora da vista atual continuam fora da vista; o mapa não usa fitBounds ou recentralização automática por dados.
- Atualizar posições mantém a instância e a vista do mapa. Ao abrir explicitamente um popup, Leaflet pode ajustar a vista para mostrar o conteúdo inteiro. O ajuste é desativado para atualizações subsequentes do popup aberto e reativado na próxima abertura pelo usuário. O conteúdo React é medido após sua inserção para evitar cortes no celular.

Equipamentos usam ícone de monitor em formato quadrado:

| Estado da API | Rótulo | Cor |
| --- | --- | --- |
| Operational | Operacional | Verde |
| Malfunctioning | Com falha | Vermelho |
| UnderMaintenance | Em manutenção | Amarelo |
| Deactivated | Desativado | Cinza |
| Stolen | Furtado / roubado | Magenta |
| NotInstalled | Não instalado | Cinza claro |

Agentes usam ícone de pessoa em formato circular:

| Estado da API | Rótulo | Cor |
| --- | --- | --- |
| Available | Disponível | Verde |
| Busy | Ocupado | Laranja |
| Offline | Offline | Cinza |

Estados inesperados usam apresentação neutra e rótulo explícito. Não são confundidos com Operational/Available. A legenda e os títulos/popups fornecem nomes textuais, além das cores e formas.

## Popups e privacidade

Equipamento: tipo, marca/modelo, serialNumber, status e bairro.

Agente: nome, badgeNumber, role, availability, bairro e locationUpdatedAt formatado em pt-BR no fuso do navegador. Datas inválidas têm fallback "Não informado".

CPF, CPF mascarado, e-mail, vínculo de usuário e atributos extras não entram na projeção dos registros destinada ao mapa. Não são exibidos em popups ou logs. Os campos solicitados, como nome/matrícula do agente, continuam disponíveis somente no fluxo autenticado existente.

Os valores da API são filhos de elementos JSX, sem inserção de HTML bruto. O HTML dos ícones contém somente SVG estático local; classes vêm de tabelas controladas. Valores desconhecidos e propriedades como `__proto__`/`toString` não são utilizados como classes ou HTML da API.

## Testes e evidências

| Verificação | Resultado |
| --- | --- |
| `npm.cmd run build` | Aprovado |
| `npm.cmd run lint` | Aprovado, sem desabilitar regras |
| `node --test tests/services.test.js tests/markers.test.js` | 29 aprovados, 0 falhas |
| Navegador | Chrome headless realmente executado, via CDP |
| Dados do navegador | API e tiles interceptados com fixtures locais |
| Marcadores reais do Azure | Não comprovados nesta entrega |

Os testes Node cobrem zero, limites, valores inválidos, todos os estados, estados desconhecidos, IDs duplicados, arrays vazios, datas, dados pessoais excluídos e contagens mantidas. A suíte anterior de HTTP/autenticação também foi executada.

O teste de navegador começou com os dois arrays vazios: nenhum marcador foi criado e ambas as mensagens de ausência apareceram. Depois foram usadas fixtures exclusivas do teste, sem inserir registros em banco ou adicionar dados de demonstração ao produto. Foram verificados:

1. Sete posições válidas de equipamento (incluindo zero) e três de agente; rejeição de posições nulas, strings e fora do limite.
2. Seis apresentações de status de equipamentos e três disponibilidades de agentes.
3. Toggle independente de cada camada, sem GETs adicionais.
4. Popups com os campos esperados; string HTML exibida como texto, sem criação de imagem/script; ausência do campo pessoal de teste.
5. Atualização de posição com popup aberto sem alterar o deslocamento do mapa, mantendo a mesma instância Leaflet.
6. Popup visível nos quatro lados em tela móvel de 390px, sem overflow horizontal.
7. Estados vazios, falha da API, zoom, verificação dos quatro endpoints, logout, papel Agent bloqueado e novo login após reload.
8. Nenhuma exceção JavaScript no cenário; nenhum polling observado.

As capturas foram inspecionadas. Os tiles estão identificados como teste, sem cartografia real. Artefatos locais ignorados pelo Git: `artifacts/etapa-4/browser-check.mjs`, `browser-result.json`, `empty-state.png`, `equipment-popup.png`, `agent-popup.png`, `dashboard-desktop.png`, `dashboard-mobile.png` e `mobile-popup.png`.

**Não há afirmação de marcadores reais renderizados.** Conforme o contexto informado, a API atual retorna arrays vazios. Nesse caso a aplicação mostra as mensagens de ausência, sem registros fictícios. Nenhum login ou GET autenticado foi realizado contra o Azure nesta validação; o teste utilizou interceptação local e não substitui essa integração.

## Preservação e pendências

Sem alterações no backend, SQL Server, migrations, Azure ou `frontend/heatmap.html`. Nenhum seeder foi executado. A configuração `.env.local` do usuário não foi modificada. Nenhuma dependência adicional foi instalada.

Pendências: validar marcadores com registros reais georreferenciados quando estiverem disponíveis na API; conferir legibilidade em conjuntos maiores e possíveis sobreposições de posições. Qualquer cadastro ou preparação de dados precisa acontecer fora desta entrega, pelo fluxo autorizado do projeto.

Heatmap visual e etapas posteriores não foram iniciados.
