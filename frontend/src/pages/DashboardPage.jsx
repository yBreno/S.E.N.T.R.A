// Bibliotecas externas
import { lazy, Suspense, useEffect, useRef, useState } from 'react';

// Componentes
import DashboardSidebar from '../components/dashboard/DashboardSidebar.jsx';
import DashboardHeader from '../components/dashboard/DashboardHeader.jsx';
import DashboardIndicators from '../components/dashboard/DashboardIndicators.jsx';
import ModuleBoundary from '../components/ModuleBoundary.jsx';
import NetworkStatus from '../components/dashboard/NetworkStatus.jsx';
import RecentAlerts from '../components/dashboard/RecentAlerts.jsx';
const MapView = lazy(() => import('../components/map/MapView.jsx'));
const EquipmentPage = lazy(() => import('./EquipmentPage.jsx'));
const AgentPage = lazy(() => import('./AgentPage.jsx'));
const MaintenancePage = lazy(() => import('./MaintenancePage.jsx'));
const PredictivePage = lazy(() => import('./PredictivePage.jsx'));
import { CityOverview, MaintenanceHighlights } from '../components/dashboard/OperationsOverview.jsx';
import { createEmptyFilters, neighborhoodFilterValue, UNKNOWN_FILTER_VALUE } from '../components/map/operationalFilters.js';
import { canMonitor, canMaintain } from '../services/auth.js';

// Hooks personalizados
import { useEndpointVerification } from '../hooks/useEndpointVerification.js';
import { useHeatmap } from '../hooks/useHeatmap.js';

// Estilos
import './DashboardPage.css';
import '../components/dashboard/OperationsOverview.css';

export default function DashboardPage({ session, onLogout }) {
  const monitoring = canMonitor(session.user);
  const [active, setActive] = useState(() => window.location.hash === '#manutencoes' && canMaintain(session.user) ? 'manutencoes' : !monitoring || window.location.hash === '#equipamentos' ? 'equipamentos' : window.location.hash === '#agentes' ? 'agentes' : window.location.hash === '#cidade-preditiva' ? 'cidade-preditiva' : 'dashboard');
  const [relatedEquipment, setRelatedEquipment] = useState('');
  const inventory = active === 'equipamentos';
  const agents = active === 'agentes';
  const maintenance = active === 'manutencoes';
  const predictive = active === 'cidade-preditiva';
  const [inventorySaving, setInventorySaving] = useState(false);
  const verification = useEndpointVerification(session.token, onLogout, monitoring && !inventory && !agents && !maintenance && !predictive, canMaintain(session.user));
  const [mapFilters, setMapFilters] = useState(createEmptyFilters);
  const heatmap = useHeatmap({ token: session.token, onLogout, verification, active: monitoring && !inventory && !agents && !maintenance && !predictive });
  const dashboardResult = verification.results.find(result => result.id === 'dashboard');
  const [collapsed, setCollapsed] = useState(false);
  const [mobileOpen, setMobileOpen] = useState(false);
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
    if (inventorySaving || (target === 'manutencoes' && !canMaintain(session.user)) || (!monitoring && !['equipamentos', 'manutencoes'].includes(target))) return;
    setRelatedEquipment('');
    setActive(target);
    setMobileOpen(false);
    if (target !== 'dashboard') heatmap.changeEnabled(target === 'mapa-de-calor');
  }

  function related(target, id) {
    if (inventorySaving) return;
    setRelatedEquipment(id); setActive(target); setMobileOpen(false); window.location.hash = target;
  }

  return (
    <div className={`dashboard-layout ${collapsed ? 'sidebar-is-collapsed' : ''}`} id="dashboard">
      <a className="skip-link" href="#conteudo-operacional">Ir para o conteúdo</a>
      <DashboardSidebar user={session.user} active={active} collapsed={collapsed} mobileOpen={mobileOpen}
        busy={inventorySaving} onCollapse={() => setCollapsed(value => !value)} onClose={closeMenu} onNavigate={navigate} onLogout={onLogout} />
      <div className="dashboard-workspace">
        <DashboardHeader user={session.user} verification={verification} mobileOpen={mobileOpen}
          inventory={inventory || agents || maintenance || predictive} inventoryEyebrow={predictive ? 'Análise preventiva da cidade' : undefined}
          inventoryTitle={predictive ? 'Cidade preditiva' : maintenance ? 'Manutenções' : agents ? 'Agentes' : 'Equipamentos'} inventorySubtitle={predictive ? 'Riscos explicáveis e ações preventivas com dados persistidos' : maintenance ? 'Planejamento, intervenção e histórico dos equipamentos' : agents ? 'Profissionais de mobilidade, segurança e atuação em campo' : undefined} onMenu={() => setMobileOpen(value => !value)} menuButtonRef={menuButton} />
        <main className="dashboard-main" id="conteudo-operacional">
          <ModuleBoundary><Suspense fallback={<p role="status">Carregando módulo…</p>}>
          {predictive ? <PredictivePage session={session} onLogout={onLogout} onSavingChange={setInventorySaving} onEquipment={id => related('equipamentos', id)} onMaintenance={id => related('manutencoes', id)} onAgents={() => related('agentes', '')} /> : maintenance ? <MaintenancePage key={relatedEquipment} equipmentId={relatedEquipment} session={session} onLogout={onLogout} onChanged={verification.refresh} onSavingChange={setInventorySaving} onEquipment={id => related('equipamentos', id)} /> : agents ? <AgentPage key={relatedEquipment} agentId={relatedEquipment} session={session} onLogout={onLogout} onChanged={verification.refresh} onSavingChange={setInventorySaving} /> : inventory ? <EquipmentPage key={relatedEquipment} equipmentId={relatedEquipment} session={session} onLogout={onLogout} onChanged={verification.refresh} onSavingChange={setInventorySaving} onMaintenance={id => related('manutencoes', id)} /> : <>
          <DashboardIndicators loading={verification.loading} result={dashboardResult} error={verification.error} />
          <div className="operations-sync" aria-live="polite"><p>Indicadores da cidade inteira · Sincronização automática a cada {verification.intervalMs / 1000} s após cada ciclo.</p><p>{verification.updatedAt ? <>Último ciclo completo bem-sucedido: <time dateTime={verification.updatedAt.toISOString()}>{verification.updatedAt.toLocaleString('pt-BR')}</time></> : 'Ainda sem ciclo completo bem-sucedido.'}</p>{verification.error && <p role="alert">{verification.error}</p>}</div>
          <div className="operations-grid">
            <section className="map-panel" id="mapa-operacional" aria-labelledby="map-heading">
              <MapView verification={verification} heatmap={heatmap} filters={mapFilters} onFiltersChange={setMapFilters} navigation={{ equipment: id => related('equipamentos', id), agent: id => related('agentes', id), maintenance: canMaintain(session.user) ? id => related('manutencoes', id) : null }} />
            </section>
            <aside className="dashboard-secondary" aria-label="Resumo da operação">
              <RecentAlerts result={verification.results.find(item => item.id === 'alerts')} equipments={verification.results.find(item => item.id === 'equipments')?.items} loading={verification.loading} onEquipment={id => related('equipamentos', id)} />
              <MaintenanceHighlights verification={verification} canMaintain={canMaintain(session.user)} onEquipment={id => related('equipamentos', id)} onMaintenance={id => related('manutencoes', id)} />
              <NetworkStatus result={dashboardResult} loading={verification.loading} />
            </aside>
          </div>
          <CityOverview verification={verification} onNeighborhood={label => { setMapFilters(previous => ({ ...previous, neighborhood: label === 'Bairro não informado' ? UNKNOWN_FILTER_VALUE : neighborhoodFilterValue(label) })); document.getElementById('mapa-operacional')?.scrollIntoView({ block: 'start' }); }} />
          </>}
          </Suspense></ModuleBoundary>
          <footer className="dashboard-footer">
            <span><span className="footer-location" />São Paulo, SP</span>
            <span>{inventory || agents || maintenance ? 'Atualização manual do módulo' : 'Sincronização automática da Central'}{verification.updatedAt && <> · Último sucesso da Central: <time dateTime={verification.updatedAt.toISOString()}>
              {verification.updatedAt.toLocaleString('pt-BR')}
            </time></>}</span>
          </footer>
        </main>
      </div>
    </div>
  );
}
