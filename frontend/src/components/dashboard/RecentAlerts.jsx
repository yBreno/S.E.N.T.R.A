// Componentes
import DashboardIcon from './DashboardIcon.jsx';

export default function RecentAlerts() {
  return (
    <section className="dashboard-side-panel recent-alerts" id="alertas-recentes" aria-labelledby="alerts-heading">
      <header className="side-panel-heading"><h2 id="alerts-heading">Alertas recentes</h2><p>Eventos que exigem atenção</p></header>
      <div className="alerts-empty">
        <span className="empty-state-icon"><DashboardIcon name="bell" /></span>
        <strong>Alertas ainda indisponíveis</strong>
        <p>Este painel ainda não recebe alertas e ocorrências.</p>
        <span className="empty-state-note">O monitoramento de recursos continua no mapa.</span>
      </div>
    </section>
  );
}
