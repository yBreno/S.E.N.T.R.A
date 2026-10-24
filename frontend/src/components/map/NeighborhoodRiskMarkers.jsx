import { CircleMarker, Tooltip } from 'react-leaflet';
import { normalizeCoordinates } from './markerData.js';
import { riskLabels } from '../../services/predictive.js';

export default function NeighborhoodRiskMarkers({ result, onNeighborhood }) {
  return (result?.data?.neighborhoods ?? []).filter(item => ['High', 'Critical'].includes(item.level)).map(item => {
    const center = normalizeCoordinates(item.latitude, item.longitude);
    return center ? <CircleMarker key={item.neighborhood} center={center} radius={22} className="predictive-neighborhood-marker"
      pathOptions={{ color: '#f5cb00', weight: 2, dashArray: '4 4', fillOpacity: .12 }}
      eventHandlers={{ click: () => onNeighborhood?.(item.neighborhood) }}>
      <Tooltip>{item.neighborhood} · Índice {riskLabels[item.level]}: {item.score}/100 · {new Date(item.evaluatedAt).toLocaleString('pt-BR')}{result.stale || result.error ? ' · Leitura desatualizada' : ''} · Não é probabilidade</Tooltip>
    </CircleMarker> : null;
  });
}
