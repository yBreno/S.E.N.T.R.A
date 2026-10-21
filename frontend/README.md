# Frontend S.E.N.T.R.A.

React 19 + Vite 8 em JavaScript/JSX. Dashboard operacional com Leaflet, marcadores de
equipamentos/agentes, filtros, inspeção e Heat Map Incidents/Failures/Risk.
Consulte o [README principal](../README.md) para escopo, arquitetura, backend e limitações.

## Executar

Na raiz, em PowerShell:

```powershell
npm.cmd ci --prefix frontend
if (-not (Test-Path -LiteralPath frontend/.env.local)) {
  Copy-Item -LiteralPath frontend/.env.example -Destination frontend/.env.local
}
npm.cmd --prefix frontend run dev -- --host localhost --port 5173 --strictPort
```

Acesse `http://localhost:5173`. `VITE_API_BASE_URL` deve apontar para a API já disponível,
sem `/api`, credenciais, query ou fragmento. O exemplo usa `http://localhost:5129`;
HTTPS local utiliza `https://localhost:7130` e requer certificado confiável.
Preserve `.env.local` existente; nenhuma variável `VITE_*` pode conter segredos.
Configuração Vite é incorporada durante execução/build: reinicie o processo ao mudá-la.

Para usar temporariamente a API DEMO, sem editar arquivos privados, em um terminal dedicado:

```powershell
$env:VITE_API_BASE_URL = 'http://127.0.0.1:5180'
npm.cmd --prefix frontend run dev -- --host localhost --port 5173 --strictPort
# Após Ctrl+C:
Remove-Item Env:VITE_API_BASE_URL
```

A variável vale somente para o terminal e processos filhos. A API DEMO deve estar previamente
iniciada pelo procedimento protegido do README principal. O Vite não inicia backend ou banco.
Use uma conta existente; token fica apenas em memória e recarregar a página exige novo login.

## Comportamento

- Dashboard, equipamentos, agentes e Incidents padrão compartilham a rodada inicial de GETs.
- Filtros de marcadores são locais, sem novos GETs; camadas são independentes.
- Atualização é manual, com cancelamento e preservação sinalizada da última resposta válida.
- O calor tem cache por consulta; aplicar filtros e atualizar calor são ações explícitas.
- Coordenadas inválidas são descartadas; ausência de dados não gera registros fictícios.
- Layout responsivo e alterações de dados/filtros preservam a instância Leaflet.
- Tiles HTTPS oficiais OSM com atribuição e política `strict-origin-when-cross-origin`
  no HTML e no TileLayer. O responsável informou validação da correção no site publicado.
- Sem polling ou clustering. Não há telas CRUD ou painel preditivo completo nesta interface.

## Verificar

Na raiz:

```powershell
npm.cmd --prefix frontend run build
npm.cmd --prefix frontend run lint
node --test frontend/tests/*.test.js
```

Os 72 testes automatizados cobrem serviços, contratos, filtros, coordenadas, UTC, tradução,
cache e cancelamento. Não substituem a verificação visual/autenticada contra a API real.
O build gera `frontend/dist`; não publica automaticamente.

Os [relatórios históricos](../docs/README.md) preservam evidências das etapas anteriores.
O protótipo HTML e os assets do template foram removidos; o Heat Map oficial está nos
componentes React e foi preservado.
