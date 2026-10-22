// Bibliotecas externas
import { useEffect, useRef } from 'react';
import { useMap } from 'react-leaflet';

// Serviços e utilitários
import { loadHeatmapFactory } from './leafletHeatAdapter.js';
import { attachGeographicHeatmap } from './geographicHeatmap.js';

const options = {
  minOpacity: 0,
  gradient: {
    0.25: '#3498db',
    0.5: '#2ecc71',
    0.75: '#f1c40f',
    1: '#e74c3c'
  }
};
export default function HeatmapLayer({
  points,
  max,
  onError
}) {
  const map = useMap();

  const layerRef = useRef(null);
  const geographicRef = useRef(null);

  const latest = useRef({
    points,
    max
  });

  useEffect(() => {
    latest.current = {
      points,
      max
    };
    geographicRef.current?.updatePoints(points);
    layerRef.current?.setOptions({
      max
    });
    layerRef.current?.setLatLngs(points);
  }, [points, max]);

  useEffect(() => {
    let cancelled = false;
    let layer;
    let geographic;
    loadHeatmapFactory().then(createLayer => {
      if (cancelled) return;
      layer = createLayer(latest.current.points, {
        ...options,
        max: latest.current.max
      });
      geographic = attachGeographicHeatmap(map, layer, latest.current.points);
      layer.addTo(map);
      layerRef.current = layer;
      geographicRef.current = geographic;
      onError('');
    }).catch(error => {
      if (!cancelled) onError(error.message);
    });

    return () => {
      cancelled = true;
      geographic?.remove();
      layer?.remove();
      layerRef.current = null;
      geographicRef.current = null;
    };
  }, [map, onError]);

  return null;
}
