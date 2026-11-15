// The live preview: the ship flying a loop on a small black arena, thrusting and firing, in the game's vector look.
// It shows size and shape, not handling: every ship in the game thrusts the same way.
import { drawShipOutline } from './draw-ship.js';
import { cssColour } from './draw-level.js';

const W = 300, H = 170;
const K = 2.2; // the preview draws the ship this much bigger than life, so its shape can be seen

// Returns { start(), stop(), setShip(fn) }. `getShip()` is asked for the ship each frame so edits show at once.
export function createPreview(canvas, getShip) {
  let raf = 0, t0 = 0, last = 0;
  const shots = [];
  let fireAt = 0;

  function frame(now) {
    raf = requestAnimationFrame(frame);
    if (!t0) { t0 = now; last = now; }
    const dt = Math.min(0.05, (now - last) / 1000);
    last = now;
    const t = (now - t0) / 1000;
    const ship = getShip();
    const dpr = window.devicePixelRatio || 1;
    if (canvas.width !== Math.round(W * dpr)) { canvas.width = Math.round(W * dpr); canvas.height = Math.round(H * dpr); }
    const ctx = canvas.getContext('2d');
    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    ctx.fillStyle = '#000'; ctx.fillRect(0, 0, W, H);

    // A figure of eight, heading along the path
    const speed = 0.55;
    const a = t * speed;
    const px = W / 2 + Math.sin(a) * 95, py = H / 2 + Math.sin(a * 2) * 40;
    const vx = Math.cos(a) * 95 * speed, vy = Math.cos(a * 2) * 80 * speed;
    const heading = Math.atan2(vy, vx) + Math.PI / 2; // nose is -y, so a ship pointing along +x is turned a quarter turn
    const c = Math.cos(heading), s = Math.sin(heading);
    const place = (x, y) => [px + (x * c - y * s) * K, py + (x * s + y * c) * K];
    const pts = ship.points;

    // Shots from the nose (the point farthest up)
    if (t > fireAt && pts.length) {
      fireAt = t + 0.55;
      const nose = pts.reduce((m, p) => (p[1] < m[1] ? p : m), pts[0]);
      const [nx, ny] = place(nose[0], nose[1]);
      shots.push({ x: nx, y: ny, dx: Math.sin(heading) * 190, dy: -Math.cos(heading) * 190, life: 1.1 });
    }
    for (let i = shots.length - 1; i >= 0; i--) {
      const b = shots[i];
      b.x += b.dx * dt; b.y += b.dy * dt; b.life -= dt;
      if (b.life <= 0) { shots.splice(i, 1); continue; }
      ctx.save(); ctx.globalAlpha = Math.min(1, b.life * 2);
      ctx.strokeStyle = cssColour(ship.color); ctx.shadowColor = cssColour(ship.color); ctx.shadowBlur = 6; ctx.lineWidth = 2;
      ctx.beginPath(); ctx.moveTo(b.x, b.y); ctx.lineTo(b.x - b.dx * 0.03, b.y - b.dy * 0.03); ctx.stroke(); ctx.restore();
    }

    // The thrust flame, flickering behind the lowest point
    if (pts.length) {
      const tail = pts.reduce((m, p) => (p[1] > m[1] ? p : m), pts[0]);
      const flick = 5 + Math.sin(t * 40) * 2 + Math.random() * 2;
      const flame = [[tail[0] - 2.5, tail[1]], [tail[0], tail[1] + flick], [tail[0] + 2.5, tail[1]]];
      drawShipOutline(ctx, flame.map(([x, y]) => [x, y]), 'FFFFA000', (x, y) => place(x, y), { width: 1.4, glow: 8, fill: 0.4 });
    }
    drawShipOutline(ctx, pts, ship.color, (x, y) => place(x, y), { width: 1.6, glow: 10 });
  }

  return {
    start() { if (!raf) { t0 = 0; raf = requestAnimationFrame(frame); } },
    stop() { cancelAnimationFrame(raf); raf = 0; },
    get running() { return raf !== 0; },
  };
}
