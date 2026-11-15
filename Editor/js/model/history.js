// Undo and redo by snapshot: a level or ship is small, so each step keeps a whole copy.
export const cloneModel = (level) => structuredClone(level);

export class History {
  constructor(limit = 200) {
    this.limit = limit;
    this.past = [];
    this.future = [];
  }

  // Call with a copy of the model as it is BEFORE an edit
  checkpoint(snapshot) {
    this.past.push(snapshot);
    if (this.past.length > this.limit) this.past.shift();
    this.future = [];
  }

  // Throw away the last checkpoint, for an edit that was cancelled
  discardLast() {
    this.past.pop();
  }

  get canUndo() { return this.past.length > 0; }
  get canRedo() { return this.future.length > 0; }

  // Each returns the model to switch to (or null), and keeps `current` so the move can be reversed
  undo(current) {
    if (!this.past.length) return null;
    this.future.push(cloneModel(current));
    return this.past.pop();
  }

  redo(current) {
    if (!this.future.length) return null;
    this.past.push(cloneModel(current));
    return this.future.pop();
  }

  clear() {
    this.past = [];
    this.future = [];
  }
}
