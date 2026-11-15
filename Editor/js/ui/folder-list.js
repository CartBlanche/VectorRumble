// "Where does the file go?": one row per system, the reader's own highlighted; a click copies the folder path. Like SolidOps' Export.
import { h } from './dom.js';
import { SYSTEMS, detectPlatform, systemEntry, folderName } from '../io/platform.js';

// kind: 'level' or 'ship'. Returns an element.
export function folderList(kind) {
  const detected = detectPlatform(typeof navigator === 'undefined' ? {} : navigator);
  let system = SYSTEMS.some((s) => s.id === detected) ? detected : 'mac';
  const sub = folderName(kind);
  const status = h('p', { class: 'stats' }, '');
  const list = h('div', { class: 'plat' });
  const render = () => {
    list.replaceChildren(...SYSTEMS.map((s) => {
      const entry = systemEntry(s.id, kind);
      const button = h('button', { type: 'button', 'aria-pressed': String(s.id === system), onclick: async () => {
        system = s.id;
        render();
        if (entry.path) {
          try { await navigator.clipboard.writeText(entry.path); status.textContent = 'Folder path copied.'; } catch { status.textContent = 'Select the path and copy it yourself: the browser did not allow it.'; }
        }
      } }, h('b', {}, s.label), h('span', { class: entry.path ? '' : 'prose' }, entry.path ?? entry.note));
      return button;
    }));
  };
  render();
  return h('div', { class: 'folder-list' },
    h('p', { class: 'section-title' }, `Save the downloaded file in the ${sub} folder on your:`),
    list,
    h('p', { class: 'stats' }, `Then start Vector Rumble and pick it in the ${kind === 'ship' ? 'ship' : 'arena'} list. Click a system to copy its folder path. If the folder does not exist yet, create it (or run the game once).`),
    status);
}
