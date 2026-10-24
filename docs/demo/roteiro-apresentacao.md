# Roteiro de apresentação — cerca de 6min30

Preparar um ensaio novo conforme [preparação do ambiente](preparacao-ambiente.md),
usando API DEMO com `-InteractiveAcceptance` e frontend `VITE_DEMO_MODE=true`.
Use `admin.s07@sentra.invalid` ou `operationmanager.s07@sentra.invalid` e a senha
privada definida pelo operador. Não mostrar terminais com credenciais.

## 0:00–0:40 — Login e visão geral

Apontar a faixa **DEMO · DADOS SINTÉTICOS** e explicar: ruas, profissionais,
ocorrências e sinais são fictícios; o mapa usa uma região ilustrativa de São Paulo.
Entrar e mostrar os cartões da Central: **12 cadastrados, sete operacionais,
três indisponíveis, 58,33% de disponibilidade e sete agentes ativos**.
Os dois recursos desativado/não instalado não entram na contagem de indisponíveis.

## 0:40–1:30 — Inventário Inteligente: cenário A

Na sidebar, abrir **Equipamentos**. A lista tem 12 itens em duas páginas.
Buscar `DEMO-S07-C-PREV`, abrir **Detalhes** e mostrar série, marca/modelo,
categoria, características de câmera, endereço completo, referência e coordenadas.
Destacar **Operacional** e a previsão preventiva vencida: risco não equivale a falha.

Limpar a busca e citar os exemplos `E-BALANCA`, `A-CANCELA`, `A-ESCOLAR` e `A-TAXI`:
categorias de uso reaproveitam sensores/veículos existentes, sem novas subclasses.
A cancela representa o controlador IoT; não é uma simulação de atuação física.

## 1:30–2:30 — Central, agentes e falha: cenários A, B e D

Voltar ao **Dashboard**. Mostrar camadas Equipamentos/Agentes e a legenda dos oito
tipos. São 12 marcadores de equipamentos e nove profissionais em cinco bairros.
Aplicar região **Centro**: quatro equipamentos; agentes ocupado e indisponível,
sem profissional disponível naquele bairro. Limpar filtros ao terminar.

Em **Agentes**, filtrar Disponibilidade: quatro disponíveis, três ocupados e dois
indisponíveis. As três categorias de atuação estão representadas. Voltar ao Dashboard.

No painel **Alertas recentes**, localizar `DEMO-S07-B-RADAR` e abrir **Ver equipamento**:
o radar está **Em manutenção**, com necessidade imediata. A ocorrência sintética,
o alerta crítico e a corretiva em andamento correspondem à mesma falha.
O mapa preserva a localização e o estado operacional do recurso.

## 2:30–3:15 — Intervenções e histórico: cenário E

Abrir **Manutenções** e usar **Estado da ordem**:

- **Agendada**: duas ordens; `DEMO-S07-E-BALANCA` tem preventiva futura e permanece operacional;
  `DEMO-S07-B-CAMERA` tem corretiva programada e está com defeito.
- **Em andamento**: duas ordens; radar com corretiva e `DEMO-S07-E-TOTEM` com preventiva.
- **Concluída**: cinco ordens; mostrar o histórico de `DEMO-S07-E-HISTORICO`.

Abrir um detalhe para mostrar previsão, início efetivo, término, natureza e
responsável quando atribuído. Agendamento não comprova início ou conclusão.
Não iniciar/concluir ordens no host DEMO: esses estados já foram preparados.

## 3:15–4:30 — Cidade Preditiva e evidências: cenários C e D

Abrir **Cidade preditiva**, buscar `DEMO-S07-C-PREV` e mostrar **85/100, Alto**.
Abrir os fatores/evidências: três alertas de falhas resolvidas em trinta dias
contribuem 30 pontos; três corretivas concluídas, 20; preventiva vencida, 25;
três sinais categóricos sintéticos de bateria/temperatura, dez. A situação atual
contribui zero por estar operacional. O total é calculado pelo motor existente.

Explicar: é prioridade operacional baseada em evidências; não significa 85% de
probabilidade de quebra, previsão por aprendizado de máquina ou telemetria real.

Limpar a busca; filtrar bairro **Centro**. Mostrar **Exposição por bairro**:
concentração de ocorrências, recorrência de falhas, indisponibilidade e lacuna de
agentes disponíveis. O Centro é o bairro prioritário, com nível **Crítico**.
O valor exato depende da referência/janela e do horário das ocorrências.
No Dashboard, o círculo regional e o selo P da câmera permitem reencontrar essa análise.

## 4:30–5:45 — Decisão e ordem preventiva: cenário C

Limpar os filtros de busca/bairro/nível para reencontrar todas as recomendações.
Em **Recomendações priorizadas**, localizar o motivo que começa por **DEMO-S07 C**,
destinado à câmera operacional. Abrir as evidências registradas, clicar
**Aceitar recomendação**, revisar a confirmação e clicar **Confirmar** uma vez.
Esperar a mensagem de sucesso.

Abrir **Manutenções**, buscar `DEMO-S07-C-PREV` e filtrar **Agendada**:
deve haver **uma** nova ordem **Preventiva**, com vínculo à recomendação e sem início
efetivo. Consultar seus detalhes. O total geral agora é dez ordens, três agendadas.
O equipamento continua **Operacional**; uma intervenção só mudaria a situação ao começar.

A operação é idempotente: repetição do mesmo aceite retorna a mesma ordem,
conforme testes HTTP/SQL. A recomendação deixa a lista de pendentes; não é necessário
repetir cliques para demonstrar essa garantia. A recomendação regional D permanece
consultiva; não desloca agentes automaticamente e seu aceite não está habilitado neste DEMO.

## 5:45–6:30 — Retorno à Central e encerramento

Voltar ao **Dashboard** e clicar **Atualizar** se necessário. Conferir três ordens
agendadas e duas em andamento. O risco atual da câmera passa a **70/100** ao
replanejar a preventiva, mas o histórico permanece e o equipamento segue operacional.
A explicação da recomendação guarda a avaliação original de 85; planejamento não
significa reparo já executado.

Encerrar com a história completa: inventário localizado → acompanhamento no mapa →
falha ou risco explicado → recomendação → decisão do operador → ordem vinculada.
O diferencial apresentado é a integração entre operação atual e prevenção explicável,
com permissões preservadas e rastreabilidade dos registros sintéticos.

## Conferência antes da sessão

- Preparar perto do horário da apresentação; validar as quantidades antes de gravar ou apresentar.
- Não executar análise persistida, cadastro/edição, descarte ou outras escritas: o host DEMO as bloqueia.
- O botão **Atualizar** consulta os dados; não gera recomendações ou ordens.
- A carga repetida não reinicia decisões. Um novo ensaio desde o início exige outro banco novo autorizado.
- Para evidenciar permissões, reservar tempo fora dos 6min30: CentralOperator consulta mas não aceita;
  MaintenanceTechnician consulta ordens; Agent não acessa o inventário interno.
