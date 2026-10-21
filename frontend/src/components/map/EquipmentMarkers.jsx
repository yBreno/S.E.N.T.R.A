import { Marker } from 'react-leaflet'
import { displayText, getEquipmentState } from './markerData.js'
import { getMarkerIcon } from './markerIcons.js'
import MapPopup from './MapPopup.jsx'

export default function EquipmentMarkers({ markers, onSelect, selection }) {
  return markers.map((record) => {
    const state = getEquipmentState(record.status)
    const title = `Equipamento: ${displayText(record.serialNumber)} · ${state.label}`
    return (
      <Marker key={record.id} position={record.position} icon={getMarkerIcon('equipment', state)} title={title} alt={title}
        eventHandlers={{ click: () => onSelect({ kind: 'equipment', id: record.id }) }}>
        <MapPopup kind="equipment" record={record} selected={selection?.kind === 'equipment' && selection.id === record.id} />
      </Marker>
    )
  })
}
