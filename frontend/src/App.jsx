// Componentes
import AuthLayout from './components/auth/AuthLayout.jsx';
import LoginForm from './components/LoginForm.jsx';
import DashboardPage from './pages/DashboardPage.jsx';

// Hooks personalizados
import { useAuth } from './hooks/useAuth.js';

// Serviços e utilitários
import { apiConfig } from './config/api.js';

// Estilos
import './App.css';

function App() {
  const auth = useAuth();
  const Layout = auth.session && auth.canMonitor ? 'div' : AuthLayout;

  return (
    <Layout
      className={auth.session && auth.canMonitor ? 'app-dashboard' : 'app-shell'}
    >
      {apiConfig.error && (
        <p className="message error" role="alert">
          {apiConfig.error}
        </p>
      )}
      {auth.isRestoring
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
          : !auth.canMonitor
            ? (
              <section className="panel">
                <h2>Acesso ao monitoramento indisponível</h2>
                <p role="alert">Seu papel ({auth.session.user.role}) não permite acessar esta tela. É necessário Admin, OperationManager ou CentralOperator.</p>
                <button onClick={() => auth.logout()}>Voltar ao login</button>
              </section>
            )
            : (
              <DashboardPage session={auth.session} onLogout={auth.logout} />
            )}
    </Layout>
  );
}

export default App;
