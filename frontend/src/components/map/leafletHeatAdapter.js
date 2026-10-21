// Bibliotecas externas
import L from 'leaflet';

let pluginPromise;
export function loadHeatmapFactory() {
  if (!pluginPromise) {
    // O plugin 0.2.0 consulta L também durante redraw/zoom, não apenas no import.
    // Mantém o alias necessário, apontando para a mesma instância usada pelo React Leaflet.
    if (window.L && window.L !== L) return Promise.reject(new Error('Há outra instância Leaflet global incompatível.'));
    window.L = L;
    pluginPromise = import('leaflet.heat').then(plugin => {
      // O bundle CommonJS exporta simpleheat em vez de criar o global que ele mesmo usa.
      if (typeof window.simpleheat !== 'function' && typeof plugin.default === 'function') window.simpleheat = plugin.default;
      if (typeof window.simpleheat !== 'function') throw new Error('Renderizador indisponível.');
      if (typeof L.heatLayer !== 'function') throw new Error('Plugin indisponível.');

      return (points, options) => {
        const layer = L.heatLayer(points, options);
        if (!(layer instanceof L.Layer)) throw new Error('Instância Leaflet incompatível.');
        const originalRemove = layer.onRemove;
        layer.onRemove = function (map) {
          // O release 0.2.0 não cancela o frame de redraw pendente ao remover.
          if (this._frame) L.Util.cancelAnimFrame(this._frame);
          this._frame = null;

          return originalRemove.call(this, map);
        };

        return layer;
      };
    }).catch(() => {
      pluginPromise = null;
      throw new Error('Não foi possível carregar a camada de calor. Tente desativar e ativar a camada.');
    });
  }

  return pluginPromise;
}
