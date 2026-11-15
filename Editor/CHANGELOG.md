# Changelog

## 0.2.0 (unreleased)
- Ships tab: draw an outline of 3 to 24 points (Move, Add, Delete and freehand Draw tools), with Mirror on by default so both sides change together; snap to 0.5, 1 or 2 px; make the outline symmetrical from either half; centre, scale and flip it. Radius is worked out from the outline and Mass moves within 20% of what the size suggests. Ghost outlines of the shipped ships and the radius circles (15, 20 and the 22 px limit) to judge size; a live preview of the ship thrusting and firing; remix any shipped ship or start from a simple arrow; Checks panel; autosave to the library, ship thumbnails in the Library, export (download or copy). `PlayerIndex` is always written empty.
- The header buttons (New, Library, Checks, Export) now act on whichever tab is showing, and the editor opens on the tab it was last on.
- Importing a ship file opens it in the Ships tab.
- Help covers ships.
- "Levels" is now "Arenas" everywhere the player sees it (tab, dialogs, checks, Help), matching the game's own word and its `Arenas` folder.
- Export lists the folder for every system like the SolidOps editor does (the reader's own highlighted, click to copy the path): `~/Library/Application Support/Vector Rumble/Arenas|Ships` on macOS, `%LOCALAPPDATA%\Vector Rumble\...` on Windows, `~/.local/share/Vector Rumble/...` on Linux, plus the iPhone/iPad and Android notes. These are the folders the game will use once its `Helper.GetMyDocumentsFolder` matches SolidOps' (game-side change, not made yet).

## 0.1.0 (unreleased)
- Levels tab: Select, Wall (drag or chain), Shapes (rectangle, polygon, ring, cross, pillars) and Erase tools; grid, end and angle snapping; symmetry (left-right, top-bottom, 4-way, half turn); selection tools (turn, flip, scale, duplicate, nudge); undo and redo; templates and remixes of the shipped arenas; a Space overlay and a Ship size ruler; Checks panel; library with autosave; import, export (download, copy, picture); Help; light and dark themes; touch and phone layout. The Ships tab is a placeholder.
- Editor core: XML reading and writing for levels (`VectorRumble:Arena`) and ships (`VectorRumble:Ship`) with lossless extras, level and ship models, level and ship checks, undo history, a library for both kinds, platform folder text, and Node tests. No interface yet.
