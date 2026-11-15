// Checks on a ship. Warnings only: nothing here stops a save or an export.
// Each result: { id, severity: 'warn' | 'note', message, point? (index) } for the UI to point at.
import { signedArea, selfIntersections } from '../geometry.js';
import { isColour } from '../io/numbers.js';
import { BUILTIN_SHIPS } from '../model/builtins.js';
import { MIN_POINTS, MAX_POINTS, MAX_REACH, MIN_RADIUS, MAX_RADIUS, MIN_MASS, MAX_MASS, reach, massRange, baseMass } from '../model/ship.js';

const MIN_AREA = 40;   // px squared: Nimble, the smallest shipped, is about 70
const MAX_AREA = 900;  // the biggest shipped is about 600
const MIN_EDGE = 0.5;  // px

// `others` are the names of the other ships in the library
export function checkShip(ship, { others = [] } = {}) {
  const out = [];
  const add = (id, severity, message, extra = {}) => out.push({ id, severity, message, ...extra });
  const pts = ship.points;
  const name = ship.name.trim();

  if (!name) add('name-missing', 'warn', 'The ship has no name, so the game cannot list it.');
  else if (BUILTIN_SHIPS.some((n) => n.toLowerCase() === name.toLowerCase())) add('name-builtin', 'warn', `"${name}" is the name of a ship that comes with the game. Pick another so they are not confused.`);
  else if (others.includes(name)) add('name-clash', 'warn', `Another ship in your library is called "${name}", so their exported files clash.`);
  if (!isColour(ship.color)) add('colour', 'warn', 'The colour is not eight hex digits (AARRGGBB), so the game cannot load it.');

  if (pts.length < MIN_POINTS) add('few-points', 'warn', `A ship needs at least ${MIN_POINTS} points to be a shape; this one has ${pts.length}.`);
  if (pts.length > MAX_POINTS) add('many-points', 'warn', `A ship may have at most ${MAX_POINTS} points; this one has ${pts.length}.`);
  pts.forEach((p, i) => {
    if (!p.every(Number.isFinite)) add('bad-point', 'warn', `Point ${i + 1} is not a number.`, { point: i });
    else if (Math.hypot(p[0], p[1]) > MAX_REACH) add('too-far', 'warn', `Point ${i + 1} is ${Math.round(Math.hypot(p[0], p[1]))} px from the centre; ships may reach ${MAX_REACH} px.`, { point: i });
    const next = pts[(i + 1) % pts.length];
    if (pts.length > 1 && Math.hypot(next[0] - p[0], next[1] - p[1]) < MIN_EDGE) add('same-point', 'warn', `Points ${i + 1} and ${((i + 1) % pts.length) + 1} are in the same place.`, { point: i });
  });

  if (pts.length >= MIN_POINTS) {
    const area = Math.abs(signedArea(pts));
    if (area < MIN_AREA) add('tiny', 'warn', 'The ship is very small; it will be hard to see and to hit.');
    if (area > MAX_AREA) add('huge', 'warn', 'The ship is bigger than any that comes with the game.');
    for (const [i, j] of selfIntersections(pts)) add('crossing', 'warn', `The outline crosses itself between points ${i + 1}-${i + 2} and ${j + 1}-${((j + 1) % pts.length) + 1}.`, { point: i });
    const mirrored = pts.every(([x, y]) => pts.some(([a, b]) => Math.abs(a + x) < 0.6 && Math.abs(b - y) < 0.6));
    if (!mirrored) add('asymmetric', 'note', 'The ship is not symmetrical left to right. That is allowed.');
  }

  // Mass and Radius against the budget
  if (ship.radius < MIN_RADIUS || ship.radius > MAX_RADIUS) add('radius-range', 'warn', `Radius ${ship.radius} is outside ${MIN_RADIUS} to ${MAX_RADIUS}.`);
  else if (pts.length >= MIN_POINTS && ship.radius < Math.ceil(reach(pts))) add('radius-small', 'warn', `Radius ${ship.radius} is smaller than the outline, so shots and walls would touch it before it looks hit.`);
  if (ship.mass < MIN_MASS || ship.mass > MAX_MASS) add('mass-range', 'warn', `Mass ${ship.mass} is outside ${MIN_MASS} to ${MAX_MASS}.`);
  else if (pts.length >= MIN_POINTS) {
    const [lo, hi] = massRange(ship.radius);
    if (ship.mass < lo || ship.mass > hi) add('mass-budget', 'warn', `Mass ${ship.mass} does not suit a ship of radius ${ship.radius} (about ${Math.round(baseMass(ship.radius))}; the editor allows ${lo} to ${hi}).`);
  }
  if (ship.playerIndex) add('player-index', 'note', `PlayerIndex is "${ship.playerIndex}". The game gives numbered controllers to the ships that come with it; a community ship is better left empty.`);
  return out;
}
