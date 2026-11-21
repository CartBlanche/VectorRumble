# itch.io page: style guide and first-draft copy

Anything marked (verify) is an itch.io figure or a claim about the game I have not checked against the live page or a playtest. Read the draft copy against the game before publishing.

## Style guide

**Feel:** 1980s vector arcade cabinet, seen through a bloom filter. Thin glowing lines on near-black, a little CRT warmth, no filled shapes, no gradients on surfaces. Everything is outline; glow does the lighting.

**Palette** (colours taken from the art in `source/gen.py` and the game's power-up icons)

| Role | Hex | Use |
|---|---|---|
| Space | `#06061a` | Page and art background (near-black blue, never pure black behind text) |
| Ink | `#eaf0ff` | Titles and body text |
| Arena green | the game's wall green | Arena borders, only in gameplay art |
| Ship magenta | `#ff7ad9` | Destroyer, primary accent |
| Electric blue | `#3d5bff` / `#6a7cff` | Secondary accent, links |
| Violet | `#8a4bff` | Glow and gradients behind the logo |
| Score yellow | `#ffe14a` | HUD text, highlights |
| Power-up lime / cornflower / orange | `#9dff2e` / `#6495ed` / `#ff8c1a` | Double Laser / Triple Laser / Rocket, always with that meaning |
| Thruster blue | `#4db8ff` | Flames |

**Type:** Orbitron (or Eurostile, then Arial) semi-bold for headings, stretched wide, with a thin black stroke so it reads over art. Body text stays in the page's default font for readability (verify the page's custom CSS options before styling it).

**itch.io theme editor values:** BG `#06061a`, BG 2 `#0b0d2a` (or `#06061a` for a flat look), Text `#eaf0ff`, Link `#ff7ad9`, Font Lato, Size Large, Header font Orbitron (itch offers the Google fonts). Button or border colours under "More options": `#ff7ad9` for accents, `#3d5bff` for borders (not yet seen on screen).

**Rules**
- Outline only, 1 to 1.5 px lines at 1280x720; glow, not fill.
- Use the real logo (`Content/Textures/title.png`) and the real ship and power-up shapes; never invent look-alikes (the first banner draft did, and it was wrong).
- Each power-up colour keeps its meaning everywhere.
- Keep banner text inside x 320 to 1280 of 1600 (verify itch's crop).
- Screenshots: 1280x720, menus taken with Menu fireworks off (`VR_FIREWORKS=0`).

## Screenshots now in `screenshots/`

Menus: `main`, `play`, `play_ja`, `pick_defender`, `pick_destroyer`, `pick_nimble`, `progress`, `rules_ai3`, `settings`, `settings_de`.
Matches (new, from a four-ship AI dogfight): `match_rocket_pickup` (rocket pickup in its brackets, shielded ships), `match_rocket_trail` (a rocket streaking across), `match_explosion_doublelaser` (explosion and a Double Laser pickup).

Honest notes: ships are small at 1280x720 and the matches are sparse, so a crop of the action may sell better than the full frame. Still missing: a tight thruster close-up, a mine shot, the editor, and a busy melee. Those need a harness addition (force power-ups and give weapons) and a screen capture of `Editor/`.

## Draft copy

Moved to `page-copy.html` (open it in a browser; each block has a Copy button).

## Still to confirm before publishing
- Content counts (5 ships, 4 arenas) are from the project notes; a count on the page should match the shipped build.
- Whether to say "online"; only LAN is documented.
- Banner crop, GIF 3 MB limit, cover ratio, whether the custom CSS is allowed on your plan.
- A native-speaker check on the new translated "Menu fireworks" label.
