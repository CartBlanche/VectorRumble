// Numbers in the game's files. The game splits them on a single space (Arena.Load, Ship.Load), so a double space, a newline,
// or a leading or trailing space makes the file fail to load. We write one line of single-spaced numbers, at most 3 decimals.

export function formatNumber(n) {
  const rounded = Math.round(n * 1000) / 1000;
  return String(Object.is(rounded, -0) ? 0 : rounded);
}

export const joinNumbers = (numbers) => numbers.map(formatNumber).join(' ');

// True when the text is exactly what we would write: numbers separated by single spaces, nothing around them
export const isGameSafe = (text) => text === text.trim().split(/\s+/).join(' ');

// Lenient read: any whitespace. Returns null when something is not a number.
export function parseNumbers(text) {
  const parts = text.trim() === '' ? [] : text.trim().split(/\s+/);
  const numbers = parts.map((p) => (/^[-+]?(\d+\.?\d*|\.\d+)([eE][-+]?\d+)?$/.test(p) ? Number(p) : NaN));
  return numbers.some(Number.isNaN) ? null : numbers;
}

// The game's colours: AARRGGBB, eight hex digits (the loader reads the text as one number, so six digits would mean alpha 0)
export const isColour = (text) => /^[0-9a-fA-F]{8}$/.test(text);
export const normaliseColour = (text) => String(text).trim().toUpperCase();
