import { h } from './dom.js';
import { dialog } from './dialog.js';
import { TEMPLATES, SHIPPED } from '../edit/templates.js';
import { newLevel } from '../model/level.js';
import { renderPicture } from '../view/draw-level.js';

// Pick a starting point; resolves with a template or null
export function chooseTemplate({ first = false } = {}) {
  let picked = null;
  const card = (t) => {
    const canvas = h('canvas');
    renderPicture(canvas, newLevel(t.name, { walls: t.walls() }), { width: 240 });
    return h('button', { type: 'button', class: 'card', onclick: () => { picked = t; document.querySelector('.dialog-buttons button')?.click(); } },
      canvas, h('b', {}, t.name), h('span', {}, t.description));
  };
  return dialog({
    title: first ? 'Start your first arena' : 'New arena',
    wide: true,
    body: [
      h('p', {}, 'Pick a starting point. You can change everything afterwards.'),
      h('div', { class: 'cards' }, TEMPLATES.map(card)),
      h('p', { class: 'section-title', style: 'margin-top:14px' }, 'Remix an arena that comes with the game'),
      h('div', { class: 'cards' }, SHIPPED.map(card)),
    ],
    buttons: [{ label: 'Cancel', value: 'cancel' }],
    dismiss: 'cancel',
  }).then(() => picked);
}
