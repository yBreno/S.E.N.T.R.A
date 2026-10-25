import { useEffect, useRef, useState } from 'react';
import {
  buildResetPassword,
  createPasswordRecoveryApi,
  recoveryError,
} from '../../services/passwordRecovery.js';

const api = createPasswordRecoveryApi();
export default function PasswordRecoveryForm({ route, onBack, configurationError }) {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [confirmation, setConfirmation] = useState('');
  const [available, setAvailable] = useState(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [message, setMessage] = useState('');
  const [complete, setComplete] = useState(false);
  const pending = useRef(null);
  const saving = useRef(false);
  useEffect(() => {
    const controller = new AbortController();
    api
      .availability(controller.signal)
      .then(setAvailable)
      .catch(() => {
        if (!controller.signal.aborted) setAvailable(false);
      });
    return () => {
      controller.abort();
      pending.current?.abort();
    };
  }, []);
  async function submit(event) {
    event.preventDefault();
    if (saving.current || !available || configurationError) return;
    setError('');
    setMessage('');
    const data =
      route.kind === 'reset' ? buildResetPassword(route.token, password, confirmation) : null;
    if (data && !data.payload) {
      setError(Object.values(data.errors).join(' '));
      return;
    }
    const controller = new AbortController();
    pending.current = controller;
    const timeout = setTimeout(() => controller.abort(), 15000);
    saving.current = true;
    setBusy(true);
    try {
      if (route.kind === 'request') setMessage(await api.request(email, controller.signal));
      else {
        await api.confirm(data.payload, controller.signal);
        setComplete(true);
        setMessage(
          'Senha redefinida. As sessões anteriores foram encerradas. Entre com a nova senha.',
        );
      }
    } catch (failure) {
      setError(recoveryError(failure));
    } finally {
      clearTimeout(timeout);
      setPassword('');
      setConfirmation('');
      pending.current = null;
      saving.current = false;
      setBusy(false);
    }
  }
  const invalidLink = route.kind === 'reset' && !route.token;
  return (
    <section
      className='panel login-panel'
      aria-labelledby='recovery-title'
    >
      <h2 id='recovery-title'>
        {route.kind === 'request' ? 'Esqueci minha senha' : 'Redefinir senha'}
      </h2>
      <p className='login-introduction'>
        {route.kind === 'request'
          ? 'Informe o e-mail cadastrado. Nunca solicitamos que compartilhe sua senha ou o link recebido.'
          : 'Escolha uma nova senha. O link expira em 15 minutos e pode ser utilizado uma única vez.'}
      </p>
      {available === null && <p role='status'>Verificando disponibilidade…</p>}
      {available === false && (
        <p
          className='message error'
          role='alert'
        >
          Recuperação por e-mail indisponível neste ambiente. Tente novamente mais tarde.
        </p>
      )}
      {invalidLink && (
        <p
          className='message error'
          role='alert'
        >
          Link inválido. Abra novamente o e-mail ou solicite outro link.
        </p>
      )}
      {!complete && !invalidLink && (
        <form
          onSubmit={submit}
          aria-busy={busy}
        >
          {route.kind === 'request' ? (
            <div className='login-field'>
              <label htmlFor='recovery-email'>E-mail cadastrado</label>
              <input
                id='recovery-email'
                type='email'
                required
                maxLength={200}
                autoComplete='email'
                disabled={busy}
                value={email}
                onChange={(event) => setEmail(event.target.value)}
              />
            </div>
          ) : (
            <>
              <div className='login-field'>
                <label htmlFor='recovery-password'>Nova senha</label>
                <input
                  id='recovery-password'
                  type='password'
                  autoComplete='new-password'
                  required
                  disabled={busy}
                  value={password}
                  onChange={(event) => setPassword(event.target.value)}
                  aria-describedby='recovery-rules'
                />
              </div>
              <p
                className='inventory-muted'
                id='recovery-rules'
              >
                Ao menos oito caracteres, maiúscula, minúscula, número e símbolo. Máximo de 72 bytes
                UTF-8.
              </p>
              <div className='login-field'>
                <label htmlFor='recovery-confirmation'>Confirmar nova senha</label>
                <input
                  id='recovery-confirmation'
                  type='password'
                  autoComplete='new-password'
                  required
                  disabled={busy}
                  value={confirmation}
                  onChange={(event) => setConfirmation(event.target.value)}
                />
              </div>
            </>
          )}
          <button
            className='login-submit'
            disabled={busy || !available || Boolean(configurationError)}
          >
            {busy
              ? 'Enviando…'
              : route.kind === 'request'
                ? 'Solicitar link de redefinição'
                : 'Confirmar nova senha'}
          </button>
        </form>
      )}
      {error && (
        <p
          className='message error'
          role='alert'
        >
          {error}
        </p>
      )}
      {message && (
        <p
          className='message success'
          role='status'
        >
          {message}
        </p>
      )}
      <div className='recovery-navigation'>
        {route.kind === 'reset' && !complete && (
          <a href='#esqueci-minha-senha'>Solicitar outro link</a>
        )}
        <button
          type='button'
          className='secondary'
          disabled={busy}
          onClick={onBack}
        >
          Voltar ao login
        </button>
      </div>
    </section>
  );
}
