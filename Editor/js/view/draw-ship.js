// Drawing a ship on a canvas in the game's look: neon outline with a glow on black.
// The editor canvas shows a ruler grid, the radius circles, ghost outlines of other ships and the outline's points;
// `drawShipOutline` is the plain look, used by the live preview and the thumbnails.
import { cssColour } from './draw-level.js';
import { MAX_REACH } from '../model/ship.js';

const ACCENT = '#ffb000';
const css = (name, fallback) => (typeof document !== 'undefined' ? getComputedStyle(document.documentElement).getPropertyValue(name).trim() || fallback : fallback);

function outlinePath(ctx, pts, toScreen) {
  ctx.beginPath();
  pts.forEach(([x, y], i) => { const [sx, sy] = toScreen(x, y); if (i) ctx.lineTo(sx, sy); else ctx.moveTo(sx, sy); });
  ctx.closePath();
}

// The ship as the game draws it: lines in its colour with a soft glow. `toScreen` maps ship-local px to canvas px.
export function drawShipOutline(ctx, pts, colour, toScreen, { width = 2, glow = 10, fill = 0.1, alpha = 1 } = {}) {
  if (pts.length < 2) return;
  const c = cssColour(colour);
  ctx.save();
  ctx.globalAlpha = alpha;
  ctx.lineJoin = 'round';
  outlinePath(ctx, pts, toScreen);
  if (fill) { ctx.globalAlpha = alpha * fill; ctx.fillStyle = c; ctx.fill(); ctx.globalAlpha = alpha; }
  ctx.shadowColor = c; ctx.shadowBlur = glow;
  ctx.strokeStyle = c; ctx.lineWidth = width;
  ctx.stroke();
  ctx.restore();
}

// state: { selected:int, ghosts:[{points,color,name}], hover:int, stroke:[[x,y]]|null, mirror:bool, snap:number, dpr, radius:int, problemPoints:Set }
export function drawShipEditor(ctx, view, ship, state = {}) {
  const { width, height } = view;
  const s = view.scale;
  const at = (x, y) => view.toScreen(x, y);
  ctx.setTransform(state.dpr ?? 1, 0, 0, state.dpr ?? 1, 0, 0);
  ctx.fillStyle = css('--map-bg', '#05060a');
  ctx.fillRect(0, 0, width, height);
  ctx.fillStyle = '#000';
  const [x0, y0] = at(-34, -34);
  ctx.fillRect(x0, y0, 68 * s, 68 * s);

  // Ruler grid: every px when zoomed in, every 5 always, stronger every 10
  const lines = (step, colour) => {
    ctx.strokeStyle = colour; ctx.lineWidth = 1; ctx.beginPath();
    for (let v = -34; v <= 34; v += step) {
      const [ax, ay] = at(v, -34), [bx, by] = at(v, 34);
      ctx.moveTo(Math.round(ax) + 0.5, ay); ctx.lineTo(Math.round(bx) + 0.5, by);
      const [cx, cy] = at(-34, v), [dx, dy] = at(34, v);
      ctx.moveTo(cx, Math.round(cy) + 0.5); ctx.lineTo(dx, Math.round(dy) + 0.5);
    }
    ctx.stroke();
  };
  if (s >= 12) lines(1, 'rgba(255,255,255,0.05)');
  lines(5, 'rgba(255,255,255,0.08)');
  lines(10, 'rgba(255,255,255,0.16)');

  // Ruler numbers on the centre lines
  if (s >= 7) {
    ctx.fillStyle = 'rgba(255,255,255,0.4)'; ctx.font = '10px ui-monospace, Menlo, monospace'; ctx.textAlign = 'center'; ctx.textBaseline = 'top';
    for (let v = -30; v <= 30; v += 10) if (v) { const [px, py] = at(v, 0); ctx.fillText(String(v), px, py + 3); }
    ctx.textAlign = 'left'; ctx.textBaseline = 'middle';
    for (let v = -30; v <= 30; v += 10) if (v) { const [px, py] = at(0, v); ctx.fillText(String(v), px + 4, py); }
  }

  // The centre lines; the vertical one is the mirror line
  ctx.strokeStyle = state.mirror ? 'rgba(255,176,0,0.55)' : 'rgba(255,255,255,0.28)';
  ctx.setLineDash(state.mirror ? [6, 4] : []);
  ctx.beginPath(); ctx.moveTo(...at(0, -34)); ctx.lineTo(...at(0, 34)); ctx.stroke();
  ctx.setLineDash([]);
  ctx.strokeStyle = 'rgba(255,255,255,0.28)';
  ctx.beginPath(); ctx.moveTo(...at(-34, 0)); ctx.lineTo(...at(34, 0)); ctx.stroke();

  // Radius circles: the two the shipped ships use (faint), the reach limit, and this ship's own
  const circle = (r, colour, dash, lw = 1) => { ctx.strokeStyle = colour; ctx.lineWidth = lw; ctx.setLineDash(dash); ctx.beginPath(); ctx.arc(...at(0, 0), r * s, 0, Math.PI * 2); ctx.stroke(); ctx.setLineDash([]); };
  circle(15, 'rgba(120,200,255,0.35)', [3, 5]);
  circle(20, 'rgba(120,200,255,0.35)', [3, 5]);
  circle(MAX_REACH, 'rgba(255,90,90,0.4)', [2, 6]);
  if (s >= 7) {
    ctx.fillStyle = 'rgba(120,200,255,0.55)'; ctx.font = '10px ui-monospace, Menlo, monospace'; ctx.textAlign = 'left'; ctx.textBaseline = 'alphabetic';
    for (const r of [15, 20]) ctx.fillText(`r${r}`, ...at(r * 0.707 + 0.4, -r * 0.707 - 0.4));
    ctx.fillStyle = 'rgba(255,90,90,0.6)'; ctx.fillText(`limit ${MAX_REACH}`, ...at(MAX_REACH * 0.707 + 0.4, -MAX_REACH * 0.707 - 0.4));
  }
  if (state.radius) circle(state.radius, ACCENT, [], 1.5);

  // Ghosts of other ships
  for (const g of state.ghosts ?? []) drawShipOutline(ctx, g.points, g.color, at, { width: 1.2, glow: 0, fill: 0, alpha: 0.45 });

  // The ship
  drawShipOutline(ctx, ship.points, ship.color, at, { width: Math.max(1.6, s * 0.18), glow: 12 });

  // The nose: a small arrow above the top of the outline
  const noseY = Math.min(...ship.points.map((p) => p[1]));
  ctx.fillStyle = 'rgba(255,255,255,0.5)'; ctx.font = '600 10px ui-sans-serif, system-ui'; ctx.textAlign = 'center'; ctx.textBaseline = 'bottom';
  if (s >= 6) ctx.fillText('▲ nose', ...at(0, Math.max(-33, noseY - 1.2)));

  // Points
  ship.points.forEach(([x, y], i) => {
    const [px, py] = at(x, y);
    const selected = state.selected === i;
    const bad = state.problemPoints?.has(i);
    ctx.beginPath(); ctx.arc(px, py, selected ? 6 : (state.hover === i ? 5.5 : 4), 0, Math.PI * 2);
    ctx.fillStyle = selected ? ACCENT : (bad ? '#ff5a5a' : '#fff'); ctx.fill();
    ctx.lineWidth = 1.5; ctx.strokeStyle = '#000'; ctx.stroke();
  });

  // A freehand stroke in progress
  if (state.stroke?.length > 1) {
    ctx.strokeStyle = ACCENT; ctx.lineWidth = 2; ctx.beginPath();
    state.stroke.forEach(([x, y], i) => { const [px, py] = at(x, y); if (i) ctx.lineTo(px, py); else ctx.moveTo(px, py); });
    ctx.stroke();
    if (state.mirror) {
      ctx.strokeStyle = 'rgba(255,176,0,0.45)'; ctx.beginPath();
      state.stroke.forEach(([x, y], i) => { const [px, py] = at(-x, y); if (i) ctx.lineTo(px, py); else ctx.moveTo(px, py); });
      ctx.stroke();
    }
  }
  // Where a tap would add a point
  if (state.add) {
    const [px, py] = at(state.add[0], state.add[1]);
    ctx.strokeStyle = ACCENT; ctx.lineWidth = 1.5; ctx.beginPath(); ctx.arc(px, py, 6, 0, Math.PI * 2); ctx.stroke();
    if (state.mirror && Math.abs(state.add[0]) >= 0.3) { const [mx, my] = at(-state.add[0], state.add[1]); ctx.globalAlpha = 0.5; ctx.beginPath(); ctx.arc(mx, my, 6, 0, Math.PI * 2); ctx.stroke(); ctx.globalAlpha = 1; }
  }
}

// A small picture of a ship for lists and cards
export function renderShipPicture(canvas, ship, { size = 96 } = {}) {
  const dpr = typeof window === 'undefined' ? 1 : window.devicePixelRatio || 1;
  canvas.width = Math.round(size * dpr); canvas.height = Math.round(size * dpr);
  const ctx = canvas.getContext('2d');
  ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
  ctx.fillStyle = '#000'; ctx.fillRect(0, 0, size, size);
  const k = size / 52;
  drawShipOutline(ctx, ship.points, ship.color, (x, y) => [size / 2 + x * k, size / 2 + y * k], { width: Math.max(1.2, k * 0.9), glow: 6 });
  return canvas;
}
