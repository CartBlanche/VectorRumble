// Mouse, pen and touch on the ship canvas. One pointer works the tool; two pinch to zoom and pan; the wheel zooms.
// app: { editor, view, state (what the canvas draws), tool(), redraw(), grid... }
import { hitPoint, nearestEdge } from '../edit/ship-editor.js';

export function attachShipPointer(canvas, app) {
  const pointers = new Map();
  let gesture = null, pinch = null, panning = null, spaceDown = false;

  const local = (event) => { const r = canvas.getBoundingClientRect(); return [event.clientX - r.left, event.clientY - r.top]; };
  const spot = (event) => app.view.toLocal(...local(event));
  const tolerance = (event) => (event.pointerType === 'touch' ? 16 : 9) / app.view.scale;  // px on the ship, from a finger's or a mouse's reach

  const tools = {
    move: {
      down(p, event) {
        const i = hitPoint(app.editor.points, p[0], p[1], tolerance(event));
        if (i < 0) { app.editor.select(-1); return null; }
        return app.editor.beginDrag(i) ? { drag: true } : null;
      },
      move(g, p) { app.editor.dragTo(p[0], p[1]); },
      up() { app.editor.endDrag(); },
    },
    add: {
      down(p, event) {
        const i = hitPoint(app.editor.points, p[0], p[1], tolerance(event));
        if (i >= 0) return app.editor.beginDrag(i) ? { drag: true } : null; // a tap on a point picks it up instead
        const added = app.editor.addPointAt(p[0], p[1]);
        return added >= 0 && app.editor.beginDrag(added) ? { drag: true } : null; // and drag on to place it exactly
      },
      move(g, p) { app.editor.dragTo(p[0], p[1]); },
      up() { app.editor.endDrag(); },
    },
    delete: {
      down(p, event) {
        const i = hitPoint(app.editor.points, p[0], p[1], tolerance(event));
        if (i >= 0) app.editor.deletePoint(i);
        return null;
      },
      move() {},
      up() {},
    },
    draw: {
      down(p) { app.state.stroke = [p]; return { stroke: true }; },
      move(g, p) { const s = app.state.stroke; const l = s.at(-1); if (Math.hypot(p[0] - l[0], p[1] - l[1]) > 0.25) s.push(p); },
      up() {
        const stroke = app.state.stroke;
        app.state.stroke = null;
        if (stroke && stroke.length > 4) app.editor.drawStroke(stroke);
        else app.onShortStroke?.();
      },
    },
  };

  canvas.addEventListener('pointerdown', (event) => {
    try { canvas.setPointerCapture?.(event.pointerId); } catch { /* not an active pointer */ }
    pointers.set(event.pointerId, local(event));
    if (pointers.size === 2) {
      if (gesture) { app.editor.drag && app.editor.endDrag(); app.state.stroke = null; gesture = null; }
      const [a, b] = [...pointers.values()];
      pinch = { distance: Math.hypot(a[0] - b[0], a[1] - b[1]), centre: [(a[0] + b[0]) / 2, (a[1] + b[1]) / 2] };
      return;
    }
    if (event.button === 1 || event.button === 2 || spaceDown) { panning = { last: local(event) }; canvas.style.cursor = 'grabbing'; return; }
    if (event.button !== 0 && event.pointerType === 'mouse') return;
    const tool = tools[app.tool()];
    const g = tool?.down(spot(event), event);
    if (g) gesture = { tool, state: g };
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
    if (panning) { const now = local(event); app.view.panBy(now[0] - panning.last[0], now[1] - panning.last[1]); panning.last = now; app.redraw(); return; }
    const p = spot(event);
    if (gesture) { gesture.tool.move(gesture.state, p, event); app.redraw(); return; }
    // Hovering: which point is under the pointer, and where an Add would land
    app.state.hover = hitPoint(app.editor.points, p[0], p[1], tolerance(event));
    app.state.add = app.tool() === 'add' && app.state.hover < 0 && event.pointerType === 'mouse' ? app.editor.clean(p[0], p[1]) : null;
    app.redraw();
  });

  const finish = (event, cancelled) => {
    pointers.delete(event.pointerId);
    if (pinch) { if (pointers.size < 2) pinch = null; return; }
    if (panning) { panning = null; canvas.style.cursor = ''; return; }
    if (!gesture) return;
    const g = gesture;
    gesture = null;
    if (cancelled && g.state.stroke) app.state.stroke = null;
    else g.tool.up(g.state);
    app.redraw();
  };
  canvas.addEventListener('pointerup', (e) => finish(e, false));
  canvas.addEventListener('pointercancel', (e) => finish(e, true));
  canvas.addEventListener('pointerleave', () => { if (!gesture) { app.state.hover = -1; app.state.add = null; app.redraw(); } });
  canvas.addEventListener('contextmenu', (e) => e.preventDefault());
  canvas.addEventListener('wheel', (event) => {
    event.preventDefault();
    const [x, y] = local(event);
    app.view.zoomAt(x, y, Math.exp(-event.deltaY * (event.ctrlKey ? 0.01 : 0.0015)));
    app.redraw();
  }, { passive: false });

  return { setSpace(on) { spaceDown = on; canvas.style.cursor = on ? 'grab' : ''; } };
}
