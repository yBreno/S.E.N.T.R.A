import { useState } from 'react';
import {
  availableStatusTransitions,
  equipmentTypes,
  maintenanceNeeds,
  specificFields,
  addressFields,
} from './equipmentModel.js';
import { getEquipmentState } from '../map/markerData.js';
import Fields from '../inventory/InventoryDetails.jsx';
export default function EquipmentDetails({
  equipment,
  address,
  addressError,
  canManage,
  canChangeStatus,
  saving,
  onEdit,
  onStatus,
  onRetryAddress,
  onMaintenance,
}) {
  const [next, setNext] = useState('');
  const [reason, setReason] = useState('');
  const [confirm, setConfirm] = useState(false);
  const state = getEquipmentState(equipment.status);
  const transitions = availableStatusTransitions(equipment.status, canManage);
  const date = equipment.nextMaintenanceDate ? new Date(equipment.nextMaintenanceDate) : null;
  return (
    <section
      className='inventory-panel'
      aria-labelledby='equipment-detail-title'
    >
      <div className='inventory-section-heading'>
        <div>
          <p className='inventory-eyebrow'>Detalhes do equipamento</p>
          <h2 id='equipment-detail-title'>{equipment.serialNumber}</h2>
          <span className={`inventory-badge state-${state.tone}`}>{state.label}</span>
        </div>
        {canManage && (
          <button
            onClick={onEdit}
            disabled={saving}
          >
            Editar equipamento
          </button>
        )}
      </div>
      <h3>Identificação</h3>
      <Fields
        entries={[
          ['Tipo técnico', equipmentTypes[equipment.type] ?? equipment.type],
          ['Categoria de uso', equipment.category],
          ['Número de série', equipment.serialNumber],
          ['Marca', equipment.brand],
          ['Modelo', equipment.model],
          [
            'Necessidade de manutenção',
            maintenanceNeeds[equipment.maintenanceNeed] ?? equipment.maintenanceNeed,
          ],
          [
            'Próxima data prevista',
            date && Number.isFinite(date.getTime()) ? date.toLocaleString('pt-BR') : null,
          ],
        ]}
      />
      {onMaintenance && (
        <div className='inventory-actions'>
          <button
            className='secondary'
            disabled={saving}
            onClick={onMaintenance}
          >
            Manutenções e histórico deste equipamento
          </button>
        </div>
      )}
      <p className='inventory-muted'>
        A previsão isolada não representa uma ordem. O agendamento, o início da intervenção e a
        conclusão são registrados no módulo de Manutenções, conforme seu perfil de acesso.
      </p>
      <h3>Características específicas</h3>
      <Fields
        entries={(specificFields[equipment.type] ?? []).map((spec) => [
          spec.label,
          equipment.attributes[spec.name],
        ])}
      />
      {equipment.attributes.currentAgentId && (
        <Fields
          entries={[['Agente vinculado (identificador)', equipment.attributes.currentAgentId]]}
        />
      )}
      <h3>Localização completa</h3>
      {address ? (
        <Fields entries={addressFields.map((spec) => [spec.label, address[spec.name]])} />
      ) : (
        <p
          className='message error'
          role='alert'
        >
          {addressError
            ? 'Não foi possível carregar o endereço completo.'
            : 'Endereço indisponível.'}{' '}
          <button
            className='secondary'
            onClick={onRetryAddress}
            disabled={saving}
          >
            Tentar novamente
          </button>
        </p>
      )}
      {canChangeStatus && (
        <div className='inventory-status-editor'>
          <h3>Situação operacional</h3>
          <p className='inventory-muted'>
            Mudanças respeitam as regras do servidor. Uma intervenção em andamento deve ser
            concluída em Manutenções. O estado Furtado / roubado encerra as transições disponíveis.
          </p>
          {transitions.length === 0 ? (
            <p>Nenhuma alteração de situação disponível para este estado e perfil.</p>
          ) : (
            <form
              onSubmit={async (event) => {
                event.preventDefault();
                if (saving || !next || !confirm) return;
                const saved = await onStatus(next, reason);
                if (saved) {
                  setNext('');
                  setReason('');
                  setConfirm(false);
                }
              }}
            >
              <fieldset disabled={saving}>
                <legend>Alterar situação</legend>
                <div className='inventory-form-grid'>
                  <label htmlFor='equipment-nextStatus'>
                    Nova situação
                    <select
                      id='equipment-nextStatus'
                      required
                      value={next}
                      onChange={(event) => {
                        setNext(event.target.value);
                        setConfirm(false);
                      }}
                    >
                      <option value=''>Selecione</option>
                      {transitions.map((status) => (
                        <option
                          key={status}
                          value={status}
                        >
                          {getEquipmentState(status).label}
                        </option>
                      ))}
                    </select>
                  </label>
                  <label htmlFor='equipment-statusReason'>
                    Motivo (opcional)
                    <input
                      id='equipment-statusReason'
                      maxLength={300}
                      value={reason}
                      onChange={(event) => setReason(event.target.value)}
                    />
                  </label>
                </div>
                <label className='inventory-checkbox'>
                  <span>
                    Confirmo a alteração{next ? ` para ${getEquipmentState(next).label}` : ''}.
                  </span>
                  <input
                    type='checkbox'
                    checked={confirm}
                    onChange={(event) => setConfirm(event.target.checked)}
                  />
                </label>
              </fieldset>
              <button
                type='submit'
                disabled={saving || !next || !confirm}
              >
                {saving ? 'Atualizando…' : 'Confirmar situação'}
              </button>
            </form>
          )}
        </div>
      )}
    </section>
  );
}
