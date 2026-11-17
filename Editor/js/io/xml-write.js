// Writing editor models as the game's XML. Element order follows the shipped files. Numbers are single-spaced on one line.
import { el, textEl, comment, serializeDocument } from './xml.js';
import { joinNumbers, formatNumber, normaliseColour } from './numbers.js';
import { stampText } from './stamp.js';

function wrap(type, children, model) {
  const asset = el('Asset', [['Type', `VectorRumble:${type}`], ...model.extra.attrs], [
    ...model.extra.lead.map((c) => comment(c.value)),
    ...children,
    ...model.extra.nodes,
  ]);
  const root = el('XnaContent', [['xmlns:VectorRumble', 'VectorRumble']], [
    comment(stampText()),
    ...(model.extra.rootComments ?? []).map(comment),
    asset,
  ]);
  return serializeDocument(root);
}

const optional = (name, value) => (value ? textEl(name, value) : null);

export function writeLevel(level) {
  return wrap('Arena', [
    textEl('Name', level.name),
    optional('Author', level.author),
    optional('Description', level.description),
    textEl('Color', normaliseColour(level.color)),
    textEl('Walls', joinNumbers(level.walls.flat())),
  ], level);
}

export function writeShip(ship) {
  return wrap('Ship', [
    textEl('Mass', Math.round(ship.mass)),
    textEl('Radius', Math.round(ship.radius)),
    textEl('Color', normaliseColour(ship.color)),
    el('Shape', [], [textEl('Points', joinNumbers(ship.points.flat()))]),
    ship.thrusters ? el('Thrusters', [], ship.thrusters.map((t) => el('Thruster', [['X', formatNumber(t.x)], ['Y', formatNumber(t.y)], ['Size', formatNumber(t.size ?? 1)]]))) : null,
    textEl('Name', ship.name),
    optional('Author', ship.author),
    optional('Description', ship.description),
    textEl('PlayerIndex', ship.playerIndex ?? ''),
  ], ship);
}

export const writeAny = (model) => (model.kind === 'ship' ? writeShip(model) : writeLevel(model));
