# itch.io art

Everything for the itch.io page lives here.

| File | Size | Use |
|---|---|---|
| `banner-1600x400.png` | 1600x400 | Page banner. Keep titles and text inside x 320..1280 (itch crops the sides; verify). |
| `cover-630x500.png` | 630x500 | Cover image (itch minimum is 315x250, same look at that size). |

Both use the game's real logo (`Content/Textures/title.png`, 1920 px wide) and the real in-game power-up icons.

## Source

`source/gen.py` draws both as SVG from the real ship outlines in the script (copied from `Content/Ships`) and the logo texture:

    python3 itch/source/gen.py        # writes itch/source/out/*.svg (not committed)

Render each SVG to PNG at its own size with a browser, for example headless Chrome:

    "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome" --headless=new --disable-gpu --hide-scrollbars \
      --screenshot=itch/banner-1600x400.png --window-size=1600,400 file://$PWD/itch/source/out/B_dogfight_banner.svg
    (the cover is 630x500)

If a ship outline or a power-up icon changes in the game, update the shapes in `gen.py` to match.

Screenshots for the page go in `itch/screenshots/` (1280x720, taken with F12 or the `Core/DevShots.cs` harness, with the Menu fireworks setting off for menus: `VR_FIREWORKS=0`).
