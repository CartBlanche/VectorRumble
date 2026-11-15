// The picture is the game's 1280x720 screen; the viewport maps it to the canvas with zoom and pan.
import { SCREEN_W, SCREEN_H } from '../geometry.js';

export class Viewport {
  constructor() {
    this.width = 800;   // canvas size in CSS pixels
    this.height = 450;
    this.scale = 1;     // CSS pixels per game pixel
    this.x = 0;         // where the game's (0, 0) sits on the canvas
    this.y = 0;
    this.padding = 20;
    this.minScale = 0.2;
    this.maxScale = 12;
  }

  resize(width, height) {
    const fitScale = this.fitScale();
    const wasFit = Math.abs(this.scale - fitScale) < 1e-6 || !this.initialised;
    this.width = width; this.height = height;
    if (wasFit) this.fit();
    this.initialised = true;
  }

  fitScale() {
    return Math.max(0.05, Math.min((this.width - this.padding * 2) / SCREEN_W, (this.height - this.padding * 2) / SCREEN_H));
  }

  fit() {
    this.scale = this.fitScale();
    this.x = (this.width - SCREEN_W * this.scale) / 2;
    this.y = (this.height - SCREEN_H * this.scale) / 2;
    this.minScale = this.scale * 0.5;
  }

  toScreen(gx, gy) { return [this.x + gx * this.scale, this.y + gy * this.scale]; }
  toGame(sx, sy) { return [(sx - this.x) / this.scale, (sy - this.y) / this.scale]; }
  toNormalised(sx, sy) { const [gx, gy] = this.toGame(sx, sy); return [gx / SCREEN_W, gy / SCREEN_H]; }

  zoomAt(sx, sy, factor) {
    const next = Math.min(this.maxScale, Math.max(this.minScale, this.scale * factor));
    const k = next / this.scale;
    this.x = sx - (sx - this.x) * k;
    this.y = sy - (sy - this.y) * k;
    this.scale = next;
  }

  panBy(dx, dy) { this.x += dx; this.y += dy; }
  centreOn(nx, ny) { this.x = this.width / 2 - nx * SCREEN_W * this.scale; this.y = this.height / 2 - ny * SCREEN_H * this.scale; }
}
