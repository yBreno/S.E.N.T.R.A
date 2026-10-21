// Bibliotecas externas
import { useEffect, useRef, useState } from 'react';

// Componentes
import DashboardSidebar from '../components/dashboard/DashboardSidebar.jsx';
import DashboardHeader from '../components/dashboard/DashboardHeader.jsx';
import DashboardIndicators from '../components/dashboard/DashboardIndicators.jsx';
import NetworkStatus from '../components/dashboard/NetworkStatus.jsx';
import RecentAlerts from '../components/dashboard/RecentAlerts.jsx';
import MapView from '../components/map/MapView.jsx';

// Hooks personalizados
import { useEndpointVerification } from '../hooks/useEndpointVerification.js';
import { useHeatmap } from '../hooks/useHeatmap.js';

// Estilos
import './DashboardPage.css';

export default function DashboardPage({ session, onLogout }) {
  const verification = useEndpointVerification(session.token, onLogout);
  const heatmap = useHeatmap({ token: session.token, onLogout, verification });
  const dashboardResult = verification.results.find(result => result.id === 'dashboard');
  const [collapsed, setCollapsed] = useState(false);
  const [mobileOpen, setMobileOpen] = useState(false);
  const [active, setActive] = useState('dashboard');
  const menuButton = useRef(null);

  useEffect(() => {
    const media = window.matchMedia('(max-width: 760px)');
    const change = event => { if (!event.matches) setMobileOpen(false); };
    media.addEventListener('change', change);

    return () => media.removeEventListener('change', change);
  }, []);

  function closeMenu() {
    setMobileOpen(false);
    menuButton.current?.focus();
  }

  function navigate(target) {
    setActive(target);
    setMobileOpen(false);
    if (target !== 'dashboard') heatmap.changeEnabled(target === 'mapa-de-calor');
  }

  return (
    <div className={`dashboard-layout ${collapsed ? 'sidebar-is-collapsed' : ''}`} id="dashboard">
      <a className="skip-link" href="#conteudo-operacional">Ir para o conteúdo</a>
      <DashboardSidebar user={session.user} active={active} collapsed={collapsed} mobileOpen={mobileOpen}
        onCollapse={() => setCollapsed(value => !value)} onClose={closeMenu} onNavigate={navigate} onLogout={onLogout} />
      <div className="dashboard-workspace">
        <DashboardHeader user={session.user} verification={verification} mobileOpen={mobileOpen}
          onMenu={() => setMobileOpen(value => !value)} menuButtonRef={menuButton} />
        <main className="dashboard-main" id="conteudo-operacional">
          <DashboardIndicators loading={verification.loading} result={dashboardResult} error={verification.error} />
          <div className="operations-grid">
            <section className="map-panel" id="mapa-operacional" aria-labelledby="map-heading">
              <MapView verification={verification} heatmap={heatmap} />
            </section>
            <aside className="dashboard-secondary" aria-label="Resumo da operação">
              <RecentAlerts />
              <NetworkStatus result={dashboardResult} loading={verification.loading} />
            </aside>
          </div>
          <footer className="dashboard-footer">
            <span><span className="footer-location" />São Paulo, SP</span>
            <span>Atualização manual{verification.updatedAt && <> · Última tentativa: <time dateTime={verification.updatedAt.toISOString()}>
              {verification.updatedAt.toLocaleString('pt-BR')}
            </time></>}</span>
          </footer>
        </main>
      </div>
    </div>
  );
}
