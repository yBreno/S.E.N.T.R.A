# Documentação do S.E.N.T.R.A.

O [README principal](../README.md) descreve o estado atual, a configuração e as limitações.
O [guia do frontend](../frontend/README.md) detalha a execução da interface.

## Histórico de implementação e validação

Os relatórios abaixo registram o estado **na data de cada etapa**. Contagens de testes,
dados de banco, pendências e autorizações relatadas não descrevem necessariamente o
ambiente atual e não autorizam repetir operações de escrita.

- [Etapa 2: integração e autenticação](integracao-api-etapa-2.md).
- [Etapa 3: dashboard e mapa](dashboard-mapa-etapa-3.md).
- [Etapa 4: marcadores](marcadores-etapa-4.md).
- [Etapa 5: filtros e inspeção](filtros-inspecao-etapa-5.md).
- [Etapa 6: mapa de calor](heatmap-etapa-6.md).
- [Etapa 7.2: planejamento e carga DEMO](dados-demonstracao-etapa-7-2.md).
- [Etapa 7.3: validação SQL Server LocalDB](validacao-localdb-etapa-7-3.md).
- [Etapa 7.4: preparação da API DEMO](preparacao-api-demo-etapa-7-4.md).
- [Etapa 7.5: execução HTTP DEMO](execucao-http-demo-etapa-7-5.md).
- [Etapa 7.6: validação visual com dados locais](validacao-frontend-demo-etapa-7-6.md).

## Mudanças posteriores aos relatórios

- `locationUpdatedAt` passou a sair explicitamente em UTC no DTO de agentes; a interface
  apresenta o instante no fuso do navegador. `TrafficAgent` é apresentado como
  **Agente de trânsito**, sem mudar o enum da API. Há testes para ambas as correções.
- A política de referência do HTML e do TileLayer é `strict-origin-when-cross-origin`.
  O responsável pelo projeto informou a correção e validação dos tiles no site publicado.
- O protótipo `frontend/heatmap.html` foi removido; o mapa de calor oficial está no React.
- O roteiro pontual `Invoke-DemoHttpValidation.ps1`, vinculado ao fingerprint inicial e
  à criação do primeiro Admin da Etapa 7.5, foi removido. Sua menção nos relatórios é histórica.
- `scripts/Start-DemoApi.ps1` permanece como inicializador local protegido. Projetos DEMO,
  migrations e testes foram preservados; consulte o README antes de executar qualquer ferramenta.
