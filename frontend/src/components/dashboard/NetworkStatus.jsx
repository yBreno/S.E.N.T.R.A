// Serviços e utilitários
import { getDashboardSummary } from './dashboardData.js';

export default function NetworkStatus({ result, loading }) {
  const data = result?.data;
  const summary = getDashboardSummary(data);
  const usable = summary?.consistent;

  return (
    <section className="dashboard-side-panel network-status" aria-labelledby="network-heading" aria-busy={loading}>
      <header className="side-panel-heading"><h2 id="network-heading">Situação da rede</h2><p>Distribuição dos equipamentos</p></header>
      {!usable ? <p className="network-empty" role="status">
        {loading ? 'Carregando distribuição…' : 'Distribuição indisponível. Atualize os dados para tentar novamente.'}
      </p> : (
        <>
          <div className="network-body">
            <div className="network-chart">
              <svg viewBox="0 0 144 144" role="img" aria-label={`Distribuição de ${data.totalEquipments} equipamentos por situação`}>
                <circle className="donut-track" cx="72" cy="72" r="57" />
                {summary.distribution.map((item, index) => {
                  if (!item.count) return null;
                  const start = summary.distribution.slice(0, index).reduce((total, previous) => total + previous.percentage, 0);

                  return <circle key={item.name} cx="72" cy="72" r="57" pathLength="100"
                    fill="none" stroke={item.color} strokeWidth="12" strokeDasharray={`${item.percentage} ${100 - item.percentage}`}
                    strokeDashoffset={-start} transform="rotate(-90 72 72)" />;
                })}
              </svg>
              <div className="network-total"><strong>{new Intl.NumberFormat('pt-BR').format(data.totalEquipments)}</strong><span>Total</span></div>
            </div>
            <ul className="network-legend">{summary.distribution.map(item => (
              <li key={item.name}><span className="network-dot" style={{ backgroundColor: item.color }} />
                <span>{item.label}</span><strong>{new Intl.NumberFormat('pt-BR', { maximumFractionDigits: 1 }).format(item.percentage)}%</strong>
                <span className="sr-only">({item.count} equipamentos)</span>
              </li>
            ))}</ul>
          </div>
          {data.totalEquipments === 0 && <p className="network-empty">Nenhum equipamento retornado pela API.</p>}
          <p className="network-note">Distribuição geral, sem os filtros do mapa.</p>
          {(result?.error || result?.stale || loading) && <p className="network-stale" role="status">Exibindo a última resposta válida; os dados podem estar desatualizados.</p>}
        </>
      )}
    </section>
  );
}
