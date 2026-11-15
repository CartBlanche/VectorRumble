// A canonical form of an XML file for comparing "same meaning": comments dropped, whitespace between elements ignored,
// number lists normalised, attributes sorted.
import { parseXml } from '../js/io/xml.js';

const norm = (node) => {
  if (node.type === 'text') return node.value.trim().split(/\s+/).join(' ');
  return {
    name: node.name,
    attrs: [...node.attrs].sort(([a], [b]) => a.localeCompare(b)),
    children: node.children.filter((c) => c.type !== 'comment').map(norm).filter((c) => c !== ''),
  };
};

export const canonicalise = (xml) => norm(parseXml(xml).children.find((c) => c.type === 'element'));
