// The ship canvas: ship-local pixels (the ship's centre is (0, 0), nose toward -y) mapped to the canvas with zoom and pan.
export const SHOW = 30; // px each side of the centre that fit on screen at the start

export class ShipView {
  constructor() {
    this.width = 600; this.height = 600;
    this.scale = 10; this.x = 300; this.y = 300;   // where the ship's (0, 0) sits on the canvas
    this.minScale = 3; this.maxScale = 80;
  }

  fitScale() { return Math.max(1, Math.min(this.width, this.height) / (SHOW * 2 + 6)); }

  resize(width, height) {
    const wasFit = Math.abs(this.scale - this.fitScale()) < 1e-6 || !this.initialised;
    this.width = width; this.height = height;
    if (wasFit) this.fit();
    this.initialised = true;
  }

  fit() {
    this.scale = this.fitScale();
    this.x = this.width / 2; this.y = this.height / 2;
    this.minScale = this.scale * 0.5;
    this.maxScale = this.scale * 8;
  }

  toScreen(px, py) { return [this.x + px * this.scale, this.y + py * this.scale]; }
  toLocal(sx, sy) { return [(sx - this.x) / this.scale, (sy - this.y) / this.scale]; }

  zoomAt(sx, sy, factor) {
    const next = Math.min(this.maxScale, Math.max(this.minScale, this.scale * factor));
    const k = next / this.scale;
    this.x = sx - (sx - this.x) * k;
    this.y = sy - (sy - this.y) * k;
    this.scale = next;
  }

  panBy(dx, dy) { this.x += dx; this.y += dy; }
}
