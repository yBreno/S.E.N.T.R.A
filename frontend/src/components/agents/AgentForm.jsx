import { useState } from 'react';
import InventoryField from '../inventory/InventoryField.jsx';
import InventoryDetails from '../inventory/InventoryDetails.jsx';
import {
  buildAgentPayload,
  createAgentForm,
  identityFields,
  operationalFields,
} from './agentModel.js';

export default function AgentForm({ agent, saving, onSave, onCancel }) {
  const [form, setForm] = useState(() => createAgentForm(agent));
  const [errors, setErrors] = useState({});
  const update = (key, value) => setForm((current) => ({ ...current, [key]: value }));
  async function submit(event) {
    event.preventDefault();
    if (saving) return;
    const parsed = buildAgentPayload(form, agent);
    setErrors(parsed.errors);
    if (!parsed.payload) {
      queueMicrotask(() => document.getElementById('agent-form-errors')?.focus());
      return;
    }
    try {
      await onSave(parsed.payload, agent?.id);
    } catch (error) {
      setErrors(
        Object.fromEntries(
          Object.keys(error.fieldErrors ?? {}).map((key) => [
            key[0].toLowerCase() + key.slice(1),
            'Confira o formato, o tamanho e o valor deste campo.',
          ]),
        ),
      );
      queueMicrotask(() => document.getElementById('agent-form-errors')?.focus());
    }
  }
  return (
    <section
      className='inventory-panel'
      aria-labelledby='agent-form-title'
    >
      <div className='inventory-section-heading'>
        <div>
          <p className='inventory-eyebrow'>Mobilidade e segurança urbana</p>
          <h2 id='agent-form-title'>{agent ? 'Editar agente' : 'Cadastrar agente'}</h2>
        </div>
        <button
          className='secondary'
          onClick={onCancel}
          disabled={saving}
        >
          Cancelar
        </button>
      </div>
      <p className='inventory-muted'>
        Campos com * são obrigatórios. Categoria de atuação identifica a função profissional e não
        concede permissões de acesso ao sistema.
      </p>
      {Object.keys(errors).length > 0 && (
        <div
          id='agent-form-errors'
          className='message error'
          role='alert'
          tabIndex={-1}
        >
          <strong>Revise os campos indicados.</strong>
          <ul>
            {Object.entries(errors).map(([key, error]) => (
              <li key={key}>
                <a href={`#agent-${key}`}>{error}</a>
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
          <legend>Identificação profissional</legend>
          <div className='inventory-form-grid'>
            {identityFields.map((spec) => (
              <InventoryField
                key={spec.name}
                idPrefix='agent-'
                spec={spec}
                value={form[spec.name]}
                error={errors[spec.name]}
                onChange={(value) => update(spec.name, value)}
              />
            ))}
            {!agent && (
              <InventoryField
                idPrefix='agent-'
                spec={{
                  name: 'cpf',
                  label: 'CPF',
                  required: true,
                  maxLength: 14,
                  autoComplete: 'off',
                  placeholder: 'Somente no cadastro inicial',
                }}
                value={form.cpf}
                error={errors.cpf}
                onChange={(value) => update('cpf', value)}
              />
            )}
          </div>
          {agent && (
            <>
              <InventoryDetails
                entries={[
                  ['CPF protegido', agent.maskedCpf],
                  ['Conta de acesso atual', agent.userId || 'Sem vínculo'],
                ]}
              />
              <p className='inventory-muted'>O CPF é imutável e exibido somente mascarado.</p>
            </>
          )}
          <InventoryField
            idPrefix='agent-'
            spec={{
              name: 'userId',
              label: 'Conta de acesso (ID opcional)',
              maxLength: 36,
              autoComplete: 'off',
            }}
            value={form.userId}
            error={errors.userId}
            onChange={(value) => update('userId', value)}
          />
          <p className='inventory-muted'>
            Use o identificador de conta fornecido pelo Admin. O vínculo permite identificar o
            profissional da conta; não cria usuário nem muda seu cargo ou senha. Uma conta só pode
            estar vinculada a um agente. Na edição, apagar o identificador remove o vínculo sem
            desativar a conta.
          </p>
        </fieldset>
        <fieldset disabled={saving}>
          <legend>Localização e disponibilidade</legend>
          <div className='inventory-form-grid'>
            {operationalFields.map((spec) => (
              <InventoryField
                key={spec.name}
                idPrefix='agent-'
                spec={spec}
                value={form[spec.name]}
                error={errors[spec.name]}
                onChange={(value) => update(spec.name, value)}
              />
            ))}
          </div>
        </fieldset>
        <p className='inventory-muted'>
          O bairro representa a área atual informada para operação, sem delimitação por polígonos.
          Disponível e Ocupado contam como agentes ativos; Offline não conta. Veículos são
          associados nos detalhes depois do cadastro.
        </p>
        <div className='inventory-actions'>
          <button
            type='submit'
            disabled={saving}
          >
            {saving ? 'Salvando…' : agent ? 'Salvar alterações' : 'Cadastrar agente'}
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
