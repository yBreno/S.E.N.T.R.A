import { useEffect, useMemo, useRef, useState, useSyncExternalStore } from 'react';
import InventoryDetails from '../components/inventory/InventoryDetails.jsx';
import { canManageEquipments, canMaintain } from '../services/auth.js';
import {
  createPredictiveApi,
  predictiveError,
  recommendationPriority,
  riskLabels,
  prioritizeRecommendations,
} from '../services/predictive.js';
import { createVerificationResource } from '../services/verificationResource.js';
import './EquipmentPage.css';
import './PredictivePage.css';

const formatDate = (value) =>
  value && Number.isFinite(Date.parse(value))
    ? new Date(value).toLocaleString('pt-BR')
    : 'Não informado';
const points = (value) => Number(value).toLocaleString('pt-BR', { maximumFractionDigits: 2 });
function Factors({ items }) {
  return (
    <details className='predictive-factors'>
      <summary>Entender fatores e evidências ({items.length})</summary>
      {items.length ? (
        <ul>
          {items.map((factor) => (
            <li key={factor.name}>
              <strong>
                {factor.label}: {points(factor.points)} pontos
              </strong>
              <p>{factor.rawDescription}</p>
              <p className='inventory-muted'>
                {factor.rule} · Peso: {points(factor.weight * 100)} pontos · Evidências:{' '}
                {factor.recordCount} registro(s).
              </p>
            </li>
          ))}
        </ul>
      ) : (
        <p className='inventory-muted'>Sem fatores estruturados disponíveis nesta recomendação.</p>
      )}
    </details>
  );
}
function Level({ value }) {
  return (
    <span className={`inventory-badge risk-${value?.toLowerCase()}`}>
      {riskLabels[value] ?? 'Não informada'}
    </span>
  );
}
function Source({ result, loading, children }) {
  return (
    <>
      {result?.error && (
        <p
          role='alert'
          className='status-notice error'
        >
          {predictiveError(result.error, { reading: true })}{' '}
          {result.stale ? 'Exibindo a última leitura válida.' : 'Dados desta fonte indisponíveis.'}
        </p>
      )}
      {result?.receivedAt && (
        <p className='inventory-muted'>
          Leitura desta fonte:{' '}
          <time dateTime={result.receivedAt}>{formatDate(result.receivedAt)}</time>
          {result.stale ? ' · Dados desatualizados' : ''}
        </p>
      )}
      {result?.data ? (
        children
      ) : loading ? (
        <p role='status'>Carregando dados desta fonte…</p>
      ) : !result?.error ? (
        <p className='inventory-muted'>Ainda sem leitura desta fonte.</p>
      ) : null}
    </>
  );
}
function More({ total, limit, onMore }) {
  return (
    <div className='inventory-list-summary'>
      <span>
        Exibindo {Math.min(total, limit)} de {total} resultado(s).
      </span>
      {total > limit && (
        <button
          className='secondary-button'
          onClick={onMore}
        >
          Mostrar mais
        </button>
      )}
    </div>
  );
}

export default function PredictivePage({
  session,
  onLogout,
  onSavingChange,
  onEquipment,
  onMaintenance,
  onAgents,
}) {
  const api = useMemo(() => createPredictiveApi(session), [session]);
  const resource = useMemo(
    () =>
      createVerificationResource({
        load: api.load,
        poll: false,
        onUnauthorized: () => onLogout('Sua sessão expirou. Entre novamente.'),
      }),
    [api, onLogout],
  );
  const state = useSyncExternalStore(resource.subscribe, resource.getSnapshot);
  useEffect(() => {
    resource.start();
    return resource.stop;
  }, [resource]);
  const [filters, setFilters] = useState({ search: '', neighborhood: '', level: '' });
  const [limit, setLimit] = useState({ equipment: 10, neighborhoods: 10, recommendations: 10 });
  const [confirmation, setConfirmation] = useState(null);
  const [saving, setSaving] = useState(false);
  const [notice, setNotice] = useState(null);
  const [failedAt, setFailedAt] = useState(0);
  const busy = useRef(false);
  const pending = useRef(null);
  const confirmationPanel = useRef(null);
  useEffect(
    () => () => {
      pending.current?.abort();
      pending.current = null;
      onSavingChange(false);
    },
    [onSavingChange],
  );
  useEffect(() => {
    if (confirmation) confirmationPanel.current?.focus();
  }, [confirmation]);
  const sources = Object.fromEntries(state.results.map((result) => [result.id, result]));
  const equipment = sources.equipment?.data;
  const neighborhoods = sources.neighborhoods?.data;
  const recommendations = prioritizeRecommendations(sources.recommendations?.data ?? []);
  const uncertain = failedAt > (state.updatedAt?.getTime() ?? 0);
  const disabled = saving || state.loading || uncertain;
  const manager = canManageEquipments(session.user);
  const selectedNeighborhoods = [
    ...new Set(
      [...(equipment ?? []), ...(neighborhoods ?? []), ...recommendations]
        .map((item) => item.neighborhood)
        .filter(Boolean),
    ),
  ].sort((a, b) => a.localeCompare(b, 'pt-BR'));
  const normalize = (value) =>
    value
      .normalize('NFD')
      .replace(/\p{Diacritic}/gu, '')
      .toLowerCase();
  const matches = (item) =>
    (!filters.neighborhood || item.neighborhood === filters.neighborhood) &&
    (!filters.level || item.level === filters.level) &&
    (!filters.search ||
      normalize(
        `${item.serialNumber ?? ''} ${item.neighborhood} ${item.equipmentType ?? ''} ${item.reason ?? ''}`,
      ).includes(normalize(filters.search.trim())));
  const filteredEquipment = (equipment ?? []).filter(matches);
  const filteredNeighborhoods = (neighborhoods ?? []).filter(matches);
  const filteredRecommendations = recommendations.filter((item) =>
    matches({ ...item, level: recommendationPriority(item) }),
  );

  async function execute() {
    if (busy.current || !confirmation || disabled) return;
    busy.current = true;
    setSaving(true);
    onSavingChange(true);
    setNotice(null);
    const controller = new AbortController();
    pending.current = controller;
    const deadline = setTimeout(() => controller.abort(), 20000);
    try {
      const result =
        confirmation.type === 'analyze'
          ? await api.analyze(controller.signal)
          : await api.resolve(confirmation.id, confirmation.type, controller.signal);
      if (controller.signal.aborted) return;
      setNotice({
        kind: 'success',
        message:
          confirmation.type === 'analyze'
            ? `Análise concluída: ${result.recommendationsCreated} recomendação(ões) e ${result.preventiveAlertsCreated} alerta(s) preventivo(s) criados. Nenhuma ordem é criada pela análise.`
            : confirmation.type === 'dismiss'
              ? 'Recomendação descartada.'
              : confirmation.recommendationType === 'SendMaintenance'
                ? 'Recomendação aceita. Consulte a ordem no módulo de manutenções.'
                : 'Aceite registrado para avaliação de reforço. Nenhum agente foi deslocado automaticamente.',
      });
      setConfirmation(null);
      resource.refresh();
    } catch (error) {
      if (pending.current !== controller) return;
      if (error.status === 401) onLogout('Sua sessão expirou. Entre novamente.');
      else {
        setFailedAt(Date.now());
        setNotice({ kind: 'error', message: predictiveError(error) });
        setConfirmation(null);
      }
    } finally {
      clearTimeout(deadline);
      if (pending.current === controller) {
        pending.current = null;
        busy.current = false;
        setSaving(false);
        onSavingChange(false);
      }
    }
  }

  return (
    <div
      className='equipment-page predictive-page'
      aria-busy={saving || state.loading}
    >
      <section
        className='inventory-panel'
        aria-labelledby='predictive-heading'
      >
        <div className='inventory-section-heading'>
          <div>
            <p className='inventory-eyebrow'>Inteligência operacional</p>
            <h2 id='predictive-heading'>Cidade preditiva</h2>
            <p className='inventory-muted'>
              Índices explicáveis a partir de dados persistidos. Pontos de 0 a 100 expressam
              prioridade operacional; não são probabilidades de falha ou de ocorrência.
            </p>
          </div>
          <div className='inventory-actions'>
            <button
              className='secondary-button'
              disabled={saving || state.loading}
              onClick={resource.refresh}
            >
              {state.loading ? 'Atualizando…' : 'Atualizar dados'}
            </button>
            <button
              className='primary-button'
              disabled={disabled}
              onClick={() => {
                setNotice(null);
                setConfirmation({ type: 'analyze' });
              }}
            >
              Executar análise
            </button>
          </div>
        </div>
        <p className='inventory-muted'>
          Atualização manual · Último ciclo completo:{' '}
          {state.updatedAt ? (
            <time dateTime={state.updatedAt.toISOString()}>
              {formatDate(state.updatedAt.toISOString())}
            </time>
          ) : (
            'Ainda não concluído'
          )}
          . Horários apresentados no fuso do navegador; avaliações e registros da API em UTC.
        </p>
        <p className='inventory-muted'>
          As fontes podem ser lidas em instantes diferentes; confira os horários antes de agir. Sem
          histórico suficiente, a ausência de pontuação não comprova segurança.
        </p>
        {state.error && (
          <p
            role='alert'
            className='status-notice error'
          >
            {state.error}
          </p>
        )}
        {notice && (
          <p
            role={notice.kind === 'error' ? 'alert' : 'status'}
            className={`status-notice ${notice.kind}`}
          >
            {notice.message}
          </p>
        )}
        {uncertain && (
          <p role='status'>
            Atualize todas as fontes com sucesso antes de enviar outra ação. A operação anterior
            pode ter sido concluída no servidor.
          </p>
        )}
        {confirmation && (
          <section
            className='predictive-confirmation'
            ref={confirmationPanel}
            tabIndex='-1'
            aria-labelledby='predictive-confirm-heading'
          >
            <h3 id='predictive-confirm-heading'>
              {confirmation.type === 'analyze'
                ? 'Confirmar análise persistida'
                : confirmation.type === 'dismiss'
                  ? 'Confirmar descarte'
                  : 'Confirmar aceite'}
            </h3>
            <p>
              {confirmation.type === 'analyze'
                ? 'A análise pode registrar recomendações e alertas preventivos, respeitando as pendências existentes. Não abre ordens automaticamente.'
                : confirmation.type === 'dismiss'
                  ? 'A recomendação será descartada. Os registros de origem serão preservados.'
                  : confirmation.recommendationType === 'SendMaintenance'
                    ? 'O aceite reutiliza o fluxo de manutenção: agenda preventiva para equipamento operacional ou abre corretiva para equipamento em falha. Uma ordem ativa impede duplicação.'
                    : 'Registra a decisão de avaliar reforço nesta região. O despacho e a disponibilidade dos agentes continuam sob controle operacional.'}
            </p>
            <div className='inventory-actions'>
              <button
                className='primary-button'
                disabled={disabled}
                onClick={execute}
              >
                {saving ? 'Processando…' : 'Confirmar'}
              </button>
              <button
                className='secondary-button'
                disabled={saving}
                onClick={() => setConfirmation(null)}
              >
                Cancelar
              </button>
            </div>
          </section>
        )}
        <InventoryDetails
          entries={[
            [
              'Equipamentos de alto risco',
              equipment ? equipment.filter((item) => item.level === 'High').length : 'Indisponível',
            ],
            [
              'Bairros com risco alto/crítico',
              neighborhoods
                ? neighborhoods.filter((item) => ['High', 'Critical'].includes(item.level)).length
                : 'Indisponível',
            ],
            [
              'Recomendações pendentes',
              sources.recommendations?.data ? recommendations.length : 'Indisponível',
            ],
            [
              'Histórico recente insuficiente',
              equipment
                ? equipment.filter((item) => !item.hasSufficientHistory).length
                : 'Indisponível',
            ],
          ]}
        />
        <div className='inventory-form-grid predictive-filters'>
          <label>
            Buscar equipamento ou bairro
            <input
              id='predictive-search'
              type='search'
              value={filters.search}
              onChange={(event) => setFilters({ ...filters, search: event.target.value })}
            />
          </label>
          <label>
            Bairro
            <select
              id='predictive-neighborhood'
              value={filters.neighborhood}
              onChange={(event) => setFilters({ ...filters, neighborhood: event.target.value })}
            >
              <option value=''>Todos os bairros</option>
              {selectedNeighborhoods.map((name) => (
                <option key={name}>{name}</option>
              ))}
            </select>
          </label>
          <label>
            Nível de risco/prioridade
            <select
              id='predictive-level'
              value={filters.level}
              onChange={(event) => setFilters({ ...filters, level: event.target.value })}
            >
              <option value=''>Todos os níveis</option>
              {Object.entries(riskLabels).map(([value, label]) => (
                <option
                  key={value}
                  value={value}
                >
                  {label}
                </option>
              ))}
            </select>
          </label>
        </div>
      </section>

      <section
        className='inventory-panel'
        aria-labelledby='equipment-risk-heading'
      >
        <h2 id='equipment-risk-heading'>Equipamentos por risco</h2>
        <p className='inventory-muted'>
          Falhas e corretivas na janela indicada; preventivas e situação atuais. Sinais de
          telemetria são eventos categóricos conhecidos, sem inferir anomalias a partir de payloads.
        </p>
        <Source
          result={sources.equipment}
          loading={state.loading}
        >
          {!filteredEquipment.length ? (
            <p className='inventory-empty'>Nenhum equipamento encontrado para os filtros atuais.</p>
          ) : (
            filteredEquipment.slice(0, limit.equipment).map((item) => (
              <article
                className='predictive-card'
                key={item.equipmentId}
              >
                <div className='predictive-card-heading'>
                  <h3>
                    {item.serialNumber} · {item.neighborhood || 'Bairro não informado'}
                  </h3>
                  <span>
                    <Level value={item.level} /> <strong>{points(item.score)}/100</strong>
                  </span>
                </div>
                <p>{item.summary}</p>
                <p
                  className={!item.hasSufficientHistory ? 'predictive-caution' : 'inventory-muted'}
                >
                  {item.historyDescription}
                </p>
                <p className='inventory-muted'>
                  Janela: {formatDate(item.windowFrom)} a {formatDate(item.evaluatedAt)} · Avaliado:{' '}
                  <time dateTime={item.evaluatedAt}>{formatDate(item.evaluatedAt)}</time>
                </p>
                {item.recommendedAction && (
                  <p>
                    <strong>Ação sugerida:</strong> {item.recommendedAction}
                  </p>
                )}
                <Factors items={item.factors} />
                <div className='inventory-actions'>
                  <button
                    className='secondary-button'
                    disabled={saving}
                    onClick={() => onEquipment(item.equipmentId)}
                  >
                    Ver equipamento
                  </button>
                  {canMaintain(session.user) && (
                    <button
                      className='secondary-button'
                      disabled={saving}
                      onClick={() => onMaintenance(item.equipmentId)}
                    >
                      Consultar manutenções
                    </button>
                  )}
                </div>
              </article>
            ))
          )}
          <More
            total={filteredEquipment.length}
            limit={limit.equipment}
            onMore={() => setLimit({ ...limit, equipment: limit.equipment + 10 })}
          />
        </Source>
      </section>

      <section
        className='inventory-panel'
        aria-labelledby='neighborhood-risk-heading'
      >
        <h2 id='neighborhood-risk-heading'>Exposição por bairro</h2>
        <p className='inventory-muted'>
          Densidade relativa entre os bairros observados, falhas recorrentes, horário de pico,
          indisponibilidade e agentes disponíveis no bairro cadastrado. A posição declarada não
          comprova cobertura física; dados atuais de disponibilidade não reconstroem o passado.
        </p>
        <Source
          result={sources.neighborhoods}
          loading={state.loading}
        >
          {!filteredNeighborhoods.length ? (
            <p className='inventory-empty'>Nenhum bairro encontrado para os filtros atuais.</p>
          ) : (
            filteredNeighborhoods.slice(0, limit.neighborhoods).map((item) => (
              <article
                className='predictive-card'
                key={item.neighborhood}
              >
                <div className='predictive-card-heading'>
                  <h3>{item.neighborhood}</h3>
                  <span>
                    <Level value={item.level} /> <strong>{points(item.score)}/100</strong>
                  </span>
                </div>
                <p>{item.summary}</p>
                <p className='inventory-muted'>
                  Incidentes desde {formatDate(item.incidentWindowFrom)} · Falhas desde{' '}
                  {formatDate(item.failureWindowFrom)} · Avaliado:{' '}
                  <time dateTime={item.evaluatedAt}>{formatDate(item.evaluatedAt)}</time>
                </p>
                <Factors items={item.factors} />
              </article>
            ))
          )}
          <More
            total={filteredNeighborhoods.length}
            limit={limit.neighborhoods}
            onMore={() => setLimit({ ...limit, neighborhoods: limit.neighborhoods + 10 })}
          />
        </Source>
      </section>

      <section
        className='inventory-panel'
        aria-labelledby='recommendations-heading'
      >
        <h2 id='recommendations-heading'>Recomendações priorizadas</h2>
        <p className='inventory-muted'>
          Prioridade derivada do índice registrado ou de gatilhos de anomalia/MTBF. Evidências
          refletem o momento da geração; confira a leitura atual antes de agir. Só gestores podem
          aceitar ou descartar.
        </p>
        <Source
          result={sources.recommendations}
          loading={state.loading}
        >
          {!filteredRecommendations.length ? (
            <p className='inventory-empty'>
              Nenhuma recomendação pendente para os filtros atuais. Execute a análise para avaliar
              os registros disponíveis.
            </p>
          ) : (
            filteredRecommendations.slice(0, limit.recommendations).map((item) => (
              <article
                className='predictive-card'
                key={item.id}
              >
                <div className='predictive-card-heading'>
                  <h3>
                    {item.type === 'SendMaintenance'
                      ? 'Inspeção e manutenção'
                      : 'Avaliar reforço de agentes'}{' '}
                    · {item.neighborhood || 'Bairro não informado'}
                  </h3>
                  <Level value={recommendationPriority(item)} />
                </div>
                <p>{item.reason}</p>
                <p>
                  <strong>Ação:</strong>{' '}
                  {item.action ??
                    (item.type === 'SendMaintenance'
                      ? 'Verificar histórico, situação atual e planejamento da manutenção.'
                      : 'Avaliar reforço operacional na região; o aceite não despacha agentes.')}
                </p>
                {item.evidence && <p>{item.evidence}</p>}
                {item.rule && <p className='inventory-muted'>Regra: {item.rule}</p>}
                <p className='inventory-muted'>
                  Registrada: <time dateTime={item.createdAt}>{formatDate(item.createdAt)}</time>
                  {item.riskScore != null
                    ? ` · Índice registrado: ${points(item.riskScore)}/100`
                    : ' · Sem pontuação numérica registrada'}
                </p>
                {item.factors.length > 0 && <Factors items={item.factors} />}
                {item.type === 'SendMaintenance' && !item.equipmentId && (
                  <p className='predictive-caution'>
                    Triagem regional: identifique um equipamento e use o módulo de manutenção. Esta
                    recomendação não pode ser convertida diretamente em uma ordem.
                  </p>
                )}
                <div className='inventory-actions'>
                  {item.equipmentId && (
                    <button
                      className='secondary-button'
                      disabled={saving}
                      onClick={() => onEquipment(item.equipmentId)}
                    >
                      Ver equipamento
                    </button>
                  )}
                  {item.type === 'DeployAgent' && (
                    <button
                      className='secondary-button'
                      disabled={saving}
                      onClick={onAgents}
                    >
                      Consultar agentes
                    </button>
                  )}
                  {manager && (
                    <>
                      {(item.type !== 'SendMaintenance' || item.equipmentId) && (
                        <button
                          className='primary-button'
                          disabled={disabled || sources.recommendations.stale}
                          onClick={() =>
                            setConfirmation({
                              type: 'accept',
                              id: item.id,
                              recommendationType: item.type,
                            })
                          }
                        >
                          Aceitar recomendação
                        </button>
                      )}
                      <button
                        className='secondary-button'
                        disabled={disabled || sources.recommendations.stale}
                        onClick={() => setConfirmation({ type: 'dismiss', id: item.id })}
                      >
                        Descartar
                      </button>
                    </>
                  )}
                </div>
              </article>
            ))
          )}
          <More
            total={filteredRecommendations.length}
            limit={limit.recommendations}
            onMore={() => setLimit({ ...limit, recommendations: limit.recommendations + 10 })}
          />
        </Source>
      </section>

      <section
        className='inventory-panel'
        aria-labelledby='mtbf-heading'
      >
        <h2 id='mtbf-heading'>MTBF e sinais de anomalia</h2>
        <p className='inventory-muted'>
          Estimativa descritiva do tempo médio entre falhas, com fallback por tipo quando
          necessário. Não é modelo estatístico calibrado. A razão entre tempo decorrido e MTBF não é
          probabilidade; o prazo é uma extrapolação, sem garantia de falha futura. Equipamentos sem
          intervalos válidos ficam fora do MTBF e continuam visíveis na avaliação de risco.
        </p>
        <Source
          result={sources.failures}
          loading={state.loading}
        >
          {!sources.failures?.data?.length ? (
            <p className='inventory-empty'>
              Sem histórico suficiente para estimativas MTBF nos equipamentos operacionais.
            </p>
          ) : (
            <details>
              <summary>
                Consultar {sources.failures.data.length} estimativa(s) descritiva(s)
              </summary>
              {sources.failures.data.map((item) => (
                <article
                  className='predictive-card'
                  key={item.equipmentId}
                >
                  <h3>
                    {item.serialNumber} · {item.neighborhood}
                  </h3>
                  <p>{item.explanation}</p>
                  <p className='inventory-muted'>
                    Base:{' '}
                    {item.mtbfBasis === 'Equipment'
                      ? 'próprio equipamento'
                      : 'outros registros do mesmo tipo'}{' '}
                    · {item.sampleSize} intervalo(s) observado(s) · MTBF: {points(item.mtbfDays)}{' '}
                    dias · Extrapolação restante: {points(item.estimatedDaysToFailure)} dias.
                  </p>
                  {item.sampleSize < 3 && (
                    <p className='predictive-caution'>
                      Amostra pequena: estimativa especialmente incerta.
                    </p>
                  )}
                  <button
                    className='secondary-button'
                    disabled={saving}
                    onClick={() => onEquipment(item.equipmentId)}
                  >
                    Ver equipamento
                  </button>
                </article>
              ))}
            </details>
          )}
        </Source>
        <p className='inventory-muted'>
          Picos de volume usam média e desvio de janelas equivalentes de semanas anteriores. Sem
          dados de cobertura de coleta, semanas sem registros contam como zero; um pico pode
          refletir mudança de cadastro/coleta. Falhas repetidas usam um limiar de contagem, sem
          probabilidade calibrada.
        </p>
        <Source
          result={sources.anomalies}
          loading={state.loading}
        >
          {!sources.anomalies?.data?.length ? (
            <p className='inventory-empty'>
              Nenhuma anomalia detectada nos registros e janelas configurados.
            </p>
          ) : (
            <details>
              <summary>Consultar {sources.anomalies.data.length} sinal(is) detectado(s)</summary>
              {sources.anomalies.data.map((item, index) => (
                <article
                  className='predictive-card'
                  key={`${item.kind}-${item.equipmentId ?? item.neighborhood}-${index}`}
                >
                  <h3>
                    {item.subject} · {item.neighborhood}
                  </h3>
                  <p>{item.explanation}</p>
                  <p className='inventory-muted'>
                    Evidências: {item.recordCount} registro(s) · Detectado:{' '}
                    <time dateTime={item.detectedAt}>{formatDate(item.detectedAt)}</time>
                  </p>
                </article>
              ))}
            </details>
          )}
        </Source>
      </section>
    </div>
  );
}
