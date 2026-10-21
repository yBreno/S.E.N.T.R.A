// Bibliotecas externas
import { useMemo } from 'react';

// Serviços e utilitários
import { buildOperationalLayers } from '../components/map/operationalFilters.js';

export function useOperationalMarkers(verification, filters) {
  return useMemo(() => buildOperationalLayers(verification.results, filters), [verification.results, filters]);
}
