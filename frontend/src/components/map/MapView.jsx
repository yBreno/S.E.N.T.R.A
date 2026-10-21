import { useEffect, useMemo, useState } from 'react'
import { MapContainer, TileLayer, useMap } from 'react-leaflet'
import 'leaflet/dist/leaflet.css'
import './MapView.css'
import { useOperationalMarkers } from '../../hooks/useOperationalMarkers.js'
import EquipmentMarkers from './EquipmentMarkers.jsx'
import AgentMarkers from './AgentMarkers.jsx'
import MapLayersControl from './MapLayersControl.jsx'
import './Markers.css'
import { createEmptyFilters } from './operationalFilters.js'
import OperationalFilters from './OperationalFilters.jsx'
import { useMarkerInspection } from '../../hooks/useMarkerInspection.js'
import RecordInspection from './RecordInspection.jsx'
import { useHeatmap } from '../../hooks/useHeatmap.js'
import HeatmapLayer from './HeatmapLayer.jsx'
import HeatmapControls from './HeatmapControls.jsx'

const saoPauloCenter = [-23.5505, -46.6333]

function MapResizeObserver() {
  const map = useMap()
  useEffect(() => {
    let frame
    const observer = new ResizeObserver(() => {
      cancelAnimationFrame(frame)
      frame = requestAnimationFrame(() => map.invalidateSize({ pan: false }))
    })
    observer.observe(map.getContainer())
    return () => {
      observer.disconnect()
      cancelAnimationFrame(frame)
    }
  }, [map])
  return null
}

export default function MapView({ verification, token, onLogout }) {
  const heatmap = useHeatmap({ token, onLogout, verification })
  const [heatmapError, setHeatmapError] = useState('')
  const [filters, setFilters] = useState(createEmptyFilters)
  const layers = useOperationalMarkers(verification, filters)
  const [showEquipments, setShowEquipments] = useState(true)
  const [showAgents, setShowAgents] = useState(true)
  const { selection, inspection, select } = useMarkerInspection(layers, { showEquipments, showAgents })
  const [tiles, setTiles] = useState({ loading: true, error: false })
  const tileEvents = useMemo(() => ({
    loading: () => setTiles({ loading: true, error: false }),
    tileerror: () => setTiles((current) => ({ ...current, error: true })),
    load: () => setTiles((current) => ({ ...current, loading: false })),
  }), [])

  return (
    <div className="map-view">
      <OperationalFilters filters={filters} onChange={setFilters} onClear={() => setFilters(createEmptyFilters())}
        neighborhoodOptions={layers.equipments.neighborhoodOptions} />
      <MapLayersControl layers={layers} showEquipments={showEquipments} showAgents={showAgents}
        onEquipmentsChange={setShowEquipments} onAgentsChange={setShowAgents}
        loading={verification.loading} error={verification.error} heatmap={heatmap} />
      <HeatmapControls heatmap={heatmap} layerError={heatmapError} />
      <p className="map-instructions" id="map-instructions">Arraste para navegar. Use os controles + e − para aproximar ou afastar.</p>
      <div className="map-region" role="region" aria-label="Mapa interativo de São Paulo" aria-describedby="map-instructions">
        <MapContainer className="operational-map" center={saoPauloCenter} zoom={12} zoomControl scrollWheelZoom>
          <TileLayer
            url="https://tile.openstreetmap.org/{z}/{x}/{y}.png"
            referrerPolicy="strict-origin-when-cross-origin"
            attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
            maxZoom={19}
            keepBuffer={0}
            updateWhenIdle
            eventHandlers={tileEvents}
          />
          <MapResizeObserver />
          {heatmap.enabled && heatmap.data && <HeatmapLayer points={heatmap.data.heatPoints} max={heatmap.data.renderMax} onError={setHeatmapError} />}
          {showEquipments && <EquipmentMarkers markers={layers.equipments.filtered} onSelect={select} selection={selection} />}
          {showAgents && <AgentMarkers markers={layers.agents.filtered} onSelect={select} selection={selection} />}
        </MapContainer>
      </div>
      <div className="map-status" aria-live="polite">
        {tiles.error ? <p role="alert">Não foi possível carregar parte do mapa base. Confira sua conexão com o OpenStreetMap.</p> :
          tiles.loading ? <p role="status">Carregando mapa base…</p> : <p>Mapa base OpenStreetMap</p>}
      </div>
      <RecordInspection inspection={inspection} loading={verification.loading} onClose={() => select(null)} />
    </div>
  )
}
