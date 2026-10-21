# Etapa 2 — integração React e API

Implementação de autenticação em memória e verificação de endpoints, em JavaScript/JSX. Sem bibliotecas novas e sem mapa ou heatmap visual.

## Arquivos e responsabilidades

Criados:

- `frontend/src/config/api.js`: leitura e validação de `VITE_API_BASE_URL`. Rejeita configuração ausente, esquema diferente de HTTP/HTTPS, credenciais na URL, query, fragmento e sufixo `/api`. Normaliza barras finais e apresenta erro de configuração sem reproduzir o valor inválido.
- `frontend/src/services/http.js`: GET/POST JSON com fetch nativo, Bearer opcional e AbortSignal. Diferencia 400, 401, 403, 404, 500, outros status, rede, configuração e contrato inválido. Não interpreta corpos de erro nem os exibe, pois podem ser HTML, texto ou conter dados sensíveis.
- `frontend/src/services/auth.js`: `POST /api/auth/login` com `email`/`password`; valida `token`, `expiresAt` e `user.name`/`user.role`. A sessão retém apenas os campos necessários. Define os três papéis de monitoramento.
- `frontend/src/services/verification.js`: consulta os quatro endpoints em paralelo com resultados independentes; valida formas básicas das respostas e calcula as quantidades.
- `frontend/src/hooks/useAuth.js`: sessão React, carregamento/erro de login, cancelamento de login pendente, logout e encerramento da sessão por expiração.
- `frontend/src/hooks/useEndpointVerification.js`: carga inicial, atualização manual, encerramento da sessão em 401 e cancelamento ao desmontar ou substituir uma consulta. Respostas canceladas não atualizam o estado. Falhas parciais permanecem visíveis por endpoint.
- `frontend/src/components/LoginForm.jsx`: e-mail, senha, loading e mensagem de erro. Sem credenciais preenchidas; limpa a senha do campo ao enviar.
- `frontend/src/components/EndpointVerification.jsx`: nome/papel, resumo do dashboard, quantidades, mensagens por endpoint, última tentativa, atualização manual e logout.
- `frontend/tests/services.test.js`: 19 testes isolados com `node:test` e transporte controlado, sem instalar ferramentas adicionais.
- Este relatório.

Modificados:

- `frontend/src/App.jsx`: composição das telas e bloqueio visual de papéis sem acesso. Papéis diferentes de Admin, OperationManager e CentralOperator não montam a tela de consultas protegidas.
- `frontend/src/App.css` e `frontend/src/index.css`: estilos mínimos e responsivos, substituindo a apresentação padrão do Vite.
- `frontend/index.html`: idioma pt-BR e título da aplicação.
- `frontend/.env.example`: comentário atualizado para indicar o consumo da configuração.
- `frontend/README.md`: configuração, comandos, comportamento e validação manual.

O arquivo `.env.example` e a auditoria da etapa 1 já estavam presentes como arquivos não rastreados antes desta etapa; não foram removidos. Nenhum manifesto ou lockfile foi alterado.

## Comportamento

O token fica exclusivamente no estado React; não há localStorage, sessionStorage, cookie de sessão frontend ou persistência equivalente. Recarregar exige novo login. Logout, expiração e 401 eliminam a sessão da aplicação. Nenhuma senha ou JWT é exibido em logs ou na interface.

Após login com papel autorizado, são enviados:

1. `GET /api/dashboard`: total, disponibilidade, agentes ativos e contagem por status.
2. `GET /api/equipments`: quantidade do array retornado.
3. `GET /api/agents`: quantidade do array retornado.
4. `GET /api/heatmap?metric=Incidents`: quantidade de pontos, sem renderização geográfica.

A interface mantém o motivo específico de erros 400/403/404/500/rede. Uma resposta 401 encerra a sessão quando a rodada de consultas termina. Falhas de um endpoint não descartam sucessos dos demais. Dados vazios são exibidos como zero; respostas com contrato inválido são erros, sem contagem inventada.

A autorização no React organiza a interface. Os atributos de autorização existentes no backend continuam sendo a proteção efetiva. O React não altera papéis nem cria usuários.

StrictMode permanece habilitado. A limpeza dos efeitos aborta consultas e timers; resultados de execuções canceladas são ignorados. O token não está associado a um singleton HTTP global: é fornecido explicitamente nas chamadas protegidas.

## Evidências e limites da validação

- Build frontend aprovado com Vite 8.3.4.
- Lint aprovado sem desabilitar regras.
- 19 testes de serviços aprovados: configuração, headers, POST de login, descarte da senha da sessão, HTTP com erros não JSON, rede, JSON inválido, cancelamento, expiração no contrato de login, papéis e resultados parciais.
- Os dados e tokens de teste são fixtures sintéticas, não credenciais da aplicação. Esses testes não comprovam integração com a API real nem constituem teste de navegador dos hooks React.
- Sondagens HTTP locais em `5129`, `7130` e `5062` retornaram `ECONNREFUSED`. Não foi obtido JWT real e não foram realizadas consultas autenticadas contra o backend nesta etapa.
- A API não foi iniciada. Em Development, sua inicialização chama seeders e inicia o worker de simulação; isso pode alterar o banco.
- Nenhuma alteração em backend, migrations, banco, Azure ou `frontend/heatmap.html`. A suíte backend não foi reexecutada nesta etapa, pois o backend não foi modificado; os 49 testes aprovados são a evidência da etapa 1.

Pendência: executar a validação manual abaixo com API disponível e conta existente autorizada. Não há validação de navegador automatizada nesta entrega.

## Execução local

Comandos executados a partir da raiz do repositório, em PowerShell:

```powershell
Copy-Item -LiteralPath frontend/.env.example -Destination frontend/.env.local
Set-Location frontend
npm.cmd ci
npm.cmd run dev -- --host localhost --port 5173 --strictPort
```

Se `.env.local` já existir, preserve suas configurações e ajuste apenas `VITE_API_BASE_URL` para o perfil efetivamente usado. A URL base não deve conter `/api`. Acesse `http://localhost:5173`. Reinicie Vite após alterar a configuração. `npm.cmd` evita o bloqueio de `npm.ps1` pela política do PowerShell, sem alterar a política do sistema.

Em outro terminal, na raiz do repositório, **quando houver autorização para executar a inicialização existente do backend**:

```powershell
dotnet run --project backend/Api/Api.csproj --launch-profile http
```

Esse perfil configura `http://localhost:5129`. Requer .NET 10, SQL Server/LocalDB acessível com banco já preparado e `Jwt:Key` configurada no ambiente/user-secrets. Não execute migrations ou seeders manualmente como parte desta etapa. Mesmo sem scripts manuais, os seeders já presentes são chamados pela própria inicialização em Development.

Alternativa HTTPS:

```powershell
dotnet run --project backend/Api/Api.csproj --launch-profile https
```

Configure `VITE_API_BASE_URL=https://localhost:7130` e utilize certificado de desenvolvimento confiável. O perfil HTTPS também publica HTTP, mas `UseHttpsRedirection` pode redirecionar as chamadas. Não desabilite validação de certificado no frontend. `5062` não é a porta dos perfis inspecionados.

Use e-mail/senha de uma conta já cadastrada, sem copiar credenciais para arquivos versionados. Registro de conta, alterações de papel e preparação do banco não integram esta etapa.

Verificações locais:

```powershell
Set-Location frontend
npm.cmd run build
npm.cmd run lint
node --test tests/services.test.js
```

## Validação manual pendente

1. Sem `.env.local` ou com URL inválida: mensagem de configuração, login desabilitado e nenhuma chamada.
2. API desligada: login apresenta falha de conexão e libera nova tentativa.
3. Credenciais inválidas: mensagem 401, sem acesso à tela protegida.
4. Conta de monitoramento: nome/papel, quatro consultas e quantidades reais. Conferir requisições na aba Network sem compartilhar seus cabeçalhos ou corpos sensíveis.
5. Conta Agent ou MaintenanceTechnician: mensagem de acesso indisponível e nenhuma consulta dos quatro endpoints.
6. Falha isolada 403/404/500: mensagem específica no endpoint, preservando os demais resultados.
7. Atualização manual: nova rodada de GETs, botão temporariamente desabilitado e horário atualizado.
8. Logout ou expiração durante chamadas: retorno ao login, requisições canceladas e ausência de resultados antigos.
9. Recarregar a página: novo login obrigatório.

Etapa 3 não iniciada.
