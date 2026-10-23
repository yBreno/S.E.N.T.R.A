import { agentRoleLabel, agentStates } from '../map/markerData.js';

export const agentRoles = Object.fromEntries(['TrafficAgent', 'MunicipalGuard', 'PoliceOfficer'].map(role => [role, agentRoleLabel(role)]));
export const availabilityOptions = Object.fromEntries(Object.entries(agentStates).map(([value, state]) => [value, state.label]));
const options = values => Object.entries(values).map(([value, label]) => ({ value, label }));
export const identityFields = [
  { name: 'name', label: 'Nome do profissional', required: true, maxLength: 150 },
  { name: 'badgeNumber', label: 'Matrícula', required: true, maxLength: 30 },
  { name: 'role', label: 'Categoria de atuação', required: true, options: options(agentRoles) }
];
export const operationalFields = [
  { name: 'neighborhood', label: 'Bairro / área atual de atuação', required: true, maxLength: 120 },
  { name: 'latitude', label: 'Latitude GPS', required: true, type: 'number', min: -90, max: 90, step: 'any' },
  { name: 'longitude', label: 'Longitude GPS', required: true, type: 'number', min: -180, max: 180, step: 'any' },
  { name: 'availability', label: 'Disponibilidade operacional', required: true, options: options(availabilityOptions) }
];
export function validCpf(value) {
  const cpf = String(value ?? '').replace(/\D/g, '');
  if (cpf.length !== 11 || new Set(cpf).size === 1) return false;
  const digit = length => {
    const sum = [...cpf.slice(0, length)].reduce((total, part, index) => total + Number(part) * (length + 1 - index), 0);
    const remainder = sum * 10 % 11;
    return remainder === 10 ? 0 : remainder;
  };
  return Number(cpf[9]) === digit(9) && Number(cpf[10]) === digit(10);
}
export function createAgentForm(agent) {
  return { name: agent?.name ?? '', badgeNumber: agent?.badgeNumber ?? '', role: agent?.role ?? 'TrafficAgent', availability: agent?.availability ?? 'Offline', neighborhood: agent?.neighborhood ?? '', latitude: agent?.latitude ?? '', longitude: agent?.longitude ?? '', cpf: '' };
}
export function buildAgentLocation(form) {
  const errors = {};
  const payload = { neighborhood: String(form.neighborhood ?? '').trim(), availability: form.availability };
  if (!payload.neighborhood || payload.neighborhood.length > 120) errors.neighborhood = 'Informe o bairro ou área atual de atuação com até 120 caracteres.';
  if (!Object.hasOwn(availabilityOptions, form.availability)) errors.availability = 'Selecione uma disponibilidade válida.';
  for (const spec of operationalFields.filter(item => item.type === 'number')) {
    const raw = String(form[spec.name] ?? '').trim();
    const number = raw ? Number(raw) : NaN;
    if (!Number.isFinite(number) || number < spec.min || number > spec.max) errors[spec.name] = `Informe ${spec.label.toLowerCase()} entre ${spec.min} e ${spec.max}.`;
    payload[spec.name] = number;
  }
  return { payload: Object.keys(errors).length ? null : payload, errors };
}
export function buildAgentPayload(form, original) {
  const location = buildAgentLocation(form);
  const errors = { ...location.errors };
  const name = String(form.name ?? '').trim();
  const badgeNumber = String(form.badgeNumber ?? '').trim();
  if (!name || name.length > 150) errors.name = 'Informe o nome com até 150 caracteres.';
  if (!badgeNumber || badgeNumber.length > 30) errors.badgeNumber = 'Informe a matrícula com até 30 caracteres.';
  if (!Object.hasOwn(agentRoles, form.role)) errors.role = 'Selecione uma categoria de atuação válida.';
  if (!original && !validCpf(form.cpf)) errors.cpf = 'Informe um CPF válido com onze dígitos.';
  const payload = { ...location.payload, name, badgeNumber, role: form.role, userId: original?.userId ?? null };
  if (!original) payload.cpf = String(form.cpf ?? '').replace(/\D/g, '');
  return { payload: Object.keys(errors).length ? null : payload, errors };
}
const normalized = value => String(value ?? '').normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLowerCase().trim();
export function filterAgents(items, filters) {
  const query = normalized(filters.search);
  return items.filter(agent => (!query || normalized([agent.name, agent.badgeNumber, agentRoleLabel(agent.role), agent.neighborhood].join(' ')).includes(query)) &&
    (!filters.role || agent.role === filters.role) && (!filters.availability || agent.availability === filters.availability) &&
    (!filters.neighborhood || normalized(agent.neighborhood).includes(normalized(filters.neighborhood))));
}
