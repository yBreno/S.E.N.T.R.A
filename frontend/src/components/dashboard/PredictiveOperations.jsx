import {
  prioritizeRecommendations,
  recommendationPriority,
  riskLabels,
} from '../../services/predictive.js';
import './PredictiveOperations.css';

const date = (value) =>
  value ? new Date(value).toLocaleString('pt-BR') : 'Não registrada nesta instância';
const points = (value) => value.toLocaleString('pt-BR', { maximumFractionDigits: 1 });
const statusLabels = {
  NotRun: 'Não executada',
  Running: 'Em execução',
  Completed: 'Concluída',
  Failed: 'Falhou ou foi cancelada',
};
export default function PredictiveOperations({
  resource,
  onEquipment,
  onNeighborhood,
  onMaintenance,
  onPredictive,
}) {
  const result = resource.result;
  const data = result?.data;
  const execution = data?.automation.execution;
  const recommendations = prioritizeRecommendations(data?.recommendations ?? []);
  const regions = [...(data?.neighborhoods ?? [])]
    .filter((item) => ['High', 'Critical'].includes(item.level))
    .sort((a, b) => b.score - a.score);
  return (
    <section
      className='dashboard-side-panel predictive-operations'
      aria-labelledby='predictive-operations-heading'
    >
      <header className='side-panel-heading'>
        <h2 id='predictive-operations-heading'>Prevenção operacional</h2>
        <p>Riscos e ações preventivas · índice, sem probabilidade calibrada</p>
      </header>
      <div className='inventory-actions'>
        <button
          className='secondary'
          disabled={resource.loading}
          onClick={resource.refresh}
        >
          {resource.loading ? 'Atualizando prevenção…' : 'Atualizar prevenção'}
        </button>
        <button
          className='secondary'
          onClick={onPredictive}
        >
          Ver análises e fatores
        </button>
      </div>
      {resource.loading && <p role='status'>Consultando riscos e recomendações…</p>}
      {(result?.error || result?.stale || resource.error) && (
        <p role='alert'>
          Prevenção indisponível ou desatualizada.{' '}
          {data
            ? 'Exibindo a última leitura válida; confira o horário antes de agir.'
            : 'Não interprete dados ausentes como risco zero.'}
        </p>
      )}
      {data && (
        <>
          <p className='operations-note'>
            Leitura preditiva: <time dateTime={data.evaluatedAt}>{date(data.evaluatedAt)}</time> ·
            Atualização a cada 5 minutos ou manual. Risco alto não significa falha atual; a situação
            operacional continua independente.
          </p>
          <p>
            {data.equipmentRisks.length} equipamento(s) de alto risco · {regions.length} bairro(s)
            de risco alto/crítico · {recommendations.length} recomendação(ões) pendente(s).
          </p>
          <details className='predictive-run-status'>
            <summary>Execução da análise e automação</summary>
            <p>
              Automação{' '}
              {data.automation.enabled
                ? data.automation.multipleInstancesSupported
                  ? `configurada: intervalo ${data.automation.intervalMinutes} minutos`
                  : 'bloqueada: coordenação distribuída indisponível'
                : 'desligada'}
              .
            </p>
            <p>
              {statusLabels[execution.status]} · Origem:{' '}
              {execution.origin === 'Automatic'
                ? 'automática'
                : execution.origin === 'Manual'
                  ? 'manual'
                  : 'nenhuma'}
              .
            </p>
            <p>
              Última automática concluída: {date(execution.lastAutomaticCompletedAt)} · Última
              análise concluída: {date(execution.lastCompletedAt)}.
            </p>
            {execution.status === 'Completed' && (
              <p>
                Resultado desta execução: {execution.recommendationsCreated ?? 'não informado'}{' '}
                recomendação(ões), {execution.preventiveAlertsCreated ?? 'não informado'} alerta(s)
                preventivo(s).
              </p>
            )}
            <p className='operations-note'>
              Estado em memória desta instância. Reiniciar apaga o registro; não representa outras
              réplicas. A leitura desta página não executa análises nem aceita recomendações.
            </p>
          </details>
          {data.equipmentRisks.length > 0 && (
            <details open>
              <summary>Equipamentos com risco alto</summary>
              <ul className='operations-resource-list'>
                {data.equipmentRisks.slice(0, 4).map((item) => (
                  <li key={item.equipmentId}>
                    <strong>
                      {item.serialNumber} · {points(item.score)}/100
                    </strong>
                    <span>
                      {item.neighborhood} · Avaliado: {date(item.evaluatedAt)}
                    </span>
                    <span>{item.summary}</span>
                    <button
                      className='secondary'
                      onClick={() => onEquipment(item.equipmentId)}
                    >
                      Ver equipamento
                    </button>
                    {onMaintenance && (
                      <button
                        className='secondary'
                        onClick={() => onMaintenance(item.equipmentId)}
                      >
                        Consultar manutenção
                      </button>
                    )}
                  </li>
                ))}
              </ul>
            </details>
          )}
          {regions.length > 0 && (
            <p className='predictive-neighborhoods'>
              Regiões prioritárias:{' '}
              {regions.slice(0, 4).map((item) => (
                <button
                  key={item.neighborhood}
                  className='secondary'
                  onClick={() => onNeighborhood(item.neighborhood)}
                >
                  {item.neighborhood} · {riskLabels[item.level]} · {points(item.score)}/100
                </button>
              ))}
            </p>
          )}
          <h3>Recomendações preventivas pendentes</h3>
          {!recommendations.length ? (
            <p>Nenhuma recomendação pendente na leitura atual.</p>
          ) : (
            <ul className='operations-resource-list'>
              {recommendations.slice(0, 6).map((item) => (
                <li key={item.id}>
                  <strong>
                    {riskLabels[recommendationPriority(item)] ?? 'Prioridade não informada'} ·{' '}
                    {item.neighborhood || 'Bairro não informado'}
                  </strong>
                  <span>{item.reason}</span>
                  <time dateTime={item.createdAt}>{date(item.createdAt)}</time>
                  {item.equipmentId ? (
                    <>
                      <button
                        className='secondary'
                        onClick={() => onEquipment(item.equipmentId)}
                      >
                        Ver equipamento
                      </button>
                      {onMaintenance && item.type === 'SendMaintenance' && (
                        <button
                          className='secondary'
                          onClick={() => onMaintenance(item.equipmentId)}
                        >
                          Consultar manutenção
                        </button>
                      )}
                    </>
                  ) : (
                    item.neighborhood && (
                      <button
                        className='secondary'
                        onClick={() => onNeighborhood(item.neighborhood)}
                      >
                        Ver bairro no mapa
                      </button>
                    )
                  )}
                  <button
                    className='secondary'
                    onClick={onPredictive}
                  >
                    Consultar recomendação e evidências
                  </button>
                </li>
              ))}
            </ul>
          )}
          {recommendations.length > 6 && (
            <p className='operations-note'>
              Exibindo as seis primeiras. Consulte todas em Cidade preditiva.
            </p>
          )}
        </>
      )}
    </section>
  );
}
