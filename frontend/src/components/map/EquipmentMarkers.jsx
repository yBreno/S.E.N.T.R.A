// Bibliotecas externas
import { Marker } from 'react-leaflet';

// Componentes
import MapPopup from './MapPopup.jsx';

// Serviços e utilitários
import { displayText, getEquipmentState } from './markerData.js';
import { getMarkerIcon } from './markerIcons.js';
import { equipmentTypeLabel } from './operationalFilters.js';
import { maintenanceAttention } from '../../services/operations.js';

export default function EquipmentMarkers({
  markers,
  onSelect,
  selection
}) {
  return markers.map(record => {
    const state = getEquipmentState(record.status);
    const attention = maintenanceAttention(record);
    const badge = attention.immediate ? 'immediate' : attention.upcoming ? 'upcoming' : '';
    const title = `${equipmentTypeLabel(record.type)}: ${displayText(record.serialNumber)} · ${state.label}${badge ? attention.immediate ? ' · Manutenção imediata' : ' · Manutenção vencida ou próxima' : ''}`;

    return (
      <Marker
        key={record.id}
        position={record.position}
        icon={getMarkerIcon('equipment', state, record.type, badge)}
        title={title}
        alt={title}
        eventHandlers={{
          click: () => onSelect({
            kind: 'equipment',
            id: record.id
          })
        }}
      >
        <MapPopup
          kind="equipment"
          record={record}
          selected={selection?.kind === 'equipment' && selection.id === record.id}
        />
      </Marker>
    );
  });
}
