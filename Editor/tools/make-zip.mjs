// Builds dist/vector-rumble-editor-<version>.zip for itch.io (an HTML5 project): index.html and favicon.png at the root, plus css/ and js/.
// Tests, tools, fixtures and the project notes are left out. No dependencies: it writes the zip format itself.
// Run from Editor/:  node tools/make-zip.mjs
import { readdirSync, readFileSync, statSync, mkdirSync, writeFileSync } from 'node:fs';
import { join, dirname, relative, sep } from 'node:path';
import { fileURLToPath } from 'node:url';
import { deflateRawSync } from 'node:zlib';
import { EDITOR_VERSION } from '../js/version.js';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const INCLUDE = ['index.html', 'favicon.svg', 'css', 'js'];
if (process.argv.includes('--with-license')) INCLUDE.push('LICENSE');

function* walk(path) {
  if (statSync(path).isDirectory()) {
    for (const name of readdirSync(path).sort()) if (!name.startsWith('.')) yield* walk(join(path, name));
  } else {
    yield path;
  }
}

const CRC_TABLE = Array.from({ length: 256 }, (_, n) => {
  let c = n;
  for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1;
  return c >>> 0;
});
const crc32 = (buffer) => {
  let c = 0xffffffff;
  for (const byte of buffer) c = CRC_TABLE[(c ^ byte) & 0xff] ^ (c >>> 8);
  return (c ^ 0xffffffff) >>> 0;
};

export function buildZip(files) {
  // A fixed timestamp keeps the zip identical from one build to the next (2026-01-01 00:00)
  const dosTime = 0;
  const dosDate = ((2026 - 1980) << 9) | (1 << 5) | 1;
  const parts = [];
  const central = [];
  let offset = 0;

  for (const { name, data } of files) {
    const nameBytes = Buffer.from(name, 'utf8');
    const deflated = deflateRawSync(data, { level: 9 });
    const useDeflate = deflated.length < data.length;
    const body = useDeflate ? deflated : data;
    const crc = crc32(data);

    const local = Buffer.alloc(30);
    local.writeUInt32LE(0x04034b50, 0);
    local.writeUInt16LE(20, 4);          // version needed
    local.writeUInt16LE(0x0800, 6);      // flags: file names are UTF-8
    local.writeUInt16LE(useDeflate ? 8 : 0, 8);
    local.writeUInt16LE(dosTime, 10);
    local.writeUInt16LE(dosDate, 12);
    local.writeUInt32LE(crc, 14);
    local.writeUInt32LE(body.length, 18);
    local.writeUInt32LE(data.length, 22);
    local.writeUInt16LE(nameBytes.length, 26);
    local.writeUInt16LE(0, 28);
    parts.push(local, nameBytes, body);

    const entry = Buffer.alloc(46);
    entry.writeUInt32LE(0x02014b50, 0);
    entry.writeUInt16LE(20, 4);          // version made by
    entry.writeUInt16LE(20, 6);
    entry.writeUInt16LE(0x0800, 8);
    entry.writeUInt16LE(useDeflate ? 8 : 0, 10);
    entry.writeUInt16LE(dosTime, 12);
    entry.writeUInt16LE(dosDate, 14);
    entry.writeUInt32LE(crc, 16);
    entry.writeUInt32LE(body.length, 20);
    entry.writeUInt32LE(data.length, 24);
    entry.writeUInt16LE(nameBytes.length, 28);
    entry.writeUInt32LE(offset, 42);
    central.push(entry, nameBytes);

    offset += local.length + nameBytes.length + body.length;
  }

  const directory = Buffer.concat(central);
  const end = Buffer.alloc(22);
  end.writeUInt32LE(0x06054b50, 0);
  end.writeUInt16LE(files.length, 8);
  end.writeUInt16LE(files.length, 10);
  end.writeUInt32LE(directory.length, 12);
  end.writeUInt32LE(offset, 16);
  return Buffer.concat([...parts, directory, end]);
}

if (process.argv[1] === fileURLToPath(import.meta.url)) {
  const files = [];
  for (const entry of INCLUDE) {
    const path = join(root, entry);
    try { statSync(path); } catch { continue; }
    for (const file of walk(path)) files.push({ name: relative(root, file).split(sep).join('/'), data: readFileSync(file) });
  }
  mkdirSync(join(root, 'dist'), { recursive: true });
  const zip = buildZip(files);
  const zipName = `vector-rumble-editor-${EDITOR_VERSION}.zip`;
  writeFileSync(join(root, 'dist', zipName), zip);
  const total = files.reduce((n, f) => n + f.data.length, 0);
  console.log(`dist/${zipName}: ${files.length} files, ${(total / 1024).toFixed(0)} KB raw, ${(zip.length / 1024).toFixed(0)} KB zipped`);
}
