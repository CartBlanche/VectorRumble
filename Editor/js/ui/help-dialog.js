import { h } from './dom.js';
import { dialog } from './dialog.js';
import { EDITOR_VERSION } from '../version.js';
import { FOLDERS } from '../io/platform.js';

const key = (k) => h('kbd', {}, k);

export function helpDialog() {
  const row = (a, b) => h('tr', {}, h('td', {}, a), h('td', {}, b));
  return dialog({
    title: 'Help',
    wide: true,
    body: [
      h('p', { class: 'section-title' }, 'Drawing'),
      h('table', {},
        row('Wall tool', 'Drag to draw one wall. Or tap points to chain walls: tap the last point again (or press Esc) to finish.'),
        row('Shapes tool', 'Pick a shape, then drag. Rectangles are corner to corner; polygons and rings go from the middle out; pillars run along the line you drag.'),
        row('Select tool', 'Tap a wall, drag a box round several, drag walls to move them. The round handles on a selected wall are its corners: drag one and every wall joined there follows.'),
        row('Erase tool', 'Tap a wall, or drag across several, to remove them.'),
        row('Symmetry', 'Everything you draw is repeated across the middle (left to right, top to bottom, both, or a half turn). The best way to make a fair arena.'),
        row('Snapping', 'Grid and Ends are on by default so corners close. Angle snaps to 45 or 15 degrees while chaining. Hold nothing, switch them off, and draw freely.')),
      h('p', { class: 'section-title', style: 'margin-top:14px' }, 'Seeing what a ship sees'),
      h('table', {},
        row('Space', 'Green is where a ship can fly. Red is a closed-off pocket a ship could spawn in and not get out of.'),
        row('Ship size', 'Rings at the pointer: the shipped ships (solid), the biggest a community ship may be (dashed), and the clearance the computer pilots keep (dotted).')),
      h('p', { class: 'section-title', style: 'margin-top:14px' }, 'Ships'),
      h('table', {},
        row('Outline', 'A ship is a closed outline of 3 to 24 points, nose up. It turns about the centre (the crossing lines), so keep it centred. Move drags points; Add puts a new point on the outline; Delete removes one; Draw sketches the whole outline freehand.'),
        row('Mirror', 'On by default: whatever you do to one side happens to the other. When drawing freehand with Mirror on, draw only the right side, nose to tail.'),
        row('Size and weight', 'Radius is worked out from the outline (the dashed rings show the shipped radii 15 and 20, the red ring the limit). Mass moves within 20% of what the size suggests, so ships stay fair.'),
        row('Compare', 'Faint outlines of the ships that come with the game, to judge size and shape.'),
        row([key('V'), ' ', key('A'), ' ', key('X'), ' ', key('F'), ' ', key('M')], 'Move, Add, Delete, Draw, Mirror on or off (on the Ships tab)'),
        row([key('Arrows')], 'Nudge the selected point')),
      h('p', { class: 'section-title', style: 'margin-top:14px' }, 'Keys and gestures (arenas)'),
      h('table', {},
        row([key('V'), ' ', key('W'), ' ', key('S'), ' ', key('E')], 'Select, Wall, Shapes, Erase'),
        row([key('Delete')], 'Delete the selection'),
        row([key('Ctrl/Cmd'), '+', key('Z'), ' / ', key('Y')], 'Undo / redo'),
        row([key('Ctrl/Cmd'), '+', key('A'), ' / ', key('D')], 'Select all / duplicate'),
        row([key('Arrows')], 'Nudge the selection (hold Shift for bigger steps)'),
        row([key('G')], 'Grid on or off'),
        row([key('Esc')], 'Finish a chain, or clear the selection'),
        row('Wheel, pinch', 'Zoom. Two fingers, the middle or right mouse button, or Space and drag, pan.')),
      h('p', { class: 'section-title', style: 'margin-top:14px' }, 'Getting a file into the game'),
      h('p', {}, 'Export downloads the arena or ship as XML. Put it in the game\'s Arenas or Ships folder (create it if needed), then start the game: it appears in the arena or ship list by itself. Export lists the folder for each system and copies a path when you click it.'),
      h('table', {}, ...Object.values(FOLDERS).flatMap((f) => [row(f.label, `${f.root}${f.sep}Arenas`), row('', `${f.root}${f.sep}Ships`)])),
      h('p', { class: 'stats', style: 'margin-top:10px' }, 'Arenas and ships are kept in this browser, so export anything you want to keep. Clearing the site\'s data removes the library.'),
      h('p', { class: 'stats' }, `Vector Rumble Editor ${EDITOR_VERSION}`),
    ],
  });
}
