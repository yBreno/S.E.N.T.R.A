import { userRoles } from '../../services/authorization.js';
import { validCpf } from '../agents/agentModel.js';

export function buildUserForm(form, original) {
  const errors = {};
  const payload = { name: String(form.name ?? '').trim(), email: String(form.email ?? '').trim() };
  if (!payload.name || payload.name.length > 150) errors.name = 'Informe um nome com até 150 caracteres.';
  if (!/^[^\s@]+@[^\s@]+$/.test(payload.email) || payload.email.length > 200) errors.email = 'Informe um e-mail válido com até 200 caracteres.';
  if (original) payload.revision = original.revision;
  else {
    payload.role = form.role; payload.cpf = String(form.cpf ?? '').replace(/\D/g, ''); payload.password = form.password ?? '';
    if (!Object.hasOwn(userRoles, form.role)) errors.role = 'Selecione um cargo de acesso.';
    if (!validCpf(payload.cpf)) errors.cpf = 'Informe um CPF válido.';
    if (payload.password.length < 8 || new TextEncoder().encode(payload.password).length > 72 ||
        !/[A-Z]/.test(payload.password) || !/[a-z]/.test(payload.password) || !/[0-9]/.test(payload.password) || !/[^a-zA-Z0-9]/.test(payload.password))
      errors.password = 'Use ao menos oito caracteres, maiúscula, minúscula, número e símbolo, com até 72 bytes UTF-8.';
  }
  return { payload: Object.keys(errors).length ? null : payload, errors };
}
