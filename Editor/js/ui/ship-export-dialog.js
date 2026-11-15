import { h } from './dom.js';
import { dialog } from './dialog.js';
import { folderList } from './folder-list.js';
import { exportFileName, downloadText } from '../io/export.js';

export async function exportShipDialog({ ship, xml, problems }) {
  const file = exportFileName(ship.name, 'ship');
  const warnings = problems.filter((p) => p.severity === 'warn').length;
  const status = h('p', { class: 'stats' }, '');
  const textarea = h('textarea', { readonly: true, 'aria-label': 'The ship file', onfocus: (e) => e.target.select() }, xml);
  const note = (text) => { status.textContent = text; };
  const download = h('button', { type: 'button', class: 'primary', onclick: () => { downloadText(file, xml); note(`Saved ${file} to your downloads.`); } }, `Download ${file}`);
  const copy = h('button', { type: 'button', onclick: async () => {
    try { await navigator.clipboard.writeText(xml); note('Copied.'); } catch { textarea.select(); note('Select the text and copy it yourself: the browser did not allow it.'); }
  } }, 'Copy');
  await dialog({
    title: 'Export ship',
    wide: true,
    body: [
      h('p', {}, warnings ? `${warnings} warning${warnings === 1 ? '' : 's'} (see Checks). You can still export: warnings never block it.` : 'No warnings.'),
      folderList('ship'),
      textarea,
      h('div', { class: 'dialog-buttons', style: 'justify-content:flex-start' }, download, copy),
      status,
    ],
    buttons: [{ label: 'Close', value: true }],
  });
}
