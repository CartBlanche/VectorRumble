// Vector Rumble Editor: the page. Wires the level editor, the library, the canvas and the dialogs together.
import { EDITOR_VERSION } from './version.js';
import { $, h } from './ui/dom.js';
import { dialog, messageDialog } from './ui/dialog.js';
import { LevelEditor } from './edit/level-editor.js';
import { GRID_SIZES } from './edit/snap.js';
import { SYMMETRIES } from './edit/symmetry.js';
import { SHAPES } from './edit/templates.js';
import { newLevel, MAX_WALLS } from './model/level.js';
import { Library } from './model/library.js';
import { NEON } from './model/builtins.js';
import { readAny, XmlError } from './io/xml-read.js';
import { writeLevel, writeShip } from './io/xml-write.js';
import { createShipsPage } from './ships-page.js';
import { checkLevel } from './validate/level-checks.js';
import { buildGrid } from './validate/grid.js';
import { Viewport } from './view/viewport.js';
import { drawLevel } from './view/draw-level.js';
import { attachPointer } from './view/pointer.js';
import { createPanels } from './ui/panels.js';
import { chooseTemplate } from './ui/new-dialog.js';
import { exportDialog } from './ui/export-dialog.js';
import { libraryDialog } from './ui/library-dialog.js';
import { helpDialog } from './ui/help-dialog.js';
import { loadSettings, saveSettings } from './settings.js';
import { TEMPLATES } from './edit/templates.js';

const settings = loadSettings();
const library = new Library();
const view = new Viewport();
const canvas = $('canvas');
const ctx = canvas.getContext('2d');
const draw = { selection: new Set(), grid: { on: true, size: 18 }, dpr: 1 };
let currentId = null;
let tool = 'select';
let problems = [];
let ready = false;
let timers = {};
let panels = null;
let activeTab = 'levels';
let levelProblems = [];
let ships = null;

$('brand-version').textContent = EDITOR_VERSION;

// ---- the editor ----
const editor = new LevelEditor(newLevel('New Arena'), (reason) => changed(reason));

function changed(reason) {
  draw.selection = editor.selection;
  draw.grid = { on: settings.grid, size: settings.gridSize };
  $('undo').disabled = !editor.canUndo;
  $('redo').disabled = !editor.canRedo;
  $('delete').disabled = editor.selection.size === 0;
  panels?.syncLevel();
  panels?.syncSelection();
  if (reason === 'open') { draw.chain = null; draw.preview = null; draw.marker = null; }
  if (reason !== 'select' && reason !== 'drag' && ready) {
    schedule('analyse', analyse, reason === 'open' ? 0 : 200);
    if (reason !== 'open') { schedule('save', save, 500); $('save-state').textContent = 'Saving…'; }
  } else if (reason === 'drag') {
    schedule('analyse', analyse, 250);
    schedule('save', save, 700);
  }
  if (reason !== 'select') updateHint();
  redraw();
}

function schedule(name, fn, ms) {
  clearTimeout(timers[name]);
  timers[name] = setTimeout(fn, ms);
}

// ---- checks and the space overlay ----
function analyse() {
  const level = editor.level;
  const grid = level.walls.length <= MAX_WALLS * 2 ? buildGrid(level.walls) : null;
  const others = library.list('level').filter((e) => e.id !== currentId).map((e) => e.name);
  problems = checkLevel(level, { others, grid });
  draw.space = settings.space ? grid : null;
  levelProblems = problems;
  if (activeTab === 'levels') showBadge(problems);
  panels.showChecks(problems);
  redraw();
}

function showBadge(list) {
  const warns = list.filter((p) => p.severity === 'warn').length;
  const badge = $('checks-badge');
  badge.disabled = false;
  badge.textContent = warns ? `${warns} warning${warns === 1 ? '' : 's'}` : (list.length ? `${list.length} note${list.length === 1 ? '' : 's'}` : 'No problems');
  badge.className = `checks-badge ${warns ? 'warn' : 'ok'}`;
  $('export-level').disabled = false;
}

// ---- saving ----
function save() {
  const level = editor.level;
  const result = library.put(currentId, level, writeLevel(level));
  currentId = result.id;
  $('save-state').textContent = result.saved ? 'Saved' : 'Not saved';
  const banner = $('banner');
  banner.hidden = result.saved;
  banner.textContent = library.error ?? '';
}

function openLevel(level, id) {
  currentId = id;
  editor.open(level);
  panels.show('level');
  view.fit();
  schedule('save', save, 0); // makes sure a freshly imported or created level is in the library, and last
}

function openEntry(id) {
  const entry = library.get(id);
  if (!entry) return;
  try {
    const parsed = readAny(entry.xml);
    if (parsed.kind === 'ship') { showTab('ships'); ships.openEntry(id); return; }
    if (activeTab !== 'levels') showTab('levels');
    currentId = id;
    editor.open(parsed.level);
    library.setLast('level', id);
    panels.show('level');
    view.fit();
    $('save-state').textContent = 'Saved';
  } catch (error) {
    messageDialog('Cannot open it', error.message);
  }
}

async function newFromTemplate(first = false) {
  const template = await chooseTemplate({ first });
  if (!template) return false;
  const level = newLevel(library.uniqueName('level', 'New Arena'), { walls: template.walls() });
  level.color = NEON[Math.floor(Math.random() * NEON.length)];
  openLevel(level, null);
  return true;
}

// ---- the panels ----
panels = createPanels({
  editor,
  onCheckClick(r) {
    if (r.wall != null) editor.select([r.wall]);
    draw.marker = r.at ?? null;
    if (r.at) {
      const [gx, gy] = [r.at[0], r.at[1]];
      if (view.scale > view.fitScale() * 1.01) view.centreOn(gx, gy);
    }
    clearTimeout(timers.marker);
    timers.marker = setTimeout(() => { draw.marker = null; redraw(); }, 3000);
    redraw();
  },
});

// ---- the Ships tab ----
ships = createShipsPage({
  library, settings,
  onStatus(text, list, error) {
    if (text != null) { $('save-state').textContent = text; const b = $('banner'); b.hidden = text !== 'Not saved'; if (!b.hidden) b.textContent = error ?? ''; }
    if (list && activeTab === 'ships') showBadge(list);
  },
});

// ---- drawing ----
let frame = 0;
function redraw() {
  if (frame) return;
  frame = requestAnimationFrame(() => {
    frame = 0;
    drawLevel(ctx, view, editor.level, draw);
  });
}

function resize() {
  const rect = canvas.parentElement.getBoundingClientRect();
  const dpr = window.devicePixelRatio || 1;
  draw.dpr = dpr;
  canvas.width = Math.max(1, Math.round(rect.width * dpr));
  canvas.height = Math.max(1, Math.round(rect.height * dpr));
  view.resize(rect.width, rect.height);
  redraw();
}
new ResizeObserver(resize).observe(canvas.parentElement);

// ---- tools and options ----
const options = () => ({ grid: settings.grid, gridSize: settings.gridSize, endpoints: settings.endpoints, angle: settings.angle, shape: settings.shape, sides: settings.sides, ruler: settings.ruler, addToSelection: $('add-select').getAttribute('aria-pressed') === 'true' });
const app = { editor, view, draw, tool: () => tool, options, redraw, onChain: () => updateHint() };
attachPointer(canvas, app);

const HINTS = {
  select: 'Tap a wall to select it. Drag a wall to move it, drag its round corner handles to reshape, or drag on empty space for a box.',
  wall: 'Drag to draw a wall, or tap points to chain walls.',
  chain: 'Tap the next point. Tap the last point again, or press Esc, to finish.',
  shapes: 'Choose a shape in the toolbar, then drag on the map.',
  erase: 'Tap a wall, or drag across several, to remove them.',
};
function updateHint() {
  $('hint').textContent = tool === 'wall' && draw.chain ? HINTS.chain : HINTS[tool];
}

function setTool(next) {
  if (tool === 'wall' && next !== 'wall') app.endChain?.();
  tool = next;
  for (const b of document.querySelectorAll('[data-tool]')) b.classList.toggle('active', b.dataset.tool === next);
  $('shape-options').hidden = next !== 'shapes';
  draw.preview = null; draw.snap = null; draw.hover = null;
  canvas.style.cursor = next === 'select' ? 'default' : 'crosshair';
  updateHint();
  redraw();
}
document.querySelectorAll('[data-tool]').forEach((b) => b.addEventListener('click', () => setTool(b.dataset.tool)));

function toggleButton(id, key, extra) {
  const b = $(id);
  const apply = () => { b.setAttribute('aria-pressed', String(!!settings[key])); extra?.(); };
  b.addEventListener('click', () => { settings[key] = !settings[key]; saveSettings(settings); apply(); changed('select'); });
  apply();
}
toggleButton('snap-grid', 'grid');
toggleButton('snap-ends', 'endpoints');
toggleButton('show-ship', 'ruler', () => { if (!settings.ruler) draw.ruler = null; });
toggleButton('show-space', 'space', () => { if (ready) analyse(); });
$('add-select').addEventListener('click', (e) => e.currentTarget.setAttribute('aria-pressed', String(e.currentTarget.getAttribute('aria-pressed') !== 'true')));

const angleButton = $('snap-angle');
const showAngle = () => { angleButton.textContent = `Angle: ${settings.angle ? `${settings.angle}°` : 'off'}`; angleButton.setAttribute('aria-pressed', String(!!settings.angle)); };
angleButton.addEventListener('click', () => { settings.angle = settings.angle === 0 ? 45 : settings.angle === 45 ? 15 : 0; saveSettings(settings); showAngle(); });
showAngle();

$('grid-size').replaceChildren(...GRID_SIZES.map((s) => h('option', { value: s }, `${s} px`)));
$('grid-size').value = settings.gridSize;
$('grid-size').addEventListener('change', (e) => { settings.gridSize = Number(e.target.value); saveSettings(settings); changed('select'); });

$('symmetry').replaceChildren(...SYMMETRIES.map((s) => h('option', { value: s.id, title: s.hint }, s.label)));
$('symmetry').addEventListener('change', (e) => { editor.symmetry = e.target.value; settings.symmetry = e.target.value; saveSettings(settings); panels.syncSelection(); });
$('symmetry').value = editor.symmetry = SYMMETRIES.some((s) => s.id === settings.symmetry) ? settings.symmetry : 'none';

$('shape-kind').replaceChildren(...SHAPES.map((s) => h('option', { value: s.id, title: s.hint }, s.label)));
$('shape-kind').value = SHAPES.some((s) => s.id === settings.shape) ? settings.shape : 'rect';
const showSides = () => { $('sides-label').hidden = !['poly', 'ring'].includes(settings.shape); };
$('shape-kind').addEventListener('change', (e) => { settings.shape = e.target.value; saveSettings(settings); showSides(); });
$('shape-sides').value = settings.sides;
$('shape-sides').addEventListener('input', (e) => { settings.sides = Math.min(16, Math.max(3, Math.round(Number(e.target.value)) || 6)); saveSettings(settings); });
showSides();

$('undo').addEventListener('click', () => editor.undo());
$('redo').addEventListener('click', () => editor.redo());
$('delete').addEventListener('click', () => editor.deleteSelected());
$('zoom-in').addEventListener('click', () => { view.zoomAt(view.width / 2, view.height / 2, 1.25); redraw(); });
$('zoom-out').addEventListener('click', () => { view.zoomAt(view.width / 2, view.height / 2, 0.8); redraw(); });
$('zoom-fit').addEventListener('click', () => { view.fit(); redraw(); });

// ---- keyboard ----
document.addEventListener('keydown', (event) => {
  const target = event.target;
  const typing = target instanceof HTMLElement && (target.matches('input:not([type="color"]):not([type="checkbox"]), textarea') || target.isContentEditable);
  if (document.querySelector('.overlay')) return;
  const mod = event.ctrlKey || event.metaKey;
  const k = event.key.toLowerCase();
  if (event.key === ' ' && !typing) { app.setSpace?.(true); if (target === document.body || target === canvas) event.preventDefault(); return; }
  if (typing || $('levels-pane').hidden) return;
  if (mod && k === 'z') { event.preventDefault(); event.shiftKey ? editor.redo() : editor.undo(); return; }
  if (mod && k === 'y') { event.preventDefault(); editor.redo(); return; }
  if (mod && k === 'a') { event.preventDefault(); editor.selectAll(); return; }
  if (mod && k === 'd') { event.preventDefault(); editor.duplicateSelected(); return; }
  if (mod) return;
  if (event.key === 'Delete' || event.key === 'Backspace') { event.preventDefault(); editor.deleteSelected(); return; }
  if (event.key === 'Escape') { if (draw.chain) { app.endChain(); updateHint(); redraw(); } else editor.clearSelection(); return; }
  if (event.key === 'Enter' && draw.chain) { app.endChain(); updateHint(); redraw(); return; }
  if (event.key === '?') { helpDialog(); return; }
  if (event.key.startsWith('Arrow')) {
    event.preventDefault();
    const step = event.shiftKey ? 3 : 1;
    const px = settings.grid ? settings.gridSize * step : step;
    const [dx, dy] = { ArrowLeft: [-1, 0], ArrowRight: [1, 0], ArrowUp: [0, -1], ArrowDown: [0, 1] }[event.key];
    editor.nudge((dx * px) / 1280, (dy * px) / 720);
    return;
  }
  const tools = { v: 'select', w: 'wall', s: 'shapes', e: 'erase' };
  if (tools[k]) setTool(tools[k]);
  else if (k === 'g') { settings.grid = !settings.grid; saveSettings(settings); $('snap-grid').setAttribute('aria-pressed', String(settings.grid)); changed('select'); }
  else if (k === '+' || k === '=') { view.zoomAt(view.width / 2, view.height / 2, 1.25); redraw(); }
  else if (k === '-') { view.zoomAt(view.width / 2, view.height / 2, 0.8); redraw(); }
});
document.addEventListener('keyup', (event) => { if (event.key === ' ') app.setSpace?.(false); });

// ---- header: new, import, library, export ----
$('new-level').addEventListener('click', () => (activeTab === 'ships' ? ships.newShipFlow() : newFromTemplate()));
$('open-help').addEventListener('click', () => helpDialog());
$('checks-badge').addEventListener('click', () => {
  if (activeTab === 'ships') document.querySelector('[data-sside="checks"]').click();
  else panels.show('checks');
});

async function doExport() {
  clearTimeout(timers.save);
  save();
  analyse();
  await exportDialog({ level: editor.level, xml: writeLevel(editor.level), problems });
}
$('export-level').addEventListener('click', () => (activeTab === 'ships' ? ships.doExport() : doExport()));

$('open-library').addEventListener('click', () => (activeTab === 'ships' ? shipLibrary() : levelLibrary()));

function levelLibrary() {
  return libraryDialog(library, {
    open: openEntry,
    duplicate(id) {
      try {
        const { level } = readAny(library.get(id).xml);
        level.name = library.uniqueName('level', `${level.name} copy`);
        library.put(null, level, writeLevel(level));
        library.setLast('level', currentId); // the copy is listed; the level being edited stays open
      } catch (error) { messageDialog('Cannot duplicate', error.message); }
    },
    exportEntry: async (id) => { openEntry(id); await doExport(); },
    remove(id) {
      library.remove(id);
      if (id === currentId) {
        const next = library.list('level')[0];
        if (next) openEntry(next.id);
        else openLevel(newLevel(library.uniqueName('level', 'New Arena')), null);
      }
    },
  });
}

function shipLibrary() {
  return libraryDialog(library, {
    open: (id) => ships.openEntry(id),
    duplicate(id) {
      try {
        const { ship } = readAny(library.get(id).xml);
        ship.name = library.uniqueName('ship', `${ship.name} copy`);
        library.put(null, ship, writeShip(ship));
        library.setLast('ship', ships.currentId); // the copy is listed; the ship being edited stays open
      } catch (error) { messageDialog('Cannot duplicate', error.message); }
    },
    exportEntry: async (id) => { if (ships.openEntry(id)) await ships.doExport(); },
    remove(id) {
      library.remove(id);
      if (id === ships.currentId) {
        const next = library.list('ship')[0];
        if (!(next && ships.openEntry(next.id))) ships.newShipFlow(true);
      }
    },
  }, 'ship');
}

$('open-file').addEventListener('change', async (event) => {
  const files = [...event.target.files];
  event.target.value = '';
  const notes = [];
  let openedFirst = false;
  for (const file of files) {
    try {
      const parsed = readAny(await file.text());
      const model = parsed.level ?? parsed.ship;
      const kind = parsed.kind;
      let id = null;
      const clash = library.findByName(kind, model.name);
      if (clash) {
        const choice = await dialog({
          title: 'Already in your library',
          body: h('p', {}, `A ${kind} called "${model.name}" is already in your library.`),
          buttons: [{ label: 'Skip', value: 'skip' }, { label: 'Import as a copy', value: 'copy' }, { label: 'Replace', value: 'replace', primary: true }],
          dismiss: 'skip',
        });
        if (choice === 'skip') { notes.push(`${file.name}: skipped.`); continue; }
        if (choice === 'replace') id = clash.id;
        else model.name = library.uniqueName(kind, `${model.name} copy`);
      }
      if (kind === 'level') {
        const xml = writeLevel(model);
        const put = library.put(id, model, xml);
        if (!openedFirst) { openEntry(put.id); openedFirst = true; }
        notes.push(`${file.name}: imported${parsed.warnings.length ? `. ${parsed.warnings.map((w) => w.message).join(' ')}` : '.'}`);
      } else {
        const put = library.put(id, model, writeShip(model));
        if (!openedFirst) { showTab('ships'); ships.openEntry(put.id); openedFirst = true; }
        notes.push(`${file.name}: imported${parsed.warnings.length ? `. ${parsed.warnings.map((w) => w.message).join(' ')}` : '.'}`);
      }
    } catch (error) {
      notes.push(`${file.name}: ${error instanceof XmlError ? error.message : `could not be read (${error.message})`}`);
    }
  }
  if (files.length && (notes.length > 1 || notes.some((n) => !/: imported\.$/.test(n)))) await messageDialog('Import', notes);
  saveSettings(settings);
});

// ---- theme, tabs, phone hint ----
$('theme-toggle').addEventListener('click', () => {
  const dark = document.documentElement.dataset.theme
    ? document.documentElement.dataset.theme === 'dark'
    : !window.matchMedia('(prefers-color-scheme: light)').matches;
  settings.theme = dark ? 'light' : 'dark';
  document.documentElement.dataset.theme = settings.theme;
  saveSettings(settings);
  redraw();
});

function showTab(name) {
  activeTab = name;
  $('levels-pane').hidden = name !== 'levels';
  $('ships-pane').hidden = name !== 'ships';
  $('tab-levels').setAttribute('aria-selected', String(name === 'levels'));
  $('tab-ships').setAttribute('aria-selected', String(name === 'ships'));
  $('new-level').title = name === 'ships' ? 'Start a new ship' : 'Start a new arena';
  if (name === 'levels') { ships.hide(); resize(); if (ready) { showBadge(levelProblems); redraw(); } } else { ships.show(); }
  if (settings.tab !== name) { settings.tab = name; saveSettings(settings); }
}
$('tab-levels').addEventListener('click', () => showTab('levels'));
$('tab-ships').addEventListener('click', () => showTab('ships'));

if (window.matchMedia('(max-width: 700px) and (orientation: portrait)').matches && matchMedia('(pointer: coarse)').matches) {
  try { if (!sessionStorage.getItem('vr-rotate-dismissed')) $('rotate-hint').hidden = false; } catch { $('rotate-hint').hidden = false; }
}
$('rotate-dismiss').addEventListener('click', () => { $('rotate-hint').hidden = true; try { sessionStorage.setItem('vr-rotate-dismissed', '1'); } catch { /* fine */ } });

// ---- start ----
async function start() {
  setTool('select');
  const last = library.last.level ?? library.list('level')[0]?.id;
  if (last && library.get(last)) openEntry(last);
  else {
    const made = await newFromTemplate(true);
    if (!made) openLevel(newLevel('New Arena', { walls: TEMPLATES[0].walls() }), null);
  }
  ready = true;
  analyse();
  if (settings.tab === 'ships') showTab('ships');
  if (!library.persistent) { $('banner').hidden = false; $('banner').textContent = library.error ?? 'This browser is not keeping data for the page.'; }
  $('save-state').textContent = library.persistent ? 'Saved' : '';
  window.vrEditor = { editor, view, draw, app, library, settings, get problems() { return problems; }, get tool() { return tool; }, setTool, showTab, ships, ready: true };
}
resize();
start();
