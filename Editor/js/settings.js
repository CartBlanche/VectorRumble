// The editor's own settings (theme, snapping, overlays), kept in local storage when the browser allows it.
const KEY = 'vrumble-editor-settings';

export const DEFAULTS = {
  theme: null, grid: true, gridSize: 18, endpoints: true, angle: 0, symmetry: 'none',
  space: false, ruler: false, shape: 'rect', sides: 6,
  tab: 'levels', mirror: true, shipSnap: 1, ghost: 'Blaster',
};

export function loadSettings() {
  try { return { ...DEFAULTS, ...JSON.parse(localStorage.getItem(KEY) ?? '{}') }; } catch { return { ...DEFAULTS }; }
}

export function saveSettings(settings) {
  try { localStorage.setItem(KEY, JSON.stringify(settings)); } catch { /* not kept: fine */ }
}
