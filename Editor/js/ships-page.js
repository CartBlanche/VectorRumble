// The Ships tab: canvas, toolbar, side panel, live preview, checks, autosave and export. main.js creates it and hands over the tab.
import { $, h } from './ui/dom.js';
import { messageDialog, dialog } from './ui/dialog.js';
import { ShipEditor, SNAPS } from './edit/ship-editor.js';
import { newShip, MAX_POINTS, MAX_THRUSTERS, MIN_THRUSTER_SIZE, MAX_THRUSTER_SIZE, massRange, baseMass } from './model/ship.js';
import { SHIPPED_SHIPS } from './model/ship-builtins.js';
import { NEON } from './model/builtins.js';
import { writeShip } from './io/xml-write.js';
import { readAny } from './io/xml-read.js';
import { checkShip } from './validate/ship-checks.js';
import { ShipView } from './view/ship-view.js';
import { drawShipEditor, renderShipPicture } from './view/draw-ship.js';
import { attachShipPointer } from './view/ship-pointer.js';
import { createPreview } from './view/preview.js';
import { cssColour } from './view/draw-level.js';
import { chooseShipStart } from './ui/ship-new-dialog.js';
import { exportShipDialog } from './ui/ship-export-dialog.js';
import { saveSettings } from './settings.js';
import { signedArea } from './geometry.js';

const HINTS = {
  move: 'Drag a point to move it. Tap a point to select it (Delete removes it). Pinch or wheel to zoom.',
  add: 'Tap the outline to add a point there, then drag to place it. With Mirror on, its twin appears on the other side.',
  delete: 'Tap a point to remove it. A ship needs at least 3.',
  thruster: (mirror) => `Tap to add a thruster: a flame that points straight back. Drag one to move it, tap one to select it (Delete removes it). Up to ${MAX_THRUSTERS}.${mirror ? ' With Mirror on, a pair appears.' : ''}`,
  draw: (mirror) => (mirror
    ? 'Draw the RIGHT side of the ship from the nose down to the tail. The left side is made for you.'
    : 'Draw the whole outline in one stroke. It is closed up and simplified to a few points.'),
};

export function createShipsPage({ library, settings, onStatus }) {
  const canvas = $('ship-canvas');
  const view = new ShipView();
  const state = { selected: -1, hover: -1, ghosts: [], stroke: null, add: null, mirror: true, dpr: 1, radius: 0, problemPoints: new Set() };
  let currentId = null;
  let tool = 'move';
  let problems = [];
  let ready = false;
  let visible = false;
  const timers = {};
  let frame = 0;

  const editor = new ShipEditor(newShip('New Ship'), (reason) => changed(reason));
  editor.mirror = settings.mirror !== false;
  editor.snap = SNAPS.includes(settings.shipSnap) ? settings.shipSnap : 1;
  const preview = createPreview($('ship-preview'), () => editor.ship);

  const schedule = (name, fn, ms) => { clearTimeout(timers[name]); timers[name] = setTimeout(fn, ms); };

  function changed(reason) {
    state.selected = editor.selected;
    state.mirror = editor.mirror;
    state.radius = editor.ship.radius;
    $('ship-undo').disabled = !editor.canUndo;
    $('ship-redo').disabled = !editor.canRedo;
    $('ship-delete').disabled = !(editor.selectedThruster >= 0 || (editor.selected >= 0 && editor.points.length > 3));
    state.selectedThruster = editor.selectedThruster;
    syncPanel();
    if (reason === 'open') { state.stroke = null; state.add = null; state.hover = -1; }
    if (ready && reason !== 'select') {
      schedule('analyse', analyse, reason === 'drag' ? 250 : 120);
      if (reason !== 'open') { schedule('save', save, reason === 'drag' ? 700 : 500); onStatus?.('Saving…'); }
    }
    if (reason === 'select') updateHint();
    redraw();
  }

  // ---- checks ----
  function analyse() {
    const others = library.list('ship').filter((e) => e.id !== currentId).map((e) => e.name);
    problems = checkShip(editor.ship, { others });
    state.problemPoints = new Set(problems.filter((p) => p.point != null).map((p) => p.point));
    showChecks();
    onStatus?.(null, problems);
    redraw();
  }

  function showChecks() {
    const body = $('ship-side-checks');
    body.replaceChildren();
    if (!problems.length) { body.append(h('p', { class: 'checks-ok' }, 'No problems found. This ship is ready to export.')); return; }
    const warns = problems.filter((r) => r.severity === 'warn');
    body.append(h('p', { class: 'stats' }, `${warns.length} warning${warns.length === 1 ? '' : 's'}, ${problems.length - warns.length} note${problems.length - warns.length === 1 ? '' : 's'}. Warnings never stop you saving or exporting.`));
    for (const r of [...warns, ...problems.filter((x) => x.severity !== 'warn')]) {
      body.append(h('button', { type: 'button', class: `check ${r.severity}`, onclick: () => { if (r.point != null) editor.select(r.point); else if (r.thruster != null) editor.selectThruster(r.thruster); } }, h('span', { class: 'dot' }), h('span', {}, r.message)));
    }
  }

  // ---- saving ----
  function save() {
    const result = library.put(currentId, editor.ship, writeShip(editor.ship));
    currentId = result.id;
    onStatus?.(result.saved ? 'Saved' : 'Not saved', null, library.error);
  }

  function openShip(ship, id = null) {
    currentId = id;
    editor.open(ship);
    view.fit();
    schedule('save', save, 0);
    syncGhosts();
  }

  function openEntry(id) {
    const entry = library.get(id);
    if (!entry) return false;
    try {
      const parsed = readAny(entry.xml);
      if (parsed.kind !== 'ship') return false;
      currentId = id;
      editor.open(parsed.ship);
      library.setLast('ship', id);
      view.fit();
      syncGhosts();
      onStatus?.('Saved');
      return true;
    } catch (error) {
      messageDialog('Cannot open it', error.message);
      return false;
    }
  }

  async function newShipFlow(first = false) {
    const start = await chooseShipStart({ first });
    if (!start) return false;
    const base = start.shipped ? `${start.name} remix` : 'New Ship';
    const name = library.uniqueName('ship', base);
    const ship = start.shipped
      ? newShip(name, { points: start.points.map((p) => p.slice()), fields: { color: start.color, radius: start.radius, mass: start.mass, thrusters: start.thrusters?.map((t) => ({ ...t })) ?? null } })
      : newShip(name);
    if (!start.shipped) ship.color = NEON[Math.floor(Math.random() * NEON.length)];
    openShip(ship, null);
    return true;
  }

  async function doExport() {
    clearTimeout(timers.save);
    save();
    analyse();
    await exportShipDialog({ ship: editor.ship, xml: writeShip(editor.ship), problems });
  }

  // ---- drawing ----
  function redraw() {
    if (frame || !visible) return;
    frame = requestAnimationFrame(() => {
      frame = 0;
      drawShipEditor(canvas.getContext('2d'), view, editor.ship, state);
    });
  }

  function resize() {
    const rect = canvas.parentElement.getBoundingClientRect();
    if (!rect.width || !rect.height) return;
    const dpr = window.devicePixelRatio || 1;
    state.dpr = dpr;
    canvas.width = Math.max(1, Math.round(rect.width * dpr));
    canvas.height = Math.max(1, Math.round(rect.height * dpr));
    view.resize(rect.width, rect.height);
    redraw();
  }
  new ResizeObserver(resize).observe(canvas.parentElement);

  const app = { editor, view, state, tool: () => tool, redraw, onFullThrusters: () => { $('ship-hint').textContent = `A ship has at most ${MAX_THRUSTERS} thrusters. Drag one to move it, or select one and press Delete.`; }, onShortStroke: () => { $('ship-hint').textContent = 'That stroke was too short. Press and drag to draw the outline.'; } };
  const pointer = attachShipPointer(canvas, app);

  // ---- toolbar ----
  const hint = () => { const t = HINTS[tool]; return typeof t === 'function' ? t(editor.mirror) : t; };
  function updateHint() { $('ship-hint').textContent = hint(); }
  function setTool(next) {
    tool = next;
    for (const b of document.querySelectorAll('[data-ship-tool]')) b.classList.toggle('active', b.dataset.shipTool === next);
    canvas.style.cursor = next === 'move' ? 'default' : 'crosshair';
    state.add = null; state.stroke = null; state.addThruster = null;
    updateHint();
    redraw();
  }
  document.querySelectorAll('[data-ship-tool]').forEach((b) => b.addEventListener('click', () => setTool(b.dataset.shipTool)));

  const mirrorButton = $('ship-mirror');
  const showMirror = () => { mirrorButton.setAttribute('aria-pressed', String(editor.mirror)); state.mirror = editor.mirror; updateHint(); redraw(); };
  mirrorButton.addEventListener('click', () => { editor.mirror = !editor.mirror; settings.mirror = editor.mirror; saveSettings(settings); showMirror(); });
  showMirror();

  const snap = $('ship-snap');
  snap.replaceChildren(...SNAPS.map((s) => h('option', { value: s }, `${s} px`)));
  snap.value = editor.snap;
  snap.addEventListener('change', (e) => { editor.snap = Number(e.target.value); settings.shipSnap = editor.snap; saveSettings(settings); });

  const ghost = $('ship-ghost');
  ghost.replaceChildren(h('option', { value: 'none' }, 'None'), h('option', { value: 'all' }, 'All five'), ...Object.keys(SHIPPED_SHIPS).map((n) => h('option', { value: n }, n)));
  ghost.value = settings.ghost && (settings.ghost === 'none' || settings.ghost === 'all' || SHIPPED_SHIPS[settings.ghost]) ? settings.ghost : 'Blaster';
  function syncGhosts() {
    const pick = ghost.value;
    state.ghosts = pick === 'none' ? [] : Object.entries(SHIPPED_SHIPS).filter(([n]) => pick === 'all' || n === pick).map(([name, s]) => ({ name, points: s.points, color: s.color }));
    redraw();
  }
  ghost.addEventListener('change', () => { settings.ghost = ghost.value; saveSettings(settings); syncGhosts(); });
  syncGhosts();

  $('ship-undo').addEventListener('click', () => editor.undo());
  $('ship-redo').addEventListener('click', () => editor.redo());
  $('ship-delete').addEventListener('click', () => editor.deleteSelected());
  $('ship-zoom-in').addEventListener('click', () => { view.zoomAt(view.width / 2, view.height / 2, 1.25); redraw(); });
  $('ship-zoom-out').addEventListener('click', () => { view.zoomAt(view.width / 2, view.height / 2, 0.8); redraw(); });
  $('ship-zoom-fit').addEventListener('click', () => { view.fit(); redraw(); });
  $('ship-sym-right').addEventListener('click', () => editor.makeSymmetric('right'));
  $('ship-sym-left').addEventListener('click', () => editor.makeSymmetric('left'));
  $('ship-centre').addEventListener('click', () => editor.centre());
  $('ship-smaller').addEventListener('click', () => editor.scaleBy(0.9));
  $('ship-bigger').addEventListener('click', () => editor.scaleBy(1.1));
  $('ship-flip').addEventListener('click', () => editor.flip());

  // ---- side panel: the ship ----
  const name = h('input', { type: 'text', id: 'ship-name', maxlength: '60', oninput: () => editor.setField('name', name.value) });
  const author = h('input', { type: 'text', id: 'ship-author', maxlength: '60', placeholder: 'Optional', oninput: () => editor.setField('author', author.value) });
  const description = h('textarea', { id: 'ship-description', maxlength: '300', placeholder: 'Optional: what is this ship like to fly?', oninput: () => editor.setField('description', description.value) });
  const picker = h('input', { type: 'color', 'aria-label': 'Any colour', oninput: () => editor.setField('color', `FF${picker.value.slice(1).toUpperCase()}`) });
  const swatches = h('div', { class: 'swatches', role: 'group', 'aria-label': 'Ship colour' }, NEON.map((c) => h('button', {
    type: 'button', class: 'swatch', style: `background:${cssColour(c)}`, 'aria-label': `Colour ${cssColour(c)}`, dataset: { colour: c }, onclick: () => editor.setField('color', c),
  })), picker);
  const weight = h('input', { type: 'range', id: 'ship-weight', min: '-1', max: '1', step: '0.05', 'aria-label': 'Mass, lighter to heavier', oninput: () => editor.setWeight(Number(weight.value)) });
  const weightLabel = h('p', { class: 'stats', id: 'ship-weight-label' });
  const sizeLabel = h('p', { class: 'stats', id: 'ship-size-label' });
  const compare = h('p', { class: 'stats', id: 'ship-compare' });
  const thrusterLabel = h('p', { class: 'stats', id: 'ship-thruster-label' });
  const thrusterSize = h('input', { type: 'range', id: 'ship-thruster-size', min: String(MIN_THRUSTER_SIZE), max: String(MAX_THRUSTER_SIZE), step: '0.1', 'aria-label': 'Size of the selected thruster', oninput: () => editor.setThrusterSize(Number(thrusterSize.value)) });
  const thrusterSizeLabel = h('p', { class: 'stats', id: 'ship-thruster-size-label' });
  const thrusterAuto = h('button', { type: 'button', id: 'ship-thrusters-auto', title: 'Let the game put a flame at each of the rear points', onclick: () => editor.resetThrusters() }, 'Automatic');
  const thrusterNone = h('button', { type: 'button', id: 'ship-thrusters-none', title: 'A ship with no flame', onclick: () => editor.clearThrusters() }, 'None');
  const thrusterTool = h('button', { type: 'button', id: 'ship-thrusters-place', title: 'Place thrusters on the ship (T)', onclick: () => setTool('thruster') }, 'Place');
  $('ship-side-ship').append(
    h('div', { class: 'preview-box' }, $('ship-preview'), h('p', { class: 'stats' }, 'Live preview: size and shape, not handling. Every ship thrusts the same way.')),
    h('label', { class: 'field' }, 'Name', name),
    h('label', { class: 'field' }, 'Author', author),
    h('label', { class: 'field' }, 'Description', description),
    h('div', { class: 'field' }, h('span', { class: 'section-title' }, 'Colour'), swatches),
    h('div', { class: 'field' }, h('span', { class: 'section-title' }, 'Size and weight'), sizeLabel, compare,
      h('div', { class: 'weight-row' }, h('span', {}, 'Lighter'), weight, h('span', {}, 'Heavier')), weightLabel),
    h('div', { class: 'field' }, h('span', { class: 'section-title' }, 'Thrusters'), thrusterLabel,
      h('div', { class: 'weight-row' }, h('span', {}, 'Smaller'), thrusterSize, h('span', {}, 'Bigger')), thrusterSizeLabel,
      h('div', { class: 'row' }, thrusterTool, thrusterAuto, thrusterNone)),
    h('p', { class: 'stats' }, 'Radius is worked out from the outline, so ships stay fair: the bigger the ship, the heavier it can be. Mass moves within 20% of what the size suggests.'),
  );

  const tabs = [...document.querySelectorAll('[data-sside]')];
  const showSide = (which) => {
    for (const t of tabs) t.setAttribute('aria-selected', String(t.dataset.sside === which));
    $('ship-side-ship').hidden = which !== 'ship'; $('ship-side-checks').hidden = which !== 'checks';
  };
  tabs.forEach((t) => t.addEventListener('click', () => showSide(t.dataset.sside)));

  function compareText(ship) {
    const r = ship.radius;
    if (r < 15) return 'Smaller than every ship that comes with the game.';
    if (r === 15) return 'The size of Nimble and Bat-ter Up (radius 15).';
    if (r < 20) return 'Between the small ships (radius 15) and the large ones (20).';
    if (r === 20) return 'The size of Blaster, Defender and Destroyer (radius 20).';
    return 'Bigger than every ship that comes with the game.';
  }

  function syncPanel() {
    const s = editor.ship;
    for (const [input, value] of [[name, s.name], [author, s.author], [description, s.description]]) {
      if (document.activeElement !== input && input.value !== value) input.value = value;
    }
    for (const sw of swatches.querySelectorAll('.swatch')) sw.setAttribute('aria-pressed', String(sw.dataset.colour === s.color));
    const hex = `#${s.color.slice(2, 8).toLowerCase()}`;
    if (picker.value !== hex && /^#[0-9a-f]{6}$/.test(hex)) picker.value = hex;
    const [lo, hi] = massRange(s.radius);
    const usual = Math.round(baseMass(s.radius));
    sizeLabel.textContent = `${s.points.length} of ${MAX_POINTS} points · Radius ${s.radius} · reach ${editor.reach.toFixed(1)} px · area ${Math.round(Math.abs(signedArea(s.points)))} px²`;
    compare.textContent = compareText(s);
    if (document.activeElement !== weight) weight.value = String(Math.round(editor.weight * 20) / 20);
    syncThrusters();
    weightLabel.textContent = `Mass ${s.mass} (usual for this size ${usual}, allowed ${lo} to ${hi}). ${s.mass > usual + 1 ? 'Heavier ships shrug off knocks but are harder to turn.' : s.mass < usual - 1 ? 'Lighter ships are knocked about more.' : 'The usual weight for this size.'}`;
  }

  function syncThrusters() {
    const list = editor.thrusters;
    thrusterLabel.textContent = !editor.thrustersPlaced
      ? `Not placed: the game puts a flame at the rear points (${list.length} here). Use Place to choose where they go.`
      : list.length === 0 ? 'None: this ship has no flame.' : `${list.length} of ${MAX_THRUSTERS} thrusters. Each flame points straight back.`;
    const t = list[editor.selectedThruster];
    thrusterSize.disabled = !t;
    if (t && document.activeElement !== thrusterSize) thrusterSize.value = String(t.size);
    thrusterSizeLabel.textContent = t ? `Selected thruster: size ${t.size} (1 is the usual flame).` : 'Tap a thruster on the ship to change its size.';
    thrusterAuto.disabled = !editor.thrustersPlaced;
    thrusterNone.disabled = editor.thrustersPlaced && list.length === 0;
  }

  // ---- keyboard (when this tab is showing) ----
  document.addEventListener('keydown', (event) => {
    if (!visible || document.querySelector('.overlay')) return;
    const target = event.target;
    const typing = target instanceof HTMLElement && (target.matches('input:not([type="color"]):not([type="checkbox"]):not([type="range"]), textarea') || target.isContentEditable);
    if (event.key === ' ' && !typing) { pointer.setSpace(true); if (target === document.body || target === canvas) event.preventDefault(); return; }
    if (typing) return;
    const mod = event.ctrlKey || event.metaKey;
    const k = event.key.toLowerCase();
    if (mod && k === 'z') { event.preventDefault(); event.shiftKey ? editor.redo() : editor.undo(); return; }
    if (mod && k === 'y') { event.preventDefault(); editor.redo(); return; }
    if (mod) return;
    if (event.key === 'Delete' || event.key === 'Backspace') { event.preventDefault(); editor.deleteSelected(); return; }
    if (event.key === 'Escape') { state.stroke = null; editor.select(-1); editor.selectThruster(-1); redraw(); return; }
    if (event.key.startsWith('Arrow')) {
      event.preventDefault();
      const [dx, dy] = { ArrowLeft: [-1, 0], ArrowRight: [1, 0], ArrowUp: [0, -1], ArrowDown: [0, 1] }[event.key];
      const step = editor.snap * (event.shiftKey ? 2 : 1);
      const t = editor.selectedThruster;
      if (t >= 0) {
        if (!editor.beginThrusterDrag(t)) return;
        const { x, y } = editor.thrusters[t];
        editor.dragTo(x + dx * step, y + dy * step);
        editor.endDrag();
        return;
      }
      const i = editor.selected;
      if (i < 0 || !editor.beginDrag(i)) return;
      const [x, y] = editor.points[i];
      editor.dragTo(x + dx * step, y + dy * step);
      editor.endDrag();
      return;
    }
    const tools = { v: 'move', a: 'add', x: 'delete', f: 'draw', t: 'thruster' };
    if (tools[k]) setTool(tools[k]);
    else if (k === 'm') { mirrorButton.click(); }
    else if (k === '+' || k === '=') { view.zoomAt(view.width / 2, view.height / 2, 1.25); redraw(); }
    else if (k === '-') { view.zoomAt(view.width / 2, view.height / 2, 0.8); redraw(); }
  });
  document.addEventListener('keyup', (event) => { if (event.key === ' ') pointer.setSpace(false); });

  // ---- showing and hiding the tab ----
  async function show() {
    visible = true;
    preview.start();
    resize();
    if (!ready) {
      ready = true;
      const last = library.last.ship ?? library.list('ship')[0]?.id;
      if (last && library.get(last) && openEntry(last)) { /* opened */ }
      else if (!(await newShipFlow(true))) openShip(newShip(library.uniqueName('ship', 'New Ship')), null);
      analyse();
    } else analyse();
    updateHint();
  }
  function hide() { visible = false; preview.stop(); }

  setTool('move');
  showSide('ship');
  window.addEventListener('visibilitychange', () => { if (document.hidden) preview.stop(); else if (visible) preview.start(); });

  return {
    editor, view, state, show, hide, openEntry, newShipFlow, doExport, setTool, resize,
    get problems() { return problems; },
    get currentId() { return currentId; },
    get visible() { return visible; },
    get ready() { return ready; },
    save,
    analyse,
    openShip,
  };
}
