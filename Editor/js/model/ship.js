// A ship: an outline (local pixels, nose toward -y), a colour, and Mass and Radius. The editor derives Radius and Mass from the
// outline so community ships stay in the range the shipped ones live in.
// Shipped ships: radius 15 (Nimble 10 px long, Bat-ter Up) or 20 (Blaster, Defender, Destroyer); mass 20 with radius 15, 32 with 20.

export const MIN_POINTS = 3;
export const MAX_POINTS = 24;
export const MAX_THRUSTERS = 3;
export const MIN_THRUSTER_SIZE = 0.5;
export const MAX_THRUSTER_SIZE = 2;
export const MAX_REACH = 22;      // farthest point from the centre, in px
export const MIN_RADIUS = 12;
export const MAX_RADIUS = 24;
export const MIN_MASS = 12;
export const MAX_MASS = 40;

export function newShip(name = 'New Ship', options = {}) {
  const points = options.points ?? [[0, -12], [9, 10], [0, 6], [-9, 10]];
  const ship = {
    kind: 'ship',
    name,
    author: '',
    description: '',
    color: 'FF00FF00',
    points,
    playerIndex: '', // always empty: a spare ship (the game gives the five shipped ones their numbered controllers)
    thrusters: null, // null: not placed, the game puts a flame at the rear points; [] none; else [{ x, y, size }] up to 3
    extra: { lead: [], attrs: [], nodes: [], rootComments: [] },
    ...options.fields,
  };
  const stats = deriveStats(ship.points);
  ship.radius = ship.radius ?? stats.radius;
  ship.mass = ship.mass ?? stats.mass;
  return ship;
}

// What the game does when a file has no <Thrusters>: a flame at each of the outline's rearmost points (within 1 px of the lowest), up to 3
export function autoThrusters(points) {
  if (!points.length) return [];
  const rear = Math.max(...points.map((p) => p[1]));
  return points.filter((p) => p[1] >= rear - 1).slice(0, MAX_THRUSTERS).map(([x, y]) => ({ x, y, size: 1 }));
}

// The thrusters the game will show for this ship, placed or not
export const thrustersOf = (ship) => ship.thrusters ?? autoThrusters(ship.points);

// The flame's colour, as the game picks it: amber, or ice blue on red, orange and yellow ships where amber would vanish
export function flameColour(argb) {
  const r = parseInt(argb.slice(2, 4), 16), g = parseInt(argb.slice(4, 6), 16), b = parseInt(argb.slice(6, 8), 16);
  const max = Math.max(r, g, b), min = Math.min(r, g, b);
  if (![r, g, b].every(Number.isFinite) || max - min < 60) return 'FFFFA000';
  let hue;
  if (max === r) hue = 60 * (((g - b) / (max - min) + 6) % 6);
  else if (max === g) hue = 60 * ((b - r) / (max - min) + 2);
  else hue = 60 * ((r - g) / (max - min) + 4);
  const apart = Math.abs((hue - 30 + 540) % 360 - 180);
  return apart < 60 ? 'FF6EC8FF' : 'FFFFA000';
}

export function reach(points) {
  return points.reduce((m, [x, y]) => Math.max(m, Math.hypot(x, y)), 0);
}

const clamp = (v, lo, hi) => Math.min(hi, Math.max(lo, v));

// Radius from the farthest point (the shipped ones are within 1 to 5 px of it); mass follows radius the way the shipped pair does
export function deriveStats(points, weight = 1) {
  const radius = clamp(Math.ceil(reach(points)) + 2, MIN_RADIUS, MAX_RADIUS);
  const base = baseMass(radius);
  const mass = Math.round(clamp(base * clamp(weight, 0.8, 1.2), MIN_MASS, MAX_MASS));
  return { radius, mass, baseMass: Math.round(base) };
}

// The mass the shipped ships suggest for a radius (20 at 15, 32 at 20)
export const baseMass = (radius) => 20 + (radius - 15) * 2.4;

// The mass slider's range for a radius: +-20% of what the size suggests
export function massRange(radius) {
  const base = baseMass(radius);
  return [Math.max(MIN_MASS, Math.round(base * 0.8)), Math.min(MAX_MASS, Math.round(base * 1.2))];
}
