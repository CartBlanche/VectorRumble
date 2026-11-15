// Where a ship can be in a level: the same rule the game's NavGrid uses, at a finer grid. A spot is open when a ship's centre
// can be there: inside the safe area (0.05..0.95 of the screen, the edge bounces ships back) with the ship's clearance to spare,
// and at least that far from every wall.
import { SCREEN_W, SCREEN_H, pointSegmentDistance } from '../geometry.js';

export const SAFE = 0.05;          // WorldBase.SafeDimensions: 5% in from each side
export const SHIP_CLEARANCE = 30;  // px: World builds NavGrid with 30
export const BIGGEST_SHIP_GAP = 52; // px: the widest shipped ship (radius 20 plus a little) needs this to pass; a community radius tops out at 24
export const AI_GAP = 60;          // px: two clearances; narrower than this the pilots' route finder will not go through

export function buildGrid(walls, { cell = 10, clearance = SHIP_CLEARANCE } = {}) {
  const px = walls.map(([ax, ay, bx, by]) => [ax * SCREEN_W, ay * SCREEN_H, bx * SCREEN_W, by * SCREEN_H]);
  const columns = Math.ceil(SCREEN_W / cell);
  const rows = Math.ceil(SCREEN_H / cell);
  const left = SCREEN_W * SAFE, right = SCREEN_W * (1 - SAFE), top = SCREEN_H * SAFE, bottom = SCREEN_H * (1 - SAFE);
  const open = new Uint8Array(columns * rows);
  let inside = 0;

  for (let y = 0; y < rows; y++) {
    for (let x = 0; x < columns; x++) {
      const cx = (x + 0.5) * cell, cy = (y + 0.5) * cell;
      if (cx - clearance < left || cx + clearance > right || cy - clearance < top || cy + clearance > bottom) continue;
      inside++;
      let clear = true;
      for (const [ax, ay, bx, by] of px) {
        if (pointSegmentDistance(cx, cy, ax, ay, bx, by) < clearance) { clear = false; break; }
      }
      if (clear) open[y * columns + x] = 1;
    }
  }

  // Connected regions of open cells (8 neighbours: a diagonal squeeze is a squeeze, but the cells are small)
  const region = new Int32Array(open.length).fill(-1);
  const sizes = [];
  const stack = [];
  for (let start = 0; start < open.length; start++) {
    if (!open[start] || region[start] >= 0) continue;
    const id = sizes.length;
    let size = 0;
    region[start] = id;
    stack.push(start);
    while (stack.length) {
      const c = stack.pop();
      size++;
      const cx = c % columns, cy = (c / columns) | 0;
      for (let dy = -1; dy <= 1; dy++) {
        for (let dx = -1; dx <= 1; dx++) {
          const nx = cx + dx, ny = cy + dy;
          if (nx < 0 || ny < 0 || nx >= columns || ny >= rows) continue;
          const n = ny * columns + nx;
          if (open[n] && region[n] < 0) { region[n] = id; stack.push(n); }
        }
      }
    }
    sizes.push(size);
  }

  // Where each region is (the first cell found, as normalised x, y), for pointing at it
  const spots = sizes.map(() => null);
  for (let c = 0; c < region.length; c++) {
    const id = region[c];
    if (id >= 0 && !spots[id]) spots[id] = [((c % columns) + 0.5) * cell / SCREEN_W, (((c / columns) | 0) + 0.5) * cell / SCREEN_H];
  }

  const total = sizes.reduce((a, b) => a + b, 0);
  const main = sizes.length ? sizes.indexOf(Math.max(...sizes)) : -1;
  return { cell, columns, rows, open, region, sizes, spots, main, openCells: total, insideCells: inside };
}
