// Bibliotecas externas
import { useState } from 'react';

// Serviços e utilitários
import { resolveMarkerSelection } from '../components/map/operationalFilters.js';

export function useMarkerInspection(layers, visibility) {
  const [selection, setSelection] = useState(null);
  const inspection = resolveMarkerSelection(selection, layers, visibility);
  // Guarda apenas a identidade, sem copiar o registro. Remove identidades que saíram
  // da camada para que limpar os filtros não restaure uma seleção antiga.
  if (selection && !inspection) setSelection(null);

  return {
    selection,
    inspection,
    select: setSelection,
  };
}
