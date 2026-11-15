import assert from 'node:assert/strict';
import { test } from './harness.mjs';
import { Library } from '../js/model/library.js';
import { newLevel } from '../js/model/level.js';
import { newShip } from '../js/model/ship.js';
import { writeAny } from '../js/io/xml-write.js';

const memory = () => { const m = new Map(); return { getItem: (k) => m.get(k) ?? null, setItem: (k, v) => m.set(k, v), removeItem: (k) => m.delete(k) }; };

console.log('Library');
test('levels and ships are kept apart, listed by name, and survive a reload', () => {
  const storage = memory();
  const lib = new Library(storage);
  const a = newLevel('Zed'), b = newLevel('Alpha'), s = newShip('Dart');
  lib.put(null, a, writeAny(a)); const bId = lib.put(null, b, writeAny(b)).id; const sId = lib.put(null, s, writeAny(s)).id;
  assert.deepEqual(lib.list('level').map((e) => e.name), ['Alpha', 'Zed']);
  assert.deepEqual(lib.list('ship').map((e) => e.name), ['Dart']);
  const again = new Library(storage);
  assert.deepEqual(again.list('level').map((e) => e.name), ['Alpha', 'Zed']);
  assert.equal(again.last.level, bId); assert.equal(again.last.ship, sId);
  assert.ok(again.get(bId).xml.includes('<Name>Alpha</Name>'));
});
test('unique names are per kind', () => {
  const lib = new Library(memory());
  const l = newLevel('New'); lib.put(null, l, writeAny(l));
  assert.equal(lib.uniqueName('level', 'New'), 'New 2');
  assert.equal(lib.uniqueName('ship', 'New'), 'New');
  assert.equal(lib.findByName('level', 'New').name, 'New');
  assert.equal(lib.findByName('ship', 'New'), null);
});
test('replace, remove and last', () => {
  const lib = new Library(memory());
  const l = newLevel('A'); const id = lib.put(null, l, writeAny(l)).id;
  l.name = 'B'; lib.put(id, l, writeAny(l));
  assert.equal(lib.list('level').length, 1); assert.equal(lib.list('level')[0].name, 'B');
  lib.remove(id);
  assert.equal(lib.list('level').length, 0); assert.equal(lib.last.level, null);
});
test('no storage: it works in memory and says so; a full store reports why', () => {
  const lib = new Library(null);
  const l = newLevel('A');
  assert.equal(lib.put(null, l, writeAny(l)).saved, false);
  assert.equal(lib.persistent, false); assert.ok(lib.error);
  const full = new Library({ getItem: () => null, setItem: () => { const e = new Error('x'); e.name = 'QuotaExceededError'; throw e; } });
  assert.equal(full.put(null, l, writeAny(l)).saved, false);
  assert.ok(/full/.test(full.error));
});
test('damaged storage starts empty', () => {
  const lib = new Library({ getItem: () => '{not json', setItem() {} });
  assert.equal(lib.list('level').length, 0);
});
