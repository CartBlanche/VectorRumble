// Symmetry while drawing: everything drawn is repeated about the centre of the screen.
export const SYMMETRIES = [
  { id: 'none', label: 'Off', hint: 'Draw one wall at a time' },
  { id: 'h', label: 'Left–right', hint: 'Mirror across the vertical middle line' },
  { id: 'v', label: 'Top–bottom', hint: 'Mirror across the horizontal middle line' },
  { id: 'both', label: '4-way', hint: 'Mirror both ways: four copies' },
  { id: 'rot', label: 'Half turn', hint: 'Turn 180 degrees about the centre: two copies' },
];

const key = (w) => {
  const a = `${w[0].toFixed(5)},${w[1].toFixed(5)}`;
  const b = `${w[2].toFixed(5)},${w[3].toFixed(5)}`;
  return a < b ? `${a}|${b}` : `${b}|${a}`;
};

const flipX = (w) => [1 - w[0], w[1], 1 - w[2], w[3]];
const flipY = (w) => [w[0], 1 - w[1], w[2], 1 - w[3]];

// The wall and its copies, without repeats (a wall on the middle line is its own mirror image)
export function expandWall(wall, mode) {
  let all;
  switch (mode) {
    case 'h': all = [wall, flipX(wall)]; break;
    case 'v': all = [wall, flipY(wall)]; break;
    case 'both': all = [wall, flipX(wall), flipY(wall), flipY(flipX(wall))]; break;
    case 'rot': all = [wall, flipY(flipX(wall))]; break;
    default: return [wall];
  }
  const seen = new Set();
  return all.filter((w) => !seen.has(key(w)) && seen.add(key(w)));
}

export function expandWalls(walls, mode) {
  const out = [];
  const seen = new Set();
  for (const w of walls) {
    for (const c of expandWall(w, mode)) {
      if (!seen.has(key(c))) { seen.add(key(c)); out.push(c); }
    }
  }
  return out;
}
