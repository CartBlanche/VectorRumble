// The ship being edited: its outline, the selected point and the undo history. No DOM, so the tests can drive it.
// Every change goes through a method that records one undo step; `onChange(reason)` tells the interface to redraw and autosave.
// The outline is a closed polygon of [x, y] points in ship-local pixels, nose toward -y. Radius is always worked out from the
// outline; Mass is the player's choice within the range that radius allows (see model/ship.js).
import { pointSegmentDistance } from '../geometry.js';
import { History, cloneModel } from '../model/history.js';
import { MAX_POINTS, MIN_POINTS, MAX_REACH, MAX_THRUSTERS, MIN_THRUSTER_SIZE, MAX_THRUSTER_SIZE, deriveStats, massRange, baseMass, reach, autoThrusters, thrustersOf } from '../model/ship.js';

export const SNAPS = [0.5, 1, 2];
const AXIS = 0.3;       // a point this close to the centre line counts as on it
const PAIR = 0.6;       // the mirror image of a point is "the same place" within this

const r1 = (v) => Math.round(v * 2) / 2;
const sameShape = (a, b) => JSON.stringify(a) === JSON.stringify(b);

export const snapTo = (v, size) => Math.round(v / size) * size;

// Keep a point within the allowed reach, so a drag cannot make an illegal ship
export function clampReach(x, y, limit = MAX_REACH) {
  const d = Math.hypot(x, y);
  return d <= limit ? [x, y] : [(x / d) * limit, (y / d) * limit];
}

// The index of the point nearest (x, y) within `tolerance` px, or -1
export function hitPoint(points, x, y, tolerance = 2) {
  let best = -1;
  let bestD = tolerance;
  points.forEach(([px, py], i) => {
    const d = Math.hypot(px - x, py - y);
    if (d <= bestD) { bestD = d; best = i; }
  });
  return best;
}

// The index of the thruster nearest (x, y) within `tolerance` px, or -1
export function hitThruster(thrusters, x, y, tolerance = 2) {
  let best = -1;
  let bestD = tolerance;
  thrusters.forEach((t, i) => {
    const d = Math.hypot(t.x - x, t.y - y);
    if (d <= bestD) { bestD = d; best = i; }
  });
  return best;
}

// The thruster that mirrors thruster i (left to right), or -1 if there is none or i is on the centre line
export function thrusterPartner(thrusters, i) {
  const t = thrusters[i];
  if (Math.abs(t.x) < AXIS) return -1;
  return thrusters.findIndex((u, j) => j !== i && Math.hypot(u.x + t.x, u.y - t.y) < PAIR);
}

// The edge (index of its first point) nearest (x, y) and how far away it is
export function nearestEdge(points, x, y) {
  let best = -1;
  let bestD = Infinity;
  points.forEach((a, i) => {
    const b = points[(i + 1) % points.length];
    const d = pointSegmentDistance(x, y, a[0], a[1], b[0], b[1]);
    if (d < bestD) { bestD = d; best = i; }
  });
  return { edge: best, distance: bestD };
}

// The point whose position is the mirror image (left to right) of point i, or -1 if there is none (or it is on the centre line)
export function partnerOf(points, i) {
  const [x, y] = points[i];
  if (Math.abs(x) < AXIS) return -1;
  let best = -1;
  let bestD = PAIR;
  points.forEach(([a, b], j) => {
    if (j === i) return;
    const d = Math.hypot(a + x, b - y);
    if (d < bestD) { bestD = d; best = j; }
  });
  return best;
}

// Ramer-Douglas-Peucker on an open path
function simplifyPath(path, tolerance) {
  if (path.length < 3) return path;
  const [a, b] = [path[0], path[path.length - 1]];
  let index = 0;
  let far = 0;
  for (let i = 1; i < path.length - 1; i++) {
    const d = pointSegmentDistance(path[i][0], path[i][1], a[0], a[1], b[0], b[1]);
    if (d > far) { far = d; index = i; }
  }
  if (far <= tolerance) return [a, b];
  return [...simplifyPath(path.slice(0, index + 1), tolerance).slice(0, -1), ...simplifyPath(path.slice(index), tolerance)];
}

// A drawn stroke as an outline of at most `budget` points. With `mirror` the stroke is the right-hand side, nose to tail,
// and the left-hand side is made from it; without, the stroke is closed up as it is.
export function outlineFromStroke(stroke, { mirror = false, budget = 16 } = {}) {
  let path = stroke.map(([x, y]) => [r1(x), r1(y)]);
  if (mirror) path = path.map(([x, y]) => [Math.abs(x), y]);
  if (path.length < 3) return null;
  let tolerance = 0.6;
  let simple = simplifyPath(path, tolerance);
  const total = (p) => (mirror ? 2 * p.length - 2 : p.length);
  while (total(simple) > budget && tolerance < 12) { tolerance *= 1.35; simple = simplifyPath(path, tolerance); }
  simple = simple.map(([x, y]) => [r1(x), r1(y)]);
  if (!mirror) {
    // Drop a closing point that repeats the first
    if (simple.length > 3 && Math.hypot(simple[0][0] - simple.at(-1)[0], simple[0][1] - simple.at(-1)[1]) < 1.5) simple.pop();
    return simple.length >= MIN_POINTS ? simple : null;
  }
  simple[0] = [0, simple[0][1]];
  simple[simple.length - 1] = [0, simple.at(-1)[1]];
  const chain = simple.filter((p, i) => i === 0 || i === simple.length - 1 || p[0] >= 1);
  const outline = [...chain, ...chain.slice(1, -1).reverse().map(([x, y]) => [-x, y])];
  return outline.length >= MIN_POINTS ? outline : null;
}

// Make an outline left-right symmetrical by copying one half onto the other. `keep` is 'right' or 'left'.
export function mirrorHalf(points, keep = 'right') {
  const sign = keep === 'right' ? 1 : -1;
  const n = points.length;
  const inHalf = points.map(([x]) => x * sign >= -AXIS);
  if (!inHalf.some(Boolean)) return points;
  // Rotate to start at the first point of the kept half's run, so that run is contiguous
  let start = 0;
  for (let i = 0; i < n; i++) if (inHalf[i] && !inHalf[(i + n - 1) % n]) { start = i; break; }
  const ordered = Array.from({ length: n }, (_, k) => points[(start + k) % n]);
  const flags = Array.from({ length: n }, (_, k) => inHalf[(start + k) % n]);
  let run = 0;
  while (run < n && flags[run]) run++;
  if (flags.slice(run).some(Boolean)) {
    // The kept half is in several pieces: fall back to going round the centre
    const half = points.filter(([x]) => x * sign >= -AXIS).map(([x, y]) => [Math.abs(x) * sign, y]);
    const all = [...half, ...half.filter(([x]) => Math.abs(x) >= AXIS).map(([x, y]) => [-x, y])];
    return all.sort((a, b) => Math.atan2(a[0], -a[1]) - Math.atan2(b[0], -b[1]));
  }
  const chain = ordered.slice(0, run).map(([x, y]) => [Math.abs(x) < AXIS ? 0 : x, y]);
  const mirrored = chain.filter(([x]) => x !== 0).reverse().map(([x, y]) => [-x, y]);
  return [...chain, ...mirrored];
}

export class ShipEditor {
  constructor(ship, onChange = () => {}) {
    this.onChange = () => {};
    this.history = new History();
    this.selected = -1;          // the selected point
    this.selectedThruster = -1;  // the selected thruster (a point and a thruster are never both selected)
    this.mirror = true;          // edit both sides at once
    this.snap = 1;               // px
    this.open(ship);
    this.onChange = onChange;
  }

  get points() { return this.ship.points; }

  open(ship) {
    this.ship = ship;
    this.history.clear();
    this.selected = -1;
    this.selectedThruster = -1;
    this.lastField = null;
    this.drag = null;
    this.onChange('open');
  }

  // ---- history ----
  checkpoint() { this.history.checkpoint(cloneModel(this.ship)); this.lastField = null; }
  get canUndo() { return this.history.canUndo; }
  get canRedo() { return this.history.canRedo; }
  undo() { const s = this.history.undo(this.ship); if (s) { this.ship = s; this.selected = -1; this.selectedThruster = -1; this.onChange('undo'); } }
  redo() { const s = this.history.redo(this.ship); if (s) { this.ship = s; this.selected = -1; this.selectedThruster = -1; this.onChange('redo'); } }

  // Edits in the middle of a drag or while typing share one undo step
  edit(reason, fn) {
    const before = cloneModel(this.ship);
    fn();
    if (sameShape(before, this.ship)) return false;
    this.history.checkpoint(before);
    this.lastField = null;
    this.onChange(reason);
    return true;
  }

  // ---- details ----
  setField(name, value) {
    if (this.ship[name] === value) return;
    if (this.lastField !== name) this.checkpoint();
    this.lastField = name;
    this.ship[name] = value;
    this.onChange('field');
  }

  // Radius follows the outline; Mass keeps its place in the range the new radius allows ("weight": heavier or lighter than usual)
  restat(previousRadius) {
    const weight = this.ship.mass / baseMass(previousRadius);
    const { radius } = deriveStats(this.ship.points);
    this.ship.radius = radius;
    const [lo, hi] = massRange(radius);
    this.ship.mass = Math.min(hi, Math.max(lo, Math.round(baseMass(radius) * weight)));
  }

  // The mass slider: -1 (lightest the size allows) to +1 (heaviest)
  setWeight(w) {
    const [lo, hi] = massRange(this.ship.radius);
    const mid = baseMass(this.ship.radius);
    const mass = Math.round(w < 0 ? mid + (mid - lo) * w : mid + (hi - mid) * w);
    if (mass === this.ship.mass) return;
    if (this.lastField !== 'mass') this.checkpoint();
    this.lastField = 'mass';
    this.ship.mass = Math.min(hi, Math.max(lo, mass));
    this.onChange('field');
  }
  get weight() {
    const [lo, hi] = massRange(this.ship.radius);
    const mid = baseMass(this.ship.radius);
    const m = this.ship.mass;
    return m < mid ? (mid === lo ? 0 : (m - mid) / (mid - lo)) : (hi === mid ? 0 : (m - mid) / (hi - mid));
  }

  // ---- points ----
  select(i) { if (this.selected !== i || (i >= 0 && this.selectedThruster >= 0)) { this.selected = i; if (i >= 0) this.selectedThruster = -1; this.onChange('select'); } }

  // Add a point where the player tapped: on the nearest edge. With mirror on, its mirror image goes into the matching edge.
  addPointAt(x, y) {
    if (this.points.length >= MAX_POINTS) return -1;
    const p = this.clean(x, y);
    let index = -1;
    this.edit('points', () => {
      const pts = this.points;
      const { edge } = nearestEdge(pts, p[0], p[1]);
      const onAxis = Math.abs(p[0]) < AXIS;
      const mirrorOk = this.mirror && !onAxis && pts.length + 2 <= MAX_POINTS;
      if (!mirrorOk) {
        pts.splice(edge + 1, 0, onAxis ? [0, p[1]] : p);
        index = edge + 1;
        return;
      }
      const a = pts[edge];
      const b = pts[(edge + 1) % pts.length];
      const twin = (q) => { const j = partnerOf(pts, pts.indexOf(q)); return j >= 0 ? j : (Math.abs(q[0]) < AXIS ? pts.indexOf(q) : -1); };
      const ta = twin(a), tb = twin(b);
      const other = ta >= 0 && tb >= 0 ? pts.findIndex((_, k) => (k === ta && (k + 1) % pts.length === tb) || (k === tb && (k + 1) % pts.length === ta)) : -1;
      const m = [-p[0], p[1]];
      if (other === edge) {
        // The edge is its own mirror image (it crosses the centre line): both new points go into it, in order along it
        const t = (q) => (q[0] - a[0]) * (b[0] - a[0]) + (q[1] - a[1]) * (b[1] - a[1]);
        const [first, second] = t(p) <= t(m) ? [p, m] : [m, p];
        pts.splice(edge + 1, 0, first, second);
        index = pts.indexOf(p);
      } else if (other >= 0) {
        // Insert the later one first so the earlier index stays right
        const entries = [[edge, p], [other, m]].sort((u, v) => v[0] - u[0]);
        for (const [e, q] of entries) pts.splice(e + 1, 0, q);
        index = pts.indexOf(p);
      } else {
        pts.splice(edge + 1, 0, p);
        index = edge + 1;
      }
    });
    if (index >= 0) { this.selected = index; this.onChange('select'); }
    return index;
  }

  // A point placed by the player: snapped, rounded, kept inside the reach; near the centre line it sits on it
  clean(x, y) {
    const sx = snapTo(x, this.snap), sy = snapTo(y, this.snap);
    const [cx, cy] = clampReach(sx, sy);
    return [Math.abs(cx) < this.snap / 2 + 1e-9 ? 0 : r1(cx), r1(cy)];
  }

  // ---- drag a point ----
  beginDrag(i) {
    if (i < 0 || i >= this.points.length) return false;
    this.checkpoint();
    this.drag = { i, partner: this.mirror ? partnerOf(this.points, i) : -1, moved: false, before: this.points[i].slice() };
    this.selected = i;
    return true;
  }

  dragTo(x, y) {
    const d = this.drag;
    if (!d) return;
    if (d.thruster) return this.dragThrusterTo(x, y);
    const pts = this.points;
    let [nx, ny] = this.clean(x, y);
    // A point on the centre line stays on it while mirror is on
    if (this.mirror && Math.abs(d.before[0]) < AXIS) nx = 0;
    if (nx === pts[d.i][0] && ny === pts[d.i][1]) return;
    pts[d.i] = [nx, ny];
    if (d.partner >= 0) pts[d.partner] = [-nx, ny];
    d.moved = true;
    this.restat(this.ship.radius);
    this.onChange('drag');
  }

  endDrag() {
    const d = this.drag;
    this.drag = null;
    if (!d) return;
    if (d.thruster) {
      if (!d.moved) { this.history.discardLast(); if (d.wasAuto) this.ship.thrusters = null; this.onChange('select'); return; }
      this.onChange('thrusters');
      return;
    }
    if (!d.moved) { this.history.discardLast(); this.onChange('select'); return; }
    this.onChange('points');
  }

  // ---- delete ----
  deletePoint(i) {
    if (i < 0 || i >= this.points.length) return false;
    if (this.points.length <= MIN_POINTS) return false;
    return this.edit('points', () => {
      const pts = this.points;
      const drop = new Set([i]);
      const j = this.mirror ? partnerOf(pts, i) : -1;
      if (j >= 0 && pts.length - 2 >= MIN_POINTS) drop.add(j);
      this.ship.points = pts.filter((_, k) => !drop.has(k));
      this.selected = -1;
      this.restat(this.ship.radius);
    });
  }
  deleteSelected() {
    if (this.selectedThruster >= 0) return this.deleteThruster(this.selectedThruster);
    return this.selected >= 0 && this.deletePoint(this.selected);
  }

  // ---- whole outline ----
  setOutline(points) {
    if (!points || points.length < MIN_POINTS) return false;
    return this.edit('points', () => { this.ship.points = points.map(([x, y]) => [r1(x), r1(y)]); this.selected = -1; this.restat(this.ship.radius); });
  }

  drawStroke(stroke) {
    const outline = outlineFromStroke(stroke.map(([x, y]) => clampReach(x, y)), { mirror: this.mirror });
    return outline ? this.setOutline(outline) : false;
  }

  makeSymmetric(keep) { return this.setOutline(mirrorHalf(this.points, keep)); }

  // Slide the whole outline so the middle of its bounding box sits on the centre (ships turn about the origin)
  centre() {
    const xs = this.points.map((p) => p[0]), ys = this.points.map((p) => p[1]);
    const dx = r1(-(Math.min(...xs) + Math.max(...xs)) / 2), dy = r1(-(Math.min(...ys) + Math.max(...ys)) / 2);
    if (!dx && !dy) return false;
    return this.edit('points', () => {
      this.ship.points = this.points.map(([x, y]) => [x + dx, y + dy]);
      this.moveThrusters(([x, y]) => [x + dx, y + dy]);
      this.restat(this.ship.radius);
    });
  }

  scaleBy(factor) {
    return this.edit('points', () => {
      const scaled = this.points.map(([x, y]) => clampReach(r1(x * factor), r1(y * factor)));
      this.ship.points = scaled;
      this.moveThrusters(([x, y]) => clampReach(r1(x * factor), r1(y * factor)));
      this.restat(this.ship.radius);
    });
  }

  flip() { return this.edit('points', () => { this.ship.points = this.points.map(([x, y]) => [x, -y]).reverse(); this.moveThrusters(([x, y]) => [x, -y]); this.restat(this.ship.radius); }); }

  get reach() { return reach(this.points); }

  // ---- thrusters ----
  // `ship.thrusters` is null until the player places one: then the game puts a flame at the rear points, and so do we.
  get thrusters() { return thrustersOf(this.ship); }
  get thrustersPlaced() { return this.ship.thrusters != null; }

  // Make the thrusters the ship's own (what the game would have shown), so one can be moved or removed
  ownThrusters() { if (!this.ship.thrusters) this.ship.thrusters = autoThrusters(this.points).map((t) => ({ ...t })); return this.ship.thrusters; }

  // A whole-outline change (centre, scale, flip) takes placed thrusters along; not-placed ones follow the outline by themselves
  moveThrusters(fn) {
    if (!this.ship.thrusters) return;
    this.ship.thrusters = this.ship.thrusters.map((t) => { const [x, y] = fn([t.x, t.y]); return { ...t, x: r1(x), y: r1(y) }; });
  }

  selectThruster(i) {
    if (this.selectedThruster === i && this.selected < 0) return;
    this.selectedThruster = i;
    if (i >= 0) this.selected = -1;
    this.onChange('select');
  }

  // A thruster put where the player tapped (snapped and kept within the reach). With mirror on, a pair if there is room for two.
  addThrusterAt(x, y) {
    if ((this.ship.thrusters ?? []).length >= MAX_THRUSTERS) return -1;
    const [cx, cy] = this.clean(x, y);
    let index = -1;
    this.edit('thrusters', () => {
      // the first one placed starts the ship's own list (the not-placed ones were only the game's guess)
      const list = this.ship.thrusters ?? (this.ship.thrusters = []);
      list.push({ x: cx, y: cy, size: 1 });
      index = list.length - 1;
      if (this.mirror && Math.abs(cx) >= AXIS && list.length < MAX_THRUSTERS) list.push({ x: -cx, y: cy, size: 1 });
    });
    if (index >= 0) { this.selectedThruster = index; this.selected = -1; this.onChange('select'); }
    return index;
  }

  beginThrusterDrag(i) {
    if (i < 0 || i >= this.thrusters.length) return false;
    this.checkpoint();
    const wasAuto = !this.ship.thrusters;
    const list = this.ownThrusters();
    this.drag = { thruster: true, i, wasAuto, partner: this.mirror ? thrusterPartner(list, i) : -1, moved: false, before: { ...list[i] } };
    this.selectedThruster = i;
    this.selected = -1;
    return true;
  }

  dragThrusterTo(x, y) {
    const d = this.drag;
    if (!d?.thruster) return;
    const list = this.ship.thrusters;
    let [nx, ny] = this.clean(x, y);
    if (this.mirror && Math.abs(d.before.x) < AXIS) nx = 0; // one on the centre line stays on it
    if (nx === list[d.i].x && ny === list[d.i].y) return;
    list[d.i] = { ...list[d.i], x: nx, y: ny };
    if (d.partner >= 0) list[d.partner] = { ...list[d.partner], x: -nx, y: ny };
    d.moved = true;
    this.onChange('drag');
  }

  deleteThruster(i) {
    if (i < 0 || i >= this.thrusters.length) return false;
    return this.edit('thrusters', () => {
      const list = this.ownThrusters();
      const drop = new Set([i]);
      const j = this.mirror ? thrusterPartner(list, i) : -1;
      if (j >= 0) drop.add(j);
      this.ship.thrusters = list.filter((_, k) => !drop.has(k));
      this.selectedThruster = -1;
    });
  }

  // The size slider: the selected thruster (and its twin with mirror on)
  setThrusterSize(size) {
    const i = this.selectedThruster;
    if (i < 0 || !this.ship.thrusters?.[i]) return;
    const value = Math.round(Math.min(MAX_THRUSTER_SIZE, Math.max(MIN_THRUSTER_SIZE, size)) * 10) / 10;
    const list = this.ship.thrusters;
    const j = this.mirror ? thrusterPartner(list, i) : -1;
    if (list[i].size === value && (j < 0 || list[j].size === value)) return;
    if (this.lastField !== 'thrusterSize') this.checkpoint();
    this.lastField = 'thrusterSize';
    list[i] = { ...list[i], size: value };
    if (j >= 0) list[j] = { ...list[j], size: value };
    this.onChange('field');
  }

  // Back to "not placed": the game puts a flame at the rear points
  resetThrusters() {
    if (this.ship.thrusters == null) return false;
    return this.edit('thrusters', () => { this.ship.thrusters = null; this.selectedThruster = -1; });
  }

  // A ship with no flame at all
  clearThrusters() {
    if (this.ship.thrusters?.length === 0) return false;
    return this.edit('thrusters', () => { this.ship.thrusters = []; this.selectedThruster = -1; });
  }
}
