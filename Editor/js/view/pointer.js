// Mouse, pen and touch on the canvas. One finger or the mouse works the current tool; two fingers pinch to zoom and pan;
// the middle or right button, or Space and drag, pans; the wheel zooms.
import { SCREEN_W, SCREEN_H } from '../geometry.js';
import { snapPoint } from '../edit/snap.js';
import { hitWall, hitEnd } from '../edit/level-editor.js';
import { shapeWalls } from '../edit/templates.js';

const DRAG_PX = 6; // less than this and a press is a tap

// `app` supplies: editor, view, tool(), options() -> { grid, gridSize, endpoints, angle, sides, shape, ruler },
// view state in `app.draw` (what drawLevel is given), redraw(), and onChain(bool).
export function attachPointer(canvas, app) {
  const pointers = new Map();
  let gesture = null;     // the one-finger gesture in progress
  let pinch = null;
  let panning = null;
  let spaceDown = false;
  const draw = app.draw;

  const local = (event) => {
    const r = canvas.getBoundingClientRect();
    return [event.clientX - r.left, event.clientY - r.top];
  };
  const normal = (event) => app.view.toNormalised(...local(event));
  const tolerance = () => 10 / app.view.scale; // 10 screen px, as game px

  const snap = (n, extra = {}) => {
    const o = app.options();
    const result = snapPoint(n[0], n[1], {
      grid: o.grid, gridSize: o.gridSize, endpoints: o.endpoints, tolerance: Math.max(4, 12 / app.view.scale),
      walls: app.editor.walls, ...extra,
    });
    draw.snap = result.kind === 'none' ? null : [result.x, result.y];
    return [result.x, result.y];
  };

  const toolDone = () => { draw.preview = null; draw.box = null; draw.hover = null; draw.snap = null; };
  const endChain = () => { draw.chain = null; draw.preview = null; app.onChain?.(false); };
  app.endChain = endChain;
  app.cancel = () => { gesture?.cancel?.(); gesture = null; toolDone(); endChain(); app.redraw(); };
  app.setSpace = (down) => { spaceDown = down; canvas.style.cursor = down ? 'grab' : ''; };

  // ---- tools ----
  const tools = {
    select: {
      down(n, event) {
        const additive = event.shiftKey || event.ctrlKey || event.metaKey || app.options().addToSelection;
        const sel = app.editor.selection;
        // A corner of a selected wall: drag it (joined corners go with it)
        const corner = sel.size && sel.size <= 60 ? hitEnd(app.editor.walls, n[0], n[1], sel, 12 / app.view.scale) : null;
        if (corner) {
          app.editor.beginDrag();
          return { kind: 'corner', corner, moved: false };
        }
        const hit = hitWall(app.editor.walls, n[0], n[1], tolerance());
        if (hit >= 0) {
          if (additive) { app.editor.toggle(hit); return { kind: 'none' }; }
          if (!sel.has(hit)) app.editor.select([hit]);
          const w = app.editor.walls[hit];
          const anchor = nearestEnd(w, n);
          app.editor.beginDrag();
          return { kind: 'move', origin: n, anchor, moved: false, ids: [...app.editor.selection] };
        }
        return { kind: 'box', origin: n, additive, moved: false };
      },
      move(g, n) {
        if (g.kind === 'corner') {
          const [x, y] = snap(n, { skip: new Set([g.corner[0]]) });
          app.editor.dragCorner(g.corner, x, y);
          g.moved = true;
        } else if (g.kind === 'move') {
          const dxn = n[0] - g.origin[0], dyn = n[1] - g.origin[1];
          if (!g.moved && Math.hypot(dxn * SCREEN_W, dyn * SCREEN_H) * app.view.scale < DRAG_PX) return;
          g.moved = true;
          const skip = new Set(g.ids);
          const [tx, ty] = snap([g.anchor[0] + dxn, g.anchor[1] + dyn], { skip });
          app.editor.dragWalls(g.ids, tx - g.anchor[0], ty - g.anchor[1]);
        } else if (g.kind === 'box') {
          if (!g.moved && Math.hypot((n[0] - g.origin[0]) * SCREEN_W, (n[1] - g.origin[1]) * SCREEN_H) * app.view.scale < DRAG_PX) return;
          g.moved = true;
          draw.box = [g.origin[0], g.origin[1], n[0], n[1]];
        }
      },
      up(g, n) {
        if (g.kind === 'corner' || g.kind === 'move') app.editor.endDrag();
        else if (g.kind === 'box') {
          if (g.moved) app.editor.select(app.editor.wallsInBox(g.origin[0], g.origin[1], n[0], n[1]), g.additive);
          else if (!g.additive) app.editor.clearSelection();
        }
        toolDone();
      },
    },

    wall: {
      down(n) {
        const from = draw.chain ?? null;
        const p = snap(n, { from, angle: from ? app.options().angle : 0 });
        return { kind: 'wall', from: from ?? p, chained: !!from, down: n, moved: false };
      },
      move(g, n) {
        if (!g.moved && Math.hypot((n[0] - g.down[0]) * SCREEN_W, (n[1] - g.down[1]) * SCREEN_H) * app.view.scale < DRAG_PX) return;
        g.moved = true;
        const end = snap(n, { from: g.from, angle: app.options().angle });
        draw.preview = [[g.from[0], g.from[1], end[0], end[1]]];
      },
      up(g, n) {
        const end = g.moved || g.chained ? snap(n, { from: g.from, angle: app.options().angle }) : g.from;
        const length = Math.hypot((end[0] - g.from[0]) * SCREEN_W, (end[1] - g.from[1]) * SCREEN_H);
        draw.preview = null;
        if (g.chained) {
          if (length < 2) { endChain(); return; }           // tapping the last point again finishes the chain
          app.editor.addWalls([[g.from[0], g.from[1], end[0], end[1]]]);
          draw.chain = end;
        } else if (g.moved && length >= 2) {
          app.editor.addWalls([[g.from[0], g.from[1], end[0], end[1]]]);   // a drag draws one wall
        } else if (!g.moved) {
          draw.chain = g.from; app.onChain?.(true);                          // a tap starts a chain
        }
        draw.snap = null;
      },
    },

    shapes: {
      down(n) { return { kind: 'shape', a: snap(n), moved: false }; },
      move(g, n) {
        g.moved = true;
        const b = snap(n);
        g.b = b;
        const o = app.options();
        draw.preview = shapeWalls(o.shape, g.a, b, { sides: o.sides });
      },
      up(g) {
        const o = app.options();
        if (g.moved && g.b) {
          const walls = shapeWalls(o.shape, g.a, g.b, { sides: o.sides });
          if (walls.length) app.editor.addWalls(walls);
        }
        toolDone();
      },
    },

    erase: {
      down(n) {
        const g = { kind: 'erase', hit: new Set() };
        const i = hitWall(app.editor.walls, n[0], n[1], tolerance());
        if (i >= 0) g.hit.add(i);
        draw.hover = g.hit;
        return g;
      },
      move(g, n) {
        const i = hitWall(app.editor.walls, n[0], n[1], tolerance());
        if (i >= 0) g.hit.add(i);
        draw.hover = g.hit;
      },
      up(g) {
        if (g.hit.size) app.editor.deleteWalls([...g.hit]);
        toolDone();
      },
    },
  };

  const nearestEnd = (w, n) => {
    const d1 = Math.hypot((w[0] - n[0]) * SCREEN_W, (w[1] - n[1]) * SCREEN_H);
    const d2 = Math.hypot((w[2] - n[0]) * SCREEN_W, (w[3] - n[1]) * SCREEN_H);
    return d1 <= d2 ? [w[0], w[1]] : [w[2], w[3]];
  };

  // ---- events ----
  canvas.addEventListener('pointerdown', (event) => {
    try { canvas.setPointerCapture?.(event.pointerId); } catch { /* a pointer that is not active: nothing to capture */ }
    pointers.set(event.pointerId, local(event));
    if (pointers.size === 2) {                     // a second finger: stop what the first began, and zoom and pan instead
      gesture?.cancel?.();
      if (gesture && app.editor.drag) app.editor.endDrag();
      gesture = null; toolDone();
      const [a, b] = [...pointers.values()];
      pinch = { distance: Math.hypot(a[0] - b[0], a[1] - b[1]), centre: [(a[0] + b[0]) / 2, (a[1] + b[1]) / 2] };
      app.redraw();
      return;
    }
    if (event.button === 1 || event.button === 2 || spaceDown) {
      panning = { last: local(event) };
      canvas.style.cursor = 'grabbing';
      return;
    }
    if (event.button !== 0 && event.pointerType === 'mouse') return;
    const tool = tools[app.tool()];
    if (!tool) return;
    const n = normal(event);
    const g = tool.down(n, event);
    if (g) gesture = { tool, state: g, cancel() { /* the editor's own drag is ended above */ } };
    app.redraw();
  });

  canvas.addEventListener('pointermove', (event) => {
    if (pointers.has(event.pointerId)) pointers.set(event.pointerId, local(event));
    if (pinch && pointers.size >= 2) {
      const [a, b] = [...pointers.values()];
      const distance = Math.hypot(a[0] - b[0], a[1] - b[1]);
      const centre = [(a[0] + b[0]) / 2, (a[1] + b[1]) / 2];
      app.view.panBy(centre[0] - pinch.centre[0], centre[1] - pinch.centre[1]);
      if (pinch.distance > 0) app.view.zoomAt(centre[0], centre[1], distance / pinch.distance);
      pinch = { distance, centre };
      app.redraw();
      return;
    }
    if (panning) {
      const now = local(event);
      app.view.panBy(now[0] - panning.last[0], now[1] - panning.last[1]);
      panning.last = now;
      app.redraw();
      return;
    }
    const n = normal(event);
    if (gesture) {
      gesture.tool.move(gesture.state, n, event);
      app.redraw();
      return;
    }
    // Hovering (a mouse, no button): the chain's next wall, the grid dot, and the ship ruler
    const g = app.tool() === 'wall' ? draw.chain : null;
    if (g) {
      const end = snap(n, { from: g, angle: app.options().angle });
      draw.preview = [[g[0], g[1], end[0], end[1]]];
    } else if (app.tool() === 'wall' || app.tool() === 'shapes') {
      snap(n);
    } else { draw.snap = null; }
    draw.ruler = app.options().ruler ? [n[0] * SCREEN_W, n[1] * SCREEN_H] : null;
    app.redraw();
  });

  const finish = (event, cancelled) => {
    pointers.delete(event.pointerId);
    if (pinch) { if (pointers.size < 2) pinch = null; return; }
    if (panning) { panning = null; canvas.style.cursor = spaceDown ? 'grab' : ''; return; }
    if (!gesture) return;
    const g = gesture;
    gesture = null;
    if (cancelled) { if (app.editor.drag) app.editor.endDrag(); toolDone(); } else g.tool.up(g.state, normal(event), event);
    app.redraw();
  };
  canvas.addEventListener('pointerup', (e) => finish(e, false));
  canvas.addEventListener('pointercancel', (e) => finish(e, true));
  canvas.addEventListener('pointerleave', () => { if (!gesture) { draw.snap = null; draw.ruler = null; app.redraw(); } });
  canvas.addEventListener('contextmenu', (e) => e.preventDefault());

  canvas.addEventListener('wheel', (event) => {
    event.preventDefault();
    const [x, y] = local(event);
    app.view.zoomAt(x, y, Math.exp(-event.deltaY * (event.ctrlKey ? 0.01 : 0.0015)));
    app.redraw();
  }, { passive: false });
}
