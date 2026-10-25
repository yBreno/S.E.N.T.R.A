import { useState } from 'react';
import {
  addressFields,
  buildEquipmentPayload,
  categorySuggestions,
  createEquipmentForm,
  equipmentTypes,
  maintenanceLocked,
  maintenanceNeeds,
  specificFields,
  states,
} from './equipmentModel.js';
import { getEquipmentState } from '../map/markerData.js';
import InventoryField from '../inventory/InventoryField.jsx';

function Field({ spec, value, onChange, error, prefix = '' }) {
  return (
    <InventoryField
      spec={spec.type === 'state' ? { ...spec, options: states } : spec}
      value={value}
      onChange={onChange}
      error={error}
      prefix={prefix}
    />
  );
}

export default function EquipmentForm({
  equipment,
  address,
  addresses,
  addressError,
  saving,
  onSave,
  onCancel,
}) {
  const [form, setForm] = useState(() => createEquipmentForm(equipment, address));
  const [errors, setErrors] = useState({});
  const locked = maintenanceLocked(equipment);
  const update = (name, value) => setForm((current) => ({ ...current, [name]: value }));

  async function submit(event) {
    event.preventDefault();
    if (saving) return;
    const parsed = buildEquipmentPayload(form, equipment);
    setErrors(parsed.errors);
    if (!parsed.payload) {
      queueMicrotask(() => document.getElementById('equipment-form-errors')?.focus());
      return;
    }
    try {
      await onSave(parsed.payload, equipment?.id);
    } catch (error) {
      const fields = Object.fromEntries(
        Object.keys(error.fieldErrors ?? {}).map((key) => [
          key
            .split('.')
            .map((part) => part[0].toLowerCase() + part.slice(1))
            .join('.'),
          'Confira o formato, o tamanho e o valor deste campo.',
        ]),
      );
      setErrors(fields);
      queueMicrotask(() => document.getElementById('equipment-form-errors')?.focus());
    }
  }

  const currentAddress =
    addresses.find((item) => item.id === form.addressId) ??
    (address?.id === form.addressId ? address : null);
  const addressOptions =
    address && !addresses.some((item) => item.id === address.id)
      ? [address, ...addresses]
      : addresses;
  return (
    <section
      className='inventory-panel'
      aria-labelledby='equipment-form-title'
    >
      <div className='inventory-section-heading'>
        <div>
          <p className='inventory-eyebrow'>Inventário urbano</p>
          <h2 id='equipment-form-title'>
            {equipment ? 'Editar equipamento' : 'Cadastrar equipamento'}
          </h2>
        </div>
        <button
          type='button'
          className='secondary'
          onClick={onCancel}
          disabled={saving}
        >
          Cancelar
        </button>
      </div>
      <p className='inventory-muted'>
        Campos com * são obrigatórios.{' '}
        {equipment
          ? 'O tipo técnico é preservado. A situação pode ser alterada nos detalhes.'
          : 'O cadastro inicia como Não instalado. Altere a situação nos detalhes após salvar.'}
      </p>
      {Object.keys(errors).length > 0 && (
        <div
          className='message error'
          id='equipment-form-errors'
          role='alert'
          tabIndex={-1}
        >
          <strong>Revise os campos indicados.</strong>
          <ul>
            {Object.entries(errors).map(([key, error]) => (
              <li key={key}>
                <a href={`#equipment-${key}`}>{error}</a>
              </li>
            ))}
          </ul>
        </div>
      )}
      <form
        onSubmit={submit}
        noValidate
        aria-busy={saving}
      >
        <fieldset disabled={saving}>
          <legend>Identificação</legend>
          <div className='inventory-form-grid'>
            <label htmlFor='equipment-type'>
              Tipo técnico *
              <select
                id='equipment-type'
                value={form.type}
                disabled={Boolean(equipment)}
                onChange={(event) => {
                  const type = event.target.value;
                  setForm((current) => ({
                    ...current,
                    type,
                    category: '',
                    ...Object.fromEntries(
                      specificFields[type].map((spec) => [
                        spec.name,
                        spec.type === 'checkbox' ? false : '',
                      ]),
                    ),
                  }));
                  setErrors({});
                }}
              >
                {Object.entries(equipmentTypes).map(([value, label]) => (
                  <option
                    key={value}
                    value={value}
                  >
                    {label}
                  </option>
                ))}
              </select>
            </label>
            {[
              { name: 'serialNumber', label: 'Número de série', required: true, maxLength: 80 },
              { name: 'brand', label: 'Marca', required: true, maxLength: 80 },
              { name: 'model', label: 'Modelo', required: true, maxLength: 80 },
            ].map((spec) => (
              <Field
                key={spec.name}
                spec={spec}
                value={form[spec.name]}
                error={errors[spec.name]}
                onChange={(value) => update(spec.name, value)}
              />
            ))}
            <label htmlFor='equipment-category'>
              Categoria de uso
              <input
                id='equipment-category'
                list='equipment-categories'
                maxLength={80}
                value={form.category}
                onChange={(event) => update('category', event.target.value)}
                aria-invalid={Boolean(errors.category)}
              />
              <datalist id='equipment-categories'>
                {categorySuggestions[form.type].map((value) => (
                  <option
                    key={value}
                    value={value}
                  />
                ))}
              </datalist>
              {errors.category && <span className='inventory-field-error'>{errors.category}</span>}
            </label>
            <div>
              <span className='inventory-muted'>Situação operacional</span>
              <p>{getEquipmentState(equipment?.status ?? 'NotInstalled').label}</p>
            </div>
          </div>
          <p className='inventory-muted'>
            A categoria descreve a finalidade sem alterar o tipo técnico. Ex.: balança como sensor
            de pesagem; controlador IoT de cancela como sensor/controlador de acesso; táxi ou
            escolar como veículo.
          </p>
        </fieldset>
        <fieldset disabled={saving}>
          <legend>Características do tipo</legend>
          <div className='inventory-form-grid'>
            {specificFields[form.type]?.map((spec) => (
              <Field
                key={spec.name}
                spec={spec}
                value={form[spec.name]}
                error={errors[spec.name]}
                onChange={(value) => update(spec.name, value)}
              />
            ))}
          </div>
          {equipment?.attributes?.currentAgentId && (
            <p className='inventory-muted'>
              O vínculo atual do veículo com o agente será preservado. Gestão de agentes não faz
              parte desta tela.
            </p>
          )}
        </fieldset>
        <fieldset disabled={saving || locked}>
          <legend>Previsão de manutenção</legend>
          <div className='inventory-form-grid'>
            <label htmlFor='equipment-maintenanceNeed'>
              Necessidade de manutenção
              <select
                id='equipment-maintenanceNeed'
                value={form.maintenanceNeed}
                onChange={(event) => update('maintenanceNeed', event.target.value)}
              >
                {Object.entries(maintenanceNeeds).map(([value, label]) => (
                  <option
                    key={value}
                    value={value}
                  >
                    {label}
                  </option>
                ))}
              </select>
            </label>
            {['Scheduled', 'Periodic'].includes(form.maintenanceNeed) && (
              <label htmlFor='equipment-nextMaintenanceDate'>
                Próxima data prevista *
                <input
                  id='equipment-nextMaintenanceDate'
                  type='datetime-local'
                  value={form.nextMaintenanceDate}
                  onChange={(event) => update('nextMaintenanceDate', event.target.value)}
                  aria-invalid={Boolean(errors.nextMaintenanceDate)}
                />
                {errors.nextMaintenanceDate && (
                  <span className='inventory-field-error'>{errors.nextMaintenanceDate}</span>
                )}
              </label>
            )}
          </div>
        </fieldset>
        <p className='inventory-muted'>
          {locked
            ? 'O planejamento está protegido durante falha ou manutenção em andamento. Os valores atuais serão preservados.'
            : 'Registra somente a necessidade e a próxima data prevista. Não cria uma ordem. Se já houver ordem agendada, sua previsão é protegida; consulte Manutenções nos detalhes do equipamento.'}
        </p>
        <fieldset disabled={saving}>
          <legend>Localização de instalação ou base</legend>
          <div className='inventory-form-grid'>
            <label htmlFor='equipment-addressMode'>
              Localização
              <select
                id='equipment-addressMode'
                value={form.addressMode}
                onChange={(event) => update('addressMode', event.target.value)}
              >
                <option value='new'>
                  {equipment ? 'Informar outra localização' : 'Informar localização completa'}
                </option>
                <option value='existing'>Usar localização existente</option>
              </select>
            </label>
            {form.addressMode === 'existing' && (
              <label htmlFor='equipment-addressId'>
                Localização existente *
                <select
                  id='equipment-addressId'
                  value={form.addressId}
                  onChange={(event) => {
                    const chosen = addressOptions.find((item) => item.id === event.target.value);
                    setForm((current) => ({
                      ...current,
                      addressId: event.target.value,
                      address: chosen ?? current.address,
                    }));
                  }}
                  aria-invalid={Boolean(errors.addressId)}
                >
                  <option value=''>Selecione</option>
                  {addressOptions.map((item) => (
                    <option
                      key={item.id}
                      value={item.id}
                    >
                      {item.street}
                      {item.number ? `, ${item.number}` : ''} — {item.neighborhood}, {item.city}/
                      {item.state}
                    </option>
                  ))}
                </select>
                {errors.addressId && (
                  <span className='inventory-field-error'>{errors.addressId}</span>
                )}
              </label>
            )}
          </div>
          {form.addressMode === 'existing' && currentAddress && (
            <p className='inventory-muted'>
              CEP {currentAddress.zipCode} · Referência:{' '}
              {currentAddress.locationReference || 'Não informada'} · GPS: {currentAddress.latitude}
              , {currentAddress.longitude}
            </p>
          )}
          {addressError && (
            <p className='inventory-muted'>
              Não foi possível carregar as localizações existentes. Você pode informar uma
              localização completa ou tentar atualizar a lista.
            </p>
          )}
          {form.addressMode === 'new' && (
            <>
              <p className='inventory-muted'>
                Será criada uma localização exclusiva deste cadastro. Outros equipamentos não terão
                seu endereço alterado.
              </p>
              <div className='inventory-form-grid'>
                {addressFields.map((spec) => (
                  <Field
                    key={spec.name}
                    spec={spec}
                    prefix='address.'
                    value={form.address[spec.name]}
                    error={errors[`address.${spec.name}`]}
                    onChange={(value) =>
                      setForm((current) => ({
                        ...current,
                        address: { ...current.address, [spec.name]: value },
                      }))
                    }
                  />
                ))}
              </div>
            </>
          )}
        </fieldset>
        <div className='inventory-actions'>
          <button
            type='submit'
            disabled={saving}
          >
            {saving ? 'Salvando…' : equipment ? 'Salvar alterações' : 'Cadastrar equipamento'}
          </button>
          <button
            type='button'
            className='secondary'
            onClick={onCancel}
            disabled={saving}
          >
            Cancelar
          </button>
        </div>
      </form>
    </section>
  );
}
