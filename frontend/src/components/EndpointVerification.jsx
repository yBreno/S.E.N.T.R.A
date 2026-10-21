function DashboardSummary({
  data
}) {
  return (
    <>
      <dl>
        <dt>Total de equipamentos</dt>
        <dd>
          {data.totalEquipments}
        </dd>
        <dt>Disponibilidade</dt>
        <dd>{data.availabilityRatePercent}%</dd>
        <dt>Agentes ativos</dt>
        <dd>
          {data.activeAgents}
        </dd>
      </dl>
      <p>Equipamentos por status:</p>
      <ul>
        {Object.entries(data.byStatus).map(([status, count]) => (
          <li key={status}>{status}: {count}</li>
        ))}
      </ul>
    </>
  );
}

export default function EndpointVerification({
  verification
}) {
  const {
    loading,
    results,
    updatedAt,
    error,
    refresh
  } = verification;

  return (
    <section className="panel">
      <h2>Verificação dos endpoints protegidos</h2>
      <div className="actions">
        <button onClick={refresh} disabled={loading}>
          {loading ? 'Consultando…' : 'Atualizar dados'}
        </button>
      </div>
      <p className="muted">Consultas sem filtros regionais. O heatmap usa o período padrão da API e somente a métrica Incidents.</p>
      <div aria-live="polite" aria-busy={loading}>
        {loading && (
          <p role="status">Consultando dashboard, equipamentos, agentes e heatmap…</p>
        )}
        {error && (
          <p className="message error" role="alert">
            {error}
          </p>
        )}
        <div className="results">
          {results.map(result => (
            <article className="endpoint" key={result.id}>
              <h3>
                {result.label}
              </h3>
              <code>GET {result.path}</code>
              {result.error && (
                <p className="message error" role="alert">
                  {result.error.message}
                </p>
              )}
              {(result.stale || loading && Object.hasOwn(result, 'data')) && (
                <p className="stale-data">Última resposta válida; dados possivelmente desatualizados.</p>
              )}
              {result.receivedAt && (
                <p>Resposta válida em: <time dateTime={result.receivedAt}>
                  {new Date(result.receivedAt).toLocaleString('pt-BR')}
                </time></p>
              )}
              {Object.hasOwn(result, 'data') && <>
                {!result.stale && !loading && (
                  <p className="success">Consulta concluída.</p>
                )}
                {result.id === 'dashboard'
                  ? (
                    <DashboardSummary data={result.data} />
                  )
                  : (
                    <p>Quantidade: <strong>
                      {result.data}
                    </strong></p>
                  )}
              </>}
            </article>
          ))}
        </div>
        {updatedAt && (
          <p>Última tentativa concluída: <time dateTime={updatedAt.toISOString()}>
            {updatedAt.toLocaleString('pt-BR')}
          </time></p>
        )}
      </div>
    </section>
  );
}
