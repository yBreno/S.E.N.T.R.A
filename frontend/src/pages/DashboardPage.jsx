import { useState } from 'react'
import { useEndpointVerification } from '../hooks/useEndpointVerification.js'
import DashboardIndicators from '../components/dashboard/DashboardIndicators.jsx'
import MapView from '../components/map/MapView.jsx'
import EndpointVerification from '../components/EndpointVerification.jsx'
import './DashboardPage.css'

export default function DashboardPage({ session, onLogout }) {
  const verification = useEndpointVerification(session.token, onLogout)
  const [verificationOpen, setVerificationOpen] = useState(false)
  const dashboardResult = verification.results.find((result) => result.id === 'dashboard')

  return (
    <div className="dashboard-layout">
      <aside className="dashboard-sidebar" aria-label="Navegação operacional">
        <div className="sidebar-brand"><span className="brand-symbol" aria-hidden="true">S</span><span>OPERAÇÕES</span></div>
        <nav aria-label="Seções da dashboard">
          <a className="sidebar-link active" href="#mapa-operacional" aria-current="page">
            <svg viewBox="0 0 24 24" aria-hidden="true"><path d="m3 5 6-2 6 2 6-2v16l-6 2-6-2-6 2Zm6-2v16m6-14v16" /></svg>
            Visão operacional
          </a>
          <button className="sidebar-link" onClick={() => setVerificationOpen((open) => !open)}
            aria-expanded={verificationOpen} aria-controls="verificacao-api">
            <svg viewBox="0 0 24 24" aria-hidden="true"><path d="m8 7-5 5 5 5m8-10 5 5-5 5M14 4l-4 16" /></svg>
            Verificação da API
          </button>
        </nav>
        <div className="sidebar-note">Centro de operações<br /><span>São Paulo / SP</span></div>
      </aside>

      <div className="dashboard-workspace">
        <header className="dashboard-header">
          <div><p className="eyebrow">MONITORAMENTO URBANO</p><h1>S.E.N.T.R.A.</h1></div>
          <div className="dashboard-account">
            <div><strong>{session.user.name}</strong><span>{session.user.role}</span></div>
            <button className="secondary" onClick={() => onLogout()}>Sair</button>
          </div>
        </header>

        <main className="dashboard-main">
          <div className="dashboard-heading">
            <div><h2>Visão operacional</h2><p>São Paulo · Indicadores gerais da cidade</p></div>
            <button onClick={verification.refresh} disabled={verification.loading}>
              {verification.loading ? 'Atualizando…' : 'Atualizar dados'}
            </button>
          </div>

          <DashboardIndicators loading={verification.loading} result={dashboardResult} error={verification.error} />

          <section className="map-panel" id="mapa-operacional" aria-labelledby="map-heading">
            <div className="map-panel-heading"><h2 id="map-heading">Mapa operacional</h2><span>Mapa base · São Paulo</span></div>
            <MapView verification={verification} token={session.token} onLogout={onLogout} />
          </section>

          <details className="api-verification" id="verificacao-api" open={verificationOpen}
            onToggle={(event) => setVerificationOpen(event.currentTarget.open)}>
            <summary>Verificação dos endpoints da API</summary>
            <EndpointVerification verification={verification} />
          </details>

          <footer className="dashboard-footer">
            <span>Atualização manual dos dados operacionais</span>
            {verification.updatedAt && <span>Última tentativa: <time dateTime={verification.updatedAt.toISOString()}>
              {verification.updatedAt.toLocaleString('pt-BR')}
            </time></span>}
          </footer>
        </main>
      </div>
    </div>
  )
}
