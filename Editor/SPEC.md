# Vector Rumble Editor: Spec (draft v0.1, awaiting approval)

## 1. Purpose and constraints
- A browser editor with two tabs, **Levels** and **Ships**, so players can make arenas and ships and drop the files into the game's folders. Plain HTML, CSS and JavaScript, no build step, no dependencies, no server; it zips to itch.io like the SolidOps editor.
- Lives in `Editor/`, beside `Core/` and `Content/`. Same pattern as `SolidOps/Editor` (library in local storage, XML import and export, warnings that never block, Node unit tests, a browser smoke test, `tools/make-zip.mjs`, `CHANGELOG.md`, `README.md`). Generic modules (dialogs, undo history, library, XML read and write, viewport, theme) are adapted from SolidOps, not shared: the editors stay separate repos' code.
- Pure level and ship design. No enemies, hazards, power-ups or asteroids (yet).
- Decisions so far (2026-11-14): level scope is walls, colour and metadata only; the ship tab draws a polygon and derives stats from a budget; spec first, then build.

## 2. What the game does today (checked in code)
- **Level** = `Arena` (`Core/Gameplay/Arena.cs`): `Name`, `Color` (`AARRGGBB` hex), `Walls` (one line of single-space-separated floats, four per segment: x1 y1 x2 y2, normalised 0..1 across the 1280x720 screen, y down). Ships, asteroids and pickups spawn at random clear spots, so a level has no start. The `Walls` parse splits on a single space, so a double space throws and the level is lost.
- **Ship** = `Ship.Load` (`Core/Gameplay/Ship.cs`): `Mass` (int), `Radius` (int), `Color`, `Name`, `PlayerIndex` (empty = spare), `Shape/Points` (x y pairs, ship-local pixels, nose toward -y, same single-space rule).
- Built-ins: radius 15 (Bat-ter Up, Nimble) or 20 (Blaster, Defender, Destroyer); mass 20 or 32 (mass 20 goes with radius 15, mass 32 with radius 20); outlines fit in x -15..15, y -16..15, with 6 to 22 points.
- Both managers already read player files from `<MyDocuments>/Vector Rumble/Arenas|Ships/*.xml` (`Helper.GetMyDocumentsFolder`, `GetAssemblyTitle`, which is "Vector Rumble"). User files are never selectable in a network match (`BuiltInCount`, `BuiltInArenas`).

## 3a. Changes of plan (2026-11-15) — these override section 3 where they differ
- **No Community screen.** The game already lists every file in its `Arenas` and `Ships` folders in the arena and ship pickers; that is enough. The folder is `Arenas` (not `Levels`); there is no `Levels` folder and no fallback.
- **Folders** use SolidOps' `Helper.GetMyDocumentsFolder`: the local app data folder on desktop (`~/Library/Application Support`, `%LOCALAPPDATA%`, `~/.local/share`) and Documents on a phone, then `Vector Rumble/Arenas|Ships`. VectorRumble's own helper uses Documents today and must be changed to match.
- **Multiplayer:** the host sends a custom arena or ship across the network to clients that lack it, as RacingGame does for tracks (`SharedTracks`, `TrackRequest`/`TrackFile` packets, the file's SHA-256 as its id, a size cap). Files are small.
- The editor says "arena" everywhere, not "level".

## 3. Game-side changes (original plan, partly superseded: see 3a)
1. **Levels folder.** The game reads `<MyDocuments>/Vector Rumble/Levels` (the name the user asked for) and still reads `Arenas` so nothing made earlier is lost. `Ships` stays.
2. **Robust loading.** A bad community file is skipped with a reason, not a crash and not a silent drop: tolerant parsing (any whitespace), required elements checked, walls count checked, coordinates clamped or rejected, ship radius/mass/point-count limits enforced (section 6). Built-ins are untouched.
3. **Author and description.** Optional `<Author>` and `<Description>` on both asset types. Old files without them load as before; the game ignores unknown tags.
4. **Community menu.** A main-menu row "Community" (Arena Frame style, `FrameMenuScreen`) with two entries, Levels and Ships, each listing the files in the folder (name, author, a thumbnail drawn with `PreviewPainter`, the reason greyed if unplayable), the folder path at the bottom, an Open Folder button on desktop, and the editor's web address. Rescans each time it opens. Picking a community level or ship there takes you to Single Player with it selected. Community content stays single player (same rule as today). Strings go through `Strings.resx` in all seven languages.
5. Unit tests for the loaders (valid, hostile, legacy `Arenas`).

## 3b. Thrusters (2026-11-17)

A ship file may have an optional `<Thrusters>` after `<Shape>`: up to three `<Thruster X="" Y="" Size="" />` (ship-local px, nose toward -y; Size 0.5 to 2, default 1). The flame always points straight back (+y). No element: the game puts a flame at each of the outline's rearmost points (within 1 px of the lowest, up to 3). An empty `<Thrusters />`: no flame. The game refuses a file with more than 3, a position beyond 32 px from the centre, or a number that is not one (`CommunityContent.TryParseThrusters`); it keeps Size within 0.5 to 2. The flame colour is amber, ice blue on ships whose colour is within 60 degrees of orange on the colour wheel (`Ship.FlameColour`; `flameColour` in `model/ship.js`).

Editor: the model has `ship.thrusters`: null (not placed), `[]` (none) or `[{ x, y, size }]`. The Thruster tool, Size slider, Automatic and None buttons are on the Ships tab (see CHANGELOG 0.3.0). The editor keeps thrusters within its 22 px reach and snaps them like points. Checks: off the hull (over 4 px from every edge), too far, too many, odd size; a note for none. Not done: an Angle for side thrusters, more flame styles.

## 4. Level tab
**Canvas:** fixed 16:9, coordinates 0..1, y down, drawn in the game's look (black, neon lines, the chosen colour, glow). The playfield limits shown as a faint margin (the built-ins keep walls within 0.05..0.95).

**Edit:** a level has Name, Author, Description, Colour (a neon palette plus a free picker) and wall segments.
- Tools: Select (tap, drag, box select), Wall (drag one segment, or tap to chain), Shapes (rectangle, polygon, ring, line of pillars, cross: stamps of ordinary walls), Erase.
- Wall endpoints drag; deleting; group move, rotate, scale and mirror; undo and redo (one step per gesture).
- Snapping as toggles so it works on touch: grid with a size (default on), endpoints (default on), angle 15/45 degrees while drawing.
- **Symmetry helpers** (the best help for fair arenas): mirror left-right, mirror top-bottom, 4-way, and 180-degree rotation; everything drawn is repeated.
- Starting templates: Empty frame, Cross, Four pillars, Maze lite (the shipped ones are offered as read-only examples to remix).
- **Preview:** the game's own wall drawing, plus a toggle showing where AI pilots can route (the same grid and clearance the game's `NavGrid` builds: ship clearance 30 px) and a "ship size" ruler so the designer can see which gaps a ship fits.

**Checks (warnings only, never block save or export):**
- Required fields: name, colour; at least one wall; wall count a multiple of 4 coordinates; coordinates in 0..1 and walls inside 0.04..0.96.
- Zero-length segments; duplicate segments; segments crossing each other (allowed, but noted).
- **Open arena:** the play area is not sealed (flood fill reaches the screen edge). Ships would escape the playfield.
- **Pockets:** a region a ship cannot enter or leave (gaps under the ship clearance), and the share of the arena that is reachable (warn under 70%).
- **Choke gaps:** a gap too narrow for the biggest ship (about 52 px), and one narrow enough that the AI cannot route through it.
- Too many walls (limit 400 segments, to be confirmed against `NavGrid` build cost).
- Name clashes with a built-in or another library level.

## 5. Ships tab
**Canvas:** square, ship-local pixels, nose up (-y), with a ruler grid, the three Radius circles (small 15, large 20) and the built-in ships as ghost outlines to compare size.

**Edit:** Name, Author, Description, Colour (neon palette), outline points.
- Tools: Add point (tap on an edge to insert, tap in empty space to append), Move, Delete, Draw freehand (simplified to a point budget), Symmetry on by default (mirror about the centre line, the usual way a ship is drawn), and Remix (start from any built-in).
- Outline rules: closed polygon of 3 to 24 points, no self-crossing, every point within the budget radius, snapped to 0.5 px.
- **Budget (decided):** the editor derives **Radius** from the outline (the farthest point, rounded up, 10 to 22) and offers **Mass** from a small range tied to size, so ships stay fair: mass = clamp to 16..36 and proportional to the polygon's area relative to the built-ins' range; a "Heavier / Lighter" slider moves mass within +-20% of that value. Radius is not free. The preview says how the ship compares (smaller than Nimble, in the middle, bigger than Blaster).
- Live preview: the ship flying, thrusting and firing along a short path on a mini arena, rotating, with the game's vector look. Cannot show real handling since the game's thrust is common to all ships.
- `PlayerIndex` is always written empty (a spare ship). Community ships are never one of the five numbered controllers' defaults.

**Checks:** at least 3 points; self-intersection; area too small or too large; polygon not closed or clockwise/counter-clockwise (the editor normalises direction); a point beyond the radius limit; asymmetric (a note, not a warning); the name clashing with a built-in.

## 6. Shared: library, import, export
- Library in the browser's local storage, autosaved. Library lists levels or ships by tab, with Open, Duplicate, Export and Delete (asks first). New, Import XML (many files at once; same name asks Replace, Import as copy, or Cancel).
- **Round trip:** the editor reads the game's own XML (`XnaContent` / `Asset Type="VectorRumble:Arena"` or `VectorRumble:Ship`), keeps elements it does not know, and writes single-space numbers (at most 3 decimals, no padding). Every file starts with a comment, `exported by Vector Rumble Editor <version>`, replaced, never doubled, as in SolidOps.
- **Export** downloads `<Name>.xml` (unsafe characters replaced) and tells the player the folder for their system (detected in the browser): macOS, Windows and Linux are `~/Documents/Vector Rumble/Levels` or `Ships` (the Documents folder .NET reports; to be re-checked per platform when the game side is built). Phones: Import in the Community screen later (not in scope; same position as SolidOps' Android note).
- **Share:** a "Pack" export that downloads a level and its best-fitting ship together is out of scope for v1.
- Warnings in both tabs show as a badge and a list; clicking one selects the thing it is about.

## 7. UI and structure
- Header: tabs (Levels, Ships), Import, New, Library, checks badge, Export, theme toggle, About, Help. Same responsive rules as SolidOps (shrinking toolbars on phones, a one-time "turn sideways" hint, touch drag, pinch zoom, two-finger pan).
- Look: dark neon like the game; the Orbitron display font the game's UI uses, Hanken Grotesk for body (both from Google Fonts, as SolidOps does).
- Suggestions folded in, to give players the best experience: symmetry tools, templates and shipped-level remixing, the AI-routability and ship-size overlays, a fairness-budgeted ship, a thumbnail export (PNG) for sharing a level online.

```
Editor/
  index.html  css/editor.css  README.md  SPEC.md  CHANGELOG.md  package.json
  js/main.js version.js geometry.js
  js/model/    level.js ship.js history.js library.js ids.js
  js/io/       xml.js xml-read.js xml-write.js stamp.js export.js platform.js
  js/validate/ level-checks.js ship-checks.js grid.js
  js/edit/     level-editor.js ship-editor.js snap.js symmetry.js transform.js templates.js
  js/view/     viewport.js draw-level.js draw-ship.js preview.js
  js/ui/       dialog.js export-dialog.js properties-panel.js help.js tabs.js
  samples/ tests/ tools/ cover/
```

## 8. Build plan
1. Editor core (model, XML round trip, checks, history, library) with Node tests.
2. Levels tab (draw, snap, symmetry, templates, checks, export).
3. Ships tab (outline editing, budget, preview, checks, export).
4. UI polish, themes, touch, smoke test, README, zip.
5. Game side (section 3), with tests, then the folder text in the editor's export dialog is verified against the real game on macOS.

## 9. Open points (not blocking)
- Wall count limit and the ship clearance numbers come from `NavGrid` and `AIPilot`; they are read from the code when step 1 starts and the checks will cite them.
- Whether community levels also show a thumbnail PNG in the game (computed, not stored) is decided in step 5.
- Hosting: itch.io HTML upload, as for SolidOps; the About text and Community screen link wait for the page's address.
- Android has no folder the player can reach (same as SolidOps); out of scope.
