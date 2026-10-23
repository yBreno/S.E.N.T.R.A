// Bibliotecas externas
import { Marker } from 'react-leaflet';

// Componentes
import MapPopup from './MapPopup.jsx';

// Serviços e utilitários
import { agentRoleLabel, displayText, getAgentState } from './markerData.js';
import { getMarkerIcon } from './markerIcons.js';

export default function AgentMarkers({
  markers,
  onSelect,
  selection
}) {
  return markers.map(record => {
    const state = getAgentState(record.availability);
    const title = `${agentRoleLabel(record.role)}: ${displayText(record.name)} · ${state.label}`;

    return (
      <Marker
        key={record.id}
        position={record.position}
        icon={getMarkerIcon('agent', state, record.role)}
        title={title}
        alt={title}
        eventHandlers={{
          click: () => onSelect({
            kind: 'agent',
            id: record.id
          })
        }}
      >
        <MapPopup
          kind="agent"
          record={record}
          selected={selection?.kind === 'agent' && selection.id === record.id}
        />
      </Marker>
    );
  });
}
