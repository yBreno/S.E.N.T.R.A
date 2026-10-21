import { apiConfig } from './config/api.js'
import { useAuth } from './hooks/useAuth.js'
import LoginForm from './components/LoginForm.jsx'
import DashboardPage from './pages/DashboardPage.jsx'
import './App.css'

function App() {
  const auth = useAuth()
  return (
    <div className={auth.session && auth.canMonitor ? 'app-dashboard' : 'app-shell'}>
      {(!auth.session || !auth.canMonitor) && <header>
        <h1>S.E.N.T.R.A.</h1>
        <p>Centro de operações</p>
      </header>}
      {apiConfig.error && <p className="message error" role="alert">{apiConfig.error}</p>}
      {!auth.session ? (
        <LoginForm onLogin={auth.login} isSubmitting={auth.isSubmitting}
          error={auth.error} configurationError={apiConfig.error} />
      ) : !auth.canMonitor ? (
        <section className="panel">
          <h2>Acesso ao monitoramento indisponível</h2>
          <p role="alert">Seu papel ({auth.session.user.role}) não permite acessar esta tela. É necessário Admin, OperationManager ou CentralOperator.</p>
          <button onClick={() => auth.logout()}>Voltar ao login</button>
        </section>
      ) : (
        <DashboardPage session={auth.session} onLogout={auth.logout} />
      )}
      {(!auth.session || !auth.canMonitor) && <footer>Sessão mantida somente em memória. Atualizar a página exige novo login.</footer>}
    </div>
  )
}

export default App
