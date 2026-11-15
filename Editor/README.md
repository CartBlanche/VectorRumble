# Vector Rumble Editor

A browser editor for Vector Rumble levels and ships (the Levels and Ships tabs are built; the game's Community screen is still to come).

Plain HTML, CSS and JavaScript: no build step, no dependencies, no server. See `SPEC.md` for what it will do.

## Using it

Open `index.html` through a web server (browsers do not run ES modules from `file://`):

```
cd Editor
python3 -m http.server 8000     # then open http://localhost:8000/
```

**New** starts a level from a template (or a ship, on the Ships tab, from an arrow or a remix of a shipped ship), **Import XML** adds files you have, **Library** lists everything saved in this browser (it saves as you work). **Export…** downloads `<Name>.xml`; put it in the game's `Arenas` or `Ships` folder (Export shows the folder for each system). Press **?** for gestures and shortcuts.

## Tests

```
node tests/run.mjs     # unit tests; needs Node 18 or later, no dependencies
```

`tests/ui-smoke.html` drives the real interface with synthetic pointer events. Serve the folder as above and open <http://localhost:8000/tests/ui-smoke.html>, or headless:
`chrome --headless=new --virtual-time-budget=30000 --dump-dom http://localhost:8000/tests/ui-smoke.html` (the title says PASS or FAIL).

## Packaging for itch.io

```
node tools/make-zip.mjs     # writes dist/vector-rumble-editor-<version>.zip
```

Upload it as an HTML project; `index.html` is at the zip's root.

## Layout

| Folder | What is in it |
|---|---|
| `js/io/` | Reading and writing the game's XML without losing anything it does not understand; number, platform and export helpers |
| `js/model/` | Level and ship models, undo history, the library in local storage, the game's built-in names |
| `js/validate/` | The warnings: level checks (walls, gaps, closed-off areas) and ship checks (outline, mass and radius budget) |
| `js/edit/` | The level editor (selection, dragging, undo), snapping, symmetry, transforms, templates and shapes; the ship editor (points, mirror, freehand, stats) |
| `js/view/` | The canvases: zoom and pan, drawing in the game's look, mouse and touch, the live ship preview |
| `js/ui/` | Dialogs and the side panel; `js/ships-page.js` is the Ships tab |
| `tests/` | Unit tests; `fixtures/` holds copies of the game's own arenas and ships, which must round-trip |

The core has no DOM code, which is why the tests run in Node.
