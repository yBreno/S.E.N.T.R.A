# S.E.N.T.R.A. — Frontend

Interface web do **Sistema Estratégico de Navegação, Tráfego e Resposta Ativa**, desenvolvida com React, JavaScript e Vite.

O frontend reúne os módulos de Inventário Inteligente, Central de Operações e Cidade Preditiva em uma interface responsiva, com mapas interativos, indicadores operacionais e navegação adaptada às permissões de cada usuário.

Este diretório contém a aplicação web. Para conhecer o projeto completo, acesse o [README principal](../README.md).

## Tecnologias utilizadas

**Interface e desenvolvimento**

![JavaScript](https://img.shields.io/badge/JavaScript-202024?style=for-the-badge)
![React 19](https://img.shields.io/badge/React_19-202024?style=for-the-badge)
![Vite 8](https://img.shields.io/badge/Vite_8-202024?style=for-the-badge)
![CSS](https://img.shields.io/badge/CSS-202024?style=for-the-badge)

**Mapas e geolocalização**

![Leaflet](https://img.shields.io/badge/Leaflet-202024?style=for-the-badge)
![React Leaflet](https://img.shields.io/badge/React_Leaflet-202024?style=for-the-badge)
![OpenStreetMap](https://img.shields.io/badge/OpenStreetMap-202024?style=for-the-badge)

## Funcionalidades da interface

### Dashboard e Central de Operações

- Indicadores de equipamentos cadastrados, disponibilidade e agentes ativos.
- Mapa interativo com localização de equipamentos e agentes.
- Marcadores diferenciados por tipo e situação operacional.
- Filtros por equipamento, situação, região e disponibilidade.
- Visualização de detalhes diretamente no mapa.
- Atualização periódica das informações, com identificação de dados desatualizados.
- Mapas de calor de ocorrências, falhas e riscos.

### Inventário e manutenção

- Listagem, cadastro, consulta e edição de equipamentos.
- Formulários adaptados às características de cada tipo de equipamento.
- Gerenciamento de agentes e suas informações operacionais.
- Associação de veículos e equipamentos móveis a agentes.
- Consulta e gerenciamento de manutenções corretivas e preventivas.
- Filtros e validações nos formulários.

### Cidade Preditiva

- Visualização de índices de risco de equipamentos e regiões.
- Apresentação dos fatores que contribuíram para cada análise.
- Consulta de recomendações preventivas e histórico operacional.
- Integração com o módulo de manutenção.
- Identificação de falhas e dados indisponíveis, evitando apresentar informações inexistentes como resultados válidos.

### Autenticação e administração

- Login integrado à autenticação JWT da API.
- Recuperação de senha por e-mail com link temporário.
- Navegação dinâmica de acordo com as permissões do usuário.
- Administração de contas, cargos e estados de acesso para administradores.
- Tratamento de sessões expiradas e acessos não autorizados.

## Interface adaptada às permissões

O S.E.N.T.R.A. possui cinco perfis de acesso. A navegação exibe apenas os módulos autorizados para cada usuário.

| Perfil | Acesso à interface |
| --- | --- |
| **Admin** | Todos os módulos, incluindo Administração de Usuários |
| **OperationManager** | Gestão operacional, sem Administração de Usuários |
| **CentralOperator** | Monitoramento e ações operacionais autorizadas |
| **MaintenanceTechnician** | Equipamentos e manutenção |
| **Agent** | Acesso restrito; não possui painel operacional completo nesta versão |

Essa restrição não acontece apenas na interface. **A API também valida as permissões**, impedindo que um usuário execute operações não autorizadas mesmo que tente acessá-las diretamente.

## Como executar localmente

O frontend depende da API ASP.NET Core do S.E.N.T.R.A. para autenticação, consultas e operações.

Antes de iniciar, confira se o backend está configurado e em execução.

Se ainda não preparou o ambiente, siga o [guia de instalação completo](../docs/instalacao.md).

### 1. Pré-requisitos

- Node.js compatível com Vite 8.
- npm.
- API do S.E.N.T.R.A. em execução.

Os comandos abaixo utilizam **Windows e PowerShell**.

### 2. Instalar as dependências

Abra o PowerShell na **raiz do repositório** e execute:

```powershell
npm.cmd ci --prefix frontend
```

O npm instalará as dependências utilizando as versões registradas no `package-lock.json`.

### 3. Configurar a conexão com a API

Com a API local executando na porta `5129`, configure:

```powershell
$env:VITE_API_BASE_URL = 'http://127.0.0.1:5129'
$env:VITE_DEMO_MODE = 'false'
```

`VITE_API_BASE_URL` informa ao frontend onde encontrar o backend.

A URL deve apontar para a origem da API, **sem adicionar `/api` ao final**.

### 4. Iniciar o frontend

No mesmo terminal, execute:

```powershell
npm.cmd --prefix frontend run dev -- --host 127.0.0.1 --port 5173 --strictPort
```

Acesse no navegador:

**http://127.0.0.1:5173**

A tela de login deverá aparecer. Utilize as credenciais criadas durante a configuração do backend.

**Pronto!** O frontend está conectado à API local.

Mantenha os terminais do backend e frontend abertos enquanto utiliza o sistema.

## Executar com dados de demonstração

Quer explorar os módulos sem cadastrar equipamentos e agentes manualmente?

O S.E.N.T.R.A. possui uma DEMO local com dados sintéticos e contas de avaliação.

O [guia de preparação da DEMO](../docs/demo/preparacao-ambiente.md) explica como criar o banco isolado, carregar os registros e iniciar a API na porta `5180`.

Depois de seguir a preparação, utilize estas configurações no terminal do frontend:

```powershell
$env:VITE_API_BASE_URL = 'http://127.0.0.1:5180'
$env:VITE_DEMO_MODE = 'true'

npm.cmd --prefix frontend run dev -- --host 127.0.0.1 --port 5173 --strictPort
```

A interface exibirá a identificação **DEMO · DADOS SINTÉTICOS**.

Nesse ambiente, a maioria das operações de escrita permanece bloqueada para preservar os cenários de avaliação. Há um modo opcional para demonstrar o aceite de uma recomendação preventiva.

## Organização do frontend

```text
frontend/
├── public/
│   └── brand/          Identidade visual
├── src/
│   ├── components/     Componentes e elementos de interface
│   ├── pages/          Páginas dos módulos
│   ├── hooks/          Lógica reutilizável e gerenciamento de estado
│   ├── services/       Comunicação com a API e contratos
│   ├── config/         Configurações públicas da aplicação
│   ├── App.jsx         Composição principal da interface
│   └── main.jsx        Inicialização do React
├── tests/              Testes automatizados
├── package.json        Dependências e scripts
└── package-lock.json   Versões das dependências
```

### Como essas partes trabalham juntas?

- **Pages:** organizam as telas e os fluxos de cada módulo.
- **Components:** implementam formulários, tabelas, painéis, mapas e elementos reutilizáveis.
- **Hooks:** concentram lógica compartilhada, como sessão, consultas, atualização periódica e mapas.
- **Services:** realizam as requisições HTTP e tratam os contratos da API.
- **Config:** centraliza configurações públicas, incluindo o endereço do backend.

Essa separação facilita a manutenção e permite desenvolver funcionalidades sem concentrar toda a lógica em um único componente.

## Responsividade

A interface foi desenvolvida para diferentes tamanhos de tela, com adaptações de navegação, disposição de conteúdo, formulários e componentes.

O projeto passou por validações visuais em resoluções de **320, 390, 768 e 1440 pixels**.

As funcionalidades de mapas também incluem suporte a interações por toque, como deslocamento e zoom.

## Comunicação com o backend

O frontend utiliza requisições HTTP para acessar a API.

A interface é responsável por apresentar informações, coletar entradas e fornecer feedback ao usuário. **As regras de autorização, validações de negócio e persistência são aplicadas no backend.**

Alguns pontos importantes:

- O token JWT é enviado nas operações autenticadas.
- Respostas HTTP como `401`, `403` e `500` recebem tratamento específico.
- Consultas podem ser canceladas para evitar que respostas antigas substituam dados recentes.
- A interface diferencia ausência de registros de falhas na consulta.
- O frontend não se conecta diretamente ao SQL Server ou ao Resend.

As variáveis `VITE_*` são públicas e incorporadas ao frontend. **Nunca coloque senhas, tokens privados ou connection strings nelas.**

## Testes e qualidade

O frontend possui testes automatizados para contratos HTTP, autenticação, permissões, formulários, navegação, componentes e funcionalidades geográficas.

Na última validação local, em **25/10/2026**, foram aprovados **217 testes frontend**, sem falhas.

Para executar os testes, abra o PowerShell na raiz do projeto:

```powershell
node --test frontend/tests/*.test.js
```

Para verificar a qualidade do código:

```powershell
npm.cmd --prefix frontend run lint
```

Para gerar o build de produção:

```powershell
npm.cmd --prefix frontend run build
```

Os arquivos compilados serão gerados em `frontend/dist`.

Para visualizar esse build localmente, após encerrar o servidor de desenvolvimento:

```powershell
npm.cmd --prefix frontend run preview -- --host 127.0.0.1 --port 5173 --strictPort
```

A execução dos testes não exige a publicação da aplicação na Azure.

## Problemas comuns

| Problema | O que verificar |
| --- | --- |
| A tela não abre | Confira se o Vite está em execução na porta `5173`. |
| Erro de conexão com a API | Verifique `VITE_API_BASE_URL` e se o backend está ligado. |
| Erro `401` | A sessão pode estar expirada. Faça login novamente. |
| Erro `403` | O perfil não possui permissão para aquela operação. |
| Alguns módulos não aparecem | A sidebar exibe somente funcionalidades autorizadas ao perfil. |
| Mapa sem cartografia | Confira a conexão com a internet, necessária para carregar os tiles do OpenStreetMap. |
| Dados não aparecem | Confira a conexão com a API, os filtros e possíveis mensagens de erro. |
| Alteração de variável não surtiu efeito | Reinicie o Vite após modificar as variáveis de ambiente. |
| Não consigo editar na DEMO | O ambiente demonstrativo possui restrições intencionais de escrita. |

## Documentação relacionada

- [README principal](../README.md) — visão geral e funcionalidades do S.E.N.T.R.A.
- [Instalação completa](../docs/instalacao.md) — configuração do banco, backend e frontend.
- [Preparação da DEMO](../docs/demo/preparacao-ambiente.md) — demonstração com dados sintéticos.
- [Arquitetura](../docs/arquitetura.md) — camadas, integrações e decisões técnicas.

---

Frontend desenvolvido como parte do **S.E.N.T.R.A. — DSIN Coder Challenge 2026**.
