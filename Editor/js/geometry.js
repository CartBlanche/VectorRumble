// Plane geometry, for levels in normalised coordinates (0..1 across the 16:9 screen) and ships in local pixels.

export const SCREEN_W = 1280; // the game's base size; levels are stored as fractions of it
export const SCREEN_H = 720;

export function distance(ax, ay, bx, by) {
  return Math.hypot(bx - ax, by - ay);
}

// Distance from point p to segment a-b
export function pointSegmentDistance(px, py, ax, ay, bx, by) {
  const abx = bx - ax;
  const aby = by - ay;
  const lengthSquared = abx * abx + aby * aby;
  const t = lengthSquared < 1e-12 ? 0 : Math.min(1, Math.max(0, ((px - ax) * abx + (py - ay) * aby) / lengthSquared));
  return distance(px, py, ax + abx * t, ay + aby * t);
}

const cross = (ox, oy, ax, ay, bx, by) => (ax - ox) * (by - oy) - (ay - oy) * (bx - ox);

// True crossing only: touching or collinear segments do not count
export function segmentsIntersect(ax, ay, bx, by, cx, cy, dx, dy) {
  const d1 = cross(cx, cy, dx, dy, ax, ay);
  const d2 = cross(cx, cy, dx, dy, bx, by);
  const d3 = cross(ax, ay, bx, by, cx, cy);
  const d4 = cross(ax, ay, bx, by, dx, dy);
  return ((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) && ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0));
}

export function segmentSegmentDistance(ax, ay, bx, by, cx, cy, dx, dy) {
  if (segmentsIntersect(ax, ay, bx, by, cx, cy, dx, dy)) return 0;
  return Math.min(
    pointSegmentDistance(ax, ay, cx, cy, dx, dy),
    pointSegmentDistance(bx, by, cx, cy, dx, dy),
    pointSegmentDistance(cx, cy, ax, ay, bx, by),
    pointSegmentDistance(dx, dy, ax, ay, bx, by),
  );
}

// Signed area of a polygon of [x, y] points: positive when the points run clockwise on screen (y down)
export function signedArea(points) {
  let sum = 0;
  for (let i = 0; i < points.length; i++) {
    const [x1, y1] = points[i];
    const [x2, y2] = points[(i + 1) % points.length];
    sum += x1 * y2 - x2 * y1;
  }
  return sum / 2;
}

// The pairs of edges (indices) of a closed polygon that cross each other; neighbouring edges are not compared
export function selfIntersections(points) {
  const n = points.length;
  const found = [];
  for (let i = 0; i < n; i++) {
    for (let j = i + 2; j < n; j++) {
      if (i === 0 && j === n - 1) continue; // they share a corner
      const [a, b] = [points[i], points[(i + 1) % n]];
      const [c, d] = [points[j], points[(j + 1) % n]];
      if (segmentsIntersect(a[0], a[1], b[0], b[1], c[0], c[1], d[0], d[1])) found.push([i, j]);
    }
  }
  return found;
}
