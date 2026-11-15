// Run with: node tests/run.mjs
import { readFileSync, readdirSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';
import assert from 'node:assert/strict';
import { parseXml, XmlError } from '../js/io/xml.js';
import { readLevel, readShip, readAny, assetKind } from '../js/io/xml-read.js';
import { writeLevel, writeShip } from '../js/io/xml-write.js';
import { newLevel } from '../js/model/level.js';
import { newShip } from '../js/model/ship.js';
import { canonicalise } from './canon.mjs';
import { EDITOR_VERSION } from '../js/version.js';
import { test, summary } from './harness.mjs';

const here = dirname(fileURLToPath(import.meta.url));
const fixtures = (kind) => {
  const dir = join(here, 'fixtures', kind);
  return readdirSync(dir).filter((f) => f.endsWith('.xml')).sort().map((f) => [f, readFileSync(join(dir, f), 'utf8')]);
};

console.log('XML parser');
test('entities, attributes, comments, self-closing', () => {
  const doc = parseXml('<?xml version="1.0"?>\n<a x="1 &amp; 2"><!-- c --><b/><c>t &lt;1&gt;</c></a>');
  const a = doc.children[0];
  assert.deepEqual(a.attrs, [['x', '1 & 2']]);
  assert.equal(a.children[0].type, 'comment');
  assert.equal(a.children[2].children[0].value, 't <1>');
});
test('mismatched tags are rejected', () => assert.throws(() => parseXml('<a><b></a>'), XmlError));
test('non-game files are rejected', () => {
  assert.throws(() => readLevel('<Other/>'), XmlError);
  assert.throws(() => assetKind('<XnaContent><Asset Type="VectorRumble:Weapon"/></XnaContent>'), XmlError);
});
test('a ship is not read as a level and vice versa', () => {
  assert.throws(() => readLevel(fixtures('ships')[0][1]), XmlError);
  assert.throws(() => readShip(fixtures('levels')[0][1]), XmlError);
  assert.equal(readAny(fixtures('ships')[0][1]).kind, 'ship');
});

console.log('Round trip of the game\'s own levels and ships');
for (const [file, original] of fixtures('levels')) {
  test(`level ${file}: no warnings, same meaning, stable, stamped once`, () => {
    const { level, warnings } = readLevel(original);
    assert.deepEqual(warnings, []);
    const saved = writeLevel(level);
    assert.deepEqual(canonicalise(saved), canonicalise(original));
    assert.equal(writeLevel(readLevel(saved).level), saved);
    assert.equal(saved.split('exported by Vector Rumble Editor').length - 1, 1);
    assert.ok(saved.includes(`exported by Vector Rumble Editor ${EDITOR_VERSION}`));
  });
}
for (const [file, original] of fixtures('ships')) {
  test(`ship ${file}: no warnings, same meaning, stable, stamped once`, () => {
    const { ship, warnings } = readShip(original);
    assert.deepEqual(warnings, []);
    const saved = writeShip(ship);
    // The game's files write coordinates like 1.03 and 0.00; numbers compare by value
    const numeric = (xml) => JSON.stringify(canonicalise(xml), (k, v) => (typeof v === 'string' && /^[-\d. ]+$/.test(v) ? v.split(' ').map((n) => +n).join(' ') : v));
    assert.equal(numeric(saved), numeric(original));
    assert.equal(writeShip(readShip(saved).ship), saved);
    assert.equal(saved.split('exported by Vector Rumble Editor').length - 1, 1);
  });
}

console.log('Lenient import and lossless extras');
const messy = `<?xml version="1.0" encoding="utf-8"?>
<XnaContent xmlns:VectorRumble="VectorRumble">
  <!-- exported by Vector Rumble Editor 0.0.1 -->
  <Asset Type="VectorRumble:Arena" Custom="keep">
    <!-- lead comment -->
    <Name>Messy</Name>
    <Color>ff112233</Color>
    <Walls>0.1  0.1 0.9 0.1
       0.9 0.1 0.9 0.9 </Walls>
    <Spare>x</Spare>
    <!-- tail comment -->
  </Asset>
</XnaContent>`;
test('messy whitespace is reported where the game would fail', () => {
  assert.deepEqual(readLevel(messy).warnings.map((w) => w.where), ['Walls']);
});
test('messy file is saved cleanly, the old stamp replaced, extras kept', () => {
  const saved = writeLevel(readLevel(messy).level);
  assert.ok(saved.includes('<Walls>0.1 0.1 0.9 0.1 0.9 0.1 0.9 0.9</Walls>'));
  assert.ok(saved.includes('<Color>FF112233</Color>'));
  assert.equal(saved.split('exported by').length - 1, 1);
  assert.ok(!saved.includes('0.0.1'));
  for (const piece of ['Custom="keep"', '<!-- lead comment -->', '<!-- tail comment -->', '<Spare>x</Spare>']) assert.ok(saved.includes(piece), `missing ${piece}`);
  assert.deepEqual(readLevel(saved).warnings, []);
});
test('missing and bad fields are warned about, not thrown', () => {
  const noWalls = '<XnaContent><Asset Type="VectorRumble:Arena"><Name>A</Name><Color>12345</Color></Asset></XnaContent>';
  const w = readLevel(noWalls).warnings.map((x) => x.where).sort();
  assert.deepEqual(w, ['Color', 'Walls']);
  assert.throws(() => readLevel('<XnaContent><Asset Type="VectorRumble:Arena"><Name>A</Name><Color>FF000000</Color><Walls>0.1 x</Walls></Asset></XnaContent>'), XmlError);
});
test('a stray wall number is dropped with a warning', () => {
  const xml = '<XnaContent><Asset Type="VectorRumble:Arena"><Name>A</Name><Color>FF000000</Color><Walls>0.1 0.1 0.9 0.1 0.5</Walls></Asset></XnaContent>';
  const { level, warnings } = readLevel(xml);
  assert.equal(level.walls.length, 1);
  assert.equal(warnings.length, 1);
});
test('ship Mass and Radius that are not whole numbers are warned about', () => {
  const xml = (m) => `<XnaContent><Asset Type="VectorRumble:Ship"><Mass>${m}</Mass><Radius>15</Radius><Color>FF00FF00</Color><Shape><Points>0 -5 5 5 -5 5</Points></Shape><Name>S</Name><PlayerIndex></PlayerIndex></Asset></XnaContent>`;
  assert.deepEqual(readShip(xml('20')).warnings, []);
  assert.deepEqual(readShip(xml('20.5')).warnings.map((w) => w.where), ['Mass']);
});

console.log('New models');
test('a new level and a new ship write and read back unchanged', () => {
  const level = newLevel('Fresh');
  level.author = 'Ann & Bo'; level.description = 'A <test>';
  assert.deepEqual(readLevel(writeLevel(level)), { level, warnings: [] });
  const ship = newShip('Dart');
  assert.deepEqual(readShip(writeShip(ship)), { ship, warnings: [] });
});
test('a new ship has game-readable stats and an empty PlayerIndex', () => {
  const xml = writeShip(newShip('Dart'));
  assert.ok(/<Mass>\d+<\/Mass>/.test(xml) && /<Radius>\d+<\/Radius>/.test(xml));
  assert.ok(xml.includes('<PlayerIndex></PlayerIndex>'));
});
test('no double spaces or padding in written numbers', () => {
  const level = newLevel('N', { walls: [[0.123456, 0.1, 0.9, 0.5000001]] });
  const xml = writeLevel(level);
  assert.ok(xml.includes('<Walls>0.123 0.1 0.9 0.5</Walls>'));
});

await import('./checks-tests.mjs');
await import('./edit-tests.mjs');
await import('./library-tests.mjs');
await import('./misc-tests.mjs');
await import('./ship-tests.mjs');
summary();
