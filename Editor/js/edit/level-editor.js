// The level being edited, its selection and its undo history. No DOM here, so the tests can drive it.
// Every change goes through a method that records one undo step; `onChange(reason)` tells the interface to redraw and autosave.
import { SCREEN_W, SCREEN_H, pointSegmentDistance } from '../geometry.js';
import { History, cloneModel } from '../model/history.js';
import { MAX_WALLS } from '../model/level.js';
import { expandWalls } from './symmetry.js';
import { OPERATIONS, move, centreOf } from './transform.js';

const same = (a, b) => JSON.stringify(a) === JSON.stringify(b);

// The wall nearest to (nx, ny) within `tolerance` px, or -1
export function hitWall(walls, nx, ny, tolerance = 10) {
  let best = -1;
  let bestD = tolerance;
  const px = nx * SCREEN_W, py = ny * SCREEN_H;
  walls.forEach(([ax, ay, bx, by], i) => {
    const d = pointSegmentDistance(px, py, ax * SCREEN_W, ay * SCREEN_H, bx * SCREEN_W, by * SCREEN_H);
    if (d <= bestD) { bestD = d; best = i; }
  });
  return best;
}

// The wall end (as [wall index, 0 or 1]) within `tolerance` px, or null. Only among the walls listed in `among`.
export function hitEnd(walls, nx, ny, among, tolerance = 12) {
  let best = null;
  let bestD = tolerance;
  for (const i of among) {
    const w = walls[i];
    for (const end of [0, 1]) {
      const d = Math.hypot(w[end * 2] * SCREEN_W - nx * SCREEN_W, w[end * 2 + 1] * SCREEN_H - ny * SCREEN_H);
      if (d < bestD) { bestD = d; best = [i, end]; }
    }
  }
  return best;
}

export class LevelEditor {
  constructor(level, onChange = () => {}) {
    this.onChange = () => {};   // quiet while it is being built: the caller's variable for it does not exist yet
    this.history = new History();
    this.selection = new Set();
    this.symmetry = 'none';
    this.open(level);
    this.onChange = onChange;
  }

  get walls() { return this.level.walls; }

  open(level) {
    this.level = level;
    this.history.clear();
    this.selection = new Set();
    this.lastField = null;
    this.drag = null;
    this.onChange('open');
  }

  // ---- history ----
  checkpoint() { this.history.checkpoint(cloneModel(this.level)); this.lastField = null; }
  get canUndo() { return this.history.canUndo; }
  get canRedo() { return this.history.canRedo; }
  undo() { return this.swap(this.history.undo(this.level), 'undo'); }
  redo() { return this.swap(this.history.redo(this.level), 'redo'); }
  swap(level, reason) {
    if (!level) return false;
    this.level = level;
    this.selection = new Set([...this.selection].filter((i) => i < level.walls.length));
    this.lastField = null;
    this.onChange(reason);
    return true;
  }

  // ---- text fields: typing a name is one undo step, not one per letter ----
  setField(name, value) {
    if (this.level[name] === value) return;
    if (this.lastField !== name) this.checkpoint();
    this.lastField = name;
    this.level[name] = value;
    this.onChange('field');
  }

  // ---- adding ----
  // Adds walls (with the symmetry on) and selects them. Returns how many were added; the level holds MAX_WALLS * 2 at most.
  addWalls(walls) {
    const fresh = expandWalls(walls, this.symmetry).filter((w) => !this.level.walls.some((o) => same(o, w)));
    const room = MAX_WALLS * 2 - this.level.walls.length;
    const added = fresh.slice(0, Math.max(0, room));
    if (!added.length) return 0;
    this.checkpoint();
    const start = this.level.walls.length;
    this.level.walls.push(...added);
    this.selection = new Set(added.map((_, i) => start + i));
    this.onChange('add');
    return added.length;
  }

  // ---- selecting ----
  select(indices, additive = false) {
    this.selection = additive ? new Set([...this.selection, ...indices]) : new Set(indices);
    this.onChange('select');
  }
  toggle(index) {
    const next = new Set(this.selection);
    if (!next.delete(index)) next.add(index);
    this.selection = next;
    this.onChange('select');
  }
  clearSelection() { if (this.selection.size) { this.selection = new Set(); this.onChange('select'); } }
  selectAll() { this.select(this.level.walls.map((_, i) => i)); }

  // Walls with any part inside the box (normalised corners)
  wallsInBox(x1, y1, x2, y2) {
    const [lx, hx, ly, hy] = [Math.min(x1, x2), Math.max(x1, x2), Math.min(y1, y2), Math.max(y1, y2)];
    const out = [];
    this.level.walls.forEach(([ax, ay, bx, by], i) => {
      const inside = (x, y) => x >= lx && x <= hx && y >= ly && y <= hy;
      if (inside(ax, ay) || inside(bx, by) || segmentCrossesBox(ax, ay, bx, by, lx, ly, hx, hy)) out.push(i);
    });
    return out;
  }

  // ---- deleting ----
  deleteWalls(indices) {
    const drop = new Set(indices);
    if (!drop.size) return 0;
    this.checkpoint();
    this.level.walls = this.level.walls.filter((_, i) => !drop.has(i));
    this.selection = new Set();
    this.onChange('delete');
    return drop.size;
  }
  deleteSelected() { return this.deleteWalls([...this.selection]); }

  // ---- dragging: one undo step per gesture, however many moves ----
  beginDrag() {
    this.checkpoint();
    this.drag = { start: cloneModel(this.level.walls) };
  }
  endDrag() {
    const changed = this.drag && !same(this.drag.start, this.level.walls);
    if (!changed) this.history.discardLast();
    this.drag = null;
    if (changed) this.onChange('drag-end');
    return changed;
  }
  // Move the walls by (dx, dy) normalised, from where they were when the drag began
  dragWalls(indices, dx, dy) {
    const start = this.drag.start;
    const moved = move(indices.map((i) => start[i]), dx, dy);
    indices.forEach((i, k) => { this.level.walls[i] = moved[k]; });
    this.onChange('drag');
  }
  // Move one corner to (x, y): every wall end that sat exactly there when the drag began goes with it, so joined corners stay joined
  dragCorner(ref, x, y) {
    const start = this.drag.start;
    const [i, end] = ref;
    const ox = start[i][end * 2], oy = start[i][end * 2 + 1];
    start.forEach((w, k) => {
      for (const e of [0, 1]) {
        if (Math.abs(w[e * 2] - ox) < 1e-6 && Math.abs(w[e * 2 + 1] - oy) < 1e-6) {
          this.level.walls[k] = [...this.level.walls[k]];
          this.level.walls[k][e * 2] = x;
          this.level.walls[k][e * 2 + 1] = y;
        }
      }
    });
    this.onChange('drag');
  }

  // ---- group operations on the selection ----
  nudge(dx, dy) {
    if (!this.selection.size) return;
    this.checkpoint();
    const idx = [...this.selection];
    const moved = move(idx.map((i) => this.level.walls[i]), dx, dy);
    idx.forEach((i, k) => { this.level.walls[i] = moved[k]; });
    this.onChange('nudge');
  }
  transform(name) {
    const op = OPERATIONS[name];
    if (!op || !this.selection.size) return false;
    this.checkpoint();
    const idx = [...this.selection];
    const result = op(idx.map((i) => this.level.walls[i]));
    idx.forEach((i, k) => { this.level.walls[i] = result[k]; });
    this.onChange('transform');
    return true;
  }
  duplicateSelected(offset = [0.02, 0.035]) {
    if (!this.selection.size) return 0;
    const copies = move([...this.selection].map((i) => this.level.walls[i]), ...offset);
    const room = MAX_WALLS * 2 - this.level.walls.length;
    const added = copies.slice(0, Math.max(0, room));
    if (!added.length) return 0;
    this.checkpoint();
    const start = this.level.walls.length;
    this.level.walls.push(...added);
    this.selection = new Set(added.map((_, i) => start + i));
    this.onChange('add');
    return added.length;
  }
  // Mirror what is drawn onto the other side: used by the "Mirror the whole arena" button (copies, never moves)
  applySymmetryToAll() {
    const before = this.level.walls.length;
    const all = expandWalls(this.level.walls, this.symmetry);
    const added = all.filter((w) => !this.level.walls.some((o) => same(o, w)));
    if (!added.length) return 0;
    this.checkpoint();
    this.level.walls.push(...added.slice(0, Math.max(0, MAX_WALLS * 2 - before)));
    this.onChange('add');
    return added.length;
  }
  get selectionCentre() { return centreOf([...this.selection].map((i) => this.level.walls[i])); }
}

function segmentCrossesBox(ax, ay, bx, by, lx, ly, hx, hy) {
  // Liang-Barsky
  let t0 = 0, t1 = 1;
  const dx = bx - ax, dy = by - ay;
  for (const [p, q] of [[-dx, ax - lx], [dx, hx - ax], [-dy, ay - ly], [dy, hy - ay]]) {
    if (p === 0) { if (q < 0) return false; } else {
      const r = q / p;
      if (p < 0) { if (r > t1) return false; if (r > t0) t0 = r; } else { if (r < t0) return false; if (r < t1) t1 = r; }
    }
  }
  return true;
}
