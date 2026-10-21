import { useState } from 'react'

export default function LoginForm({ onLogin, isSubmitting, error, configurationError }) {
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')

  function submit(event) {
    event.preventDefault()
    if (isSubmitting || configurationError) return
    const credentials = { email, password }
    setPassword('')
    void onLogin(credentials)
  }

  return (
    <section className="panel login-panel">
      <h2>Entrar</h2>
      <p>Utilize uma conta existente com permissão de monitoramento.</p>
      <form onSubmit={submit} aria-busy={isSubmitting}>
        <label htmlFor="email">E-mail</label>
        <input id="email" name="email" type="email" autoComplete="username" required value={email}
          disabled={isSubmitting} onChange={(event) => setEmail(event.target.value)} />
        <label htmlFor="password">Senha</label>
        <input id="password" name="password" type="password" autoComplete="current-password" required value={password}
          disabled={isSubmitting} onChange={(event) => setPassword(event.target.value)} />
        <button type="submit" disabled={isSubmitting || Boolean(configurationError)}>
          {isSubmitting ? 'Entrando…' : 'Entrar'}
        </button>
      </form>
      {error && <p className="message error" role="alert">{error}</p>}
    </section>
  )
}
