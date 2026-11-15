import { h } from './dom.js';
import { dialog } from './dialog.js';
import { folderList } from './folder-list.js';
import { exportFileName, downloadText } from '../io/export.js';
import { renderPicture } from '../view/draw-level.js';

export async function exportDialog({ level, xml, problems }) {
  const file = exportFileName(level.name, 'level');
  const warnings = problems.filter((p) => p.severity === 'warn').length;
  const status = h('p', { class: 'stats' }, '');
  const textarea = h('textarea', { readonly: true, 'aria-label': 'The arena file', onfocus: (e) => e.target.select() }, xml);
  const note = (text) => { status.textContent = text; };

  const download = h('button', { type: 'button', class: 'primary', onclick: () => { downloadText(file, xml); note(`Saved ${file} to your downloads.`); } }, `Download ${file}`);
  const copy = h('button', { type: 'button', onclick: async () => {
    try { await navigator.clipboard.writeText(xml); note('Copied.'); } catch { textarea.select(); note('Select the text and copy it yourself: the browser did not allow it.'); }
  } }, 'Copy');
  const picture = h('button', { type: 'button', onclick: () => {
    const canvas = renderPicture(h('canvas'), level, { width: 1280, title: true });
    canvas.toBlob((blob) => {
      if (!blob) return;
      const url = URL.createObjectURL(blob);
      const a = h('a', { href: url, download: exportFileName(level.name, 'level').replace(/\.xml$/, '.png') });
      document.body.append(a); a.click(); a.remove();
      setTimeout(() => URL.revokeObjectURL(url), 10000);
      note('Saved a picture to share.');
    });
  } }, 'Save picture');

  await dialog({
    title: 'Export arena',
    wide: true,
    body: [
      h('p', {}, warnings
        ? `${warnings} warning${warnings === 1 ? '' : 's'} (see Checks). You can still export: warnings never block it.`
        : 'No warnings.'),
      folderList('level'),
      textarea,
      h('div', { class: 'dialog-buttons', style: 'justify-content:flex-start' }, download, copy, picture),
      status,
    ],
    buttons: [{ label: 'Close', value: true }],
  });
}
