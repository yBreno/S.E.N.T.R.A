// Serviços e utilitários
import {
  agentRoleLabel,
  displayText,
  formatLocationUpdatedAt,
  getAgentState,
  getEquipmentState,
} from './markerData.js';
import { equipmentTypeLabel } from './operationalFilters.js';
import { useContext } from 'react';
import { OperationalNavigation } from './operationalNavigation.js';
import { addressFields, maintenanceNeeds, specificFields } from '../equipment/equipmentModel.js';
import { MAINTENANCE_LOOKAHEAD_DAYS, maintenanceAttention } from '../../services/operations.js';

export default function OperationalRecordDetails({ kind, record }) {
  const equipment = kind === 'equipment';
  const navigation = useContext(OperationalNavigation);
  const attention = maintenanceAttention(record);
  const fields = equipment
    ? [
        ['Tipo', equipmentTypeLabel(record.type)],
        ['Marca / modelo', `${displayText(record.brand)} / ${displayText(record.model)}`],
        ['Número de série', displayText(record.serialNumber)],
        ['Status', getEquipmentState(record.status).label],
        ...(record.predictiveRisk
          ? [
              [
                'Risco preditivo',
                `Alto · ${record.predictiveRisk.score}/100${record.predictiveRisk.stale ? ' · desatualizado' : ''}`,
              ],
              ['Avaliação de risco', formatLocationUpdatedAt(record.predictiveRisk.evaluatedAt)],
            ]
          : []),
        ['Bairro', displayText(record.neighborhood)],
        ['Categoria de uso', displayText(record.category)],
        [
          'GPS',
          typeof record.latitude === 'number' && typeof record.longitude === 'number'
            ? `${record.latitude}, ${record.longitude}`
            : 'Não informado',
        ],
        ['Necessidade de manutenção', maintenanceNeeds[record.maintenanceNeed] || 'Não informada'],
        ['Próxima previsão', formatLocationUpdatedAt(record.nextMaintenanceDate)],
        ...(specificFields[record.type] || []).map((spec) => [
          spec.label,
          typeof record.attributes?.[spec.name] === 'boolean'
            ? record.attributes[spec.name]
              ? 'Sim'
              : 'Não'
            : String(record.attributes?.[spec.name] ?? 'Não informado'),
        ]),
        ...(record.address
          ? addressFields
              .filter((spec) => spec.name !== 'neighborhood')
              .map((spec) => [spec.label, String(record.address[spec.name] ?? 'Não informado')])
          : []),
      ]
    : [
        ['Nome', displayText(record.name)],
        ['Matrícula', displayText(record.badgeNumber)],
        ['Função', agentRoleLabel(record.role)],
        ['Disponibilidade', getAgentState(record.availability).label],
        ['Bairro', displayText(record.neighborhood)],
        [
          'GPS',
          typeof record.latitude === 'number' && typeof record.longitude === 'number'
            ? `${record.latitude}, ${record.longitude}`
            : 'Não informado',
        ],
        ['Localização atualizada em', formatLocationUpdatedAt(record.locationUpdatedAt)],
      ];

  return (
    <div className='operational-record-details'>
      <h3>{equipment ? 'Equipamento' : 'Agente'}</h3>
      <dl>
        {fields.map(([label, value]) => (
          <div key={label}>
            <dt>{label}</dt>
            <dd>{value}</dd>
          </div>
        ))}
      </dl>
      {equipment && record.predictiveRisk && (
        <p>
          {record.predictiveRisk.summary} Risco preditivo não altera a situação operacional.{' '}
          {navigation?.predictive && (
            <button
              className='secondary'
              onClick={navigation.predictive}
            >
              Ver fatores preditivos
            </button>
          )}
        </p>
      )}
      {equipment && (attention.immediate || attention.upcoming) && (
        <p>
          {attention.immediate
            ? 'Manutenção imediata necessária.'
            : attention.overdue
              ? 'Previsão de manutenção vencida.'
              : `Manutenção prevista nos próximos ${MAINTENANCE_LOOKAHEAD_DAYS} dias.`}
        </p>
      )}
      {navigation && (
        <div className='operational-detail-actions'>
          <button
            className='secondary'
            onClick={() =>
              equipment ? navigation.equipment(record.id) : navigation.agent(record.id)
            }
          >
            Abrir detalhes completos
          </button>
          {equipment && navigation.maintenance && (
            <button
              className='secondary'
              onClick={() => navigation.maintenance(record.id)}
            >
              Consultar manutenções
            </button>
          )}
        </div>
      )}
    </div>
  );
}
