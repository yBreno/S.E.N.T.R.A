import { http, invalidResponse } from './http.js';

export const recoveryMessage = 'Se a conta estiver apta, você receberá um link para redefinir a senha. Aguarde alguns minutos e verifique também o spam.';
export function parseRecoveryRoute(hash = '') {
  if (hash === '#esqueci-minha-senha') return { kind: 'request', token: '' };
  if (hash.split('?')[0] !== '#redefinir-senha') return null;
  const params = new URLSearchParams(hash.slice(hash.indexOf('?') + 1));
  const token = params.getAll('token').length === 1 ? params.get('token') : '';
  return { kind: 'reset', token: /^[A-Za-z0-9_-]{43}$/.test(token) ? token : '' };
}
export function buildResetPassword(token, password, confirmation) {
  const errors = {};
  if (!/^[A-Za-z0-9_-]{43}$/.test(token ?? '')) errors.token = 'Link inválido. Abra novamente o e-mail ou solicite outro link.';
  if (typeof password !== 'string' || password.length < 8 || new TextEncoder().encode(password).length > 72 ||
      !/[A-Z]/.test(password) || !/[a-z]/.test(password) || !/[0-9]/.test(password) || !/[^a-zA-Z0-9]/.test(password))
    errors.password = 'Use ao menos oito caracteres, maiúscula, minúscula, número e símbolo, com até 72 bytes UTF-8.';
  if (password !== confirmation) errors.confirmation = 'As senhas devem ser iguais.';
  return { errors, payload: Object.keys(errors).length ? null : { token, newPassword: password, confirmPassword: confirmation } };
}
export function recoveryError(error) {
  if (error?.status === 429) return 'Muitas tentativas. Aguarde 15 minutos antes de tentar novamente.';
  if (error?.status === 400) return 'Confira a senha e o link. Ele pode ter expirado ou sido substituído; solicite outro se necessário.';
  if (error?.status === 405) return 'A recuperação está desabilitada no ambiente DEMO.';
  return 'Não foi possível confirmar a operação. Tente novamente mais tarde, sem compartilhar o link.';
}
export function createPasswordRecoveryApi(client = http) {
  return {
    async availability(signal) {
      const data = await client.getJson('/api/auth/password-recovery', { signal });
      if (typeof data?.enabled !== 'boolean') throw invalidResponse();
      return data.enabled;
    },
    async request(email, signal) {
      await client.postJson('/api/auth/password-recovery/request', { email: email.trim() }, { signal });
      return recoveryMessage;
    },
    async confirm(payload, signal) {
      await client.postJson('/api/auth/password-recovery/confirm', payload, { signal });
    }
  };
}
