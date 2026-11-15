import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';
import assert from 'node:assert/strict';
import { test } from './harness.mjs';
import { History, cloneModel } from '../js/model/history.js';
import { newLevel } from '../js/model/level.js';
import { detectPlatform, whereToPutIt } from '../js/io/platform.js';
import { exportFileName } from '../js/io/export.js';
import { formatNumber, parseNumbers, isGameSafe } from '../js/io/numbers.js';
import { signedArea, selfIntersections } from '../js/geometry.js';
import { EDITOR_VERSION } from '../js/version.js';

const here = dirname(fileURLToPath(import.meta.url));

console.log('History');
test('undo and redo walk back and forth', () => {
  const h = new History();
  const level = newLevel('A');
  h.checkpoint(cloneModel(level));
  const edited = { ...cloneModel(level), name: 'B' };
  assert.equal(h.undo(edited).name, 'A');
  assert.equal(h.canRedo, true);
  assert.equal(h.redo(level).name, 'B');
  h.checkpoint(cloneModel(level));
  assert.equal(h.canRedo, false);
});

console.log('Files and platform');
test('export file names are safe', () => {
  assert.equal(exportFileName('A/B: C?'), 'A_B_ C_.xml');
  assert.equal(exportFileName('  ', 'level'), 'level.xml');
  assert.equal(exportFileName('Maze...'), 'Maze.xml');
});
test('the folder named for each platform and kind', () => {
  assert.ok(whereToPutIt('mac', 'level').includes('~/Library/Application Support/Vector Rumble/Arenas'));
  assert.ok(whereToPutIt('windows', 'ship').includes('%LOCALAPPDATA%\\Vector Rumble\\Ships'));
  assert.ok(whereToPutIt('linux', 'level').includes('~/.local/share/Vector Rumble/Arenas'));
  assert.ok(!/Levels|Community/.test(whereToPutIt('mac', 'level')));
  assert.ok(/Android/.test(whereToPutIt('android', 'level')) || /Not possible/.test(whereToPutIt('android', 'level')));
  assert.equal(detectPlatform({ userAgent: 'Mozilla/5.0 (Linux; Android 14)', platform: 'Linux armv8l' }), 'android');
  assert.equal(detectPlatform({ userAgent: 'Mozilla/5.0 (Macintosh)', platform: 'MacIntel', maxTouchPoints: 5 }), 'ios');
  assert.equal(detectPlatform({ userAgent: 'Mozilla/5.0 (Macintosh)', platform: 'MacIntel' }), 'mac');
});

console.log('Numbers and geometry');
test('numbers', () => {
  assert.equal(formatNumber(-0.0004), '0');
  assert.equal(formatNumber(1.23456), '1.235');
  assert.deepEqual(parseNumbers(' 1\n2   3 '), [1, 2, 3]);
  assert.equal(parseNumbers('1 x'), null);
  assert.ok(isGameSafe('1 2 3') && !isGameSafe('1  2') && !isGameSafe(' 1 2'));
});
test('polygon area sign and self intersection', () => {
  assert.equal(Math.abs(signedArea([[0, 0], [4, 0], [4, 3], [0, 3]])), 12);
  assert.equal(selfIntersections([[0, 0], [4, 0], [4, 3], [0, 3]]).length, 0);
  assert.equal(selfIntersections([[0, 0], [4, 3], [4, 0], [0, 3]]).length, 1);
});

console.log('Version');
test('version.js and package.json agree', () => {
  assert.equal(JSON.parse(readFileSync(join(here, '..', 'package.json'), 'utf8')).version, EDITOR_VERSION);
});
