// Starting points for a new level, and the shapes the Shapes tool stamps. Everything is built in pixels of the game's 1280x720
// picture and stored as fractions of it.
import { SCREEN_W, SCREEN_H } from '../geometry.js';
import { frameWalls } from '../model/level.js';

const norm = ([ax, ay, bx, by]) => [ax / SCREEN_W, ay / SCREEN_H, bx / SCREEN_W, by / SCREEN_H].map((v) => Math.round(v * 1000) / 1000);
const px = (...segments) => segments.map(norm);

// A closed outline from points in px
const outline = (points) => points.map((p, i) => [...p, ...points[(i + 1) % points.length]]);
const box = (cx, cy, hw, hh) => outline([[cx - hw, cy - hh], [cx + hw, cy - hh], [cx + hw, cy + hh], [cx - hw, cy + hh]]);

const C = [SCREEN_W / 2, SCREEN_H / 2];

export const TEMPLATES = [
  { id: 'frame', name: 'Empty frame', description: 'Just the four outer walls. Open space for chasing.', walls: () => frameWalls() },
  {
    id: 'cross', name: 'Cross', description: 'Four arms leave the middle open, so fights circle round it.',
    walls: () => [...frameWalls(), ...px(
      [C[0] - 250, C[1], C[0] - 80, C[1]], [C[0] + 80, C[1], C[0] + 250, C[1]],
      [C[0], C[1] - 140, C[0], C[1] - 70], [C[0], C[1] + 70, C[0], C[1] + 140])],
  },
  {
    id: 'pillars', name: 'Four pillars', description: 'A small square pillar in each quarter to hide behind.',
    walls: () => [...frameWalls(), ...[[-240, -135], [240, -135], [-240, 135], [240, 135]].flatMap(([dx, dy]) => px(...box(C[0] + dx, C[1] + dy, 25, 25)))],
  },
  {
    id: 'diamond', name: 'Diamond', description: 'An open diamond in the middle; its corners are gaps ships fly through.',
    walls: () => {
      // Four sides, each shortened at both ends: a closed diamond would be a room a ship could spawn in
      const corners = [[C[0], C[1] - 170], [C[0] + 260, C[1]], [C[0], C[1] + 170], [C[0] - 260, C[1]]];
      return [...frameWalls(), ...px(...corners.map(([x1, y1], i) => {
        const [x2, y2] = corners[(i + 1) % 4];
        const length = Math.hypot(x2 - x1, y2 - y1), cut = 70 / length;
        return [x1 + (x2 - x1) * cut, y1 + (y2 - y1) * cut, x2 - (x2 - x1) * cut, y2 - (y2 - y1) * cut];
      }))];
    },
  },
  {
    id: 'lanes', name: 'Lanes', description: 'Staggered walls make lanes and corners. Made to be mirrored.',
    walls: () => [...frameWalls(), ...px(
      [260, 36 + 140, 260, 36 + 400], [1020, 720 - 36 - 140, 1020, 720 - 36 - 400],
      [460, 150, 820, 150], [460, 570, 820, 570],
      [C[0], 270, C[0], 450])],
  },
];

// The arenas that come with the game, to remix (the game's own walls)
export const SHIPPED = [
  { id: 'one', name: 'One (remix)', description: 'A bar across the middle.', walls: () => [...frameWalls(), [0.275, 0.5, 0.725, 0.5]] },
  { id: 'two', name: 'Two (remix)', description: 'Two upright bars.', walls: () => [...frameWalls(), [0.275, 0.275, 0.275, 0.725], [0.72, 0.275, 0.72, 0.725]] },
  { id: 'three', name: 'Three (remix)', description: 'A bar and two uprights.', walls: () => [...frameWalls(), [0.275, 0.5, 0.725, 0.5], [0.275, 0.275, 0.275, 0.725], [0.725, 0.275, 0.725, 0.725]] },
];

// ---- Shapes tool: a drag from a to b (normalised) stamps one of these ----

export const SHAPES = [
  { id: 'rect', label: 'Rectangle', hint: 'Drag a corner to the opposite corner' },
  { id: 'poly', label: 'Polygon', hint: 'Drag from the middle outwards; set the sides' },
  { id: 'ring', label: 'Ring', hint: 'A polygon inside a polygon' },
  { id: 'cross', label: 'Cross', hint: 'Two lines through the middle of the box you drag' },
  { id: 'pillars', label: 'Pillars', hint: 'A row of small square pillars along the line you drag' },
];

const ellipsePoints = (cx, cy, rx, ry, sides) => Array.from({ length: sides }, (_, i) => {
  const t = -Math.PI / 2 + (i / sides) * Math.PI * 2;
  return [cx + Math.cos(t) * rx, cy + Math.sin(t) * ry];
});

export const PILLAR_HALF = 18;   // px: a pillar is 36 px across
export const PILLAR_STEP = 120;  // px between pillar centres

export function shapeWalls(id, a, b, { sides = 6 } = {}) {
  const [ax, ay, bx, by] = [a[0] * SCREEN_W, a[1] * SCREEN_H, b[0] * SCREEN_W, b[1] * SCREEN_H];
  const cx = (ax + bx) / 2, cy = (ay + by) / 2;
  const hw = Math.abs(bx - ax) / 2, hh = Math.abs(by - ay) / 2;
  switch (id) {
    case 'rect':
      return hw < 1 || hh < 1 ? [] : px(...box(cx, cy, hw, hh));
    case 'poly': {
      const r = Math.hypot(bx - ax, by - ay);
      return r < 4 ? [] : px(...outline(ellipsePoints(ax, ay, r, r, sides)));
    }
    case 'ring': {
      const r = Math.hypot(bx - ax, by - ay);
      return r < 4 ? [] : px(...outline(ellipsePoints(ax, ay, r, r, sides)), ...outline(ellipsePoints(ax, ay, r * 0.55, r * 0.55, sides)));
    }
    case 'cross':
      return hw < 1 && hh < 1 ? [] : px([cx - hw, cy, cx + hw, cy], [cx, cy - hh, cx, cy + hh]);
    case 'pillars': {
      const length = Math.hypot(bx - ax, by - ay);
      const count = Math.max(1, Math.round(length / PILLAR_STEP) + 1);
      if (length < PILLAR_HALF) return [];
      const out = [];
      for (let i = 0; i < count; i++) {
        const t = count === 1 ? 0.5 : i / (count - 1);
        out.push(...px(...box(ax + (bx - ax) * t, ay + (by - ay) * t, PILLAR_HALF, PILLAR_HALF)));
      }
      return out;
    }
    default:
      return [];
  }
}
