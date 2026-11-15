// The side panel: the level's details, the selection's tools, and the list of checks.
import { h } from './dom.js';
import { NEON } from '../model/builtins.js';
import { cssColour } from '../view/draw-level.js';
import { SCREEN_W, SCREEN_H } from '../geometry.js';

export function createPanels({ editor, onCheckClick, onTab }) {
  const levelBody = document.getElementById('side-level');
  const selBody = document.getElementById('side-selection');
  const checksBody = document.getElementById('side-checks');
  const tabs = [...document.querySelectorAll('[data-side]')];

  const show = (name) => {
    for (const t of tabs) t.setAttribute('aria-selected', String(t.dataset.side === name));
    levelBody.hidden = name !== 'level'; selBody.hidden = name !== 'selection'; checksBody.hidden = name !== 'checks';
    onTab?.(name);
  };
  tabs.forEach((t) => t.addEventListener('click', () => show(t.dataset.side)));

  // ---- Level ----
  const name = h('input', { type: 'text', id: 'level-name', maxlength: '60', oninput: () => editor.setField('name', name.value) });
  const author = h('input', { type: 'text', id: 'level-author', maxlength: '60', placeholder: 'Optional', oninput: () => editor.setField('author', author.value) });
  const description = h('textarea', { id: 'level-description', maxlength: '300', placeholder: 'Optional: what is this arena like?', oninput: () => editor.setField('description', description.value) });
  const picker = h('input', { type: 'color', 'aria-label': 'Any colour', oninput: () => editor.setField('color', `FF${picker.value.slice(1).toUpperCase()}`) });
  const swatches = h('div', { class: 'swatches', role: 'group', 'aria-label': 'Wall colour' }, NEON.map((c) => h('button', {
    type: 'button', class: 'swatch', style: `background:${cssColour(c)}`, 'aria-label': `Colour ${cssColour(c)}`, dataset: { colour: c },
    onclick: () => editor.setField('color', c),
  })), picker);
  const stats = h('p', { class: 'stats' });
  levelBody.append(
    h('label', { class: 'field' }, 'Name', name),
    h('label', { class: 'field' }, 'Author', author),
    h('label', { class: 'field' }, 'Description', description),
    h('div', { class: 'field' }, h('span', { class: 'section-title' }, 'Wall colour'), swatches),
    stats,
    h('p', { class: 'stats' }, 'The name is what the game lists and what the file is called. Pick one that is not an arena that comes with the game.'),
  );

  function syncLevel() {
    const l = editor.level;
    for (const [input, value] of [[name, l.name], [author, l.author], [description, l.description]]) {
      if (document.activeElement !== input && input.value !== value) input.value = value;
    }
    for (const s of swatches.querySelectorAll('.swatch')) s.setAttribute('aria-pressed', String(s.dataset.colour === l.color));
    const hex = `#${l.color.slice(2, 8).toLowerCase()}`;
    if (picker.value !== hex && /^#[0-9a-f]{6}$/.test(hex)) picker.value = hex;
    stats.textContent = `${l.walls.length} wall${l.walls.length === 1 ? '' : 's'}`;
  }

  // ---- Selection ----
  const op = (label, fn, title) => h('button', { type: 'button', title, onclick: fn }, label);
  const selInfo = h('p', { class: 'stats' });
  const selTools = h('div', { class: 'row' },
    op('↺ 15°', () => editor.transform('rotateLeft'), 'Turn 15 degrees left'),
    op('↻ 15°', () => editor.transform('rotateRight'), 'Turn 15 degrees right'),
    op('Half turn', () => editor.transform('halfTurn'), 'Turn 180 degrees'),
    op('Flip ↔', () => editor.transform('mirrorLeftRight'), 'Mirror left to right, in place'),
    op('Flip ↕', () => editor.transform('mirrorTopBottom'), 'Mirror top to bottom, in place'),
    op('Smaller', () => editor.transform('smaller'), 'Shrink by 10% about the middle of the selection'),
    op('Bigger', () => editor.transform('bigger'), 'Grow by 10% about the middle of the selection'),
    op('Duplicate', () => editor.duplicateSelected(), 'Copy the selection a little to the side'),
    h('button', { type: 'button', class: 'danger', onclick: () => editor.deleteSelected() }, 'Delete'));
  const mirrorAll = h('button', { type: 'button', onclick: () => editor.applySymmetryToAll(), title: 'Add the mirror image of every wall, using the symmetry chosen in the toolbar' }, 'Mirror the whole arena');
  selBody.append(selInfo, selTools, h('p', { class: 'section-title', style: 'margin-top:8px' }, 'Whole arena'), mirrorAll,
    h('p', { class: 'stats' }, 'Turning and scaling work in screen pixels, so a half turn looks like a half turn. Tip: select everything (Ctrl/Cmd+A) to flip or turn the whole arena.'));

  function syncSelection() {
    const n = editor.selection.size;
    selTools.hidden = n === 0;
    mirrorAll.hidden = editor.symmetry === 'none';
    if (n === 0) selInfo.textContent = 'Nothing selected. Use the Select tool: tap a wall, or drag a box round several.';
    else if (n === 1) {
      const [ax, ay, bx, by] = editor.walls[[...editor.selection][0]];
      const length = Math.hypot((bx - ax) * SCREEN_W, (by - ay) * SCREEN_H);
      selInfo.textContent = `1 wall selected, ${Math.round(length)} px long.`;
    } else selInfo.textContent = `${n} walls selected.`;
  }

  // ---- Checks ----
  function showChecks(results) {
    checksBody.replaceChildren();
    if (!results.length) { checksBody.append(h('p', { class: 'checks-ok' }, 'No problems found. This arena is ready to export.')); return; }
    const warns = results.filter((r) => r.severity === 'warn');
    checksBody.append(h('p', { class: 'stats' }, `${warns.length} warning${warns.length === 1 ? '' : 's'}, ${results.length - warns.length} note${results.length - warns.length === 1 ? '' : 's'}. Warnings never stop you saving or exporting. Tap one to see where.`));
    for (const r of [...warns, ...results.filter((x) => x.severity !== 'warn')]) {
      checksBody.append(h('button', { type: 'button', class: `check ${r.severity}`, onclick: () => onCheckClick(r) }, h('span', { class: 'dot' }), h('span', {}, r.message)));
    }
  }

  return { syncLevel, syncSelection, showChecks, show };
}
