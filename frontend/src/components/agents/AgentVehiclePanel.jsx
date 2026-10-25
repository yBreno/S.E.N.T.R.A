import { useState } from 'react';
import { equipmentTypes } from '../equipment/equipmentModel.js';
import InventoryDetails from '../inventory/InventoryDetails.jsx';
import { getEquipmentState } from '../map/markerData.js';

export default function AgentVehiclePanel({
  agent,
  vehicle,
  vehicles,
  error,
  vehiclesError,
  manager,
  saving,
  onSave,
  onRetry,
}) {
  const currentVehicleId = vehicle?.id ?? '';
  const [chosen, setChosen] = useState(currentVehicleId);
  const [confirmed, setConfirmed] = useState(false);
  const cannotSave = saving || !confirmed || chosen === currentVehicleId;
  const available = vehicles.filter(
    (item) => !item.currentAgentId || item.currentAgentId === agent.id,
  );

  if (vehicle && !available.some((item) => item.id === vehicle.id)) {
    available.unshift(vehicle);
  }

  async function handleSubmit(event) {
    event.preventDefault();
    if (cannotSave) return;

    try {
      await onSave(chosen || null, vehicle?.id ?? null);
    } catch {
      // O painel principal apresenta o erro sem apagar a seleção.
    }
  }

  function handleVehicleChange(event) {
    setChosen(event.target.value);
    setConfirmed(false);
  }

  function handleConfirmationChange(event) {
    setConfirmed(event.target.checked);
  }

  function renderCurrentVehicle() {
    if (error) {
      return (
        <p
          className='message error'
          role='alert'
        >
          Não foi possível consultar o vínculo atual.{' '}
          <button
            className='secondary'
            onClick={onRetry}
            disabled={saving}
          >
            Tentar novamente
          </button>
        </p>
      );
    }

    if (!vehicle) {
      return <p>Nenhum veículo associado a este agente.</p>;
    }

    return (
      <InventoryDetails
        entries={[
          ['Tipo', equipmentTypes[vehicle.type]],
          ['Número de série', vehicle.serialNumber],
          ['Placa', vehicle.licensePlate],
          ['Marca / modelo', `${vehicle.brand} / ${vehicle.model}`],
          ['Situação do veículo', getEquipmentState(vehicle.status).label],
        ]}
      />
    );
  }

  function renderVehicleManagement() {
    if (!manager || error) return null;

    if (vehiclesError) {
      return (
        <p
          className='message error'
          role='alert'
        >
          Não foi possível carregar os veículos disponíveis.{' '}
          <button
            className='secondary'
            onClick={onRetry}
            disabled={saving}
          >
            Atualizar detalhes
          </button>
        </p>
      );
    }

    return (
      <form
        aria-busy={saving}
        onSubmit={handleSubmit}
      >
        <fieldset disabled={saving}>
          <legend>Gerenciar vínculo</legend>
          <label htmlFor='agent-vehicleId'>
            Veículo
            <select
              id='agent-vehicleId'
              value={chosen}
              onChange={handleVehicleChange}
            >
              <option value=''>Sem veículo associado</option>
              {available.map((item) => (
                <option
                  key={item.id}
                  value={item.id}
                >
                  {item.licensePlate || item.serialNumber} · {equipmentTypes[item.type]} ·{' '}
                  {item.brand} {item.model} · {getEquipmentState(item.status).label}
                </option>
              ))}
            </select>
          </label>
          <p className='inventory-muted'>
            São mostrados apenas veículos livres ou já vinculados a este agente. A situação do
            veículo é informativa; associar não altera GPS ou disponibilidade do agente e não aciona
            o veículo.
          </p>
          {available.length === 0 && (
            <p>
              Nenhum veículo livre. Cadastre um equipamento móvel no módulo Equipamentos ou confira
              os vínculos existentes.
            </p>
          )}
          <label className='inventory-checkbox'>
            <span>Confirmo a alteração do vínculo deste agente.</span>
            <input
              type='checkbox'
              checked={confirmed}
              onChange={handleConfirmationChange}
            />
          </label>
        </fieldset>
        <button
          type='submit'
          disabled={cannotSave}
        >
          {saving ? 'Salvando…' : 'Salvar vínculo'}
        </button>
      </form>
    );
  }

  return (
    <section
      className='agent-vehicle-panel'
      aria-labelledby='agent-vehicle-title'
    >
      <h3 id='agent-vehicle-title'>Veículo ou equipamento móvel associado</h3>
      <p className='inventory-muted'>
        A regra atual permite um veículo por agente. Veículo, moto e veículo autônomo podem ser
        vinculados; equipamentos fixos não possuem esse vínculo no domínio.
      </p>
      {renderCurrentVehicle()}
      {renderVehicleManagement()}
    </section>
  );
}
