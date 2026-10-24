export const markerSymbols = Object.freeze({
  Camera: '<rect x="3" y="6" width="12" height="11" rx="2"/><path d="m15 10 6-3v12l-6-3M7 3h4"/>',
  Radar: '<circle cx="12" cy="12" r="8"/><path d="m12 12 5-6M4 12h3m10 0h3m-8 5v3"/>',
  Sensor: '<circle cx="12" cy="12" r="2"/><path d="M7 7a7 7 0 0 0 0 10m10-10a7 7 0 0 1 0 10M4 4a11 11 0 0 0 0 16M20 4a11 11 0 0 1 0 16"/>',
  Vehicle: '<path d="m3 12 2-6h14l2 6v7h-3v-3H6v3H3zM3 12h18M7 14h1m8 0h1"/>',
  ElectricalScooter: '<circle cx="5" cy="19" r="2"/><circle cx="19" cy="19" r="2"/><path d="M5 17h9l4-14h-4m3 3 2 11M9 9l2-5-4 1"/>',
  ParkingMeter: '<rect x="6" y="2" width="12" height="14" rx="2"/><path d="M12 16v6M10 12V6h3a2 2 0 0 1 0 4h-3"/>',
  Motorcycle: '<circle cx="5" cy="17" r="4"/><circle cx="19" cy="17" r="4"/><path d="m5 17 5-8 5 8h4L15 4h-4m-5 5h6"/>',
  AutonomousVehicle: '<rect x="3" y="8" width="18" height="11" rx="3"/><path d="M7 19v3m10-3v3M9 4h6m-3 0v4M7 13h2m6 0h2"/>',
  TrafficAgent: '<circle cx="12" cy="6" r="3"/><path d="M8 22v-8H4m12 8v-8h4M8 10h8v9H8"/>',
  MunicipalGuard: '<path d="m12 2 8 3v6c0 5-8 11-8 11S4 16 4 11V5z"/><circle cx="12" cy="9" r="2"/><path d="M8 16a4 4 0 0 1 8 0"/>',
  PoliceOfficer: '<path d="m12 2 3 5 6 1-4 5 1 7-6-3-6 3 1-7-4-5 6-1z"/>',
  Unknown: '<rect x="4" y="4" width="16" height="16"/><path d="M9 9a3 3 0 0 1 6 0c0 2-3 2-3 4m0 3v1"/>'
});
export function markerPresentation(kind, type, state, attention = '', predictive = false, stale = false) {
  const group = kind === 'equipment' ? 'equipment' : 'agent';
  const known = group === 'equipment' ? ['Camera', 'Radar', 'Sensor', 'Vehicle', 'ElectricalScooter', 'ParkingMeter', 'Motorcycle', 'AutonomousVehicle'] : ['TrafficAgent', 'MunicipalGuard', 'PoliceOfficer'];
  const name = known.includes(type) ? type : 'Unknown';
  const tone = ['operational', 'malfunctioning', 'maintenance', 'deactivated', 'stolen', 'not-installed', 'available', 'busy', 'offline'].includes(state?.tone) ? state.tone : 'unknown';
  const badge = ['immediate', 'upcoming'].includes(attention) ? attention : '';
  const prediction = group === 'equipment' && predictive === true;
  return { key: `${group}:${name}:${tone}:${badge}${prediction ? stale ? ':predictive-stale' : ':predictive' : ''}`, className: `operational-marker marker-${group} type-${name} state-${tone}${badge ? ` attention-${badge}` : ''}${prediction ? ` predictive-high${stale ? ' predictive-stale' : ''}` : ''}`, html: `<svg viewBox="0 0 24 24" aria-hidden="true">${markerSymbols[name]}</svg>${prediction ? '<span class="predictive-marker-badge" aria-hidden="true">P</span>' : ''}` };
}
