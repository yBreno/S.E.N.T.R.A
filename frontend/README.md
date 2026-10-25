# Frontend S.E.N.T.R.A.

Interface React/JavaScript, Vite e Leaflet para inventário, agentes, manutenção,
Central de Operações, Cidade Preditiva e Administração de Usuários. A API ASP.NET Core aplica autorização,
validação e persistência; o navegador utiliza HTTP, sem acesso direto ao SQL Server.
O [README principal](../README.md) mantém a descrição atual de arquitetura,
funcionalidades, contratos, permissões e limitações da entrega final.

## Executar localmente

Instale um Node compatível com a versão do Vite registrada no `package-lock.json`.
Na raiz do repositório:

```powershell
npm.cmd ci --prefix frontend
$env:VITE_API_BASE_URL = 'http://127.0.0.1:5129'
npm.cmd --prefix frontend run dev -- --host 127.0.0.1 --port 5173 --strictPort
```

Abra `http://127.0.0.1:5173`. A API deve estar rodando separadamente em um ambiente
local autorizado e permitir essa origem no CORS. Consulte sua preparação no README
principal. Não execute migrations ou seeders por causa de uma alteração no frontend.

`VITE_API_BASE_URL` é a URL pública da API, sem `/api`, credenciais, query ou fragmento.
Pode ser definida no terminal ou em um `.env.local` próprio, usando `.env.example`
como referência. Preserve qualquer `.env.local` existente. Variáveis `VITE_*` são
incorporadas ao bundle; nunca use senhas, tokens ou connection strings nelas.

Para a apresentação sintética, siga a [preparação DEMO](../docs/demo/preparacao-ambiente.md)
e o [roteiro](../docs/demo/roteiro-apresentacao.md). Esse procedimento usa API local
na porta 5180 e `VITE_DEMO_MODE=true`, com faixa visual de demonstração. Ele é
separado da configuração de produção e não requer alterar os workflows.

## Organização

| Caminho                        | Responsabilidade                                                           |
| ------------------------------ | -------------------------------------------------------------------------- |
| `src/main.jsx`, `src/App.jsx`  | Inicialização, sessão, acesso e identificação DEMO                         |
| `src/pages/`                   | Dashboard, equipamentos, agentes, manutenções, Cidade Preditiva e usuários |
| `src/components/`              | Design compartilhado, formulários, detalhes, painéis e mapa                |
| `src/hooks/`                   | Sessão, consultas, polling, inspeção e camadas operacionais                |
| `src/services/`, `src/config/` | Transporte HTTP, contratos, recursos e URL da API                          |
| `public/brand/`                | Logo utilizado e original de identidade visual preservado                  |
| `tests/`                       | Testes automatizados com `node:test` e `node:assert/strict`                |
| `package-lock.json`            | Versões reproduzíveis das dependências                                     |

Os módulos da sidebar seguem os roles do backend. **Usuários** exige Admin e permite
buscar, filtrar, paginar, cadastrar, editar, alterar cargo, desativar/reativar e
consultar histórico. A redefinição por e-mail exige disponibilidade informada pela
API e confirmação; só o titular recebe o link. O login oferece **Esqueci minha senha**,
com telas de solicitação e redefinição. Não há segredo de provedor no frontend.
O detalhe da conta mostra seu ID. No formulário de Agentes, esse ID pode vincular
o profissional a uma conta existente; o vínculo não muda cargos ou credenciais.
Voltar/avançar do navegador e acesso direto ao mapa de calor preservam a tela correta.
Falta de permissão, erro de consulta ou ausência de dados
não devem ser interpretados como uma cidade sem ocorrências.

O mapa usa tiles OpenStreetMap e depende de rede para a cartografia. Marcadores,
indicadores e análises vêm da API. O kernel geográfico de calor, os controles de
pinça e a separação entre risco e situação operacional fazem parte da implementação
atual e têm testes; não dependem do protótipo HTML antigo.

## Build e qualidade

Execute na raiz:

```powershell
node --test frontend/tests/*.test.js
npm.cmd --prefix frontend run lint
npm.cmd --prefix frontend run build
npm.cmd --prefix frontend run preview -- --host 127.0.0.1 --port 5173 --strictPort
```

O Vite gera `frontend/dist`, ignorado pelo Git. O build usa a URL disponível em
seu ambiente no momento da execução. A publicação existente usa o workflow Azure
Static Web Apps, fonte `frontend`, saída `dist` e branch de produção `main`.
Não é necessário publicar para validar uma limpeza local.

Os testes de componentes renderizam JSX real com React e o transformador já disponível
no Vite, sem instalar outro framework. Cobrem o painel de vínculo de veículo, os
valores de detalhes e o acesso à página de usuários pelos cinco cargos. O carregador
de teste ignora CSS somente na renderização Node; não participa do bundle da aplicação.

Resultados e instruções de atualização estão no
[relatório final da Sprint 08](../docs/reports/sprint-08-validacao-final.md), no
[relatório de Administração de Usuários](../docs/reports/administracao-usuarios.md) e no
[relatório de recuperação por e-mail](../docs/reports/recuperacao-senha-email.md).
O schema e a API novos devem ser publicados antes do frontend; os workflows exigem
as variáveis de liberação documentadas no relatório.
Os testes de transporte usam fixtures controladas; não fornecem dados fictícios à
aplicação normal nem comprovam o estado da aplicação publicada.

## Diagnóstico rápido

- Falha de configuração: conferir `VITE_API_BASE_URL` e reiniciar o Vite após alterar variáveis.
- Erro de login ou HTTP 401: conferir a conta/perfil e autenticar novamente.
- HTTP 403: o perfil não possui a permissão; não ampliar roles para contornar a restrição.
- Sem tiles: conferir a rede e o console; a ausência de cartografia é distinta de falha na API.
- Sem marcadores: conferir consultas, coordenadas, filtros e visibilidade das camadas.
- Escrita recusada no host DEMO: seguir os limites do roteiro; somente o aceite C é opcionalmente habilitado.

Consulte o [índice de documentação](../docs/README.md) para relatórios e procedimentos.
