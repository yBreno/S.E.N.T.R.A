// Serviços e utilitários
import { http, invalidResponse } from './http.js';
import { normalizeCoordinates } from '../components/map/markerData.js';
import { heatmapMetrics } from './heatmapQuery.js';

export function normalizeHeatmapResponse(dto, expectedMetric) {
  if (
    !dto ||
    typeof dto.metric !== 'string' ||
    !Object.hasOwn(heatmapMetrics, dto.metric) ||
    dto.metric !== expectedMetric ||
    !Array.isArray(dto.points) ||
    !Number.isInteger(dto.totalPoints) ||
    dto.totalPoints < 0 ||
    dto.totalPoints !== dto.points.length ||
    typeof dto.maxIntensity !== 'number' ||
    !Number.isFinite(dto.maxIntensity) ||
    dto.maxIntensity < 0 ||
    ![dto.from, dto.to].every(
      (value) => value == null || (typeof value === 'string' && Number.isFinite(Date.parse(value))),
    )
  )
    throw invalidResponse();
  const points = [];
  const heatPoints = [];
  let zeroCount = 0;
  let renderMax = 1;
  for (const point of dto.points) {
    const position = normalizeCoordinates(point?.latitude, point?.longitude);
    const intensity = point?.intensity;
    if (!position || typeof intensity !== 'number' || !Number.isFinite(intensity) || intensity < 0)
      continue;
    points.push({
      latitude: position[0],
      longitude: position[1],
      intensity,
      neighborhood: typeof point.neighborhood === 'string' ? point.neighborhood : null,
      label: typeof point.label === 'string' ? point.label : null,
    });
    if (intensity === 0) zeroCount++;
    else {
      heatPoints.push([position[0], position[1], intensity]);
      renderMax = Math.max(renderMax, intensity);
    }
  }

  return {
    metric: dto.metric,
    from: dto.from ?? null,
    to: dto.to ?? null,
    totalPoints: dto.totalPoints,
    maxIntensity: dto.maxIntensity,
    points,
    heatPoints,
    validCount: points.length,
    discardedCount: dto.totalPoints - points.length,
    zeroCount,
    renderMax,
  };
}
export async function getHeatmap(query, { token, signal, client = http } = {}) {
  const data = await client.getJson(query.path, {
    token,
    signal,
  });

  return normalizeHeatmapResponse(data, query.metric);
}
