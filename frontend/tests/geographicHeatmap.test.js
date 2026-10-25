// Bibliotecas externas
import test from 'node:test';
import assert from 'node:assert/strict';

// Serviços e utilitários
import {
  heatmapRadiusMeters,
  heatmapScale,
  attachGeographicHeatmap,
} from '../src/components/map/geographicHeatmap.js';
import { boundHeatKernel } from '../src/components/map/boundedHeatKernel.js';

const points = [
  [-23.55, -46.63, 0.5],
  [-23.57, -46.65, 1],
];

test('raio mais blur mantêm 1 km no terreno, do zoom global ao de rua e durante zoom fracionário', () => {
  for (const latitude of [0, -23.56, -22.21, 60]) {
    for (const zoom of [0, 8, 10, 12, 12.6, 15, 19]) {
      const scale = heatmapScale([[latitude, 0, 1]], zoom);
      const resolution =
        (2 * Math.PI * 6371000 * Math.cos((latitude * Math.PI) / 180)) / (256 * 2 ** zoom);
      assert.ok(Math.abs((scale.radius + scale.blur) * resolution - 1000) < 1e-8);
      assert.equal(scale.maxZoom, 0);
    }
  }
  const initial = heatmapScale(points, 12);
  const next = heatmapScale(points, 13);
  assert.equal(next.radius, initial.radius * 2);
  assert.equal(next.blur, initial.blur * 2);
  assert.equal(heatmapRadiusMeters, 1000);
});

test('intensidade não muda a distância e latitudes vazias ou no limite da projeção não geram NaN', () => {
  assert.deepEqual(
    heatmapScale(points, 12),
    heatmapScale(
      points.map(([lat, lng]) => [lat, lng, 100]),
      12,
    ),
  );
  for (const input of [[], [[90, 0, 1]], [[-90, 0, 1]]]) {
    const scale = heatmapScale(input, 19);
    assert.ok(Number.isFinite(scale.radius) && scale.radius > 0);
    assert.ok(Number.isFinite(scale.blur) && scale.blur > 0);
  }
});

test('zoom atualiza somente opções da camada; pan não muda o raio nem consulta a API', () => {
  const listeners = new Map();
  const calls = [];
  let zoom = 12;
  const map = {
    getZoom: () => zoom,
    on: (event, callback) => listeners.set(event, callback),
    off: (event, callback) => {
      if (listeners.get(event) === callback) listeners.delete(event);
    },
  };
  const layer = { setOptions: (options) => calls.push(options) };
  const input = structuredClone(points);
  for (let mount = 0; mount < 3; mount++) {
    const control = attachGeographicHeatmap(map, layer, input);
    assert.equal(listeners.size, 1);
    assert.ok(listeners.has('zoomend'));
    const count = calls.length;
    listeners.get('zoomend')();
    assert.equal(calls.length, count);
    zoom++;
    listeners.get('zoomend')();
    assert.equal(calls.length, count + 1);
    control.updatePoints(input.map(([lat, lng]) => [lat, lng, 0.1]));
    assert.equal(calls.length, count + 1);
    control.updatePoints([[40, 0, 0.5]]);
    assert.equal(calls.length, count + 2);
    control.remove();
    control.remove();
    assert.equal(listeners.size, 0);
  }
  assert.deepEqual(input, points);
});

function rendererFixture() {
  const allocations = [];
  const draws = [];
  const context = {
    clearRect() {},
    globalAlpha: 1,
    drawImage(...args) {
      draws.push({ args, alpha: this.globalAlpha });
    },
    getImageData: () => ({ data: new Uint8ClampedArray(4) }),
    putImageData() {},
  };
  const renderer = {
    _ctx: context,
    _width: 390,
    _height: 500,
    _max: 1,
    _data: [[195, 250, 0.01]],
    _grad: [],
    radius(radius, blur) {
      allocations.push((radius + blur) * 2);
      this._circle = { width: (radius + blur) * 2 };
      this._r = radius + blur;
      return this;
    },
    _colorize() {},
  };
  boundHeatKernel(renderer);

  return { renderer, allocations, draws };
}

test('redimensionamento com canvas temporariamente vazio não lança e volta a desenhar quando há tamanho', () => {
  const { renderer, draws } = rendererFixture();
  let reads = 0;
  renderer._ctx.getImageData = (_x, _y, width, height) => {
    if (!width || !height) throw new Error('IndexSizeError');
    reads++;
    return { data: new Uint8ClampedArray(4) };
  };
  for (const [width, height] of [
    [0, 500],
    [390, 0],
    [0, 0],
  ]) {
    renderer._width = width;
    renderer._height = height;
    assert.doesNotThrow(() => renderer.draw());
  }
  assert.equal(reads, 0);
  assert.equal(draws.length, 0);
  renderer._width = 390;
  renderer._height = 500;
  renderer.draw();
  assert.equal(reads, 1);
  assert.equal(draws.length, 1);
});

test('textura nunca ultrapassa 256 px nem fica vazia, preservando o raio desenhado inclusive no zoom 19', () => {
  const { renderer, allocations, draws } = rendererFixture();
  const radiusMethod = renderer.radius;
  boundHeatKernel(renderer);
  assert.equal(renderer.radius, radiusMethod);
  for (const zoom of [0, 12, 19]) {
    const { radius, blur } = heatmapScale(points, zoom);
    renderer.radius(radius, blur).draw(0);
    assert.ok(allocations.at(-1) <= 256.000001 && allocations.at(-1) >= 8);
    const draw = draws.at(-1);
    assert.equal(draw.args[3], 2 * (radius + blur));
    assert.equal(draw.args[4], 2 * (radius + blur));
    assert.equal(draw.args[1] + draw.args[3] / 2, 195);
    assert.equal(draw.args[2] + draw.args[4] / 2, 250);
    assert.equal(draw.alpha, 0.01);
    const count = allocations.length;
    renderer.radius(radius, blur);
    assert.equal(allocations.length, count);
  }
  assert.ok(draws.at(-1).args[3] > 7000);
});

test('alterar pontos e máximo atualiza o desenho sem alocar outra textura ou impor piso de intensidade', () => {
  const { renderer, allocations, draws } = rendererFixture();
  const { radius, blur } = heatmapScale(points, 12);
  renderer.radius(radius, blur);
  renderer._data = [
    [100, 200, 0.001],
    [120, 220, 0.6],
  ];
  renderer._max = 2;
  renderer.draw(0);
  assert.deepEqual(
    draws.map((draw) => draw.alpha),
    [0.0005, 0.3],
  );
  assert.equal(allocations.length, 1);
  renderer._data = [];
  renderer.draw(0);
  assert.equal(draws.length, 2);
});
