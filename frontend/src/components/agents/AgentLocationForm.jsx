import { useState } from 'react';
import InventoryField from '../inventory/InventoryField.jsx';
import { buildAgentLocation, createAgentForm, operationalFields } from './agentModel.js';

export default function AgentLocationForm({ agent, saving, onSave }) {
  const [form, setForm] = useState(() => createAgentForm(agent));
  const [errors, setErrors] = useState({});
  return (
    <form
      className='agent-operation-form'
      noValidate
      aria-busy={saving}
      onSubmit={async (event) => {
        event.preventDefault();
        if (saving) return;
        const parsed = buildAgentLocation(form);
        setErrors(parsed.errors);
        if (!parsed.payload) return;
        try {
          await onSave(parsed.payload);
        } catch (error) {
          setErrors(
            Object.fromEntries(
              Object.keys(error.fieldErrors ?? {}).map((key) => [
                key[0].toLowerCase() + key.slice(1),
                'Confira o valor informado.',
              ]),
            ),
          );
        }
      }}
    >
      <fieldset disabled={saving}>
        <legend>Atualizar posição e disponibilidade</legend>
        <p className='inventory-muted'>
          Esta operação usa o endpoint de localização e registra uma posição no histórico. Não
          altera identificação, categoria ou conta de acesso.
        </p>
        <div className='inventory-form-grid'>
          {operationalFields.map((spec) => (
            <InventoryField
              key={spec.name}
              idPrefix='agent-position-'
              spec={spec}
              value={form[spec.name]}
              error={errors[spec.name]}
              onChange={(value) => setForm((current) => ({ ...current, [spec.name]: value }))}
            />
          ))}
        </div>
        {Object.keys(errors).length > 0 && (
          <p
            className='inventory-field-error'
            role='alert'
          >
            Revise os campos da posição e disponibilidade.
          </p>
        )}
      </fieldset>
      <button
        type='submit'
        disabled={saving}
      >
        {saving ? 'Atualizando…' : 'Atualizar posição'}
      </button>
    </form>
  );
}
