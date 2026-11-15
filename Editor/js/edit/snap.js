// Snapping a point (normalised 0..1) while drawing or dragging. All the distances are in screen pixels of the game's 1280x720
// picture, because that is what the player sees: an angle of 45 degrees must look like 45 degrees, not 45 in a stretched square.
import { SCREEN_W, SCREEN_H } from '../geometry.js';
import { SAFE } from '../validate/grid.js';

// Grid sizes in game pixels. The grid starts at the playing area's corner (64, 36), and these all divide the half-arena
// (576 x 324 from the corner to the centre), so the frame's corners and the centre are always on the grid.
export const GRID_SIZES = [6, 12, 18, 36];
export const DEFAULT_GRID = 18;
export const ORIGIN = [SCREEN_W * SAFE, SCREEN_H * SAFE];

const clamp01 = (v) => Math.min(1, Math.max(0, v));

export function snapToGrid(px, py, size) {
  return [ORIGIN[0] + Math.round((px - ORIGIN[0]) / size) * size, ORIGIN[1] + Math.round((py - ORIGIN[1]) / size) * size];
}

// Options: grid (bool), gridSize (px), endpoints (bool), tolerance (px), walls, skip (Set of wall indices whose ends do not attract),
// from ([x, y] normalised, the other end of a wall being drawn), angle (0 off, or 45, or 15 degrees).
// Returns { x, y, kind } with kind 'endpoint' | 'angle' | 'grid' | 'none'.
export function snapPoint(nx, ny, o = {}) {
  const px = nx * SCREEN_W;
  const py = ny * SCREEN_H;
  const tolerance = o.tolerance ?? 10;

  if (o.endpoints && o.walls) {
    let best = null;
    let bestD = tolerance;
    o.walls.forEach((w, i) => {
      if (o.skip?.has(i)) return;
      for (const e of [0, 2]) {
        const d = Math.hypot(w[e] * SCREEN_W - px, w[e + 1] * SCREEN_H - py);
        if (d < bestD) { bestD = d; best = [w[e], w[e + 1]]; }
      }
    });
    if (best) return { x: best[0], y: best[1], kind: 'endpoint' };
  }

  if (o.angle && o.from) {
    const fx = o.from[0] * SCREEN_W;
    const fy = o.from[1] * SCREEN_H;
    const length = Math.hypot(px - fx, py - fy);
    if (length > 1) {
      const step = (o.angle * Math.PI) / 180;
      const a = Math.round(Math.atan2(py - fy, px - fx) / step) * step;
      let sx = fx + Math.cos(a) * length;
      let sy = fy + Math.sin(a) * length;
      if (o.grid) {
        const [gx, gy] = snapToGrid(sx, sy, o.gridSize ?? DEFAULT_GRID);
        if (Math.abs(Math.sin(a)) < 1e-6) sx = gx; // horizontal: the free coordinate goes to the grid
        else if (Math.abs(Math.cos(a)) < 1e-6) sy = gy;
      }
      return { x: clamp01(sx / SCREEN_W), y: clamp01(sy / SCREEN_H), kind: 'angle' };
    }
  }

  if (o.grid) {
    const [gx, gy] = snapToGrid(px, py, o.gridSize ?? DEFAULT_GRID);
    return { x: clamp01(gx / SCREEN_W), y: clamp01(gy / SCREEN_H), kind: 'grid' };
  }
  return { x: clamp01(nx), y: clamp01(ny), kind: 'none' };
}
