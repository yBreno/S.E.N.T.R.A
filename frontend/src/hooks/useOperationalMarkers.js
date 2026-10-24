// Bibliotecas externas
import { useMemo } from 'react';

// Serviços e utilitários
import { buildOperationalLayers } from '../components/map/operationalFilters.js';
import { attachPredictiveRisk } from '../services/predictive.js';

export function useOperationalMarkers(verification, filters, predictiveResult) {
  return useMemo(() => buildOperationalLayers(verification.results.map(result => result.id === 'equipments' && result.items ?
    { ...result, items: attachPredictiveRisk(result.items, predictiveResult) } : result), filters), [verification.results, filters, predictiveResult]);
}
