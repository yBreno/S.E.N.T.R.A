export default function DashboardIndicators({
  loading,
  result,
  error
}) {
  const failure = result?.error?.message || error;
  const data = result?.data;

  const number = value => new Intl.NumberFormat('pt-BR', {
    maximumFractionDigits: 2
  }).format(value);

  const indicators = [
    {
      label: 'Total de equipamentos',
      value: data ? number(data.totalEquipments) : '—'
    },
    {
      label: 'Disponibilidade',
      value: data ? `${number(data.availabilityRatePercent)}%` : '—'
    },
    {
      label: 'Agentes ativos',
      value: data ? number(data.activeAgents) : '—'
    }
  ];

  return (
    <section
      className="dashboard-indicators"
      aria-label="Indicadores operacionais"
      aria-busy={loading}
    >
      <dl className="indicator-list">
        {indicators.map(indicator => (
          <div key={indicator.label}>
            <dt>
              {indicator.label}
            </dt>
            <dd>
              {indicator.value}
            </dd>
          </div>
        ))}
      </dl>
      <div className="indicator-status" aria-live="polite">
        {loading
          ? (
            <p role="status">
              {data ? 'Atualizando indicadores; exibindo a última resposta válida, possivelmente desatualizada.' : 'Consultando os indicadores da API…'}
            </p>
          )
          : failure
            ? (
              <p className="message error" role="alert">{data ? 'Não foi possível atualizar. Exibindo indicadores desatualizados.' : 'Indicadores indisponíveis.'} {failure}</p>
            )
            : !data
              ? (
                <p>Sem resposta de indicadores disponível.</p>
              )
              : data.totalEquipments === 0
                ? (
                  <p>Nenhum equipamento cadastrado na resposta da API.</p>
                )
                : (
                  <p>Indicadores gerais · Sem filtros regionais</p>
                )}
      </div>
    </section>
  );
}
