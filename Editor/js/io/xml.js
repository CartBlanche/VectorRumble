// A small XML reader and writer with no DOM, so the same code runs in the browser and in Node tests.
// Tree nodes: { type: 'element', name, attrs: [[name, value], ...], children } | { type: 'text', value } | { type: 'comment', value }

export class XmlError extends Error {}

const ENTITIES = { amp: '&', lt: '<', gt: '>', quot: '"', apos: "'" };

export function decode(s) {
  return s.replace(/&(#x[0-9a-fA-F]+|#\d+|\w+);/g, (match, e) => {
    if (e[0] === '#') {
      const code = e[1] === 'x' || e[1] === 'X' ? parseInt(e.slice(2), 16) : parseInt(e.slice(1), 10);
      return String.fromCodePoint(code);
    }
    return ENTITIES[e] ?? match;
  });
}

export function encode(s, attribute = false) {
  let out = s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
  if (attribute) out = out.replace(/"/g, '&quot;');
  return out;
}

export function parseXml(source) {
  const text = source.charCodeAt(0) === 0xfeff ? source.slice(1) : source;
  const document = { type: 'document', children: [] };
  const stack = [document];
  let i = 0;

  const fail = (message) => {
    const line = text.slice(0, i).split('\n').length;
    throw new XmlError(`${message} (line ${line})`);
  };

  while (i < text.length) {
    const top = stack[stack.length - 1];

    if (text[i] !== '<') {
      const next = text.indexOf('<', i);
      const end = next < 0 ? text.length : next;
      const raw = text.slice(i, end);
      if (top.type === 'element') top.children.push({ type: 'text', value: decode(raw) });
      else if (raw.trim() !== '') fail('Text outside the root element');
      i = end;
      continue;
    }

    if (text.startsWith('<!--', i)) {
      const end = text.indexOf('-->', i + 4);
      if (end < 0) fail('Unterminated comment');
      top.children.push({ type: 'comment', value: text.slice(i + 4, end) });
      i = end + 3;
    } else if (text.startsWith('<![CDATA[', i)) {
      const end = text.indexOf(']]>', i + 9);
      if (end < 0) fail('Unterminated CDATA');
      if (top.type === 'element') top.children.push({ type: 'text', value: text.slice(i + 9, end) });
      i = end + 3;
    } else if (text.startsWith('<?', i)) {
      const end = text.indexOf('?>', i + 2);
      if (end < 0) fail('Unterminated declaration');
      i = end + 2;
    } else if (text.startsWith('<!', i)) {
      const end = text.indexOf('>', i + 2);
      if (end < 0) fail('Unterminated declaration');
      i = end + 1;
    } else if (text.startsWith('</', i)) {
      const end = text.indexOf('>', i + 2);
      if (end < 0) fail('Unterminated end tag');
      const name = text.slice(i + 2, end).trim();
      if (top.type !== 'element' || top.name !== name) fail(`Unexpected </${name}>`);
      closeElement(top);
      stack.pop();
      i = end + 1;
    } else {
      const match = /^<([^\s/>]+)/.exec(text.slice(i, i + 256));
      if (!match) fail('Bad tag');
      const element = { type: 'element', name: match[1], attrs: [], children: [] };
      i += match[0].length;
      let selfClosing = false;
      for (;;) {
        while (i < text.length && /\s/.test(text[i])) i++;
        if (i >= text.length) fail(`Unterminated <${element.name}>`);
        if (text[i] === '>') { i++; break; }
        if (text.startsWith('/>', i)) { i += 2; selfClosing = true; break; }
        const attribute = /^([^\s=/>]+)\s*=\s*("([^"]*)"|'([^']*)')/.exec(text.slice(i));
        if (!attribute) fail(`Bad attribute in <${element.name}>`);
        element.attrs.push([attribute[1], decode(attribute[3] ?? attribute[4])]);
        i += attribute[0].length;
      }
      top.children.push(element);
      if (selfClosing) closeElement(element);
      else stack.push(element);
    }
  }

  if (stack.length > 1) fail(`Missing </${stack[stack.length - 1].name}>`);
  return document;
}

// Indentation between child elements is not content: drop whitespace-only text next to elements and comments.
function closeElement(element) {
  if (element.children.some((c) => c.type !== 'text')) {
    element.children = element.children.filter((c) => c.type !== 'text' || c.value.trim() !== '');
  }
}

export function childEl(node, name) {
  return node?.children.find((c) => c.type === 'element' && c.name === name) ?? null;
}

export function childEls(node, name) {
  return node ? node.children.filter((c) => c.type === 'element' && c.name === name) : [];
}

export function textOf(node) {
  return node.children.filter((c) => c.type === 'text').map((c) => c.value).join('');
}

export function attrOf(node, name) {
  const found = node.attrs.find(([k]) => k === name);
  return found ? found[1] : null;
}

// Convenience constructors for building trees to write
export const el = (name, attrs = [], children = []) => ({
  type: 'element',
  name,
  attrs: attrs.filter((a) => a && a[1] != null),
  children: children.filter(Boolean),
});
export const textEl = (name, value, attrs = []) => el(name, attrs, [{ type: 'text', value: String(value) }]);
export const comment = (value) => ({ type: 'comment', value });

export function serialize(node, depth = 0) {
  const pad = '  '.repeat(depth);
  if (node.type === 'comment') return `${pad}<!--${node.value}-->`;
  if (node.type === 'text') return pad + encode(node.value);

  const attrs = node.attrs.map(([k, v]) => ` ${k}="${encode(v, true)}"`).join('');
  if (node.children.length === 0) return `${pad}<${node.name}${attrs} />`;
  if (node.children.every((c) => c.type === 'text')) {
    return `${pad}<${node.name}${attrs}>${node.children.map((c) => encode(c.value)).join('')}</${node.name}>`;
  }
  const inner = node.children.map((c) => serialize(c, depth + 1)).join('\n');
  return `${pad}<${node.name}${attrs}>\n${inner}\n${pad}</${node.name}>`;
}

export function serializeDocument(root) {
  return `<?xml version="1.0" encoding="utf-8"?>\n${serialize(root)}\n`;
}
