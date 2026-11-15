// A level (the game's Arena): a name, a wall colour and wall segments, normalised 0..1 across the screen, y down.
// Ships, asteroids and pickups spawn at random clear spots, so a level has no start. `author` and `description` are optional
// extras the editor writes and the game's Community screen will show; the game ignores them today.

export const MAX_WALLS = 400;

export function newLevel(name = 'New Arena', options = {}) {
  return {
    kind: 'level',
    name,
    author: '',
    description: '',
    color: 'FF00FFFF',
    // The frame the shipped arenas have: four sides at 0.05 and 0.95
    walls: options.walls ?? frameWalls(),
    extra: { lead: [], attrs: [], nodes: [], rootComments: [] }, // what we do not understand, kept for a lossless re-save
    ...options.fields,
  };
}

export function frameWalls() {
  return [
    [0.05, 0.05, 0.05, 0.95],
    [0.95, 0.05, 0.95, 0.95],
    [0.05, 0.05, 0.95, 0.05],
    [0.05, 0.95, 0.95, 0.95],
  ];
}
