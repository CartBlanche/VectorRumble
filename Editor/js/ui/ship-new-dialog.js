import { h } from './dom.js';
import { dialog } from './dialog.js';
import { SHIPPED_SHIPS } from '../model/ship-builtins.js';
import { renderShipPicture } from '../view/draw-ship.js';
import { newShip } from '../model/ship.js';

const BLANK = { name: 'Arrow', description: 'A simple dart: four points, symmetrical. Add points to make it your own.', shipped: false };

// Pick a starting point; resolves with { shipped, name, points, color, radius, mass } or null
export function chooseShipStart({ first = false } = {}) {
  let picked = null;
  const card = (start, ship) => {
    const canvas = h('canvas');
    renderShipPicture(canvas, ship, { size: 120 });
    return h('button', { type: 'button', class: 'card', onclick: () => { picked = start; document.querySelector('.dialog-buttons button')?.click(); } },
      canvas, h('b', {}, start.name), h('span', {}, start.description));
  };
  return dialog({
    title: first ? 'Start your first ship' : 'New ship',
    wide: true,
    body: [
      h('p', {}, 'Pick a starting point. You can change everything afterwards.'),
      h('div', { class: 'cards' }, card(BLANK, newShip('Arrow'))),
      h('p', { class: 'section-title', style: 'margin-top:14px' }, 'Remix a ship that comes with the game'),
      h('div', { class: 'cards' }, Object.entries(SHIPPED_SHIPS).map(([name, s]) => card(
        { shipped: true, name, points: s.points, color: s.color, radius: s.radius, mass: s.mass, description: `${s.points.length} points, radius ${s.radius}` },
        { points: s.points, color: s.color },
      ))),
    ],
    buttons: [{ label: 'Cancel', value: 'cancel' }],
    dismiss: 'cancel',
  }).then(() => picked);
}
