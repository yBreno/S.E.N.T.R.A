export function attachHeatmapPinchZoom(map, canvas, domUtil) {
  let reference = null;
  let pinching = false;
  const originalOrigin = canvas.style.transformOrigin;

  function start() {
    reference = {
      zoom: map.getZoom(),
      origin: map.layerPointToLatLng(domUtil.getPosition(canvas)),
    };
  }

  function transform(center, zoom) {
    const paneOffset = map.containerPointToLayerPoint([0, 0]);
    const pixelOrigin = map
      .project(center, zoom)
      .subtract(map.getSize().divideBy(2))
      .subtract(paneOffset)
      .round();
    const offset = map.project(reference.origin, zoom).subtract(pixelOrigin);
    const scale = map.getZoomScale(zoom, reference.zoom);

    canvas.style.transformOrigin = '0 0';
    domUtil.setTransform(canvas, offset, scale);
  }

  function zoom(event) {
    if (!event.pinch || !reference) return;
    pinching = true;
    transform(map.getCenter(), map.getZoom());
  }

  function animate(event) {
    // O encaixe final ainda usa a referência do bitmap anterior ao gesto.
    if (pinching && reference) transform(event.center, event.zoom);
  }

  function end() {
    reference = null;
    pinching = false;
    canvas.style.transformOrigin = originalOrigin;
  }

  const events = { zoomstart: start, zoom, zoomanim: animate, moveend: end };
  map.on(events);

  return () => {
    map.off(events);
    end();
  };
}
