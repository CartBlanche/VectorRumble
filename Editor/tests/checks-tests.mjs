import { readFileSync, readdirSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';
import assert from 'node:assert/strict';
import { test } from './harness.mjs';
import { readLevel, readShip } from '../js/io/xml-read.js';
import { newLevel, frameWalls } from '../js/model/level.js';
import { newShip, deriveStats } from '../js/model/ship.js';
import { checkLevel } from '../js/validate/level-checks.js';
import { checkShip } from '../js/validate/ship-checks.js';

const here = dirname(fileURLToPath(import.meta.url));
const load = (kind, reader, key) => readdirSync(join(here, 'fixtures', kind)).filter((f) => f.endsWith('.xml')).map((f) => [f, reader(readFileSync(join(here, 'fixtures', kind, f), 'utf8'))[key]]);
const ids = (results) => results.map((r) => r.id);
const lv = (walls, fields = {}) => newLevel('Test', { walls, fields });

console.log('Level checks');
for (const [file, level] of load('levels', readLevel, 'level')) {
  test(`the game's own ${file} has no warnings`, () => {
    // Built-in names are reserved for the game, so only the name check is skipped here
    const found = checkLevel(level).filter((r) => r.id !== 'name-builtin');
    assert.deepEqual(found.filter((r) => r.severity === 'warn'), []);
  });
}
test('a new level is clean', () => assert.deepEqual(checkLevel(newLevel('Fresh')), []));
test('names: builtin, clash, missing', () => {
  assert.ok(ids(checkLevel(newLevel('empty'))).includes('name-builtin'));
  assert.ok(ids(checkLevel(newLevel('Mine'), { others: ['Mine'] })).includes('name-clash'));
  assert.ok(ids(checkLevel(newLevel('  '))).includes('name-missing'));
});
test('bad colour', () => assert.ok(ids(checkLevel(lv(frameWalls(), { color: 'FF00' }))).includes('colour')));
test('no walls, outside the screen, beyond the play area, zero length, duplicate', () => {
  assert.ok(ids(checkLevel(lv([]))).includes('no-walls'));
  assert.ok(ids(checkLevel(lv([...frameWalls(), [0.5, 0.5, 1.2, 0.5]]))).includes('outside'));
  assert.ok(ids(checkLevel(lv([...frameWalls(), [0.5, 0.5, 0.97, 0.5]]))).includes('near-edge'));
  assert.ok(ids(checkLevel(lv([...frameWalls(), [0.5, 0.5, 0.5, 0.5]]))).includes('zero-length'));
  assert.ok(ids(checkLevel(lv([...frameWalls(), [0.05, 0.95, 0.05, 0.05]]))).includes('duplicate'));
});
test('too many walls', () => {
  const walls = Array.from({ length: 401 }, (_, i) => [0.1 + (i % 80) * 0.01, 0.1 + Math.floor(i / 80) * 0.1, 0.1 + (i % 80) * 0.01, 0.12 + Math.floor(i / 80) * 0.1]);
  assert.ok(ids(checkLevel(lv(walls))).includes('too-many'));
});
test('crossing walls are a note', () => {
  const r = checkLevel(lv([...frameWalls(), [0.3, 0.3, 0.7, 0.7], [0.3, 0.7, 0.7, 0.3]])).find((x) => x.id === 'crossing');
  assert.equal(r.severity, 'note');
});
test('a gap too narrow for a ship warns; a gap only the AI cannot use is a note; a roomy gap is clean', () => {
  const gap = (g) => [...frameWalls(), [0.5, 0.05, 0.5, 0.5 - g / 2 / 720], [0.5, 0.5 + g / 2 / 720, 0.5, 0.95]];
  const at = (g) => ids(checkLevel(lv(gap(g))));
  assert.ok(at(40).includes('gap-ship'));
  assert.ok(at(56).includes('gap-ai') && !at(56).includes('gap-ship'));
  assert.ok(!at(120).includes('gap-ship') && !at(120).includes('gap-ai'));
});
test('a closed room a ship could spawn in is found, and says where', () => {
  const room = [[0.4, 0.3, 0.6, 0.3], [0.6, 0.3, 0.6, 0.7], [0.6, 0.7, 0.4, 0.7], [0.4, 0.7, 0.4, 0.3]];
  const r = checkLevel(lv([...frameWalls(), ...room])).find((x) => x.id === 'pockets');
  assert.ok(r);
  assert.ok(r.at && r.at.every((v) => v > 0 && v < 1));
});
test('a level that fills the arena with walls says there is no room, or that it is cramped', () => {
  const bars = Array.from({ length: 9 }, (_, i) => [0.05, 0.1 + i * 0.1, 0.95, 0.1 + i * 0.1]);
  const found = ids(checkLevel(lv([...frameWalls(), ...bars])));
  assert.ok(found.includes('no-space') || found.includes('cramped') || found.includes('gap-ship'));
});

console.log('Ship checks');
for (const [file, ship] of load('ships', readShip, 'ship')) {
  test(`the game's own ${file} has no warnings`, () => {
    const found = checkShip(ship).filter((r) => r.id !== 'name-builtin' && r.severity === 'warn');
    assert.deepEqual(found.map((r) => `${r.id}: ${r.message}`), []);
  });
}
test('a new ship is clean', () => assert.deepEqual(checkShip(newShip('Dart')).filter((r) => r.severity === 'warn'), []));
test('the derived stats for the shipped ships land close to their own', () => {
  const real = { 'Nimble.xml': [15, 20], 'Bat-terUp.xml': [15, 20], 'Blaster.xml': [20, 32], 'Defender.xml': [20, 32], 'Destroyer.xml': [20, 32] };
  for (const [file, ship] of load('ships', readShip, 'ship')) {
    const d = deriveStats(ship.points);
    assert.ok(Math.abs(d.radius - real[file][0]) <= 3, `${file} radius ${d.radius}`);
    assert.ok(Math.abs(d.mass - real[file][1]) <= 8, `${file} mass ${d.mass}`);
  }
});
test('points: too few, too many, too far, same place, not numbers', () => {
  const s = (points, fields) => newShip('S', { points, fields: { ...fields, radius: 15, mass: 20 } });
  assert.ok(ids(checkShip(s([[0, -5], [5, 5]]))).includes('few-points'));
  assert.ok(ids(checkShip(s(Array.from({ length: 25 }, (_, i) => [Math.cos(i) * 8, Math.sin(i) * 8])))).includes('many-points'));
  assert.ok(ids(checkShip(s([[0, -30], [5, 5], [-5, 5]]))).includes('too-far'));
  assert.ok(ids(checkShip(s([[0, -8], [0, -8], [5, 5], [-5, 5]]))).includes('same-point'));
  assert.ok(ids(checkShip(s([[0, -8], [NaN, 5], [-5, 5]]))).includes('bad-point'));
});
test('a bow-tie outline crosses itself', () => {
  assert.ok(ids(checkShip(newShip('Bow', { points: [[-8, -8], [8, 8], [8, -8], [-8, 8]] }))).includes('crossing'));
});
test('tiny and huge', () => {
  assert.ok(ids(checkShip(newShip('T', { points: [[0, -2], [1, 1], [-1, 1]] }))).includes('tiny'));
  assert.ok(ids(checkShip(newShip('H', { points: [[-22, -22], [22, -22], [22, 22], [-22, 22]], fields: { radius: 24, mass: 40 } }))).includes('huge'));
});
test('mass and radius outside the budget, and an asymmetric note', () => {
  const base = newShip('B');
  assert.ok(ids(checkShip({ ...base, mass: 90 })).includes('mass-range'));
  assert.ok(ids(checkShip({ ...base, mass: 38 })).includes('mass-budget'));
  assert.ok(ids(checkShip({ ...base, radius: 5 })).includes('radius-range'));
  assert.ok(ids(checkShip({ ...base, radius: 12 })).includes('radius-small') || base.radius <= 12);
  const lopsided = newShip('L', { points: [[0, -10], [9, 8], [-2, 4]] });
  assert.equal(checkShip(lopsided).find((r) => r.id === 'asymmetric')?.severity, 'note');
});
test('ship names: builtin, clash', () => {
  assert.ok(ids(checkShip(newShip('Blaster'))).includes('name-builtin'));
  assert.ok(ids(checkShip(newShip('Mine'), { others: ['Mine'] })).includes('name-clash'));
});
test('a PlayerIndex on a community ship is a note', () => {
  assert.equal(checkShip({ ...newShip('P'), playerIndex: 'One' }).find((r) => r.id === 'player-index').severity, 'note');
});
