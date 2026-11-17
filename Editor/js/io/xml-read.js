// Reading the game's XML (XnaContent / Asset Type="VectorRumble:Arena" or "VectorRumble:Ship") into editor models.
// Lenient: accepts what a person might hand-write (extra whitespace, odd line breaks) and says where the game would fail on it.
// Anything it does not know is kept in `extra` and written back, so a re-save loses nothing.
import { parseXml, childEl, childEls, textOf, attrOf, XmlError } from './xml.js';
import { parseNumbers, isGameSafe, isColour, normaliseColour } from './numbers.js';
import { isEditorStamp } from './stamp.js';

export { XmlError };

const LEVEL_TAGS = ['Name', 'Author', 'Description', 'Color', 'Walls'];
const SHIP_TAGS = ['Mass', 'Radius', 'Color', 'Shape', 'Thrusters', 'Name', 'Author', 'Description', 'PlayerIndex'];

function open(source) {
  const document = parseXml(source);
  const root = document.children.find((c) => c.type === 'element');
  if (!root || root.name !== 'XnaContent') throw new XmlError('Not a Vector Rumble file: the root element should be <XnaContent>.');
  const asset = childEl(root, 'Asset');
  if (!asset) throw new XmlError('Not a Vector Rumble file: there is no <Asset>.');
  return { root, asset };
}

export function assetKind(source) {
  const type = attrOf(open(source).asset, 'Type') ?? '';
  if (/(^|:)Arena$/.test(type)) return 'level';
  if (/(^|:)Ship$/.test(type)) return 'ship';
  throw new XmlError(`This file holds a "${type || 'unknown'}", not an arena or a ship.`);
}

function extraOf(root, asset, known) {
  const extra = { lead: [], attrs: [], nodes: [] };
  extra.rootComments = root.children.filter((c) => c.type === 'comment' && !isEditorStamp(c.value)).map((c) => c.value);
  extra.attrs = asset.attrs.filter(([k]) => k !== 'Type');
  for (const c of asset.children) {
    if (c.type === 'comment') {
      if (!isEditorStamp(c.value)) (extra.sawElement ? extra.nodes : extra.lead).push(c);
    } else if (c.type === 'element') {
      extra.sawElement = true;
      if (!known.includes(c.name)) extra.nodes.push(c);
    }
  }
  delete extra.sawElement;
  return extra;
}

// Reads a text element, noting where it is missing
function field(asset, name, warnings, required) {
  const node = childEl(asset, name);
  if (!node) {
    if (required) warnings.push({ where: name, message: `<${name}> is missing, so the game cannot load this file.` });
    return null;
  }
  return textOf(node);
}

function readColour(asset, warnings) {
  const raw = field(asset, 'Color', warnings, true);
  if (raw == null) return 'FFFFFFFF';
  const colour = normaliseColour(raw);
  if (!isColour(colour)) {
    warnings.push({ where: 'Color', message: `The colour "${raw.trim()}" is not eight hex digits (AARRGGBB), so the game cannot load it.` });
    return /^[0-9A-F]{6}$/.test(colour) ? `FF${colour}` : 'FFFFFFFF';
  }
  return colour;
}

function numbersField(asset, path, warnings) {
  let node = asset;
  for (const step of path) node = childEl(node, step);
  const where = path.join('.');
  if (!node) {
    warnings.push({ where, message: `<${where}> is missing, so the game cannot load this file.` });
    return [];
  }
  const text = textOf(node);
  const numbers = parseNumbers(text);
  if (!numbers) throw new XmlError(`<${where}> holds something that is not a number.`);
  if (!isGameSafe(text)) {
    warnings.push({ where, message: `<${where}> uses spacing the game cannot read (it needs single spaces on one line). It is fixed on save.` });
  }
  return numbers;
}

export function readLevel(source) {
  const { root, asset } = open(source);
  if (assetKind(source) !== 'level') throw new XmlError('This file is a ship, not an arena.');
  const warnings = [];
  const numbers = numbersField(asset, ['Walls'], warnings);
  const walls = [];
  for (let i = 0; i + 3 < numbers.length; i += 4) walls.push(numbers.slice(i, i + 4));
  if (numbers.length % 4 !== 0) warnings.push({ where: 'Walls', message: `Walls has ${numbers.length} numbers; a wall needs four, so the last ${numbers.length % 4} are dropped.` });
  const level = {
    kind: 'level',
    name: (field(asset, 'Name', warnings, true) ?? '').trim(),
    author: (field(asset, 'Author', warnings, false) ?? '').trim(),
    description: (field(asset, 'Description', warnings, false) ?? '').trim(),
    color: readColour(asset, warnings),
    walls,
    extra: extraOf(root, asset, LEVEL_TAGS),
  };
  return { level, warnings };
}

// <Thrusters>: none means "not placed" (null); each <Thruster X Y Size>, kept as the file has it, with a warning where the game would refuse it
function readThrusters(asset, warnings) {
  const list = childEl(asset, 'Thrusters');
  if (!list) return null;
  const found = [];
  childEls(list, 'Thruster').forEach((node, i) => {
    const num = (name) => { const raw = attrOf(node, name); const n = raw == null ? NaN : Number(raw.trim()); return Number.isFinite(n) ? n : null; };
    const x = num('X'), y = num('Y');
    if (x == null || y == null) { warnings.push({ where: 'Thrusters', message: `Thruster ${i + 1} needs numbers for X and Y, so the game cannot load this ship.` }); return; }
    const size = attrOf(node, 'Size') == null ? 1 : num('Size');
    if (size == null) warnings.push({ where: 'Thrusters', message: `Thruster ${i + 1} has a Size that is not a number, so the game cannot load this ship.` });
    found.push({ x, y, size: size ?? 1 });
  });
  return found;
}

export function readShip(source) {
  const { root, asset } = open(source);
  if (assetKind(source) !== 'ship') throw new XmlError('This file is an arena, not a ship.');
  const warnings = [];
  const numbers = numbersField(asset, ['Shape', 'Points'], warnings);
  const points = [];
  for (let i = 0; i + 1 < numbers.length; i += 2) points.push([numbers[i], numbers[i + 1]]);
  if (numbers.length % 2 !== 0) warnings.push({ where: 'Shape.Points', message: 'Points has an odd number of values, so the game cannot load this ship.' });

  const whole = (name) => {
    const raw = field(asset, name, warnings, true);
    if (raw == null) return name === 'Mass' ? 20 : 15;
    const n = Number(raw.trim());
    if (!Number.isInteger(n)) {
      warnings.push({ where: name, message: `${name} "${raw.trim()}" is not a whole number, so the game cannot load this ship.` });
      return Number.isFinite(n) ? Math.round(n) : (name === 'Mass' ? 20 : 15);
    }
    return n;
  };

  const ship = {
    kind: 'ship',
    name: (field(asset, 'Name', warnings, true) ?? '').trim(),
    author: (field(asset, 'Author', warnings, false) ?? '').trim(),
    description: (field(asset, 'Description', warnings, false) ?? '').trim(),
    color: readColour(asset, warnings),
    mass: whole('Mass'),
    radius: whole('Radius'),
    points,
    thrusters: readThrusters(asset, warnings),
    playerIndex: (field(asset, 'PlayerIndex', warnings, true) ?? '').trim(),
    extra: extraOf(root, asset, SHIP_TAGS),
  };
  return { ship, warnings };
}

// Either kind: { kind, level|ship, warnings }
export function readAny(source) {
  return assetKind(source) === 'level'
    ? { kind: 'level', ...readLevel(source) }
    : { kind: 'ship', ...readShip(source) };
}
