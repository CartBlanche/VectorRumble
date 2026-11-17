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

console.log('Thrusters');
import { autoThrusters, thrustersOf, flameColour } from '../js/model/ship.js';
import { hitThruster, thrusterPartner } from '../js/edit/ship-editor.js';

test('not placed: the editor shows what the game would, from the outline', () => {
  const e = editor([[0, -12], [9, 10], [0, 6], [-9, 10]]);
  assert.equal(e.ship.thrusters, null);
  assert.deepEqual(e.thrusters.map((t) => [t.x, t.y]), [[9, 10], [-9, 10]]);
  assert.deepEqual(thrustersOf(e.ship), autoThrusters(e.points));
});
test('the shipped ships carry the thrusters their files have', () => {
  for (const [name, s] of Object.entries(SHIPPED_SHIPS)) {
    assert.ok(Array.isArray(s.thrusters) && s.thrusters.length >= 1 && s.thrusters.length <= 3, name);
    for (const t of s.thrusters) assert.ok(Math.hypot(t.x, t.y) <= 22, name);
  }
  assert.equal(SHIPPED_SHIPS.Destroyer.thrusters.length, 3);
});
test('adding a thruster places it, with a twin when mirror is on, and owns the list', () => {
  const e = editor();
  const i = e.addThrusterAt(3.2, 8.1);
  assert.equal(i, 0);
  assert.deepEqual(e.ship.thrusters.map((t) => [t.x, t.y, t.size]), [[3, 8, 1], [-3, 8, 1]]);
  assert.equal(e.selectedThruster, 0);
  assert.equal(e.selected, -1);
  const c = editor(); c.mirror = false; c.addThrusterAt(3, 8);
  assert.equal(c.ship.thrusters.length, 1);
});
test('a thruster on the centre line is one thruster; the limit is three', () => {
  const e = editor();
  e.addThrusterAt(0.2, 8);
  assert.deepEqual(e.ship.thrusters.map((t) => t.x), [0]);
  e.addThrusterAt(4, 8); // room for a pair
  assert.equal(e.ship.thrusters.length, 3);
  assert.equal(e.addThrusterAt(6, 8), -1);
  assert.equal(e.ship.thrusters.length, 3);
  const f = editor();
  f.addThrusterAt(4, 8);
  f.addThrusterAt(5, 9); // a pair would make four: only the one goes in
  assert.equal(f.ship.thrusters.length, 3);
});
test('dragging a thruster moves its twin; a tap without moving changes nothing', () => {
  const e = editor();
  e.addThrusterAt(4, 8);
  const i = e.ship.thrusters.findIndex((t) => t.x > 0);
  assert.ok(e.beginThrusterDrag(i));
  e.dragTo(5.2, 9.4); e.endDrag();
  assert.deepEqual(e.ship.thrusters.map((t) => [t.x, t.y]).sort(), [[-5, 9], [5, 9]]);
  const undos = e.history.canUndo;
  const f = editor();
  assert.ok(f.beginThrusterDrag(0)); // an unplaced one: grabbing it places the list...
  f.endDrag();                       // ...but not moving it puts that back
  assert.equal(f.ship.thrusters, null);
  assert.ok(undos);
  e.undo(); // the drag
  assert.deepEqual(e.ship.thrusters.map((t) => [t.x, t.y]).sort(), [[-4, 8], [4, 8]]);
});
test('a dragged thruster stays within the reach', () => {
  const e = editor();
  e.addThrusterAt(0, 8);
  e.beginThrusterDrag(0); e.dragTo(40, 40); e.endDrag();
  assert.ok(Math.hypot(e.ship.thrusters[0].x, e.ship.thrusters[0].y) <= 22.001);
});
test('deleting a thruster removes its twin; undo brings both back', () => {
  const e = editor();
  e.addThrusterAt(4, 8);
  assert.ok(e.deleteThruster(0));
  assert.deepEqual(e.ship.thrusters, []);
  e.undo();
  assert.equal(e.ship.thrusters.length, 2);
  e.selectThruster(1);
  assert.ok(e.deleteSelected());
  assert.equal(e.ship.thrusters.length, 0);
});
test('size is kept to 0.5 to 2 and moves a pair together; one undo step per slide', () => {
  const e = editor();
  e.addThrusterAt(4, 8);
  e.setThrusterSize(1.34); e.setThrusterSize(1.7); e.setThrusterSize(9);
  assert.deepEqual(e.ship.thrusters.map((t) => t.size), [2, 2]);
  e.setThrusterSize(0);
  assert.deepEqual(e.ship.thrusters.map((t) => t.size), [0.5, 0.5]);
  e.undo();
  assert.deepEqual(e.ship.thrusters.map((t) => t.size), [1, 1]);
});
test('Automatic puts them back to not placed; None means no flame', () => {
  const e = editor();
  e.addThrusterAt(4, 8);
  assert.ok(e.resetThrusters());
  assert.equal(e.ship.thrusters, null);
  assert.equal(e.resetThrusters(), false);
  assert.ok(e.clearThrusters());
  assert.deepEqual(e.ship.thrusters, []);
  assert.deepEqual(e.thrusters, []);
});
test('centre, scale and flip take placed thrusters with the outline', () => {
  const e = editor([[0, -10], [8, 8], [0, 4], [-8, 8]]);
  e.addThrusterAt(0, 6);
  e.flip();
  assert.equal(e.ship.thrusters[0].y, -6);
  const f = editor([[0, -10], [8, 8], [0, 4], [-8, 8]]);
  f.addThrusterAt(0, 6);
  f.scaleBy(0.5);
  assert.equal(f.ship.thrusters[0].y, 3);
  const g = editor([[0, 0], [8, 8], [-8, 8]]);
  g.addThrusterAt(0, 6);
  g.centre();
  assert.equal(g.ship.thrusters[0].y, 2);
});
test('hit tests and partners', () => {
  const list = [{ x: 4, y: 8 }, { x: -4, y: 8 }, { x: 0, y: 10 }];
  assert.equal(hitThruster(list, 4.5, 8.2, 2), 0);
  assert.equal(hitThruster(list, 20, 20, 2), -1);
  assert.equal(thrusterPartner(list, 0), 1);
  assert.equal(thrusterPartner(list, 2), -1);
});
test('thrusters are written after the outline and read back; not placed writes nothing', () => {
  const e = editor();
  assert.ok(!writeShip(e.ship).includes('Thrusters'));
  e.addThrusterAt(4, 8);
  const xml = writeShip(e.ship);
  assert.ok(xml.indexOf('</Shape>') < xml.indexOf('<Thrusters>') && xml.indexOf('</Thrusters>') < xml.indexOf('<Name>'));
  assert.ok(xml.includes('<Thruster X="4" Y="8" Size="1" />'));
  const back = readShip(xml);
  assert.deepEqual(back.warnings, []);
  assert.deepEqual(back.ship.thrusters, e.ship.thrusters);
  e.clearThrusters();
  const none = writeShip(e.ship);
  assert.ok(none.includes('<Thrusters />'));
  assert.deepEqual(readShip(none).ship.thrusters, []);
  assert.equal(readShip(writeShip(newShip('Plain'))).ship.thrusters, null);
});
test('a file with a bad thruster is read with a warning the game would agree with', () => {
  const base = writeShip(newShip('X')).replace('<Name>', '<Thrusters><Thruster X="a" Y="2" /><Thruster X="1" Y="2" Size="big" /></Thrusters><Name>');
  const { ship, warnings } = readShip(base);
  assert.equal(warnings.length, 2);
  assert.ok(warnings.every((w) => w.where === 'Thrusters'));
  assert.equal(ship.thrusters.length, 1);
});
test('an unknown element is still kept next to thrusters', () => {
  const xml = writeShip(newShip('X')).replace('<Name>', '<Thrusters><Thruster X="1" Y="2" /></Thrusters><Mystery>1</Mystery><Name>');
  const out = writeShip(readShip(xml).ship);
  assert.ok(out.includes('<Mystery>1</Mystery>') && out.includes('<Thruster X="1" Y="2" Size="1" />'));
});
test('checks: off the hull, too far, too many, odd size, none', () => {
  const ids = (thrusters) => checkShip(newShip('Z', { fields: { thrusters } })).map((r) => r.id);
  assert.ok(!ids(null).some((id) => id.startsWith('thruster')));
  assert.ok(!ids([{ x: 9, y: 10, size: 1 }]).some((id) => id.startsWith('thruster')));
  assert.ok(ids([{ x: 0, y: 3, size: 1 }]).includes('thruster-off-hull') === false); // inside, but within 4 px of the edge at (0,6)
  assert.ok(ids([{ x: 0, y: -1, size: 1 }]).includes('thruster-off-hull'));
  assert.ok(ids([{ x: 0, y: 30, size: 1 }]).includes('thruster-far'));
  assert.ok(ids(Array.from({ length: 4 }, () => ({ x: 9, y: 10, size: 1 }))).includes('thrusters-many'));
  assert.ok(ids([{ x: 9, y: 10, size: 3 }]).includes('thruster-size'));
  assert.ok(ids([]).includes('thrusters-none'));
  const r = checkShip(newShip('Z', { fields: { thrusters: [{ x: 9, y: 10, size: 1 }, { x: 0, y: 30, size: 1 }] } })).find((x) => x.id === 'thruster-far');
  assert.equal(r.thruster, 1);
});
test('the flame colour is amber, ice blue on red, orange and yellow ships', () => {
  assert.equal(flameColour('FF00FF00'), 'FFFFA000');
  assert.equal(flameColour('FF6495ED'), 'FFFFA000');
  assert.equal(flameColour('FFFF00FF'), 'FFFFA000');
  assert.equal(flameColour('FFFFFFFF'), 'FFFFA000');
  for (const red of ['FFFF0000', 'FFFF8000', 'FFFFFF00']) assert.equal(flameColour(red), 'FF6EC8FF', red);
});
