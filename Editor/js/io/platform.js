// Where the game looks for arenas and ships that do not ship with it, for the player's own platform, so Export can say where a file goes.
// The game builds the path as <app data folder>/Vector Rumble/<Arenas|Ships>: Helper.GetMyDocumentsFolder (the same as SolidOps': the
// local app data folder on desktop, the Documents area on a phone) and GetAssemblyTitle. Both games share that code on purpose.
export const FOLDERS = {
  mac: { label: 'macOS', root: '~/Library/Application Support/Vector Rumble', sep: '/' },
  windows: { label: 'Windows', root: '%LOCALAPPDATA%\\Vector Rumble', sep: '\\' },
  linux: { label: 'Linux', root: '~/.local/share/Vector Rumble', sep: '/' },
};

// Phones and tablets. The game keeps these folders in the app's Documents area. On iOS the Files app shows it (if the app allows
// file sharing), so a saved file can be copied in. On Android it is the app's own storage, which a phone does not let you copy into.
export const IOS_NOTE = 'On an iPhone or iPad, save the file, then use the Files app to put it in the Vector Rumble folder (On My iPhone > Vector Rumble > Vector Rumble > Arenas or Ships).';
export const ANDROID_NOTE = 'Not possible on Android yet: the game keeps its files in its own app storage, which a phone does not let you copy files into. Make and play community arenas and ships on a computer for now.';

// The systems Export lists, in order
export const SYSTEMS = [
  { id: 'mac', label: 'On Mac' },
  { id: 'windows', label: 'On Windows' },
  { id: 'linux', label: 'On Linux' },
  { id: 'android', label: 'On Android' },
  { id: 'ios', label: 'On iPhone / iPad' },
];

// iPads report themselves as Macs, so touch tells them apart.
export function detectPlatform({ userAgent = '', platform = '', maxTouchPoints = 0 } = {}) {
  if (/android/i.test(userAgent)) return 'android'; // before Linux: Android reports Linux too
  if (/iphone|ipad|ipod/i.test(userAgent) || (/mac/i.test(platform) && maxTouchPoints > 1)) return 'ios';
  if (/win/i.test(platform) || /windows/i.test(userAgent)) return 'windows';
  if (/mac/i.test(platform) || /macintosh|mac os/i.test(userAgent)) return 'mac';
  if (/linux|x11/i.test(platform) || /linux|x11/i.test(userAgent)) return /cros/i.test(userAgent) ? 'other' : 'linux';
  return 'other';
}

// kind is 'level' (an arena) or 'ship'
export function folderName(kind) {
  return kind === 'ship' ? 'Ships' : 'Arenas';
}

// The full folder for a desktop platform, or null
export function folderPath(platform, kind) {
  const f = FOLDERS[platform];
  return f ? `${f.root}${f.sep}${folderName(kind)}` : null;
}

// What to show beside each system: a path, or a note (phones)
export function systemEntry(id, kind) {
  if (id === 'android') return { note: ANDROID_NOTE };
  if (id === 'ios') return { note: IOS_NOTE };
  return { path: folderPath(id, kind) };
}

// The sentence Export adds: where the file goes for this platform
export function whereToPutIt(platform, kind) {
  if (platform === 'android') return ANDROID_NOTE;
  if (platform === 'ios') return IOS_NOTE;
  const path = folderPath(platform, kind);
  const sub = folderName(kind);
  if (path) return `Put it in ${path} (create the folder if needed), then start the game and pick it in the ${kind === 'ship' ? 'ship' : 'arena'} list.`;
  return `Put it in the Vector Rumble/${sub} folder inside your user data folder (see Help for each system), then start the game.`;
}
