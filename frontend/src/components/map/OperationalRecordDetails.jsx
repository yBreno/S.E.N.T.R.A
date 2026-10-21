import { agentRoleLabel, displayText, formatLocationUpdatedAt, getAgentState, getEquipmentState } from './markerData.js'
import { equipmentTypeLabel } from './operationalFilters.js'

export default function OperationalRecordDetails({ kind, record }) {
  const equipment = kind === 'equipment'
  const fields = equipment ? [
    ['Tipo', equipmentTypeLabel(record.type)],
    ['Marca / modelo', `${displayText(record.brand)} / ${displayText(record.model)}`],
    ['Número de série', displayText(record.serialNumber)],
    ['Status', getEquipmentState(record.status).label],
    ['Bairro', displayText(record.neighborhood)],
  ] : [
    ['Nome', displayText(record.name)], ['Matrícula', displayText(record.badgeNumber)],
    ['Função', agentRoleLabel(record.role)], ['Disponibilidade', getAgentState(record.availability).label],
    ['Bairro', displayText(record.neighborhood)], ['Localização atualizada em', formatLocationUpdatedAt(record.locationUpdatedAt)],
  ]
  return (
    <div className="operational-record-details">
      <h3>{equipment ? 'Equipamento' : 'Agente'}</h3>
      <dl>{fields.map(([label, value]) => <div key={label}><dt>{label}</dt><dd>{value}</dd></div>)}</dl>
    </div>
  )
}
