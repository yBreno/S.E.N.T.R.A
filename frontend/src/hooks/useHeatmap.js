import { useEffect, useMemo, useState, useSyncExternalStore } from 'react'
import { createHeatmapFilters, buildHeatmapQuery, defaultHeatmapQuery } from '../services/heatmapQuery.js'
import { getHeatmap } from '../services/heatmap.js'
import { createHeatmapResource } from '../services/heatmapResource.js'

export function useHeatmap({ token, onLogout, verification }) {
  const [filters, setFilters] = useState(createHeatmapFilters)
  const [enabled, setEnabled] = useState(false)
  const resource = useMemo(() => createHeatmapResource({
    load: (query, { signal }) => getHeatmap(query, { token, signal }),
    onUnauthorized: () => onLogout('A API recusou a sessão (401). Entre novamente.'),
  }), [token, onLogout])
  const state = useSyncExternalStore(resource.subscribe, resource.getSnapshot)
  const parsed = useMemo(() => buildHeatmapQuery(filters), [filters])
  const sharedResult = verification.results.find((result) => result.id === 'heatmap')
  useEffect(() => {
    resource.setShared({ result: sharedResult, loading: verification.loading, attemptedAt: verification.updatedAt?.toISOString() })
  }, [resource, sharedResult, verification.loading, verification.updatedAt])
  useEffect(() => () => resource.cancel(false), [resource])

  function changeFilters(next) {
    setFilters(next)
    resource.setQuery(buildHeatmapQuery(next).query)
  }
  function changeEnabled(next) {
    setEnabled(next)
    if (!next) resource.cancel()
  }
  return { ...state, filters, enabled, changeEnabled, changeFilters, validationError: parsed.error,
    waitingForShared: parsed.query?.key === defaultHeatmapQuery.key && verification.loading,
    apply: () => resource.request(), refresh: () => resource.request({ force: true }) }
}
