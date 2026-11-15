import { h } from './dom.js';
import { dialog, confirmDialog } from './dialog.js';
import { readAny } from '../io/xml-read.js';
import { renderPicture } from '../view/draw-level.js';
import { renderShipPicture } from '../view/draw-ship.js';

// Lists the library; actions are callbacks so main.js stays in charge: { open(id), duplicate(id), exportEntry(id), remove(id) }
export function libraryDialog(library, actions, kind = 'level') {
  return new Promise((resolve) => {
    const list = h('div', {});
    let close;
    const render = () => {
      list.replaceChildren();
      const entries = library.list(kind);
      if (!entries.length) list.append(h('p', { class: 'empty' }, 'Nothing saved yet. Your work is saved here automatically.'));
      for (const e of entries) {
        let parsed = null;
        try { parsed = readAny(library.get(e.id).xml); } catch { /* shown without a picture */ }
        const canvas = h('canvas', { class: kind === 'ship' ? 'ship-thumb' : null });
        if (parsed?.kind === 'level') renderPicture(canvas, parsed.level, { width: 192 });
        else if (parsed?.kind === 'ship') renderShipPicture(canvas, parsed.ship, { size: 108 });
        const walls = parsed?.level ? `${parsed.level.walls.length} walls` : (parsed?.ship ? `${parsed.ship.points.length} points · radius ${parsed.ship.radius}` : null);
        const when = e.updated ? new Date(e.updated).toLocaleString() : '';
        list.append(h('div', { class: 'lib-row' },
          canvas,
          h('div', { class: 'name' }, h('b', {}, e.name || '(no name)'), h('small', {}, [walls, when].filter(Boolean).join(' · '))),
          h('button', { type: 'button', class: 'primary', onclick: () => { close(); actions.open(e.id); } }, 'Open'),
          h('button', { type: 'button', onclick: () => { actions.duplicate(e.id); render(); } }, 'Duplicate'),
          h('button', { type: 'button', onclick: () => { close(); actions.exportEntry(e.id); } }, 'Export'),
          h('button', { type: 'button', class: 'danger', onclick: async () => {
            if (await confirmDialog('Delete?', `Delete "${e.name}" from your library? This cannot be undone. Export it first if you want to keep it.`, 'Delete', true)) { actions.remove(e.id); render(); }
          } }, 'Delete')));
      }
    };
    render();
    const p = dialog({ title: kind === 'level' ? 'Arena library' : 'Ship library', wide: true, body: [list], buttons: [{ label: 'Close', value: true }] });
    close = () => document.querySelector('.overlay .dialog-buttons button:last-child')?.click();
    p.then(resolve);
  });
}
