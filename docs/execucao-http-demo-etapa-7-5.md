# S.E.N.T.R.A. — Etapa 7.5: execução HTTP local autorizada

## Escopo efetivamente executado

Somente `(localdb)\MSSQLLocalDB`, banco `SENTRA_DEMO_Stage73`, com conexão Windows integrada
dedicada e sem utilizar a conexão da API existente. Nenhum acesso ao Azure. Nenhuma migration,
seeder, carga operacional ou simulação foi executada. Nenhuma configuração frontend foi alterada.

Antes de provisionar, consultas somente de leitura confirmaram IsLocalDB=1, o banco exato,
as três migrations existentes, zero usuários e os 265 registros operacionais. Não havia
FK desabilitada/não confiável nem violações em DBCC CHECKCONSTRAINTS. O fingerprint completo
inicial era o mesmo validado na Etapa 7.3:

`E2FD78E378604958E760670567A412379C2F7279D8E5F7CD0335EE5F4355A6B3`

## Senha e provisionamento

O usuário confirmou que via a janela interativa. A senha foi digitada sem eco diretamente
no terminal de backend/DemoAdmin, com confirmação. O comando criou uma única conta
`admin.demo@sentra.invalid`, papel Admin. Em seguida, o usuário digitou a senha novamente
no prompt seguro para o login HTTP. Nenhuma senha, hash de senha, chave JWT ou token foi
copiado para o chat, arquivo ou relatório; o JWT da resposta foi usado apenas na memória
do processo de validação e suas referências foram descartadas ao concluir.

Não foi utilizado endpoint público para provisionamento. O total final de usuários é 1.
Os agentes continuam sem associação ao novo usuário.

## API iniciada

Inicializada através de `scripts/Start-DemoApi.ps1 -ConfirmStart`, pelo roteiro interativo
`scripts/Invoke-DemoHttpValidation.ps1`.

- Listener confirmado no sistema operacional: somente `127.0.0.1:5180` para o processo da API.
- Log de inicialização confirmou `Hosting environment: Demo` e a URL esperada.
- Simulation:Enabled=false; worker não registrado neste modo.
- SeedAdmin:Enabled=false; nenhum seeder é chamado no modo Demo.
- Nenhuma rotina de migrations é chamada pelo startup.
- Chave JWT nova e exclusiva da execução, gerada pelo launcher sem impressão/persistência.
- A API permanece ativa localmente ao final desta etapa. PID observado: 13028; launcher: 12444.
  Esses PIDs são apenas evidência desta execução e não devem ser reutilizados para encerrar
  processos futuros sem reconferir sua identidade.

## Resultados HTTP reais

Consultas entre **20/10/2026 20:42:44 e 20:42:45, UTC−03:00**.
Base exclusiva: `http://127.0.0.1:5180`. Não são fixtures nem chamadas diretas a serviços.

| Método e endpoint | HTTP | Resultado |
| --- | ---: | --- |
| POST /api/auth/login | 200 | Sessão Admin, JWT utilizado somente em memória |
| GET /api/dashboard | 200 | 60 equipamentos, 13 agentes ativos, disponibilidade 16,67% |
| GET /api/equipments | 200 | 60 registros; 60 coordenadas válidas |
| GET /api/agents | 200 | 20 registros; 20 coordenadas válidas |
| GET /api/heatmap?metric=Incidents | 200 | 100 pontos com coordenadas e intensidade válidas |
| GET /api/heatmap?metric=Failures | 200 | 25 pontos com coordenadas e intensidade válidas |
| GET /api/heatmap?metric=Risk | 200 | 5 pontos com coordenadas e intensidade válidas |

Latitude/longitude numéricas, não nulas, finitas e dentro dos limites geográficos.
As intensidades dos pontos consultados eram maiores que zero e até 1.

## Segurança observada

| Verificação | Resultado real |
| --- | --- |
| GET dashboard sem JWT | 401 |
| GET dashboard com Bearer inválido | 401 |
| OPTIONS dashboard, origem http://localhost:5173, GET e Authorization | 204, origem permitida no cabeçalho CORS |
| OPTIONS com origem não permitida | 204 sem Access-Control-Allow-Origin; navegador não recebe autorização CORS |
| PATCH equipamento com identificador vazio, sem atingir registro existente | 405, bloqueado pelo middleware Demo |
| Autorização 403 com papel restrito | Não testada: somente um Admin foi autorizado |

Uma origem recusada por CORS não foi apresentada como HTTP 403. Não foram criados usuários
adicionais nem alterados papéis/tokens para fabricar um cenário de autorização insuficiente.
Não houve validação visual em navegador nesta etapa.

## Preservação dos registros

O executor de inspeção ganhou `OperationalFingerprint`, que cobre todos os campos de todas
as tabelas de aplicação exceto Users, além do histórico de migrations. Assim a inserção
autorizada do Admin pode ser distinguida de qualquer alteração operacional.

Fingerprint operacional antes e depois do provisionamento, HTTP e espera superior ao atraso
inicial do worker antigo:

`3B35089E613478C5C225BA93728D33163BE146B63F4E501DF4E9F7A3FA5C83BA`

| Tabela | Antes | Depois |
| --- | ---: | ---: |
| Addresses | 60 | 60 |
| Equipments | 60 | 60 |
| Agents | 20 | 20 |
| Incidents | 100 | 100 |
| Alerts | 15 | 15 |
| MaintenanceRecords | 10 | 10 |
| Users | 0 | 1 |

TelemetryLogs, AgentPositionLogs, Recommendations, Suppliers e OperationalBases continuaram
vazias. Os **265 registros operacionais e seus campos permaneceram idênticos**.
A inspeção final terminou em `2026-10-20T23:42:52Z`.

## Arquivos e correções mínimas

- Modificado `backend/DemoData.Validation/Program.cs`: fingerprint adicional somente de leitura,
  excluindo Users; os modos de carga/migration não foram executados.
- Criado `scripts/Invoke-DemoHttpValidation.ps1`: coordena prompts privados, CLI existente,
  launcher existente, requisições locais sem redirects/proxy e relatório sanitizado.
- Criado este relatório. Resultado detalhado sanitizado em `artifacts/etapa-7-5/result.json`
  (ignorado pelo Git). Logs de startup não incluem corpo de login ou cabeçalhos Authorization.

A primeira tentativa do roteiro parou antes de solicitar senha/provisionar por ArgumentException
na adaptação PowerShell de SqlConnectionStringBuilder: propriedade DataSource era tratada como
palavra-chave inválida. Corrigido para indexadores com palavras-chave canônicas. A janela dessa
tentativa foi encerrada; a nova execução repetiu a inspeção inicial antes de solicitar a senha.
Não foi necessário alterar autenticação, API, frontend, políticas Windows ou permissões SQL.

Build aprovado sem warnings/erros; 112 testes backend aprovados, sem falhas/ignorados.
Esses testes são evidência adicional; os status HTTP acima foram observados na API real local.

## Limites e próximo passo

403 real permanece pendente sob a restrição de conta única Admin. Não reexecutar o provisionamento:
o banco agora tem usuário e o roteiro exige Users vazio antes de qualquer criação. Não alterar
ou excluir o Admin para repetir testes.

React ainda não foi iniciado/configurado para este host. Uma validação futura do frontend pode
usar override VITE_API_BASE_URL somente no processo, preservando frontend/.env.local, mas exige
aprovação para a próxima etapa. Nenhuma publicação ou operação Azure foi iniciada.
