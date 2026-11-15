// The editor's own library of arenas and ships, kept in the browser's local storage.
// Each entry holds the file as XML (what Export writes) plus its kind and name for quick listing. Storage is injectable so it can be
// tested; without working storage the library lives in memory and says so (`persistent` is false), because work would be lost
// when the page closes.
const KEY = 'vrumble-library-v1';

function browserStorage() {
  try {
    const probe = `${KEY}-probe`;
    localStorage.setItem(probe, '1');
    localStorage.removeItem(probe);
    return localStorage;
  } catch {
    return null;
  }
}

export class Library {
  constructor(storage = browserStorage()) {
    this.storage = storage;
    this.persistent = storage != null;
    this.entries = new Map();
    this.last = { level: null, ship: null }; // the entry open last in each tab
    this.error = null;                       // why the last save failed, if it did
    this.load();
  }

  load() {
    try {
      const saved = JSON.parse(this.storage?.getItem(KEY) ?? 'null');
      for (const e of saved?.entries ?? []) {
        if (e && typeof e.id === 'string' && typeof e.xml === 'string' && (e.kind === 'level' || e.kind === 'ship')) {
          this.entries.set(e.id, { id: e.id, kind: e.kind, name: String(e.name ?? ''), updated: Number(e.updated) || 0, xml: e.xml });
        }
      }
      for (const kind of ['level', 'ship']) {
        const id = saved?.last?.[kind];
        this.last[kind] = this.entries.get(id)?.kind === kind ? id : null;
      }
    } catch { /* damaged or unavailable: start empty */ }
  }

  persist() {
    this.error = null;
    if (!this.storage) { this.error = 'This browser is not keeping data for the page, so your work will be lost when you close it. Export what you want to keep.'; return false; }
    try {
      this.storage.setItem(KEY, JSON.stringify({ entries: [...this.entries.values()], last: this.last }));
      return true;
    } catch (error) {
      this.error = `The browser would not store the library (${error.name === 'QuotaExceededError' ? 'storage is full' : error.message}).`;
      return false;
    }
  }

  // Entries of one kind without their XML, sorted by name
  list(kind) {
    return [...this.entries.values()]
      .filter((e) => e.kind === kind)
      .map(({ xml, ...rest }) => rest)
      .sort((a, b) => a.name.localeCompare(b.name, undefined, { numeric: true }));
  }

  get(id) {
    return this.entries.get(id) ?? null;
  }

  findByName(kind, name) {
    const wanted = (name ?? '').trim();
    return [...this.entries.values()].find((e) => e.kind === kind && e.name === wanted) ?? null;
  }

  // A name no library entry of this kind uses, starting from `base`: "New Arena", "New Arena 2", ...
  uniqueName(kind, base) {
    const taken = new Set([...this.entries.values()].filter((e) => e.kind === kind).map((e) => e.name));
    if (!taken.has(base)) return base;
    let n = 2;
    while (taken.has(`${base} ${n}`)) n++;
    return `${base} ${n}`;
  }

  // Add a model, or (with `id`) replace the entry stored under that id. `xml` is the file as written by writeAny.
  put(id, model, xml, now = Date.now()) {
    const key = id ?? `${model.kind}-${now.toString(36)}-${Math.random().toString(36).slice(2, 7)}`;
    this.entries.set(key, { id: key, kind: model.kind, name: (model.name ?? '').trim(), updated: now, xml });
    this.last[model.kind] = key;
    return { id: key, saved: this.persist() };
  }

  remove(id) {
    const entry = this.entries.get(id);
    this.entries.delete(id);
    if (entry && this.last[entry.kind] === id) this.last[entry.kind] = null;
    return this.persist();
  }

  setLast(kind, id) {
    this.last[kind] = this.entries.get(id)?.kind === kind ? id : null;
    this.persist();
  }
}
