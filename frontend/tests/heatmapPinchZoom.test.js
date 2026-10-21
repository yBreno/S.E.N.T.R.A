// Bibliotecas externas
import test from 'node:test';
import assert from 'node:assert/strict';

// Serviços e utilitários
import { attachHeatmapPinchZoom } from '../src/components/map/heatmapPinchZoom.js';

function point(x, y) {
  return {
    x,
    y,
    add: other => point(x + other.x, y + other.y),
    subtract: other => point(x - other.x, y - other.y),
    divideBy: value => point(x / value, y / value),
    round: () => point(Math.round(x), Math.round(y))
  };
}

function fixture() {
  const state = { zoom: 4, center: point(100, 60), pane: point(27, -13) };
  const size = point(400, 300);
  const position = point(-27, 13);
  const listeners = new Map();
  const transforms = [];
  const canvas = { style: { transformOrigin: '50% 50%' } };
  let resets = 0;

  function project(coordinate, zoom) {
    return point(coordinate.x * 2 ** zoom, coordinate.y * 2 ** zoom);
  }

  function origin(center = state.center, zoom = state.zoom) {
    return project(center, zoom).subtract(size.divideBy(2)).add(state.pane).round();
  }

  const map = {
    getZoom: () => state.zoom,
    getCenter: () => state.center,
    getSize: () => size,
    getZoomScale: (to, from) => 2 ** (to - from),
    project,
    layerPointToLatLng: value => value.add(origin()).divideBy(2 ** state.zoom),
    containerPointToLayerPoint: () => point(-state.pane.x, -state.pane.y),
    on: events => {
      for (const [name, callback] of Object.entries(events)) {
        if (!listeners.has(name)) listeners.set(name, new Set());
        listeners.get(name).add(callback);
      }
    },
    off: events => {
      for (const [name, callback] of Object.entries(events)) listeners.get(name)?.delete(callback);
    }
  };
  const fire = (name, event = {}) => {
    for (const callback of listeners.get(name) || []) callback(event);
  };
  const domUtil = {
    getPosition: element => { assert.equal(element, canvas); return position; },
    setTransform: (element, offset, scale) => {
      assert.equal(element, canvas);
      transforms.push({ offset, scale });
    }
  };
  map.on({ moveend: () => { resets++; } });

  return {
    state, map, canvas, domUtil, transforms, fire, project, origin,
    resets: () => resets,
    listenerCount: () => [...listeners.values()].reduce((total, entries) => total + entries.size, 0)
  };
}

function close(actual, expected) {
  assert.ok(Math.abs(actual - expected) < 1e-8);
}

for (const delta of [0.6, -0.7]) {
  test(`pinça ${delta > 0 ? 'aproxima' : 'afasta'} e desloca o bitmap sem redesenhar os pontos`, () => {
    const context = fixture();
    const { map, state, canvas, domUtil, transforms, fire, project, origin } = context;
    const coordinate = point(103, 65);
    const baselineZoom = state.zoom;
    const baselineOrigin = map.layerPointToLatLng(domUtil.getPosition(canvas));
    const bitmapPoint = project(coordinate, baselineZoom).subtract(project(baselineOrigin, baselineZoom));
    const cleanup = attachHeatmapPinchZoom(map, canvas, domUtil);

    fire('zoomstart');
    for (const fraction of [0.25, 0.5, 1]) {
      state.zoom = baselineZoom + delta * fraction;
      state.center = point(100 + fraction * 2, 60 - fraction * 3);
      fire('zoom', { pinch: true });
      const { offset, scale } = transforms.at(-1);
      const expected = project(coordinate, state.zoom).subtract(origin());
      close(offset.x + bitmapPoint.x * scale, expected.x);
      close(offset.y + bitmapPoint.y * scale, expected.y);
    }

    assert.equal(transforms.length, 3);
    assert.equal(context.resets(), 0);
    assert.equal(canvas.style.transformOrigin, '0 0');
    fire('moveend');
    assert.equal(context.resets(), 1);
    assert.equal(canvas.style.transformOrigin, '50% 50%');
    cleanup();
  });
}

test('encaixe final de zoom usa o bitmap inicial e o centro de destino, sem saltar para escala relativa ao último frame', () => {
  const context = fixture();
  const { map, state, canvas, domUtil, transforms, fire, project, origin } = context;
  const baselineOrigin = map.layerPointToLatLng(domUtil.getPosition(canvas));
  const cleanup = attachHeatmapPinchZoom(map, canvas, domUtil);
  fire('zoomstart');
  state.zoom = 4.6;
  state.center = point(102, 59);
  fire('zoom', { pinch: true });
  const destination = { center: point(104, 57), zoom: 5 };
  fire('zoomanim', destination);
  const { offset, scale } = transforms.at(-1);
  const expected = project(baselineOrigin, destination.zoom).subtract(origin(destination.center, destination.zoom));

  assert.equal(scale, 2);
  close(offset.x, expected.x);
  close(offset.y, expected.y);
  cleanup();
});

test('zoom convencional e pan permanecem sob controle nativo do plugin', () => {
  const { map, canvas, domUtil, transforms, fire } = fixture();
  const cleanup = attachHeatmapPinchZoom(map, canvas, domUtil);
  fire('zoomstart');
  fire('zoom');
  fire('zoomanim', { center: point(100, 60), zoom: 5 });
  fire('move');

  assert.equal(transforms.length, 0);
  assert.equal(canvas.style.transformOrigin, '50% 50%');
  cleanup();
});

test('remover e remontar a camada limpa listeners e estado do gesto, compatível com StrictMode', () => {
  const { map, canvas, domUtil, transforms, fire, listenerCount } = fixture();
  const initialListeners = listenerCount();
  for (let cycle = 0; cycle < 3; cycle++) {
    const cleanup = attachHeatmapPinchZoom(map, canvas, domUtil);
    assert.equal(listenerCount(), initialListeners + 4);
    fire('zoomstart');
    fire('zoom', { pinch: true });
    cleanup();
    assert.equal(listenerCount(), initialListeners);
    assert.equal(canvas.style.transformOrigin, '50% 50%');
    const count = transforms.length;
    fire('zoom', { pinch: true });
    fire('zoomanim', { center: point(100, 60), zoom: 5 });
    assert.equal(transforms.length, count);
  }
});

test('eventos de gesto sem referência inicial não transformam um bitmap desconhecido', () => {
  const { map, canvas, domUtil, transforms, fire } = fixture();
  const cleanup = attachHeatmapPinchZoom(map, canvas, domUtil);
  fire('zoom', { pinch: true });

  assert.equal(transforms.length, 0);
  cleanup();
});
