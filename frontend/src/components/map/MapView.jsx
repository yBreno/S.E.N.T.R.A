// Bibliotecas externas
import { useEffect, useMemo, useState } from 'react';
import { MapContainer, TileLayer, useMap } from 'react-leaflet';

// Estilos
import 'leaflet/dist/leaflet.css';
import './MapView.css';
import './Markers.css';

// Componentes
import EquipmentMarkers from './EquipmentMarkers.jsx';
import AgentMarkers from './AgentMarkers.jsx';
import NeighborhoodRiskMarkers from './NeighborhoodRiskMarkers.jsx';
import MapLayersControl from './MapLayersControl.jsx';
import OperationalFilters from './OperationalFilters.jsx';
import RecordInspection from './RecordInspection.jsx';
import HeatmapLayer from './HeatmapLayer.jsx';
import HeatmapControls from './HeatmapControls.jsx';
import DashboardIcon from '../dashboard/DashboardIcon.jsx';

// Hooks personalizados
import { useOperationalMarkers } from '../../hooks/useOperationalMarkers.js';
import { useMarkerInspection } from '../../hooks/useMarkerInspection.js';

// Serviços e utilitários
import { createEmptyFilters } from './operationalFilters.js';
import { OperationalNavigation } from './operationalNavigation.js';
import { markerSymbols } from './markerPresentation.js';
import { MAINTENANCE_LOOKAHEAD_DAYS } from '../../services/operations.js';
import { equipmentTypes, getNeighborhoodOptions } from './operationalFilters.js';

const saoPauloCenter = [-23.5505, -46.6333];

function MapResizeObserver() {
  const map = useMap();

  useEffect(() => {
    let frame;
    const observer = new ResizeObserver(() => {
      cancelAnimationFrame(frame);
      frame = requestAnimationFrame(() =>
        map.invalidateSize({
          pan: false,
        }),
      );
    });
    observer.observe(map.getContainer());

    return () => {
      observer.disconnect();
      cancelAnimationFrame(frame);
    };
  }, [map]);

  return null;
}

export default function MapView({
  verification,
  heatmap,
  filters,
  onFiltersChange,
  navigation,
  predictiveResult,
}) {
  const [heatmapError, setHeatmapError] = useState('');
  const [filtersOpen, setFiltersOpen] = useState(
    () => typeof window === 'undefined' || window.matchMedia('(min-width: 761px)').matches,
  );
  const setFilters = onFiltersChange;

  const layers = useOperationalMarkers(verification, filters, predictiveResult);

  const [showEquipments, setShowEquipments] = useState(true);
  const [showAgents, setShowAgents] = useState(true);

  const { selection, inspection, select } = useMarkerInspection(layers, {
    showEquipments,
    showAgents,
  });

  const [tiles, setTiles] = useState({
    loading: true,
    error: false,
  });

  const tileEvents = useMemo(
    () => ({
      loading: () =>
        setTiles({
          loading: true,
          error: false,
        }),
      tileerror: () =>
        setTiles((current) => ({
          ...current,
          error: true,
        })),
      load: () =>
        setTiles((current) => ({
          ...current,
          loading: false,
        })),
    }),
    [],
  );

  return (
    <OperationalNavigation.Provider value={navigation}>
      <div className={`map-view ${heatmap.enabled ? 'heatmap-is-enabled' : ''}`}>
        <header className='map-panel-heading'>
          <div>
            <h2 id='map-heading'>Mapa operacional</h2>
            <p>Recursos urbanos monitorados</p>
          </div>
          <div
            className='map-view-switch'
            role='group'
            aria-label='Visualização do mapa'
          >
            <button
              aria-pressed={!heatmap.enabled}
              onClick={() => heatmap.changeEnabled(false)}
            >
              <DashboardIcon name='map' />
              Operacional
            </button>
            <button
              aria-pressed={heatmap.enabled}
              onClick={() => heatmap.changeEnabled(true)}
            >
              <DashboardIcon name='heat' />
              Heat Map
            </button>
          </div>
        </header>
        <details
          className='map-filter-panel'
          open={filtersOpen}
          onToggle={(event) => setFiltersOpen(event.currentTarget.open)}
        >
          <summary>
            <DashboardIcon name='filter' />
            Filtros operacionais
          </summary>
          <OperationalFilters
            filters={filters}
            onChange={setFilters}
            onClear={() => setFilters(createEmptyFilters())}
            neighborhoodOptions={getNeighborhoodOptions(
              verification.results
                .filter((item) => ['equipments', 'agents'].includes(item.id))
                .flatMap((item) => item.items || []),
            )}
          />
        </details>
        <MapLayersControl
          layers={layers}
          showEquipments={showEquipments}
          showAgents={showAgents}
          onEquipmentsChange={setShowEquipments}
          onAgentsChange={setShowAgents}
          loading={verification.loading}
          error={verification.error}
          heatmap={heatmap}
        />
        <HeatmapControls
          heatmap={heatmap}
          layerError={heatmapError}
        />
        <p
          className='map-instructions'
          id='map-instructions'
        >
          Arraste para navegar. Use os controles + e − para aproximar ou afastar.
        </p>
        <details className='marker-type-legend'>
          <summary>Tipos e manutenção no mapa</summary>
          <ul>
            {Object.entries(equipmentTypes).map(([type, label]) => (
              <li key={type}>
                <span
                  aria-hidden='true'
                  dangerouslySetInnerHTML={{
                    __html: `<svg viewBox="0 0 24 24">${markerSymbols[type]}</svg>`,
                  }}
                />
                {label}
              </li>
            ))}
          </ul>
          <p>
            Equipamentos: quadrados. Agentes: círculos com símbolo da categoria. A cor indica a
            situação. ! indica manutenção imediata; ◷ indica previsão vencida ou nos próximos{' '}
            {MAINTENANCE_LOOKAHEAD_DAYS} dias.
          </p>
        </details>
        <div
          className='map-region'
          role='region'
          aria-label='Mapa interativo de São Paulo'
          aria-describedby='map-instructions'
        >
          <p className='map-instructions'>
            P: risco preditivo alto, independente da cor de situação. Círculo tracejado: bairro com
            índice alto/crítico.{' '}
            {predictiveResult?.data
              ? `Avaliação: ${new Date(predictiveResult.data.evaluatedAt).toLocaleString('pt-BR')}.`
              : 'Consulta preditiva ainda indisponível.'}
            {(predictiveResult?.stale || predictiveResult?.error) &&
              ' Leitura preditiva desatualizada; confira antes de agir.'}
          </p>
          <MapContainer
            className='operational-map'
            center={saoPauloCenter}
            zoom={12}
            zoomControl
            scrollWheelZoom
          >
            <TileLayer
              url='https://tile.openstreetmap.org/{z}/{x}/{y}.png'
              referrerPolicy='strict-origin-when-cross-origin'
              attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
              maxZoom={19}
              keepBuffer={0}
              updateWhenIdle
              eventHandlers={tileEvents}
            />
            <MapResizeObserver />
            {heatmap.enabled && heatmap.data && (
              <HeatmapLayer
                points={heatmap.data.heatPoints}
                max={heatmap.data.renderMax}
                onError={setHeatmapError}
              />
            )}
            {showEquipments && (
              <EquipmentMarkers
                markers={layers.equipments.filtered}
                onSelect={select}
                selection={selection}
              />
            )}
            <NeighborhoodRiskMarkers
              result={predictiveResult}
              onNeighborhood={navigation?.neighborhood}
            />
            {showAgents && (
              <AgentMarkers
                markers={layers.agents.filtered}
                onSelect={select}
                selection={selection}
              />
            )}
          </MapContainer>
        </div>
        <div
          className='map-status'
          aria-live='polite'
        >
          {tiles.error ? (
            <p role='alert'>
              Não foi possível carregar parte do mapa base. Confira sua conexão com o OpenStreetMap.
            </p>
          ) : tiles.loading ? (
            <p role='status'>Carregando mapa base…</p>
          ) : (
            <p>Mapa base OpenStreetMap</p>
          )}
        </div>
        <RecordInspection
          inspection={inspection}
          loading={verification.loading}
          onClose={() => select(null)}
        />
      </div>
    </OperationalNavigation.Provider>
  );
}
