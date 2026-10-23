// Bibliotecas externas
import { divIcon } from 'leaflet';
import { markerPresentation } from './markerPresentation.js';

const icons = new Map();
export function getMarkerIcon(kind, state, type, attention = '') {
  const presentation = markerPresentation(kind, type, state, attention);
  const key = presentation.key;
  if (!icons.has(key)) {
    // HTML estático e classes vindas exclusivamente das tabelas de estados locais.
    icons.set(key, divIcon({
      className: presentation.className,
      html: presentation.html,
      iconSize: [32, 32],
      iconAnchor: [16, 16],
      popupAnchor: [0, -19]
    }));
  }

  return icons.get(key);
}
