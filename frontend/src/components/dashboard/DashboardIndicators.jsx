// Componentes
import DashboardIcon from './DashboardIcon.jsx';

// Serviços e utilitários
import { getDashboardSummary } from './dashboardData.js';

export default function DashboardIndicators({ loading, result, error }) {
  const failure = result?.error?.message || error;
  const data = result?.data;
  const summary = getDashboardSummary(data);
  const number = (value) =>
    Number.isFinite(value)
      ? new Intl.NumberFormat('pt-BR', { maximumFractionDigits: 2 }).format(value)
      : '—';
  const indicators = [
    {
      label: 'Equipamentos cadastrados',
      value: data?.totalEquipments,
      icon: 'equipment',
      tone: 'neutral',
      note: 'Inventário da cidade',
    },
    {
      label: 'Equipamentos operacionais',
      value: summary?.operational,
      icon: 'shield',
      tone: 'green',
      note: 'Em operação',
    },
    {
      label: 'Equipamentos indisponíveis',
      value: summary?.unavailable,
      icon: 'alerts',
      tone: 'red',
      note: 'Falha, manutenção ou roubo',
    },
    {
      label: 'Disponibilidade',
      value: data?.availabilityRatePercent,
      icon: 'availability',
      tone: 'green',
      note: 'Operacionais sobre o total',
      percentage: true,
    },
    {
      label: 'Agentes ativos',
      value: data?.activeAgents,
      icon: 'agents',
      tone: 'yellow',
      note: 'Recursos em atividade',
    },
  ];

  return (
    <section
      className='dashboard-indicators'
      aria-label='Indicadores operacionais da cidade inteira'
      aria-busy={loading}
    >
      <dl className='indicator-list'>
        {indicators.map((indicator) => (
          <div
            key={indicator.label}
            className={`indicator-card indicator-${indicator.tone}`}
          >
            <dt>
              {indicator.label}
              <span className='indicator-icon'>
                <DashboardIcon name={indicator.icon} />
              </span>
            </dt>
            <dd>
              {number(indicator.value)}
              {indicator.percentage && Number.isFinite(indicator.value) ? '%' : ''}
              <span className='indicator-note'>{indicator.note}</span>
            </dd>
          </div>
        ))}
      </dl>
      <div
        className='indicator-status'
        aria-live='polite'
      >
        {loading ? (
          <p role='status'>
            {data ? 'Atualizando; exibindo a última resposta válida.' : 'Consultando indicadores…'}
          </p>
        ) : failure ? (
          <p
            className='message error'
            role='alert'
          >
            {data ? 'Exibindo dados anteriores. ' : 'Indicadores indisponíveis. '}
            {failure}
          </p>
        ) : !data ? (
          <p>Sem resposta de indicadores disponível.</p>
        ) : data.totalEquipments === 0 ? (
          <p>Nenhum equipamento cadastrado na resposta da API.</p>
        ) : null}
      </div>
    </section>
  );
}
