const adapted = new WeakSet();

export function boundHeatKernel(renderer) {
  if (adapted.has(renderer)) return;
  adapted.add(renderer);
  const originalRadius = renderer.radius;
  let previousRadius;
  let previousBlur;

  // simpleheat embutido no leaflet.heat 0.2.0: limita a textura, não a área projetada.
  renderer.radius = function (radius, blur = 15) {
    if (radius === previousRadius && blur === previousBlur) return this;
    const extent = radius + blur;
    const textureRadius = Math.max(4, Math.min(128, extent));
    const scale = textureRadius / extent;
    originalRadius.call(this, radius * scale, blur * scale);
    this._r = extent;
    previousRadius = radius;
    previousBlur = blur;

    return this;
  };

  renderer.draw = function (minOpacity = 0.05) {
    // Leaflet may briefly resize to zero while its module/layout is being removed.
    if (!(this._width > 0 && this._height > 0)) return this;
    if (!this._circle) this.radius(this.defaultRadius);
    if (!this._grad) this.gradient(this.defaultGradient);
    const context = this._ctx;
    context.clearRect(0, 0, this._width, this._height);
    for (const [x, y, intensity] of this._data) {
      context.globalAlpha = Math.min(1, Math.max(intensity / this._max, minOpacity));
      context.drawImage(this._circle, x - this._r, y - this._r, this._r * 2, this._r * 2);
    }
    const pixels = context.getImageData(0, 0, this._width, this._height);
    this._colorize(pixels.data, this._grad);
    context.putImageData(pixels, 0, 0);

    return this;
  };
}
