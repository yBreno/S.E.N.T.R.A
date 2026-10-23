import { equipmentTypes } from '../map/operationalFilters.js';

export { equipmentTypes };
export const maintenanceNeeds = { None: 'Sem necessidade', Immediate: 'Imediata', Scheduled: 'Agendada', Periodic: 'Periódica' };
export const states = 'AC AL AP AM BA CE DF ES GO MA MT MS MG PA PB PR PE PI RJ RN RS RO RR SC SP SE TO'.split(' ');
export const vehicleTypes = new Set(['Vehicle', 'Motorcycle', 'AutonomousVehicle']);
const field = (name, label, extra = {}) => ({ name, label, ...extra });
const vehicleFields = [field('licensePlate', 'Placa', { required: true, maxLength: 8 }), field('chassi', 'Chassi', { maxLength: 17 }), field('manufactureYear', 'Ano de fabricação', { type: 'number', min: 1900, max: new Date().getFullYear() + 1 })];
export const specificFields = {
  Camera: [field('hasNightVision', 'Visão noturna', { type: 'checkbox' }), field('resolution', 'Resolução', { maxLength: 20 }), field('isColor', 'Imagem colorida', { type: 'checkbox' })],
  Radar: [field('radarType', 'Finalidade do radar', { maxLength: 60, placeholder: 'Velocidade, semáforo, conversão proibida…' }), field('maxSpeedLimit', 'Velocidade máxima fiscalizada (km/h)', { type: 'number', min: 1 })],
  Sensor: [field('sensorType', 'Tipo de sensor ou controlador', { maxLength: 60, placeholder: 'Pesagem, nível, controlador de acesso…' }), field('measurementUnit', 'Unidade de medida', { maxLength: 30, placeholder: 'kg, cm, ciclos…' })],
  ElectricalScooter: [field('batteryCapacity', 'Capacidade da bateria', { maxLength: 30 }), field('maxSpeed', 'Velocidade máxima (km/h)', { type: 'number', min: 1 })],
  ParkingMeter: [field('spots', 'Quantidade de vagas', { type: 'number', min: 1 })],
  Vehicle: vehicleFields,
  Motorcycle: [...vehicleFields, field('engineCapacityCc', 'Cilindrada (cc)', { type: 'number', min: 1 })],
  AutonomousVehicle: [...vehicleFields, field('autonomyLevel', 'Nível de autonomia (0–5)', { type: 'number', min: 0, max: 5 })]
};
export const categorySuggestions = {
  Camera: ['Videomonitoramento'], Radar: ['Fiscalização de velocidade', 'Fiscalização semafórica', 'Conversão proibida'],
  Sensor: ['Balança de caminhão', 'Controlador IoT de cancela', 'Monitoramento ambiental'],
  Vehicle: ['Táxi', 'Mototáxi', 'Transporte escolar', 'Viatura da guarda municipal', 'Viatura da polícia militar'],
  Motorcycle: ['Moto de agente de trânsito', 'Mototáxi'], AutonomousVehicle: ['Fiscalização de estacionamento rotativo'],
  ElectricalScooter: ['Patinete público compartilhado'], ParkingMeter: ['Totem de estacionamento rotativo']
};
export const addressFields = [
  field('zipCode', 'CEP', { required: true, maxLength: 9, placeholder: '00000-000' }),
  field('street', 'Rua / via', { required: true, maxLength: 200 }), field('number', 'Número', { maxLength: 20 }),
  field('complement', 'Complemento', { maxLength: 100 }), field('neighborhood', 'Bairro', { required: true, maxLength: 120 }),
  field('city', 'Cidade', { required: true, maxLength: 120 }), field('state', 'UF', { required: true, type: 'state' }),
  field('country', 'País', { required: true, maxLength: 60 }), field('locationReference', 'Referência de localização', { maxLength: 200, placeholder: 'KM 203, oposto ao nº 181…' }),
  field('latitude', 'Latitude GPS', { required: true, type: 'number', min: -90, max: 90, step: 'any' }),
  field('longitude', 'Longitude GPS', { required: true, type: 'number', min: -180, max: 180, step: 'any' })
];
const normalized = value => String(value ?? '').normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLowerCase().trim();
export function filterEquipments(items, filters) {
  const query = normalized(filters.search);
  return items.filter(item => (!query || normalized([item.serialNumber, item.brand, item.model, item.category, item.neighborhood].join(' ')).includes(query)) &&
    (!filters.type || item.type === filters.type) && (!filters.status || item.status === filters.status) &&
    (!filters.neighborhood || normalized(item.neighborhood).includes(normalized(filters.neighborhood))) &&
    (!filters.maintenanceNeed || item.maintenanceNeed === filters.maintenanceNeed));
}
export function localDateTime(value) {
  if (!value) return '';
  const date = new Date(value);
  if (!Number.isFinite(date.getTime())) return '';
  return new Date(date.getTime() - date.getTimezoneOffset() * 60_000).toISOString().slice(0, 16);
}
export function createEquipmentForm(equipment, address) {
  return {
    type: equipment?.type ?? 'Camera', serialNumber: equipment?.serialNumber ?? '', brand: equipment?.brand ?? '', model: equipment?.model ?? '', category: equipment?.category ?? '',
    maintenanceNeed: equipment?.maintenanceNeed ?? 'None', nextMaintenanceDate: localDateTime(equipment?.nextMaintenanceDate),
    addressMode: equipment ? 'existing' : 'new', addressId: equipment?.addressId ?? '',
    address: Object.fromEntries(addressFields.map(({ name }) => [name, address?.[name] ?? (name === 'country' ? 'Brasil' : '')])),
    ...Object.fromEntries((specificFields[equipment?.type ?? 'Camera'] ?? []).map(({ name, type }) => [name, equipment?.attributes?.[name] ?? (type === 'checkbox' ? false : '')])),
    currentAgentId: equipment?.attributes?.currentAgentId ?? null
  };
}
export function maintenanceLocked(equipment) {
  return equipment?.status === 'Malfunctioning' || equipment?.status === 'UnderMaintenance';
}
export function buildEquipmentPayload(form, original) {
  const errors = {};
  const text = (name, label, max, required = false, source = form, key = name) => {
    const value = String(source[name] ?? '').trim();
    if (required && !value) errors[key] = `Informe ${label.toLowerCase()}.`;
    if (value.length > max) errors[key] = `Use até ${max} caracteres.`;
    return value || null;
  };
  const number = (spec, source = form, key = spec.name) => {
    const raw = String(source[spec.name] ?? '').trim();
    if (!raw && !spec.required) return null;
    const value = raw ? Number(raw) : NaN;
    if (!Number.isFinite(value) || (spec.step !== 'any' && !Number.isInteger(value)) || value < spec.min || value > (spec.max ?? Infinity)) errors[key] = `${spec.label}: informe um valor válido${spec.max != null ? ` entre ${spec.min} e ${spec.max}` : ''}.`;
    return value;
  };
  const payload = {
    serialNumber: text('serialNumber', 'número de série', 80, true), brand: text('brand', 'marca', 80, true), model: text('model', 'modelo', 80, true), category: text('category', 'categoria de uso', 80)
  };
  if (!Object.hasOwn(equipmentTypes, form.type) || (original && form.type !== original.type)) errors.type = 'Selecione um tipo válido. O tipo não pode ser alterado na edição.';
  if (!original) payload.type = form.type;
  for (const spec of specificFields[form.type] ?? []) {
    payload[spec.name] = spec.type === 'checkbox' ? Boolean(form[spec.name]) : spec.type === 'number' ? number(spec) : text(spec.name, spec.label, spec.maxLength ?? 80, spec.required);
  }
  if (vehicleTypes.has(form.type)) {
    payload.currentAgentId = original?.attributes?.currentAgentId ?? null;
    payload.licensePlate = (payload.licensePlate ?? '').replace(/[-\s]/g, '').toUpperCase();
    if (!/^[A-Z]{3}[0-9][A-Z0-9][0-9]{2}$/.test(payload.licensePlate)) errors.licensePlate = 'Informe uma placa válida (ABC1234 ou ABC1D23).';
    if (payload.chassi && payload.chassi.length !== 17) errors.chassi = 'O chassi deve ter 17 caracteres.';
  }
  // Campos intocados nao regravam nem arredondam a previsao controlada por uma ordem.
  const originalPlanning = original ? createEquipmentForm(original) : null;
  if (!maintenanceLocked(original) && (!originalPlanning || form.maintenanceNeed !== originalPlanning.maintenanceNeed || form.nextMaintenanceDate !== originalPlanning.nextMaintenanceDate)) {
    if (!Object.hasOwn(maintenanceNeeds, form.maintenanceNeed)) errors.maintenanceNeed = 'Selecione uma necessidade válida.';
    payload.maintenanceNeed = form.maintenanceNeed;
    payload.nextMaintenanceDate = null;
    if (['Scheduled', 'Periodic'].includes(form.maintenanceNeed)) {
      const date = new Date(form.nextMaintenanceDate);
      if (!form.nextMaintenanceDate || !Number.isFinite(date.getTime())) errors.nextMaintenanceDate = 'Informe a próxima data prevista.';
      else payload.nextMaintenanceDate = date.toISOString();
    }
  }
  if (form.addressMode === 'existing') {
    if (!form.addressId) errors.addressId = 'Selecione uma localização existente.';
    payload.addressId = form.addressId;
  } else if (form.addressMode === 'new') {
    payload.address = {};
    for (const spec of addressFields) {
      payload.address[spec.name] = spec.type === 'number' ? number(spec, form.address, `address.${spec.name}`) : text(spec.name, spec.label, spec.maxLength ?? 60, spec.required, form.address, `address.${spec.name}`);
    }
    if (!states.includes(form.address.state)) errors['address.state'] = 'Selecione uma UF válida.';
    payload.address.zipCode = (payload.address.zipCode ?? '').replace(/\D/g, '');
    if (payload.address.zipCode.length !== 8) errors['address.zipCode'] = 'Informe um CEP com oito dígitos.';
  } else errors.addressId = 'Escolha como informar a localização.';
  return { payload: Object.keys(errors).length ? null : payload, errors };
}

const statusTransitions = { NotInstalled: ['Operational', 'Deactivated', 'Stolen'], Operational: ['Malfunctioning', 'Deactivated', 'Stolen'], Malfunctioning: ['Deactivated', 'Stolen'], UnderMaintenance: [], Deactivated: ['Operational'], Stolen: [] };
export function availableStatusTransitions(status, isManager) {
  return (statusTransitions[status] ?? []).filter(next => isManager || !(status === 'Deactivated' && next === 'Operational'));
}
