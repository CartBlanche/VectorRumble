// Group transforms on walls, done in screen pixels so a quarter turn looks like one.
import { SCREEN_W, SCREEN_H } from '../geometry.js';

const round = (v) => Math.round(v * 1000) / 1000;

// The middle of the walls' bounding box, in pixels
export function centreOf(walls) {
  if (!walls.length) return [SCREEN_W / 2, SCREEN_H / 2];
  const xs = walls.flatMap((w) => [w[0], w[2]]);
  const ys = walls.flatMap((w) => [w[1], w[3]]);
  return [((Math.min(...xs) + Math.max(...xs)) / 2) * SCREEN_W, ((Math.min(...ys) + Math.max(...ys)) / 2) * SCREEN_H];
}

// Applies fn([x, y] in px) -> [x, y] in px to every end of the walls
function mapEnds(walls, fn) {
  return walls.map(([ax, ay, bx, by]) => {
    const [a1, a2] = fn([ax * SCREEN_W, ay * SCREEN_H]);
    const [b1, b2] = fn([bx * SCREEN_W, by * SCREEN_H]);
    return [round(a1 / SCREEN_W), round(a2 / SCREEN_H), round(b1 / SCREEN_W), round(b2 / SCREEN_H)];
  });
}

export const rotate = (walls, degrees, pivot = centreOf(walls)) => {
  const t = (degrees * Math.PI) / 180;
  const c = Math.cos(t), s = Math.sin(t);
  return mapEnds(walls, ([x, y]) => [pivot[0] + (x - pivot[0]) * c - (y - pivot[1]) * s, pivot[1] + (x - pivot[0]) * s + (y - pivot[1]) * c]);
};
export const scale = (walls, factor, pivot = centreOf(walls)) => mapEnds(walls, ([x, y]) => [pivot[0] + (x - pivot[0]) * factor, pivot[1] + (y - pivot[1]) * factor]);
export const mirrorLeftRight = (walls, pivot = centreOf(walls)) => mapEnds(walls, ([x, y]) => [2 * pivot[0] - x, y]);
export const mirrorTopBottom = (walls, pivot = centreOf(walls)) => mapEnds(walls, ([x, y]) => [x, 2 * pivot[1] - y]);
export const move = (walls, dx, dy) => walls.map(([ax, ay, bx, by]) => [round(ax + dx), round(ay + dy), round(bx + dx), round(by + dy)]);

export const OPERATIONS = {
  rotateLeft: (w) => rotate(w, -15),
  rotateRight: (w) => rotate(w, 15),
  halfTurn: (w) => rotate(w, 180),
  smaller: (w) => scale(w, 0.9),
  bigger: (w) => scale(w, 1 / 0.9),
  mirrorLeftRight: (w) => mirrorLeftRight(w),
  mirrorTopBottom: (w) => mirrorTopBottom(w),
};
