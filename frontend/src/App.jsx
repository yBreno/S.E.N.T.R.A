// Componentes
import AuthLayout from './components/auth/AuthLayout.jsx';
import LoginForm from './components/LoginForm.jsx';
import DashboardPage from './pages/DashboardPage.jsx';
import PasswordRecoveryForm from './components/auth/PasswordRecoveryForm.jsx';
import { parseRecoveryRoute } from './services/passwordRecovery.js';
import { useEffect, useState } from 'react';

// Hooks personalizados
import { useAuth } from './hooks/useAuth.js';

// Serviços e utilitários
import { apiConfig } from './config/api.js';

// Estilos
import './App.css';
import './DemoBanner.css';

function App() {
  const [recoveryRoute, setRecoveryRoute] = useState(() => parseRecoveryRoute(window.location.hash));
  const auth = useAuth();
  useEffect(() => {
    function scrubSecret() {
      if (window.location.hash.startsWith('#redefinir-senha?')) window.history.replaceState(null, '', `${window.location.pathname}${window.location.search}#redefinir-senha`);
    }
    function navigate() { const route = parseRecoveryRoute(window.location.hash); scrubSecret(); setRecoveryRoute(route); }
    scrubSecret(); window.addEventListener('hashchange', navigate);
    return () => window.removeEventListener('hashchange', navigate);
  }, []);
  const Layout = !recoveryRoute && auth.session && auth.canReadEquipments ? 'div' : AuthLayout;

  return (
    <div className={apiConfig.demoMode ? 'demo-app' : undefined}>
    {apiConfig.demoMode && <aside className="demo-banner" aria-label="Ambiente de demonstração"><strong>DEMO · DADOS SINTÉTICOS</strong><span>Ambiente local · não representa operação real</span></aside>}
    <Layout
      className={!recoveryRoute && auth.session && auth.canReadEquipments ? 'app-dashboard' : 'app-shell'}
    >
      {apiConfig.error && (
        <p className="message error" role="alert">
          {apiConfig.error}
        </p>
      )}
      {recoveryRoute ? <PasswordRecoveryForm key={recoveryRoute.kind} route={recoveryRoute} configurationError={apiConfig.error} onBack={() => { auth.logout(); setRecoveryRoute(null); window.history.replaceState(null, '', `${window.location.pathname}${window.location.search}`); }} /> : auth.isRestoring
        ? (
          <p role="status">Validando sessão anterior…</p>
        )
        : !auth.session
          ? (
            <LoginForm
              onLogin={auth.login}
              isSubmitting={auth.isSubmitting}
              error={auth.error}
              configurationError={apiConfig.error}
            />
          )
          : !auth.canReadEquipments
            ? (
              <section className="panel">
                <h2>Acesso ao monitoramento indisponível</h2>
                <p role="alert">Seu papel ({auth.session.user.role}) não permite acessar o inventário. É necessário um perfil da equipe interna.</p>
                <button onClick={() => auth.logout()}>Voltar ao login</button>
              </section>
            )
            : (
              <DashboardPage key={auth.session.token} session={auth.session} onLogout={auth.logout} />
            )}
    </Layout>
    </div>
  );
}

export default App;
