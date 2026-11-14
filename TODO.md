# Vector Rumble 2.x: what is left

State at 2026-11-14. Branch `feature/ui-revamp` (VectorRumble) is 17+ commits ahead of `main`, nothing pushed. The engine
branch `feature/splash-fixes` (VectorEngine, 3 commits off `main`) is also unpushed and untagged.

## How to pick this up

Start Claude Code in this folder. Its memory (`~/.claude/projects/-Users-dominique-Development-CartBlanche-VectorRumble/memory/`)
loads by itself, and holds the decisions behind this list. Then say, for example:

> Read TODO.md and do "Next session: AI pilots".

Rules that came out of the work so far:
- Game-specific UI stays in VectorRumble (`Core/Screens/Frame/`), never in VectorEngine, unless the user says otherwise.
- Commit only when asked. Never push; the user pushes. Never `git push --tags` in the games (tags publish to itch.io).
- Leave CI alone until told. Never commit `Core/Game.cs` if it still holds the local screenshot harness (see below).
- Many source files are CRLF: scripts that edit them must keep the line endings. macOS `sed` needs `-i ''`; there is no `timeout`.

## Next session: AI pilots, then the live background

1. Port Dean Ellis's AI (`origin/features/dellis1972/17`, commit `d0d901b`: `AIController`, `Ship.IsAI`). It was written against the old
   project layout, so port it, do not merge. Single Player offers up to three AI pilots (a row "AI pilots" on `MatchRulesScreen`).
2. AI difficulty: Easy, Medium, Hard, as a row in Settings (`SettingsScreen`, saved in `UserSettings`), probably also per game.
3. The live AI fight behind the menus (replaces the fireworks in `BackgroundScreen`), freezing with a dim "Paused" after 10 seconds
   without input and resuming on any input (mouse, key, touch, gamepad); a Settings row "Pause idle menus" under More. Not built yet.
4. The Play and Single Player descriptions already promise AI pilots. VectorRumble 2.x is not released without the AI.

## Still to do on the UI

- HUD: remove the four-player "Hold A / Press W / Press Up to join" prompts in `GameplayScreen.DrawHud` and the old four-slot local
  code behind them (`ShipControls.ForSlot`, join and leave hold timers in `Ship.ProcessInput`, `Strings.Hold_A_To_Join` and friends).
- The in-match score text and the engine's loading screen are still in the old style.
- Network match: no Rematch (needs the host to bring everyone back to the waiting room).
- Single player names the pilot after the computer's user name; no way to type a name.
- Settings has no Controls row (the game has no controls screen).
- Korean: re-run `python3 scripts/generate-cjk-fonts.py ko` whenever `Core/Strings.ko.resx` changes. Chinese and Japanese still use the larger
  LocalizedFontProcessor fonts; the same script could shrink them.

## Release path

- Engine: merge `feature/splash-fixes`, tag `v0.1.0-preview.5`, bump `EngineRef` in the games' CI. VectorRumble CI is pinned to `preview.4`, which
  lacks `SplashOptions.BloomIntensity`, the splash skip fix, `AudioManager.SetCategoryVolume` and the font reload on language change.
  The old backup branch `feature/arena-frame-menus` can then be deleted.
- Push `feature/ui-revamp`, let CI run (Desktop and Android), check the Android and iOS content builds (new fonts, Korean, strings).
- README, CHANGELOG and the itch page for 2.0.
- Native-speaker review of the translations of the new menu words (de, es, fr, ja, ko, zh-CN). Claude wrote them.

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

## Local screenshot harness (not committed)

`Core/Game.cs` in the working tree carries test hooks marked `// DEVSHOT`, switched on by environment variables: `VR_SHOT=<png>` saves the
back buffer after `VR_SHOT_AT` seconds and exits; `VR_OPEN=main|play|setup|pick|options|host|browse|about|progress`; `VR_LANG=<culture>`;
`VR_AUTOSTART` starts a single player match; `VR_THEN=pause|over|overnet|confirm|exit|notice` opens a screen over it; `VR_ACT=<rows>` selects
rows; `VR_NETGO` starts or readies in a network match. Strip them before committing (the committed `Core/Game.cs` has none). The hooks
read and could write the real settings file in `~/Library/Application Support/VectorRumble/`; avoid test steps that change the language.
