// Bibliotecas externas
import { useEffect, useRef } from 'react';

// Componentes
import Brand from '../Brand.jsx';
import DashboardIcon from './DashboardIcon.jsx';

// Serviços e utilitários
import { userInitials, userRoleLabel } from './dashboardData.js';
import { canMonitor, canMaintain } from '../../services/auth.js';
import { canAdministerUsers } from '../../services/authorization.js';

const groups = [
  { label: 'Visão geral', items: [{ label: 'Dashboard', icon: 'dashboard', target: 'dashboard' }] },
  {
    label: 'Gestão operacional',
    items: [
      { label: 'Equipamentos', icon: 'equipment', target: 'equipamentos' },
      { label: 'Agentes', icon: 'agents', target: 'agentes' },
      { label: 'Manutenções', icon: 'maintenance', target: 'manutencoes' },
    ],
  },
  {
    label: 'Monitoramento',
    items: [
      { label: 'Mapa operacional', icon: 'map', target: 'mapa-operacional' },
      { label: 'Mapa de calor', icon: 'heat', target: 'mapa-de-calor' },
    ],
  },
  {
    label: 'Inteligência',
    items: [{ label: 'Cidade preditiva', icon: 'predictive', target: 'cidade-preditiva' }],
  },
  { label: 'Sistema', items: [{ label: 'Usuários', icon: 'user', target: 'usuarios' }] },
];

export default function DashboardSidebar({
  user,
  active,
  collapsed,
  mobileOpen,
  onCollapse,
  onClose,
  onNavigate,
  onLogout,
  busy = false,
}) {
  const sidebar = useRef(null);

  useEffect(() => {
    if (mobileOpen) sidebar.current?.querySelector('.sidebar-mobile-close')?.focus();
  }, [mobileOpen]);

  function handleKey(event) {
    if (event.key === 'Escape') onClose();
    if (!mobileOpen || event.key !== 'Tab') return;
    const controls = [...sidebar.current.querySelectorAll('a[href], button:not(:disabled)')].filter(
      (element) => element.getClientRects().length > 0,
    );
    const first = controls[0];
    const last = controls.at(-1);
    if (event.shiftKey && document.activeElement === first) {
      event.preventDefault();
      last?.focus();
    } else if (!event.shiftKey && document.activeElement === last) {
      event.preventDefault();
      first?.focus();
    }
  }

  return (
    <>
      {mobileOpen && (
        <button
          className='sidebar-backdrop'
          aria-label='Fechar menu'
          onClick={onClose}
        />
      )}
      <aside
        ref={sidebar}
        id='dashboard-navigation'
        className={`dashboard-sidebar ${mobileOpen ? 'is-open' : ''}`}
        aria-label='Navegação principal'
        onKeyDown={handleKey}
      >
        <div className='sidebar-brand'>
          <a
            href={canMonitor(user) ? '#dashboard' : '#equipamentos'}
            onClick={(event) => {
              if (busy) event.preventDefault();
              else onNavigate(canMonitor(user) ? 'dashboard' : 'equipamentos');
            }}
            aria-label='S.E.N.T.R.A. — Início'
          >
            <Brand />
          </a>
          <button
            className='sidebar-mobile-close icon-button'
            aria-label='Fechar menu'
            onClick={onClose}
          >
            <DashboardIcon name='close' />
          </button>
        </div>
        <nav aria-label='Módulos do sistema'>
          {groups
            .filter((group) => group.label !== 'Sistema' || canAdministerUsers(user))
            .map((group) => (
              <section
                className='sidebar-group'
                key={group.label}
              >
                <h2>{group.label}</h2>
                {group.items
                  .filter((item) =>
                    item.target === 'usuarios'
                      ? canAdministerUsers(user)
                      : item.target === 'manutencoes'
                        ? canMaintain(user)
                        : item.target === 'equipamentos' || canMonitor(user),
                  )
                  .map((item) => (
                    <a
                      key={item.label}
                      className={`sidebar-link ${active === item.target ? 'active' : ''}`}
                      href={`#${item.target}`}
                      aria-current={active === item.target ? 'location' : undefined}
                      aria-disabled={busy}
                      title={item.label}
                      onClick={(event) => {
                        if (busy) event.preventDefault();
                        else onNavigate(item.target);
                      }}
                    >
                      <DashboardIcon name={item.icon} />
                      <span className='sidebar-label'>{item.label}</span>
                    </a>
                  ))}
              </section>
            ))}
          <button
            className='sidebar-link sidebar-logout'
            onClick={() => onLogout()}
            title='Sair'
          >
            <DashboardIcon name='logout' />
            <span className='sidebar-label'>Sair</span>
          </button>
        </nav>
        <div className='sidebar-bottom'>
          <div
            className='sidebar-user'
            title={`${user.name} — ${userRoleLabel(user.role)}`}
          >
            <span className='user-avatar'>{userInitials(user.name)}</span>
            <div>
              <strong>{user.name}</strong>
              <span>{userRoleLabel(user.role)}</span>
            </div>
          </div>
          <button
            className='sidebar-collapse'
            onClick={onCollapse}
            aria-expanded={!collapsed}
            aria-label={collapsed ? 'Expandir menu' : 'Recolher menu'}
          >
            <DashboardIcon name='collapse' />
            <span>{collapsed ? 'Expandir menu' : 'Recolher menu'}</span>
          </button>
        </div>
      </aside>
    </>
  );
}
