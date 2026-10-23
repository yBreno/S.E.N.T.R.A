import InventoryDetails from '../inventory/InventoryDetails.jsx';
import AgentLocationForm from './AgentLocationForm.jsx';
import AgentVehiclePanel from './AgentVehiclePanel.jsx';
import { agentRoleLabel, formatLocationUpdatedAt, getAgentState } from '../map/markerData.js';

export default function AgentDetails({ agent, vehicle, vehicles, vehicleError, vehiclesError, manager, saving, onEdit, onLocation, onVehicle, onRetry }) {
  const availability = getAgentState(agent.availability);
  return <section className="inventory-panel" aria-labelledby="agent-detail-title">
    <div className="inventory-section-heading"><div><p className="inventory-eyebrow">Detalhes do profissional</p><h2 id="agent-detail-title">{agent.name}</h2><span className={`inventory-badge state-${availability.tone}`}>{availability.label}</span></div>{manager && <button onClick={onEdit} disabled={saving}>Editar agente</button>}</div>
    <h3>Identificação e atuação</h3><InventoryDetails entries={[
      ['Nome', agent.name], ['Matrícula', agent.badgeNumber], ['Categoria de atuação', agentRoleLabel(agent.role)], ['CPF protegido', agent.maskedCpf], ['Conta de acesso', agent.userId ? 'Vinculada' : 'Sem vínculo'],
      ['Disponibilidade', availability.label], ['Situação operacional', agent.availability === 'Offline' ? 'Fora de atividade' : ['Available', 'Busy'].includes(agent.availability) ? 'Em atividade' : 'Não informada']
    ]} />
    <h3>Localização atual</h3><InventoryDetails entries={[
      ['Bairro / área atual de atuação', agent.neighborhood], ['Latitude GPS', agent.latitude], ['Longitude GPS', agent.longitude], ['Posição atualizada em', formatLocationUpdatedAt(agent.locationUpdatedAt)]
    ]} />
    <AgentVehiclePanel key={`${agent.id}:${vehicle?.id ?? ''}`} agent={agent} vehicle={vehicle} vehicles={vehicles} error={vehicleError} vehiclesError={vehiclesError} manager={manager} saving={saving} onSave={onVehicle} onRetry={onRetry} />
    <AgentLocationForm key={`${agent.id}:${agent.locationUpdatedAt}:${agent.availability}`} agent={agent} saving={saving} onSave={onLocation} />
  </section>;
}
