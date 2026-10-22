// Componentes
import Brand from '../Brand.jsx';

// Estilos
import './AuthLayout.css';

export default function AuthLayout({ children, className = '' }) {
  return (
    <div className={`${className} auth-shell`}>
      <header className="auth-intro">
        <Brand />
        <div className="auth-heading">
          <h1>Visão da cidade.<br />Controle da operação.</h1>
          <p>Equipamentos, equipes e ocorrências no mesmo mapa. Um ponto de partida para cada decisão.</p>
        </div>
        <ul className="auth-capabilities" aria-label="Recursos da central">
          <li><strong>Localize</strong><span>Equipamentos e agentes</span></li>
          <li><strong>Acompanhe</strong><span>A situação operacional</span></li>
          <li><strong>Analise</strong><span>Ocorrências e risco regional</span></li>
        </ul>
      </header>
      <main className="auth-access" id="acesso">
        <div className="auth-access-content">{children}</div>
        <footer className="auth-session-note">Sua sessão fica nesta aba. Ao sair ou expirar, entre novamente.</footer>
      </main>
    </div>
  );
}
