// Drawing a level on a canvas in the game's look: black, neon lines with a glow. Also used for thumbnails and the PNG picture.
import { SCREEN_W, SCREEN_H } from '../geometry.js';
import { SAFE, SHIP_CLEARANCE } from '../validate/grid.js';
import { ORIGIN } from '../edit/snap.js';

export const cssColour = (argb) => `#${String(argb).slice(2, 8)}`;

const ACCENT = '#ffb000';
const css = (name, fallback) => (typeof document !== 'undefined' ? getComputedStyle(document.documentElement).getPropertyValue(name).trim() || fallback : fallback);

function wallPath(ctx, view, w) {
  const [ax, ay] = view.toScreen(w[0] * SCREEN_W, w[1] * SCREEN_H);
  const [bx, by] = view.toScreen(w[2] * SCREEN_W, w[3] * SCREEN_H);
  ctx.moveTo(ax, ay);
  ctx.lineTo(bx, by);
}

// state: { selection:Set, preview:[walls], previewKind, grid:{on,size}, space (buildGrid result or null), ruler:[gx,gy]|null,
//          marker:[nx,ny]|null, box:[x1,y1,x2,y2]|null (normalised), snap:[nx,ny]|null, hoverWall:Set, showHandles }
export function drawLevel(ctx, view, level, state = {}) {
  const width = view.width, height = view.height;
  ctx.setTransform(state.dpr ?? 1, 0, 0, state.dpr ?? 1, 0, 0);
  ctx.fillStyle = css('--map-bg', '#05060a');
  ctx.fillRect(0, 0, width, height);

  // The screen
  const [sx, sy] = view.toScreen(0, 0);
  ctx.fillStyle = '#000';
  ctx.fillRect(sx, sy, SCREEN_W * view.scale, SCREEN_H * view.scale);
  if (!state.clean) {
    ctx.strokeStyle = 'rgba(255,255,255,0.18)';
    ctx.lineWidth = 1;
    ctx.strokeRect(sx + 0.5, sy + 0.5, SCREEN_W * view.scale, SCREEN_H * view.scale);

    // The playing area: ships bounce back from 5% in
    ctx.setLineDash([4, 6]);
    ctx.strokeStyle = 'rgba(255,255,255,0.12)';
    const [px, py] = view.toScreen(SCREEN_W * SAFE, SCREEN_H * SAFE);
    ctx.strokeRect(px + 0.5, py + 0.5, SCREEN_W * (1 - 2 * SAFE) * view.scale, SCREEN_H * (1 - 2 * SAFE) * view.scale);
    ctx.setLineDash([]);
  }

  // Where ships can be: open space tinted green, closed-off pockets red
  if (state.space) {
    const g = state.space;
    for (let y = 0; y < g.rows; y++) {
      for (let x = 0; x < g.columns; x++) {
        const c = y * g.columns + x;
        if (!g.open[c]) continue;
        ctx.fillStyle = g.region[c] === g.main ? 'rgba(80,220,140,0.13)' : 'rgba(255,90,90,0.4)';
        const [cx, cy] = view.toScreen(x * g.cell, y * g.cell);
        ctx.fillRect(cx, cy, g.cell * view.scale + 0.5, g.cell * view.scale + 0.5);
      }
    }
  }

  // Grid dots
  if (state.grid?.on) {
    const size = state.grid.size;
    if (size * view.scale >= 5) {
      ctx.fillStyle = 'rgba(255,255,255,0.22)';
      for (let gx = ORIGIN[0] % size; gx <= SCREEN_W; gx += size) {
        for (let gy = ORIGIN[1] % size; gy <= SCREEN_H; gy += size) {
          const [dx, dy] = view.toScreen(gx, gy);
          ctx.fillRect(dx - 0.5, dy - 0.5, 1.5, 1.5);
        }
      }
    }
  }

  // Walls
  const colour = cssColour(level.color);
  const thickness = Math.max(1.5, 2.2 * Math.min(2, view.scale * 1.4));
  ctx.lineCap = 'round';
  ctx.shadowColor = colour;
  ctx.shadowBlur = state.plain ? 0 : 9;
  ctx.strokeStyle = colour;
  ctx.lineWidth = thickness;
  ctx.beginPath();
  level.walls.forEach((w, i) => { if (!state.selection?.has(i)) wallPath(ctx, view, w); });
  ctx.stroke();
  ctx.shadowBlur = 0;

  // Selected walls, with a corner handle at each end
  if (state.selection?.size) {
    ctx.strokeStyle = ACCENT;
    ctx.lineWidth = thickness + 1;
    ctx.beginPath();
    for (const i of state.selection) if (level.walls[i]) wallPath(ctx, view, level.walls[i]);
    ctx.stroke();
    if (state.selection.size <= 60) {
      ctx.fillStyle = '#000';
      ctx.lineWidth = 2;
      for (const i of state.selection) {
        const w = level.walls[i];
        if (!w) continue;
        for (const e of [0, 2]) {
          const [hx, hy] = view.toScreen(w[e] * SCREEN_W, w[e + 1] * SCREEN_H);
          ctx.beginPath(); ctx.arc(hx, hy, 5, 0, Math.PI * 2); ctx.fill(); ctx.stroke();
        }
      }
    }
  }

  // Walls the eraser or a warning is pointing at
  if (state.hover?.size) {
    ctx.strokeStyle = '#ff5c5c';
    ctx.lineWidth = thickness + 2;
    ctx.beginPath();
    for (const i of state.hover) if (level.walls[i]) wallPath(ctx, view, level.walls[i]);
    ctx.stroke();
  }

  // What is being drawn
  if (state.preview?.length) {
    ctx.strokeStyle = ACCENT;
    ctx.globalAlpha = 0.9;
    ctx.lineWidth = thickness;
    ctx.setLineDash([7, 5]);
    ctx.beginPath();
    for (const w of state.preview) wallPath(ctx, view, w);
    ctx.stroke();
    ctx.setLineDash([]);
    ctx.globalAlpha = 1;
  }

  // The chain's last point
  if (state.chain) {
    const [cx, cy] = view.toScreen(state.chain[0] * SCREEN_W, state.chain[1] * SCREEN_H);
    ctx.strokeStyle = ACCENT; ctx.lineWidth = 2;
    ctx.beginPath(); ctx.arc(cx, cy, 7, 0, Math.PI * 2); ctx.stroke();
  }

  if (state.snap) {
    const [cx, cy] = view.toScreen(state.snap[0] * SCREEN_W, state.snap[1] * SCREEN_H);
    ctx.fillStyle = ACCENT;
    ctx.beginPath(); ctx.arc(cx, cy, 3.5, 0, Math.PI * 2); ctx.fill();
  }

  if (state.box) {
    const [x1, y1] = view.toScreen(state.box[0] * SCREEN_W, state.box[1] * SCREEN_H);
    const [x2, y2] = view.toScreen(state.box[2] * SCREEN_W, state.box[3] * SCREEN_H);
    ctx.fillStyle = 'rgba(255,176,0,0.12)'; ctx.strokeStyle = ACCENT; ctx.lineWidth = 1;
    ctx.fillRect(Math.min(x1, x2), Math.min(y1, y2), Math.abs(x2 - x1), Math.abs(y2 - y1));
    ctx.strokeRect(Math.min(x1, x2) + 0.5, Math.min(y1, y2) + 0.5, Math.abs(x2 - x1), Math.abs(y2 - y1));
  }

  // A ship's size at the pointer: the shipped ships are radius 15 and 20; the biggest a community ship may be is 24; pilots keep 30 clear
  if (state.ruler) {
    const [rx, ry] = view.toScreen(state.ruler[0], state.ruler[1]);
    const circle = (r, dash, colourStyle) => {
      ctx.setLineDash(dash); ctx.strokeStyle = colourStyle; ctx.lineWidth = 1.5;
      ctx.beginPath(); ctx.arc(rx, ry, r * view.scale, 0, Math.PI * 2); ctx.stroke();
    };
    circle(20, [], '#7fe3ff');
    circle(24, [3, 4], '#7fe3ff');
    circle(SHIP_CLEARANCE, [2, 6], 'rgba(255,255,255,0.45)');
    ctx.setLineDash([]);
  }

  if (state.marker) {
    const [mx, my] = view.toScreen(state.marker[0] * SCREEN_W, state.marker[1] * SCREEN_H);
    ctx.strokeStyle = '#ff5c5c'; ctx.lineWidth = 3;
    ctx.beginPath(); ctx.arc(mx, my, 16, 0, Math.PI * 2); ctx.stroke();
  }
}

// A picture of the level alone, at a given width: for thumbnails and the Save picture button
export function renderPicture(canvas, level, { width = 1280, title = false } = {}) {
  const dpr = 1;
  const height = Math.round((width * SCREEN_H) / SCREEN_W);
  canvas.width = width * dpr; canvas.height = height * dpr;
  const ctx = canvas.getContext('2d');
  const view = { width, height, scale: width / SCREEN_W, toScreen: (gx, gy) => [gx * (width / SCREEN_W), gy * (width / SCREEN_W)] };
  drawLevel(ctx, view, level, { dpr, plain: width < 400, clean: true });
  if (title) {
    ctx.fillStyle = cssColour(level.color);
    ctx.font = `${Math.round(width / 40)}px sans-serif`;
    ctx.fillText(level.name, width * 0.07, height * 0.1);
  }
  return canvas;
}
