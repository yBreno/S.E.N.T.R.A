export const heatmapRadiusMeters = 1000;

export function heatmapScale(points, zoom) {
  const latitude = points.length
    ? points.reduce((sum, point) => sum + point[0], 0) / points.length
    : 0;
  const projectedLatitude = Math.max(-85.0511287798, Math.min(85.0511287798, latitude));
  const metersPerPixel =
    (2 * Math.PI * 6371000 * Math.cos((projectedLatitude * Math.PI) / 180)) / (256 * 2 ** zoom);
  const extent = heatmapRadiusMeters / metersPerPixel;

  return {
    radius: (extent * 28) / 48,
    blur: (extent * 20) / 48,
    maxZoom: 0,
  };
}

export function attachGeographicHeatmap(map, layer, initialPoints) {
  let points = initialPoints;
  let previous = null;

  function updateScale() {
    const next = heatmapScale(points, map.getZoom());
    if (!previous || previous.radius !== next.radius || previous.blur !== next.blur) {
      layer.setOptions(next);
      previous = next;
    }
  }

  updateScale();
  map.on('zoomend', updateScale);

  return {
    updatePoints(next) {
      points = next;
      updateScale();
    },
    remove() {
      map.off('zoomend', updateScale);
    },
  };
}
