import assert from 'node:assert/strict';
import { test } from './harness.mjs';
import { snapPoint, snapToGrid, GRID_SIZES, DEFAULT_GRID } from '../js/edit/snap.js';
import { expandWall, expandWalls } from '../js/edit/symmetry.js';
import { rotate, scale, mirrorLeftRight, mirrorTopBottom, move } from '../js/edit/transform.js';
import { TEMPLATES, SHIPPED, SHAPES, shapeWalls } from '../js/edit/templates.js';
import { LevelEditor, hitWall, hitEnd } from '../js/edit/level-editor.js';
import { newLevel, frameWalls, MAX_WALLS } from '../js/model/level.js';
import { checkLevel } from '../js/validate/level-checks.js';
import { writeLevel } from '../js/io/xml-write.js';
import { readLevel } from '../js/io/xml-read.js';

const close = (a, b, eps = 1e-3) => assert.ok(Math.abs(a - b) < eps, `${a} vs ${b}`);
const level = (walls) => newLevel('T', { walls });

console.log('Snapping');
test('the frame corners and the centre are on every grid size', () => {
  for (const size of GRID_SIZES) {
    for (const [x, y] of [[64, 36], [1216, 684], [640, 360]]) {
      const [gx, gy] = snapToGrid(x + 1, y - 1, size);
      assert.deepEqual([gx, gy], [x, y], `size ${size}`);
    }
  }
});
test('endpoints win over the grid; the grid applies otherwise', () => {
  const walls = [[0.5, 0.5, 0.6, 0.5]];
  const near = snapPoint(0.5 + 4 / 1280, 0.5, { grid: true, endpoints: true, walls });
  assert.equal(near.kind, 'endpoint'); close(near.x, 0.5);
  assert.equal(snapPoint(0.31, 0.31, { grid: true }).kind, 'grid');
  assert.equal(snapPoint(0.31, 0.31, {}).kind, 'none');
  assert.equal(snapPoint(0.5, 0.5, { endpoints: true, walls, skip: new Set([0]) }).kind, 'none');
});
test('angle snapping works in screen pixels', () => {
  const from = [0.2, 0.2];
  // 45 degrees on screen is equal pixels in x and y, not equal fractions
  const s = snapPoint(0.4, 0.2 + (0.2 * 1280) / 720 + 0.01, { from, angle: 45 });
  assert.equal(s.kind, 'angle');
  close((s.x - 0.2) * 1280, (s.y - 0.2) * 720, 0.5);
  const h = snapPoint(0.6, 0.21, { from, angle: 15 });
  close(h.y, 0.2);
});
test('snapping stays on screen', () => {
  const s = snapPoint(1.4, -0.3, { grid: true });
  assert.ok(s.x <= 1 && s.y >= 0);
});

console.log('Symmetry');
test('copies per mode, without repeats', () => {
  const w = [0.1, 0.1, 0.2, 0.3];
  assert.equal(expandWall(w, 'none').length, 1);
  assert.equal(expandWall(w, 'h').length, 2);
  assert.equal(expandWall(w, 'both').length, 4);
  assert.equal(expandWall(w, 'rot').length, 2);
  assert.equal(expandWall([0.5, 0.2, 0.5, 0.4], 'h').length, 1); // on the middle line
  assert.equal(expandWall([0.2, 0.5, 0.8, 0.5], 'both').length, 1); // its own image both ways
  assert.equal(expandWall([0.2, 0.3, 0.8, 0.3], 'both').length, 2);
  assert.deepEqual(expandWall(w, 'h')[1], [0.9, 0.1, 0.8, 0.3]);
  assert.equal(expandWalls([w, w], 'h').length, 2);
});

console.log('Transforms');
test('a half turn twice is the same; a mirror twice is the same', () => {
  const walls = [[0.2, 0.3, 0.4, 0.6], [0.5, 0.5, 0.7, 0.2]];
  const pivot = [640, 360];
  const back = rotate(rotate(walls, 180, pivot), 180, pivot);
  walls.flat().forEach((v, i) => close(v, back.flat()[i]));
  const m = mirrorLeftRight(mirrorLeftRight(walls, pivot), pivot);
  walls.flat().forEach((v, i) => close(v, m.flat()[i]));
  const t = mirrorTopBottom(walls, pivot);
  close(t[0][1], 0.7);
});
test('rotating keeps lengths in pixels; scaling scales them', () => {
  const len = ([ax, ay, bx, by]) => Math.hypot((bx - ax) * 1280, (by - ay) * 720);
  const w = [[0.3, 0.3, 0.5, 0.4]];
  close(len(rotate(w, 37)[0]), len(w[0]), 0.2);
  close(len(scale(w, 2)[0]), len(w[0]) * 2, 0.2);
  close(move(w, 0.1, 0)[0][0], 0.4);
});
test('the middle of a level stays the middle after a half turn', () => {
  const walls = TEMPLATES.find((t) => t.id === 'diamond').walls();
  const turned = rotate(walls, 180, [640, 360]);
  const key = (ws) => ws.map((w) => [[w[0], w[1]], [w[2], w[3]]].map((p) => p.map((v) => v.toFixed(3)).join(',')).sort().join('|')).sort();
  assert.deepEqual(key(turned), key(walls));
});

console.log('Templates and shapes');
for (const t of [...TEMPLATES, ...SHIPPED]) {
  test(`template "${t.name}" has no warnings and survives a save`, () => {
    const l = level(t.walls());
    assert.deepEqual(checkLevel(l).filter((r) => r.severity === 'warn'), []);
    assert.deepEqual(readLevel(writeLevel(l)).level.walls, l.walls);
  });
}
test('every shape gives walls that stay on screen; tiny drags give nothing', () => {
  for (const s of SHAPES) {
    const w = shapeWalls(s.id, [0.4, 0.4], [0.55, 0.55], { sides: 6 });
    assert.ok(w.length > 0, s.id);
    assert.ok(w.flat().every((v) => v >= 0 && v <= 1), s.id);
    assert.deepEqual(shapeWalls(s.id, [0.4, 0.4], [0.4, 0.4]), [], `${s.id} tiny`);
  }
  assert.equal(shapeWalls('rect', [0.2, 0.2], [0.4, 0.4]).length, 4);
  assert.equal(shapeWalls('poly', [0.5, 0.5], [0.6, 0.5], { sides: 8 }).length, 8);
  assert.equal(shapeWalls('ring', [0.5, 0.5], [0.6, 0.5], { sides: 5 }).length, 10);
  assert.equal(shapeWalls('pillars', [0.2, 0.5], [0.8, 0.5]).length % 4, 0);
});

console.log('The level editor');
const ed = () => new LevelEditor(newLevel('E'));
test('hit testing finds walls and corners', () => {
  const walls = frameWalls();
  assert.equal(hitWall(walls, 0.05, 0.5, 8), 0);
  assert.equal(hitWall(walls, 0.5, 0.5, 8), -1);
  assert.deepEqual(hitEnd(walls, 0.051, 0.051, [0, 2]), [0, 0]);
  assert.equal(hitEnd(walls, 0.5, 0.5, [0]), null);
});
test('adding walls selects them, is one undo step, and skips repeats', () => {
  const e = ed();
  assert.equal(e.addWalls([[0.3, 0.3, 0.4, 0.3]]), 1);
  assert.deepEqual([...e.selection], [4]);
  assert.equal(e.addWalls([[0.3, 0.3, 0.4, 0.3]]), 0);
  assert.equal(e.walls.length, 5);
  e.undo();
  assert.equal(e.walls.length, 4);
  e.redo();
  assert.equal(e.walls.length, 5);
});
test('symmetry repeats what is drawn, in one undo step', () => {
  const e = ed(); e.symmetry = 'both';
  e.addWalls([[0.2, 0.2, 0.3, 0.25]]);
  assert.equal(e.walls.length, 8);
  e.undo();
  assert.equal(e.walls.length, 4);
});
test('the wall limit is kept', () => {
  const e = ed();
  const many = Array.from({ length: MAX_WALLS * 2 + 20 }, (_, i) => [0.1 + (i % 100) * 0.001, 0.2 + Math.floor(i / 100) * 0.01, 0.2, 0.3]);
  e.addWalls(many);
  assert.equal(e.walls.length, MAX_WALLS * 2);
  assert.equal(e.addWalls([[0.4, 0.4, 0.5, 0.5]]), 0);
});
test('selecting: replace, add, toggle, all, box', () => {
  const e = ed();
  e.select([0]); e.select([1], true); assert.deepEqual([...e.selection].sort(), [0, 1]);
  e.toggle(1); assert.deepEqual([...e.selection], [0]);
  e.selectAll(); assert.equal(e.selection.size, 4);
  assert.deepEqual(e.wallsInBox(0.0, 0.0, 0.1, 0.1).sort(), [0, 2]);   // the top-left corner
  assert.deepEqual(e.wallsInBox(0.3, 0.0, 0.7, 0.1).sort(), [2]);      // a box the top wall passes through
  assert.deepEqual(e.wallsInBox(0.3, 0.3, 0.6, 0.6), []);
});
test('deleting is undoable and clears the selection', () => {
  const e = ed(); e.select([0, 1]);
  assert.equal(e.deleteSelected(), 2);
  assert.equal(e.walls.length, 2); assert.equal(e.selection.size, 0);
  e.undo(); assert.equal(e.walls.length, 4);
});
test('a drag is one undo step; a drag that moved nothing leaves none', () => {
  const e = ed(); e.addWalls([[0.3, 0.3, 0.4, 0.3]]);
  const steps = e.history.past.length;
  e.beginDrag(); e.dragWalls([4], 0.01, 0); e.dragWalls([4], 0.05, 0.02);
  assert.equal(e.endDrag(), true);
  close(e.walls[4][0], 0.35);
  assert.equal(e.history.past.length, steps + 1);
  e.undo(); close(e.walls[4][0], 0.3);
  e.beginDrag(); assert.equal(e.endDrag(), false);
  assert.equal(e.history.past.length, steps);
});
test('dragging a corner moves every wall end joined there', () => {
  const e = ed();
  e.beginDrag(); e.dragCorner([0, 0], 0.1, 0.12); e.endDrag();
  assert.deepEqual(e.walls[0].slice(0, 2), [0.1, 0.12]);
  assert.deepEqual(e.walls[2].slice(0, 2), [0.1, 0.12]); // the top wall's start was the same corner
  assert.deepEqual(e.walls[1].slice(0, 2), [0.95, 0.05]); // untouched
});
test('nudge, transform, duplicate', () => {
  const e = ed(); e.select([0]);
  e.nudge(0.01, 0); close(e.walls[0][0], 0.06);
  assert.equal(e.transform('halfTurn'), true);
  assert.equal(e.transform('nope'), false);
  assert.equal(e.duplicateSelected(), 1);
  assert.equal(e.walls.length, 5); assert.deepEqual([...e.selection], [4]);
});
test('typing a name is a single undo step; other fields start a new one', () => {
  const e = ed();
  e.setField('name', 'A'); e.setField('name', 'Ab'); e.setField('name', 'Abc');
  e.setField('author', 'me');
  assert.equal(e.history.past.length, 2);
  e.undo(); assert.equal(e.level.author, '');
  e.undo(); assert.equal(e.level.name, 'E');
});
test('undo drops a selection that no longer exists', () => {
  const e = ed(); e.addWalls([[0.3, 0.3, 0.4, 0.3]]);
  assert.ok(e.selection.has(4));
  e.undo();
  assert.equal(e.selection.size, 0);
});
test('applying symmetry to the whole level adds only the missing mirrors', () => {
  const e = ed(); e.walls.push([0.2, 0.2, 0.3, 0.3]); e.symmetry = 'h';
  assert.equal(e.applySymmetryToAll(), 1);
  assert.equal(e.applySymmetryToAll(), 0);
});
