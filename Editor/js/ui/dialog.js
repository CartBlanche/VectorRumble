// Modal dialogs that return a promise. dialog({ title, body, buttons }) resolves with the pressed button's value, or `dismiss` for Escape or a tap outside.
import { h } from './dom.js';

export function dialog({ title, body, buttons = [{ label: 'OK', value: true, primary: true }], dismiss = null, wide = false }) {
  return new Promise((resolve) => {
    const previous = document.activeElement;
    const close = (value) => {
      overlay.remove();
      document.removeEventListener('keydown', onKey, true);
      previous?.focus?.();
      resolve(value);
    };
    const onKey = (event) => {
      if (event.key === 'Escape') { event.stopPropagation(); close(dismiss); }
    };
    const actions = h('div', { class: 'dialog-buttons' }, buttons.map((b) => h('button', {
      type: 'button', class: b.primary ? 'primary' : (b.danger ? 'danger' : ''), onclick: () => close(b.value),
    }, b.label)));
    const box = h('div', { class: `dialog${wide ? ' wide' : ''}`, role: 'dialog', 'aria-modal': 'true', 'aria-label': title },
      h('h2', {}, title), h('div', { class: 'dialog-body' }, body), actions);
    const overlay = h('div', { class: 'overlay', onpointerdown: (e) => { if (e.target === overlay) close(dismiss); } }, box);
    document.body.append(overlay);
    document.addEventListener('keydown', onKey, true);
    (actions.querySelector('.primary') ?? actions.querySelector('button'))?.focus();
  });
}

export const confirmDialog = (title, text, yes = 'OK', danger = false) => dialog({
  title, body: h('p', {}, text), dismiss: false,
  buttons: [{ label: 'Cancel', value: false }, { label: yes, value: true, primary: !danger, danger }],
});

export const messageDialog = (title, text) => dialog({ title, body: Array.isArray(text) ? text.map((t) => h('p', {}, t)) : h('p', {}, text) });
