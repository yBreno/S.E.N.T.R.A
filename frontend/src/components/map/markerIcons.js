import { divIcon } from 'leaflet'

const icons = new Map()
const equipmentSymbol = '<svg viewBox="0 0 24 24" aria-hidden="true"><rect x="5" y="5" width="14" height="11" rx="1"/><path d="M9 20h6m-3-4v4"/></svg>'
const agentSymbol = '<svg viewBox="0 0 24 24" aria-hidden="true"><circle cx="12" cy="8" r="3"/><path d="M5 20v-2a7 7 0 0 1 14 0v2"/></svg>'

export function getMarkerIcon(kind, state) {
  const key = `${kind}:${state.tone}`
  if (!icons.has(key)) {
    // HTML estático e classes vindas exclusivamente das tabelas de estados locais.
    icons.set(key, divIcon({
      className: `operational-marker marker-${kind} state-${state.tone}`,
      html: kind === 'equipment' ? equipmentSymbol : agentSymbol,
      iconSize: [32, 32], iconAnchor: [16, 16], popupAnchor: [0, -19],
    }))
  }
  return icons.get(key)
}
