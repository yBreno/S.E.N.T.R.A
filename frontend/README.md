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
Use uma conta existente. Para a demonstração, a sessão utiliza memória e sessionStorage
da aba; atualizar a página tenta restaurá-la com validação na API.

## Persistência temporária da sessão

- Após login, somente JWT, validade e nome/papel de apresentação são guardados, vinculados
  à URL da API. Não são persistidos senha, CPF, cookies ou respostas operacionais.
- F5 mantém a sessão apenas se o JWT e `expiresAt` ainda estiverem válidos e
  `GET /api/dashboard` aceitar o Bearer. Até essa confirmação, a dashboard não é exibida.
  Essa consulta de validação ocorre somente na restauração; não há polling ou refresh token.
- Usa-se a menor validade entre `exp` do JWT e `expiresAt`. Ler claims no navegador
  não valida assinatura nem concede autorização: os endpoints continuam protegidos pela API.
- Logout, expiração e 401 dos fluxos autenticados limpam a sessão. Falhas de restauração
  também exigem novo login; requisições canceladas não restauram estado antigo.
- Não há localStorage. Se sessionStorage estiver bloqueado, o login funciona somente em memória.
- **Risco de XSS:** scripts executados na origem podem ler ou adulterar sessionStorage.
  Esta é uma alternativa temporária para demonstração, não uma proteção equivalente a HttpOnly.
  O armazenamento é associado à aba; não equivale a “lembrar de mim” entre sessões do navegador.
- Cookies HttpOnly/Secure exigiriam suporte no backend, logout por endpoint, proteção CSRF,
  envio de credenciais e CORS compatível. SameSite depende dos domínios da interface e API;
  cookies entre sites podem exigir `SameSite=None; Secure` e sofrer bloqueio pelo navegador.
  Essa migração não foi implementada.

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

Os testes automatizados cobrem serviços, contratos, filtros, coordenadas, UTC, tradução,
cache, cancelamento e restauração/limpeza de sessão com transporte controlado.
Não substituem a verificação visual/autenticada contra a API real.
O build gera `frontend/dist`; não publica automaticamente.

Os [relatórios históricos](../docs/README.md) preservam evidências das etapas anteriores.
O protótipo HTML e os assets do template foram removidos; o Heat Map oficial está nos
componentes React e foi preservado.
