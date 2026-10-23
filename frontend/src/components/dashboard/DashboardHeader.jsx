// Componentes
import DashboardIcon from './DashboardIcon.jsx';

// Serviços e utilitários
import { userInitials } from './dashboardData.js';

export default function DashboardHeader({ user, verification, mobileOpen, onMenu, menuButtonRef, inventory = false, inventoryTitle = 'Equipamentos', inventorySubtitle = 'Cadastro, localização e situação dos recursos urbanos' }) {
  const failures = verification.results.filter(result => result.error).length;
  const healthy = !verification.loading && verification.results.length > 0 && failures === 0 && !verification.error;
  const status = verification.loading ? 'Atualizando recursos' : healthy ? 'Sistema operacional' : 'Verifique a conexão com os dados';

  return (
    <header className="dashboard-header">
      <button ref={menuButtonRef} className="mobile-menu-button icon-button" onClick={onMenu} aria-label="Abrir menu"
        aria-expanded={mobileOpen} aria-controls="dashboard-navigation"><DashboardIcon name="menu" /></button>
      <div className="header-heading">
        <p className={`system-status ${!inventory && healthy ? 'is-healthy' : ''}`} role="status"><span />{inventory ? 'Inventário inteligente da cidade' : status}</p>
        <h1>{inventory ? inventoryTitle : 'Central de Operações'}</h1>
        <p className="header-subtitle">{inventory ? inventorySubtitle : 'Visão geral dos recursos e da situação operacional da cidade'}</p>
      </div>
      <div className="header-actions">
        <div className="header-date"><span>Hoje</span><time dateTime={new Date().toISOString()}>
          {new Intl.DateTimeFormat('pt-BR', { weekday: 'long', day: 'numeric', month: 'long' }).format(new Date())}
        </time></div>
        {!inventory && <button className="refresh-button" onClick={verification.refresh} disabled={verification.loading}>
          <DashboardIcon name="refresh" /><span>{verification.loading ? 'Atualizando…' : 'Atualizar'}</span>
        </button>}
        {!inventory && <a className="icon-button notification-button" href="#alertas-recentes" aria-label="Ver painel de alertas">
          <DashboardIcon name="bell" />
        </a>}
        <span className="user-avatar header-avatar" title={user.name} aria-label={`Usuário: ${user.name}`}>{userInitials(user.name)}</span>
      </div>
    </header>
  );
}
