# Vector Rumble 2.x: what is left

State at 2026-11-14. Branch `feature/ui-revamp` (VectorRumble) is 17+ commits ahead of `main`, nothing pushed. The engine
branch `feature/splash-fixes` (VectorEngine, 3 commits off `main`) is also unpushed and untagged.

## How to pick this up

Start Claude Code in this folder. Its memory (`~/.claude/projects/-Users-dominique-Development-CartBlanche-VectorRumble/memory/`)
loads by itself, and holds the decisions behind this list. Then say, for example:

> Read TODO.md and do "the live AI fight behind the menus" (item 4 under AI pilots).

Rules that came out of the work so far:
- Game-specific UI stays in VectorRumble (`Core/Screens/Frame/`), never in VectorEngine, unless the user says otherwise.
- Commit only when asked. Never push; the user pushes. Never `git push --tags` in the games (tags publish to itch.io).
- Leave CI alone until told. Never commit `Core/Game.cs` if it still holds the local screenshot harness (see below).
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
4. The live AI fight behind the menus (replaces the fireworks in `BackgroundScreen`), freezing with a dim "Paused" after 10 seconds without
   input and resuming on any input (mouse, key, touch, gamepad); a Settings row "Pause idle menus" under More. Not built yet. `AIPilot` is
   written for a world with ships in it, so two AIs fighting needs a small harness, not new AI.
5. VectorRumble 2.x is not released without the AI; it exists now, so this is down to its quality.

## Still to do on the UI

- HUD: remove the four-player "Hold A / Press W / Press Up to join" prompts in `GameplayScreen.DrawHud` and the old four-slot local
  code behind them (`ShipControls.ForSlot`, join and leave hold timers in `Ship.ProcessInput`, `Strings.Hold_A_To_Join` and friends).
- The in-match score text and the engine's loading screen are still in the old style.
- Network match: no Rematch (needs the host to bring everyone back to the waiting room).
- Single player names the pilot after the computer's user name; no way to type a name. AI pilots are named "AI 1" to "AI 3" (`AI_Pilot_Name`).
- Settings has no Controls row (the game has no controls screen).
- Korean: re-run `python3 scripts/generate-cjk-fonts.py ko` whenever `Core/Strings.ko.resx` changes. Chinese and Japanese still use the larger
  LocalizedFontProcessor fonts; the same script could shrink them.

## Release path

- Engine: merge `feature/splash-fixes`, tag `v0.1.0-preview.5`, bump `EngineRef` in the games' CI. VectorRumble CI is pinned to `preview.4`, which
  lacks `SplashOptions.BloomIntensity`, the splash skip fix, `AudioManager.SetCategoryVolume` and the font reload on language change.
  The old backup branch `feature/arena-frame-menus` can then be deleted.
- Push `feature/ui-revamp`, let CI run (Desktop and Android), check the Android and iOS content builds (new fonts, Korean, strings).
- README, CHANGELOG and the itch page for 2.0.
- Native-speaker review of the translations of the new menu words and the AI words (AI pilots, AI difficulty, Easy, Medium, Hard, None, "AI {0}") (de, es, fr, ja, ko, zh-CN). Claude wrote them.

## Not yet tested

- Two real machines on a LAN; a phone against a PC.
- The host leaving mid-lobby; joining a match already started; an older build being refused (protocol version).
- Gamepad and touch on the new screens; how a phone pauses a match (Escape/Start on desktop and pad).
- The pause "Leave?" box and game over on a phone.

## Later

- Mockups (use agents): new ship types; clearer power-up types (today you cannot tell what you are picking up).
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
