// Serviços e utilitários
import { agentStates, equipmentStates } from './markerData.js';
import { equipmentTypes, UNKNOWN_FILTER_VALUE } from './operationalFilters.js';

// Estilos
import './OperationalFilters.css';

export default function OperationalFilters({
  filters,
  onChange,
  onClear,
  neighborhoodOptions
}) {
  const update = field => event => onChange({
    ...filters,
    [field]: event.target.value
  });

  const active = Object.values(filters).some(Boolean);

  return (
    <section className="operational-filters" aria-labelledby="operational-filters-title">
      <div className="filter-heading">
        <h3 id="operational-filters-title">Filtros operacionais</h3>
        <button
          className="secondary"
          onClick={onClear}
          disabled={!active}
        >Limpar filtros</button>
      </div>
      <p>Filtros locais das camadas. Os indicadores gerais e os totais recebidos não são filtrados.</p>
      <div className="filter-fields">
        <label htmlFor="equipment-type">Tipo de equipamento<select
          id="equipment-type"
          value={filters.equipmentType}
          onChange={update('equipmentType')}
        >
          <option value="">Todos os tipos</option>
          {Object.entries(equipmentTypes).map(([value, label]) => (
            <option key={value} value={value}>
              {label}
            </option>
          ))}
          <option value={UNKNOWN_FILTER_VALUE}>Tipo não informado / desconhecido</option>
        </select></label>
        <label htmlFor="equipment-status">Situação<select
          id="equipment-status"
          value={filters.equipmentStatus}
          onChange={update('equipmentStatus')}
        >
          <option value="">Todos os status</option>
          {Object.entries(equipmentStates).map(([value, state]) => (
            <option key={value} value={value}>
              {state.label}
            </option>
          ))}
          <option value={UNKNOWN_FILTER_VALUE}>Status não informado / desconhecido</option>
        </select></label>
        <label htmlFor="equipment-neighborhood">Região (bairro)<select
          id="equipment-neighborhood"
          value={filters.neighborhood}
          onChange={update('neighborhood')}
        >
          <option value="">Todos os bairros</option>
          {neighborhoodOptions.map(option => (
            <option key={option.value} value={option.value}>
              {option.label}
            </option>
          ))}
          {filters.neighborhood && !neighborhoodOptions.some(option => option.value === filters.neighborhood) && (
            <option value={filters.neighborhood}>Bairro selecionado (ausente na resposta atual)</option>
          )}
        </select></label>
        <label htmlFor="agent-availability">Disponibilidade do agente<select
          id="agent-availability"
          value={filters.agentAvailability}
          onChange={update('agentAvailability')}
        >
          <option value="">Todas as disponibilidades</option>
          {Object.entries(agentStates).map(([value, state]) => (
            <option key={value} value={value}>
              {state.label}
            </option>
          ))}
          <option value={UNKNOWN_FILTER_VALUE}>Disponibilidade não informada / desconhecida</option>
        </select></label>
      </div>
    </section>
  );
}
