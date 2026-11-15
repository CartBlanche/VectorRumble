// Checks on a level. Warnings only: nothing here stops a save or an export.
// Each result: { id, severity: 'warn' | 'note', message, wall? (index), at? ([x, y] normalised) } for the UI to point at.
import { SCREEN_W, SCREEN_H, segmentsIntersect, segmentSegmentDistance } from '../geometry.js';
import { isColour } from '../io/numbers.js';
import { MAX_WALLS } from '../model/level.js';
import { BUILTIN_LEVELS } from '../model/builtins.js';
import { buildGrid, BIGGEST_SHIP_GAP, AI_GAP } from './grid.js';

const MIN_POSITION = 0.04;
const MAX_POSITION = 0.96;
const MIN_REACHABLE = 0.7; // of the room ships could use if there were no walls, the share in the biggest open area
const TOUCH = 0.5;         // px: segments whose ends are this close are joined, not a gap

const px = ([ax, ay, bx, by]) => [ax * SCREEN_W, ay * SCREEN_H, bx * SCREEN_W, by * SCREEN_H];

// `others` are the names of the other levels in the library
// `grid` is an already built ship-space grid for these walls (buildGrid), to save building it twice
export function checkLevel(level, { others = [], grid: given = null } = {}) {
  const out = [];
  const add = (id, severity, message, extra = {}) => out.push({ id, severity, message, ...extra });
  const walls = level.walls;

  if (!level.name.trim()) add('name-missing', 'warn', 'The arena has no name, so the game cannot list it.');
  else if (BUILTIN_LEVELS.some((n) => n.toLowerCase() === level.name.trim().toLowerCase())) add('name-builtin', 'warn', `"${level.name.trim()}" is the name of an arena that comes with the game. Pick another so they are not confused.`);
  else if (others.includes(level.name.trim())) add('name-clash', 'warn', `Another arena in your library is called "${level.name.trim()}", so their exported files clash.`);
  if (!isColour(level.color)) add('colour', 'warn', 'The colour is not eight hex digits (AARRGGBB), so the game cannot load it.');

  if (walls.length === 0) add('no-walls', 'warn', 'The arena has no walls. It is playable, but it is the Empty arena with nothing in it.');
  if (walls.length > MAX_WALLS) add('too-many', 'warn', `The arena has ${walls.length} walls; more than ${MAX_WALLS} can slow the game and the pilots' route finder.`);

  walls.forEach((w, i) => {
    if (w.some((v) => v < 0 || v > 1)) add('outside', 'warn', `Wall ${i + 1} is outside the screen.`, { wall: i });
    else if (w.some((v) => v < MIN_POSITION || v > MAX_POSITION)) add('near-edge', 'warn', `Wall ${i + 1} is beyond the playing area (ships stay within 5% of the edge).`, { wall: i });
    const [ax, ay, bx, by] = px(w);
    if (Math.hypot(bx - ax, by - ay) < 1) add('zero-length', 'warn', `Wall ${i + 1} is a point with no length.`, { wall: i });
  });

  const seen = new Map();
  walls.forEach((w, i) => {
    const key = [[w[0], w[1]], [w[2], w[3]]].map((p) => p.join(',')).sort().join('|');
    if (seen.has(key)) add('duplicate', 'warn', `Wall ${i + 1} repeats wall ${seen.get(key) + 1}.`, { wall: i });
    else seen.set(key, i);
  });

  // Crossings and gaps: all pairs, which is fine at a few hundred walls
  const limit = Math.min(walls.length, MAX_WALLS * 2);
  // A gap only matters where ships could get to it: the space inside a small closed box is not a passage. So a gap counts when
  // the spot between the two walls is in the main open area of a fine grid (5 px cells, walls kept 4 px clear: enough to seal
  // a thin wall), not shut inside something.
  let fine = null;
  const reachable = (x, y) => {
    fine ??= buildGrid(walls, { cell: 5, clearance: 4 });
    const cx = Math.floor((x * SCREEN_W) / fine.cell), cy = Math.floor((y * SCREEN_H) / fine.cell);
    if (cx < 0 || cy < 0 || cx >= fine.columns || cy >= fine.rows) return false;
    const c = cy * fine.columns + cx;
    return fine.open[c] === 1 && fine.region[c] === fine.main;
  };
  for (let i = 0; i < limit; i++) {
    for (let j = i + 1; j < limit; j++) {
      const [a, b] = [px(walls[i]), px(walls[j])];
      if (segmentsIntersect(...a, ...b)) {
        add('crossing', 'note', `Walls ${i + 1} and ${j + 1} cross. That is allowed, but it leaves small closed corners.`, { wall: i });
        continue;
      }
      const d = segmentSegmentDistance(...a, ...b);
      if (d < TOUCH || d >= AI_GAP) continue; // joined, or a roomy gap
      const at = closestMiddle(a, b);
      if (!reachable(at[0], at[1])) continue;
      if (d < BIGGEST_SHIP_GAP) add('gap-ship', 'warn', `Walls ${i + 1} and ${j + 1} leave a gap of ${Math.round(d)} px, too narrow for the larger ships.`, { wall: i, at });
      else add('gap-ai', 'note', `Walls ${i + 1} and ${j + 1} leave a gap of ${Math.round(d)} px. Ships fit, but the computer's pilots will not route through it.`, { wall: i, at });
    }
  }

  // Where ships can go
  if (walls.length <= MAX_WALLS * 2) {
    const grid = given ?? buildGrid(walls);
    if (grid.sizes.length === 0) {
      add('no-space', 'warn', 'There is no room anywhere for a ship: the walls leave no open space.');
    } else {
      const stranded = grid.sizes.length - 1;
      if (stranded > 0) {
        const small = grid.sizes.map((s, id) => [s, id]).filter(([, id]) => id !== grid.main);
        const at = grid.spots[small[0][1]];
        add('pockets', 'warn', `${stranded === 1 ? 'An area' : `${stranded} areas`} a ship could spawn in cannot be reached from the main space, so a ship placed there would be stuck.`, { at });
      }
      const share = grid.sizes[grid.main] / Math.max(1, grid.insideCells);
      if (share < MIN_REACHABLE) add('cramped', 'warn', `Ships can only use ${Math.round(share * 100)}% of the arena (under ${Math.round(MIN_REACHABLE * 100)}%). It may feel cramped.`);
    }
  }

  return out;
}

// Halfway between the nearest points of two segments (px in, normalised out)
function closestMiddle(a, b) {
  const ends = [[a[0], a[1], b], [a[2], a[3], b], [b[0], b[1], a], [b[2], b[3], a]];
  let best = null;
  for (const [x, y, seg] of ends) {
    const t = Math.max(0, Math.min(1, ((x - seg[0]) * (seg[2] - seg[0]) + (y - seg[1]) * (seg[3] - seg[1])) / (((seg[2] - seg[0]) ** 2 + (seg[3] - seg[1]) ** 2) || 1)));
    const qx = seg[0] + (seg[2] - seg[0]) * t, qy = seg[1] + (seg[3] - seg[1]) * t;
    const d = Math.hypot(qx - x, qy - y);
    if (!best || d < best.d) best = { d, x: (x + qx) / 2, y: (y + qy) / 2 };
  }
  return [best.x / SCREEN_W, best.y / SCREEN_H];
}
