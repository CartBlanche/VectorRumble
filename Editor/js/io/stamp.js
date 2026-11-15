// The comment at the top of every file the editor writes, saying which editor wrote it. The game ignores comments.
// A file that already has one (written by this or an older editor) gets it replaced, never doubled.
import { EDITOR_NAME, EDITOR_VERSION } from '../version.js';

export const stampText = () => ` exported by ${EDITOR_NAME} ${EDITOR_VERSION} `;

export const isEditorStamp = (text) => new RegExp(`^\\s*exported by ${EDITOR_NAME}\\b`, 'i').test(text);
