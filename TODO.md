# Vector Rumble 2.x: what is left

State at 2026-11-14 (evening). VectorRumble `main` has everything from the UI revamp, AI pilots, screenshots, DevShots and the idle menus
(`feature/ui-revamp` was merged into it); nothing of it is pushed yet. VectorEngine `main` is pushed, tagged `v0.1.0-preview.5`.

## How to pick this up

Start Claude Code in this folder. Its memory (`~/.claude/projects/-Users-dominique-Development-CartBlanche-VectorRumble/memory/`)
loads by itself, and holds the decisions behind this list. Then say, for example:

> Read TODO.md and do "Tune from tester feedback" (item 1 under AI pilots).

Rules that came out of the work so far:
- Game-specific UI stays in VectorRumble (`Core/Screens/Frame/`), never in VectorEngine, unless the user says otherwise.
- Commit only when asked. Never push; the user pushes. Never `git push --tags` in the games (tags publish to itch.io).
- Leave CI alone until told. The scripted screenshot harness is `Core/DevShots.cs`, compiled only with `-p:DevShots=true` (see below); `Core/Game.cs` carries only `#if DEVSHOT` hooks.
- Many source files are CRLF: scripts that edit them must keep the line endings. macOS `sed` needs `-i ''`; there is no `timeout`.

## AI pilots: built (commit 994420a), waiting for tester feedback

Done: single player has 0 to 3 AI pilots (row "AI pilots" on `MatchRulesScreen`, default 1) and Settings has "AI difficulty" (Easy, Medium,
Hard, saved in `UserSettings`). Not a merge of Dean Ellis's `AIController` (`origin/features/dellis1972/17`, `d0d901b`): `Core/Gameplay/AIPilot.cs`
reuses his ideas (nearest rival, predictive aim, power-ups, mines) but outputs the same `ShipInput` a person does, so `Ship` has one movement
and fire path. States: Patrol, Chase (with a warning shot), Search, Flee. `Core/Gameplay/NavGrid.cs` is a grid and A* built from the arena's walls
when the match starts (`World.Navigation`), so community levels need nothing embedded. Levels are the `AIProfile` table; the numbers are first
guesses. `VR_AIDEBUG=1` draws each pilot's state (green Patrol, red Chase, yellow Search, cyan Flee) and route and logs `[AI]` transitions and
stuck events. Never watched in a long match by Claude; the user and testers are playing it.

1. Tune from tester feedback (paste `[AI]` log lines): Easy, Medium and Hard numbers, patrol bunching, Hard's cover choice (30 random samples),
   getting stuck near asteroids and walls (fixed once by sliding along obstacles; check it holds), the warning shot (without free aim it follows
   the nose, so it can hit by luck).
2. Not tested: gamepad rumble with AIs (AI ships do not vibrate any pad), touch and pad on the new rows, a long match for slow memory growth.
3. Asteroids are indestructible (`Asteroid.Damage` returns false); they do not break up. Splitting them would be a new feature.
4. Idle menus (done): after 10 seconds without input the menus freeze (fireworks and animation stop) under a dim "Paused", and any input (key,
   mouse move, touch, pad) wakes them. Settings > More > "Pause idle menus" (`UserSettings.PauseIdleMenus`, on by default). `MenuIdle`,
   `BackgroundScreen`; the network lobby and the browser never freeze (`FrameScreen.AllowsIdlePause`). The fireworks stay as the background;
   there is no live AI fight behind the menus. Not tested: touch and pad waking it on a phone, the first input after waking also acts on the menu.
5. VectorRumble 2.x is not released without the AI; it exists now, so this is down to its quality.

## Still to do on the UI

- The in-match score text and the engine's loading screen are still in the old style.
- Network match: no Rematch (needs the host to bring everyone back to the waiting room).
- Single player names the pilot after the computer's user name; no way to type a name. AI pilots are named "AI 1" to "AI 3" (`AI_Pilot_Name`).
- Settings has no Controls row (the game has no controls screen).
- CJK fonts: re-run `python3 scripts/generate-cjk-fonts.py ko ja zh-CN` whenever a `Core/Strings.<lang>.resx` changes (Korean, Japanese and Chinese are
  all built from the characters their strings use). The Chinese Large font is still about 540 KB, twice the others.

## Community arenas and ships, and the editor

Built (commits after `ebbdbed`): `Editor/` is a browser editor (Arenas and Ships tabs, see `Editor/README.md` and `Editor/SPEC.md` section 3a). The
game lists every valid file in `<app data>/Vector Rumble/Arenas` and `.../Ships` (`Helper.GetCommunityFolder`: `~/Library/Application Support`,
`%LOCALAPPDATA%`, `~/.local/share`, Documents on a phone; the old Documents folder is still read) in the single player and network pickers. There is
no Community screen. `Core/Gameplay/CommunityContent.cs` loads tolerantly and skips a bad file with a reason (`CommunityContent.Skipped`, log only).
In a network game the host sends a community arena (and each player their community ship) to those who lack it, by file id (SHA-256), checked on
arrival (`SharedFiles`, `FileRequest`/`FileData`/`FileUpload` packets, protocol version 3). Received files stay in memory only.
Not tested: two real machines (the transfer has unit tests for the packets and the file checks, but the lobby ran only in a single player smoke test),
a real phone, iOS file sharing (`Info.plist`), Android (no way in), the editor in Safari and Firefox. Ideas: list skipped files and a "save this arena"
choice for received ones on screen; show Author in the pickers.

## Release path

- Engine (done): `feature/splash-fixes` merged into VectorEngine `main`, tagged `v0.1.0-preview.5` (the splash shows the engine version), pushed.
  VectorRumble CI's `EngineRef` is bumped to it. The old backup branch `feature/arena-frame-menus` in the engine can be deleted.
- Push VectorRumble `main` (the branch `feature/ui-revamp` is merged into it), let CI run (Desktop and Android), check the Android and iOS content builds (new fonts, Korean, strings).
- README, CHANGELOG and the itch page for 2.0.
- Native-speaker review of the translations of the new menu words and the AI words (AI pilots, AI difficulty, Easy, Medium, Hard, None, "AI {0}") (de, es, fr, ja, ko, zh-CN). Claude wrote them.

## Not yet tested

- Two real machines on a LAN; a phone against a PC.
- The host leaving mid-lobby; joining a match already started; an older build being refused (protocol version).
- Gamepad and touch on the new screens; how a phone pauses a match (Escape/Start on desktop and pad).
- The pause "Leave?" box and game over on a phone.

## Later

- Mockups (use agents): new ship types; clearer power-up types (today you cannot tell what you are picking up).
- Ships and asteroids are drawn 1.5x bigger for native 720p (`Core/Gameplay/ActorScale.cs`, `VR_SCALE` overrides it to try values). Done for outlines,
  collision radii, shield rings and asteroid sizes; community ship files are unchanged. Not scaled yet: ship speed, shot speed, weapon ranges. To check:
  gaps in arenas, spawn points, the AI's nav grid. Every machine in a network match must use the same value.
- A ship's collision radius is its file's `Radius` (kept to 12..24, times the scale; 70% of it with the shield down, the shield ring is drawn at it).
  Nimble and Bat-ter Up (radius 15) are now smaller targets than the 20 px every ship had. Watch the balance. The AI's nav grid still keeps 30 px clear.
- Thrusters (flames while a ship thrusts): built in the game. A ship file may have an optional `<Thrusters>` with up to 3
  `<Thruster X="" Y="" Size="" />` (ship-local px; Size 0.5 to 2, default 1; the flame always points straight back). No element: a flame at each of the
  outline's rearmost points; an empty element: no flame. The five shipped ships have theirs. The flame is amber, ice blue on red, orange and yellow ships
  (`Ship.FlameColour`). Parsed by `CommunityContent.TryParseThrusters` (a bad one skips the file, with a reason). The editor keeps the element as an
  unknown node but cannot edit it: the Ships tab still needs a Thruster tool (draggable markers on the outline, the live preview showing their flames,
  Size, a limit of 3, a check that they sit near the hull), and `Editor/SPEC.md` needs a section for it. Later: an Angle for side thrusters, a few flame styles.
- One new asteroid shape should be "the hat": the 13-sided aperiodic monotile found in 2023 by David Smith, Craig Kaplan, Joseph Myers and
  Chaim Goodman-Strauss, an homage to the discovery.
- VectorEngine: a JSON loader for particle effects before v1.0; NuGet publishing (needs the user's API key and say-so); promote the
  language/settings code (`UserSettings`) to the engine if a third game needs it.
- SolidOps: merge `develop` into `main` only after it is public on itch.io (unrelated histories); 720p; iOS; the Windows DirectX head.

## Screenshots (F12) and the local scripted harness

F12 or Print Screen on desktop saves a PNG of the game (`Core/Screenshots.cs`, 1280x720 area without letterbox bars or the mouse pointer) to
`~/Pictures/VectorRumble/` and shows "Screenshot saved" for 2.5 s. It is a normal feature, committed with the game. Debug builds show the green
performance readout in it (F3 hides it; Release builds do not have it): hide it before taking pictures for the itch.io page. It is in VectorRumble,
not the engine; promote it to `VectorEngine` (a capture hook before the cursor and overlays) if SolidOps wants it. Not tested: pad or touch (no
key on a phone), fullscreen on a large display, a Pictures folder that is read-only.

### Scripted harness: `Core/DevShots.cs` (committed, off by default)

Compiled in only with `dotnet build Desktop -p:DevShots=true` (the `DEVSHOT` symbol; `Game.cs`, `Screenshots.cs` and `UserSettings.Save` have
`#if DEVSHOT` hooks). Environment variables drive it; the list is in the file's header comment: `VR_SHOT=<png>` saves the game's picture after
`VR_SHOT_AT` seconds and quits; `VR_OPEN=main|play|setup|pick|options|host|browse|about|progress`; `VR_AUTOSTART` starts a single player match
on `pick`; `VR_THEN=pause|over|overnet|confirm|exit|notice` and `VR_ACT=<rows>`; `VR_LANG=<culture>` and `VR_LANGSTEP`; `VR_NETGO`; `VR_LOG`.
A DEVSHOT build never writes the real settings file, so language runs are safe. Run it from a normal terminal, not a subshell: some earlier
runs launched in the background stalled (cause not found). Rebuild without the flag to get the normal game back (a plain `dotnet build` does).
