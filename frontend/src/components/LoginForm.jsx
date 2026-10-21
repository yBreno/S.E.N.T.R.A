// Bibliotecas externas
import { useState } from 'react';

export default function LoginForm({
  onLogin,
  isSubmitting,
  error,
  configurationError
}) {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');

  function submit(event) {
    event.preventDefault();
    if (isSubmitting || configurationError) return;
    const credentials = {
      email,
      password
    };
    setPassword('');
    void onLogin(credentials);
  }

  return (
    <section className="panel login-panel" aria-labelledby="login-title">
      <div className="login-heading-mark" aria-hidden="true">
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.6"
          strokeLinecap="round" strokeLinejoin="round" focusable="false">
          <path d="M7 10V7a5 5 0 0 1 10 0v3 M5 10h14v11H5z M12 14v3" />
        </svg>
      </div>
      <h2 id="login-title">Acessar a central</h2>
      <p className="login-introduction">Entre com sua conta de monitoramento para acompanhar a operação.</p>
      <form onSubmit={submit} aria-busy={isSubmitting}>
        <div className="login-field">
          <label htmlFor="email">E-mail</label>
          <input
            id="email"
            name="email"
            type="email"
            autoComplete="username"
            required
            value={email}
            disabled={isSubmitting}
            onChange={event => setEmail(event.target.value)}
          />
        </div>
        <div className="login-field">
          <label htmlFor="password">Senha</label>
          <input
            id="password"
            name="password"
            type="password"
            autoComplete="current-password"
            required
            value={password}
            disabled={isSubmitting}
            onChange={event => setPassword(event.target.value)}
          />
        </div>
        <button className="login-submit" type="submit" disabled={isSubmitting || Boolean(configurationError)}>
          {isSubmitting ? 'Entrando…' : 'Entrar'}
        </button>
      </form>
      {error && (
        <p className="message error" role="alert">
          {error}
        </p>
      )}
    </section>
  );
}
