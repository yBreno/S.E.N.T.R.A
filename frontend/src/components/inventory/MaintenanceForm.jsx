import { useState } from 'react';
import InventoryField from './InventoryField.jsx';
import { urgencies } from '../../services/maintenances.js';

export default function MaintenanceForm({ mode, equipments, equipmentId, saving, onSave, onCancel }) {
  const closing = mode === 'close';
  const schedule = mode === 'schedule';
  const [values, setValues] = useState({ equipmentId: equipmentId || '', description: '', urgency: 'Medium', periodicityDays: '', scheduledDate: '', endDate: '', cost: '' });
  const [errors, setErrors] = useState({});
  const candidates = equipments.filter(item => item.status === (schedule ? 'Operational' : 'Malfunctioning'));
  const specs = closing ? [
    { name: 'endDate', label: 'Conclusão (opcional; agora se vazia)', type: 'datetime-local' },
    { name: 'cost', label: 'Custo em R$ (opcional)', type: 'number', min: 0, step: '0.01' }
  ] : [
    { name: 'equipmentId', label: 'Equipamento', required: true, options: candidates.map(item => ({ value: item.id, label: `${item.serialNumber} · ${item.brand} ${item.model}` })) },
    { name: 'description', label: 'Descrição do serviço', required: true, maxLength: 1000 },
    { name: 'urgency', label: 'Urgência', required: true, options: Object.entries(urgencies).map(([value, label]) => ({ value, label })) },
    ...(schedule ? [{ name: 'scheduledDate', label: 'Data e hora previstas', required: true, type: 'datetime-local' }] : []),
    { name: 'periodicityDays', label: 'Periodicidade em dias (opcional)', type: 'number', min: 1, max: 36500 }
  ];
  async function submit(event) {
    event.preventDefault();
    if (saving) return;
    const invalid = {};
    for (const spec of specs) if (spec.required && !values[spec.name].trim()) invalid[spec.name] = 'Preencha este campo.';
    if (schedule && (!Number.isFinite(Date.parse(values.scheduledDate)) || Date.parse(values.scheduledDate) < Date.now())) invalid.scheduledDate = 'Informe uma data e hora futuras.';
    if (closing && values.endDate && (!Number.isFinite(Date.parse(values.endDate)) || Date.parse(values.endDate) > Date.now())) invalid.endDate = 'A conclusão não pode ser futura.';
    setErrors(invalid);
    if (Object.keys(invalid).length) return;
    const payload = closing ? { endDate: values.endDate ? new Date(values.endDate).toISOString() : null, cost: values.cost === '' ? null : Number(values.cost) } : {
      equipmentId: values.equipmentId, description: values.description.trim(), urgency: values.urgency,
      periodicityDays: values.periodicityDays ? Number(values.periodicityDays) : null,
      ...(schedule ? { kind: 'Preventive', scheduledDate: new Date(values.scheduledDate).toISOString() } : {})
    };
    try { await onSave(payload); }
    catch (error) { setErrors(Object.fromEntries(Object.keys(error?.fieldErrors || {}).map(key => [key[0].toLowerCase() + key.slice(1), 'Confira o valor informado.']))); }
  }
  return <section className="inventory-panel"><h2>{closing ? 'Concluir manutenção' : schedule ? 'Agendar preventiva' : 'Abrir corretiva'}</h2>
    <p className="inventory-muted">{closing ? 'A conclusão restaura a operação e resolve os alertas vinculados ao equipamento. A periodicidade informa a próxima previsão; não cria outra ordem automaticamente.' : schedule ? 'O equipamento continua operacional até o início efetivo da intervenção. Informe horários no seu fuso local.' : 'A intervenção inicia agora e exige equipamento com falha real. A abertura muda sua situação para Em manutenção.'}</p>
    {!closing && candidates.length === 0 && <p role="status">Nenhum equipamento elegível. Confira a situação operacional no inventário.</p>}
    <form onSubmit={submit}><fieldset disabled={saving}><legend>Dados da manutenção</legend><div className="inventory-form-grid">{specs.map(spec => <InventoryField key={spec.name} idPrefix="maintenance-" spec={spec} value={values[spec.name]} error={errors[spec.name]} onChange={value => setValues(current => ({ ...current, [spec.name]: value }))} />)}</div></fieldset>
      <div className="inventory-actions"><button type="submit" disabled={saving || (!closing && !candidates.length)}>{saving ? 'Salvando…' : closing ? 'Confirmar conclusão' : schedule ? 'Confirmar agendamento' : 'Confirmar abertura'}</button><button className="secondary" type="button" disabled={saving} onClick={onCancel}>Cancelar</button></div>
    </form></section>;
}
