import assert from 'node:assert/strict';
import { test } from './harness.mjs';
import { ShipEditor, outlineFromStroke, mirrorHalf, partnerOf, hitPoint, nearestEdge } from '../js/edit/ship-editor.js';
import { newShip, massRange } from '../js/model/ship.js';
import { SHIPPED_SHIPS } from '../js/model/ship-builtins.js';
import { checkShip } from '../js/validate/ship-checks.js';
import { writeShip } from '../js/io/xml-write.js';
import { readShip } from '../js/io/xml-read.js';

const editor = (points) => { const e = new ShipEditor(newShip('T', points ? { points } : {})); e.snap = 1; return e; };
const symmetric = (pts) => pts.every(([x, y]) => pts.some(([a, b]) => Math.abs(a + x) < 0.6 && Math.abs(b - y) < 0.6));

console.log('Ship editor');
test('shipped outlines are kept as the game has them (within rounding), and pass the checks', () => {
  for (const [name, s] of Object.entries(SHIPPED_SHIPS)) {
    const ship = newShip(name, { points: s.points, fields: { radius: s.radius, mass: s.mass } });
    assert.deepEqual(checkShip(ship).filter((r) => r.severity === 'warn' && r.id !== 'name-builtin'), [], name);
  }
});
test('adding a point with mirror on adds its twin and stays symmetrical', () => {
  const e = editor();
  const before = e.points.length;
  e.addPointAt(7, 2);
  assert.equal(e.points.length, before + 2);
  assert.ok(symmetric(e.points));
  assert.ok(e.points.some(([x, y]) => x === 7 && y === 2) && e.points.some(([x, y]) => x === -7 && y === 2));
});
test('adding on the centre line adds one point; mirror off adds one', () => {
  const e = editor();
  const at = e.addPointAt(0.2, 8);
  assert.equal(e.points.length, 5);
  assert.equal(e.points[at][0], 0);
  const f = editor(); f.mirror = false; f.addPointAt(7, 2);
  assert.equal(f.points.length, 5);
});
test('adding into an edge that is its own mirror image inserts both points in order', () => {
  const e = editor([[0, -10], [8, 8], [0, 8], [-8, 8]]);
  e.addPointAt(4, 8.2); // the tail edge from (8,8) to (0,8) ... and its mirror (0,8) to (-8,8) are different edges
  assert.ok(symmetric(e.points));
  const f = editor([[0, -10], [8, 10], [-8, 10]]); // the bottom edge crosses the centre line
  f.addPointAt(4, 10);
  assert.equal(f.points.length, 5);
  assert.ok(symmetric(f.points));
  assert.equal(new Set(f.points.map(String)).size, 5);
});
test('dragging a point moves its twin; the centre line stays put; clamped to the reach', () => {
  const e = editor();
  const i = e.points.findIndex(([x]) => x > 0);
  assert.ok(e.beginDrag(i));
  e.dragTo(5.2, 3.1); e.endDrag();
  assert.ok(symmetric(e.points));
  assert.ok(e.points.some(([x, y]) => x === 5 && y === 3));
  const nose = e.points.findIndex(([x, y]) => x === 0 && y < 0);
  e.beginDrag(nose); e.dragTo(3, -8); e.endDrag();
  assert.equal(e.points[nose][0], 0);
  e.beginDrag(nose); e.dragTo(0, -90); e.endDrag();
  assert.ok(Math.hypot(...e.points[nose]) <= 22.001);
});
test('a tap on a point without moving it is not an undo step', () => {
  const e = editor();
  e.beginDrag(0); e.endDrag();
  assert.equal(e.canUndo, false);
});
test('delete removes the twin too, never below 3 points', () => {
  const e = editor([[0, -12], [9, 10], [-9, 10]]);
  assert.equal(e.deletePoint(1), false);
  const f = editor(); // 4 points: nose, right, tail, left: removing a pair would leave 2, so only the one goes
  const right = f.points.findIndex(([x]) => x > 0);
  assert.equal(f.deletePoint(right), true);
  assert.equal(f.points.length, 3);
  const g = editor(); g.addPointAt(7, 2);
  const r = g.points.findIndex(([x, y]) => x === 7 && y === 2);
  assert.ok(g.deletePoint(r)); assert.equal(g.points.length, 4);
});
test('the 24 point limit is respected', () => {
  const e = editor(); e.mirror = false;
  for (let i = 0; i < 40; i++) e.addPointAt(Math.cos(i) * 12, Math.sin(i) * 12);
  assert.ok(e.points.length <= 24);
});
test('undo and redo restore the outline and its stats', () => {
  const e = editor();
  const start = JSON.stringify(e.ship);
  e.addPointAt(7, 2);
  const after = JSON.stringify(e.ship);
  e.undo(); assert.equal(JSON.stringify(e.ship), start);
  e.redo(); assert.equal(JSON.stringify(e.ship), after);
});
test('radius follows the outline; mass stays inside the range it allows', () => {
  const e = editor();
  e.scaleBy(1.6);
  assert.ok(e.ship.radius > 14);
  const [lo, hi] = massRange(e.ship.radius);
  assert.ok(e.ship.mass >= lo && e.ship.mass <= hi);
  e.setWeight(1); assert.equal(e.ship.mass, hi);
  e.setWeight(-1); assert.equal(e.ship.mass, lo);
  e.setWeight(0); assert.ok(Math.abs(e.weight) < 0.2);
  assert.deepEqual(checkShip(e.ship).filter((r) => r.severity === 'warn'), []);
});
test('a freehand stroke becomes a simplified outline, mirrored when mirror is on', () => {
  const stroke = []; // the right side of a dart, nose to tail, with jitter
  for (let t = 0; t <= 1; t += 0.02) stroke.push([9 * Math.sin(t * Math.PI) + (t % 0.1 < 0.02 ? 0.2 : 0), -12 + 24 * t]);
  const out = outlineFromStroke(stroke, { mirror: true });
  assert.ok(out.length >= 3 && out.length <= 16);
  assert.ok(symmetric(out));
  const e = editor(); assert.ok(e.drawStroke(stroke)); assert.ok(symmetric(e.points));
  const loop = []; for (let a = 0; a < 6.3; a += 0.1) loop.push([10 * Math.cos(a), 10 * Math.sin(a)]);
  const closed = outlineFromStroke(loop, { mirror: false });
  assert.ok(closed.length >= 3 && closed.length <= 16);
});
test('make symmetrical copies one half onto the other', () => {
  const lopsided = [[0, -12], [9, 10], [0, 6], [-4, 12]];
  const fixed = mirrorHalf(lopsided, 'right');
  assert.ok(symmetric(fixed));
  assert.equal(fixed.length, 4);
  const batter = mirrorHalf(SHIPPED_SHIPS['Bat-ter Up'].points, 'right');
  assert.ok(batter.length >= 3);
});
test('hit tests', () => {
  const pts = [[0, -10], [8, 8], [-8, 8]];
  assert.equal(hitPoint(pts, 7.5, 7.2), 1);
  assert.equal(hitPoint(pts, 0, 0), -1);
  assert.equal(nearestEdge(pts, 0, 9).edge, 1);
  assert.equal(partnerOf(pts, 1), 2);
  assert.equal(partnerOf(pts, 0), -1);
});
test('a ship made in the editor round-trips', () => {
  const e = editor(); e.addPointAt(7, 2); e.ship.name = 'Dart';
  const xml = writeShip(e.ship);
  assert.deepEqual(readShip(xml).ship, e.ship);
  assert.match(xml, /<PlayerIndex><\/PlayerIndex>|<PlayerIndex ?\/>/);
});
