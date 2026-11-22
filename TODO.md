# Vector Rumble 2.x: what is left

State at 2026-11-20. Since 11-14: ships and asteroids scaled 1.5x, ship Radius used for collision, thrusters (game, file format, editor 0.3.0), a Single Player preview with real ships, and the last ship and colour remembered (see "Later" and "Still to do on the UI"). Earlier state, 2026-11-14 (evening): VectorRumble `main` has everything from the UI revamp, AI pilots, screenshots, DevShots and the idle menus
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
- Single Player's preview (`Core/Screens/MatchPreview.cs`) shows your last ship and colour (a random built-in ship if none is saved or it is a
  community ship) against one random rival per AI pilot (ships that come with the game only, up to 3). The last ship (`LastShip`, name or community
  file id) and colour (`LastColour`) are saved in `UserSettings` and offered first on the ship screen, single player and network. Not tested: a
  community ship as the saved one after the file is deleted, a network join with the saved colour already taken (it moves to the next free one).
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

- Two real machines on a LAN; a phone against a PC. Check in that match: both show the same sky as the host's; a hop on one machine is seen (bigger, shadow, shots passing under) on the other; an older build (protocol 3) is refused.
- The host leaving mid-lobby; joining a match already started; an older build being refused (protocol version).
- Gamepad and touch on the new screens; how a phone pauses a match (Escape/Start on desktop and pad).
- The pause "Leave?" box and game over on a phone.

## Later

- Power-up icons: built (a circle with twin lime bolts, a triangle fan with three blue bolts, a rocket in square brackets with a blue flame; 40 px across at
  rest, pulsing and turning once every 4 s; pickup radius 20). See `Core/Simulation/PowerUp.cs` (`IconPart`) and the three `*PowerUp.cs` files. Not checked by
  eye in a match: the triple laser and rocket pickups (only the double laser was seen), how they read at the real size, and a network match. The mockup is
  `docs/mockups/powerup-icons.html`.
- **After the v1.x launch, multiplayer icons:** make a set of icons (same line-art style, shapes that differ by outline first, colour second, readable at about
  40 px) for what a multiplayer game needs, for example a flag for capture the flag, a base or goal, a hill to hold, a bomb or objective carrier, a team marker,
  a shield or a heal pickup, a "ready" or "host" badge. Use designer agents for mockups first, then build the ones for modes that exist.
- **After the v1.x launch:** the ships and arenas work below. Mockups are committed in `docs/mockups/ships-and-arenas.html` (open it in a browser).
  **Next up: more and better ships and arenas.** There are 5 ships and 4 arenas (`Content/Ships`, `Content/Arenas`); the user wants more of both.
  Plan: designer agents propose designs as mockups first (no game files until approved), then approved ones become files built and checked with the
  editor's rules. Ships: outline 3..24 points, reach <= 22 px, nose toward -y, Radius 12..24, Mass 12..40 (from `Editor/js/model/ship.js`), 1 to 3
  thrusters, readable at 1.5x scale, flame colour rule (`Ship.FlameColour`) so avoid red/orange/yellow only if the ice-blue flame is wanted.
  Arenas: walls normalised 0..1 on 1280x720; ships are up to 72 px across at 1.5x (Radius 24), so keep gaps at least about 90 px; the AI's nav grid keeps
  30 px clear, so check a new arena with `VR_AIDEBUG=1`. Each design should say what makes it play differently (chokepoints, cover, open lanes), not just look different.
  Mockups were made (2026-11-20): 5 ships, 5 arenas, 5 themed pairs; the user is picking which to build. Wall numbers in the agents' reports were
  unreliable for Keep and Diamond Cut (the drawings were right), so regenerate the numbers from the drawings. Check how spawns are placed (arena files have none).
  **Sci-fi homage ships as a paid itch.io pack with an ownership check.** The user wants the famous-ship homages (Vic Wyvern, Rusty Kestrel, Bold Venture,
  Nostra Tug, Strike Foil, Grid Cycle, or whichever are picked) hosted on itch.io, singly or as a "SciFi pack", and the game to check on launch whether the
  player owns them and unlock them. Start with a feasibility check (which is a first step, not a go-ahead to build): (1) the simplest route needs no code,
  since community ship files already load, so the pack is a separate itch download of ship XMLs for the community folder, and the locked ships must not be in the free build;
  (2) an ownership check on desktop would use the itch app's launch environment (`ITCHIO_API_KEY`) and the itch.io API (download keys / owned keys for the game id); confirm
  the exact calls in the itch.io docs first, and whether a check without a server of ours is possible; (3) only works when launched from the itch app, not the web, mobile or
  a plain download; (4) a client-side check can be bypassed, so decide how much that matters; (5) offline play needs a remembered unlock. Needs the user's say-so for any itch page or paid pack.
- **After the v1.x launch, arenas wider than one screen** (decided 2026-11-22: not for v1.x). SolidOps already has wide levels (`Level.Width` above 1, panning,
  `SolidOps/Core/Rendering/GameCamera.cs`, `PlanningScreen`, and a width in its editor), but VectorEngine has no camera (only a transform hook in `ParticleManager`),
  and Vector Rumble is fixed at 1280x720 (`Display.Viewport`, arena walls normalised 0..1 across one screen). Pieces: (1) move `GameCamera` into VectorEngine so both games share it;
  (2) a camera for a multi-ship brawl that keeps the local ship (and where useful the others) in view; (3) an optional `Width` in the arena file, default 1 so existing arenas
  and community files still load, and a width and pan in the editor (`Editor/SPEC.md`, `js/model`, the validators); (4) the AI nav grid, spawn placement, asteroids, background and HUD
  for a larger world; (5) networking: every machine simulates the whole arena (ships, shots, asteroids sync at about 5 Hz, the host decides damage and scores, see
  `Core/Networking/PacketType.cs`), so a per-device camera is fine for drawing, but `ShipStateSync` and `RemoteShipInterpolator` assume everyone is on one screen. Nothing
  draws off-screen ships yet: add edge-of-screen arrows or a minimap from the positions already received, and consider sending less for far-away ships. The packet
  list was read but a network match was not traced or tested. Also decide fairness (a player who cannot see an attacker) and the effect on arena file transfer between players.
- Ships and asteroids are drawn 1.5x bigger for native 720p (`Core/Gameplay/ActorScale.cs`, `VR_SCALE` overrides it to try values). Done for outlines,
  collision radii, shield rings and asteroid sizes; community ship files are unchanged. Not scaled yet: ship speed, shot speed, weapon ranges. To check:
  gaps in arenas, spawn points, the AI's nav grid. Every machine in a network match must use the same value.
- A ship's collision radius is its file's `Radius` (kept to 12..24, times the scale; 70% of it with the shield down, the shield ring is drawn at it).
  Nimble and Bat-ter Up (radius 15) are now smaller targets than the 20 px every ship had. Watch the balance. The AI's nav grid still keeps 30 px clear.
- Thrusters (flames while a ship thrusts): built in the game. A ship file may have an optional `<Thrusters>` with up to 3
  `<Thruster X="" Y="" Size="" />` (ship-local px; Size 0.5 to 2, default 1; the flame always points straight back). No element: a flame at each of the
  outline's rearmost points; an empty element: no flame. The five shipped ships have theirs. The flame is amber, ice blue on red, orange and yellow ships
  (`Ship.FlameColour`). Parsed by `CommunityContent.TryParseThrusters` (a bad one skips the file, with a reason). The editor (0.3.0) has a Thruster tool
  (T): place, drag, Size, Automatic, None, checks, flames in the canvas and preview; see `Editor/SPEC.md` 3b. Later: an Angle for side thrusters, a few flame styles.
  Flames also lean into a turn (outer flames longer, inner shorter, from the change of heading, so remote ships show it) and a turn alone puffs
  the outer thruster (a middle one puffs from its outer side); `turnFlameSwing`, `turnPuffStrength` and `turnPuffOffset` in `Ship.cs` tune it.
  A remote ship's flame is still a guess from its speed and heading. Not checked: the editor Thruster tool by eye, a network match.
- One new asteroid shape should be "the hat": the 13-sided aperiodic monotile found in 2023 by David Smith, Craig Kaplan, Joseph Myers and
  Chaim Goodman-Strauss, an homage to the discovery.
- VectorEngine: a JSON loader for particle effects before v1.0; NuGet publishing (needs the user's API key and say-so); promote the
  language/settings code (`UserSettings`) to the engine if a third game needs it.
- The sky (built 2026-11-22, `Core/Rendering/Sky*.cs`): more variety after v1.x. Nebula hues are only violet, magenta and teal so far, kept cool so no ship or wall colour (orange, red, green, blue, yellow) disappears into them. NASA pictures also show amber, gold and red (supernova remnants, infrared), blue (reflection nebulae) and dark silhouettes; try one very dim warm hue in one or two skies and check it by eye with orange and red ships and red walls. Also: more skies in the pool, more constellations, and the real sky (see item 4 above). Tuning constants are at the top of `Sky.cs` (`GlowStrength`, `StarBrightness`, `CometBrightness`).
- SolidOps: merge `develop` into `main` only after it is public on itch.io (unrelated histories); 720p; iOS; the Windows DirectX head.

## Before v1.x: explosion shockwave, black holes, hopping over ships, the sky and directional sound (asked 2026-11-22)

**Order of work (decided 2026-11-22, re-ordered the same day; the OBS video waits until all of these are in, or it would be filmed twice).** Why: the sky and the sound are
independent of gameplay and have the longest testing, so they go first; hopping is the biggest gameplay change, so it goes next while there is the most time to settle it;
then a checkpoint on two real machines; the small shockwave, black holes and the AI pass come after, because black holes and hopping give the AI new things to avoid and use.
1. The sky (item 4): drawn only at first (constellations, planets, dotted nebula plus glow, comet; mockup `docs/mockups/sky.html`); the host's choice of sky in the match setup is added with the protocol change in step 4.
2. Directional sound (item 5): engine work shared with SolidOps, independent of gameplay, so the longest test time (Desktop, Android, iOS, Content build). Needs a VectorEngine tag and the CI `EngineRef` bumped.
3. Hopping over ships and shadows (item 3): BUILT 2026-11-22 (Space or Right Shift or pad A or touch A; 0.7 s hop, 1.5 s between; no firing in the air, shots and ships pass under, asteroids and walls still stop; landing on a ship pushes apart; shadows under every ship; height is a byte in the ship state packet and the protocol version is now 4; sound reuses the menu tick; the AI does not hop yet, that is step 7; `VR_HOPAT=<s>` makes every ship hop in DevShots) - was: the biggest (new input on keyboard, pad and touch, ship collisions, height on the network, shadows). If time is short this is the one to move to v1.1.
4. Checkpoint: BUILT 2026-11-22 (protocol 4: the ship state carries the hop height, the match start carries the host's sky number, one byte); tried with a host and a client on one machine (same sky and arena on both); still to do: two real machines (the user), see "Not yet tested". Was: the network changes so far (the sky choice, the hop height) with the protocol version bumped, then a test on two real machines (see "Not yet tested"). The version only has to differ from the last RELEASED build, so black holes (step 6) can add to the same bump if nothing has been released in between; no second bump is needed.
5. Shockwave (item 1): BUILT 2026-11-22 (`Core/Rendering/Shockwave.cs`: three ripple rings from the engine's `EmitRipple`, on ship deaths (ship colour, 240 px), mines (100 px, past the 80 px blast), rockets (140 px) and hop landings (36 px, also for remote ships); `VR_DIEAT=<s>` destroys every ship in DevShots; no Settings switch yet) - small, drawn only, no network. Also gives black holes and hop landings something to show.
6. Black holes (item 2): gameplay, host-decided and the same on every machine (sent like asteroids). The open questions in the item come first (does touching one kill; what it pulls; the AI keeping away).
7. AI pass: keep away from black holes, dodge and cross ships with a hop, then the open Chase circling problem and the dodge (see the AI pilots section and the memory note `vector-rumble-ai-and-video-plan`). Measure as before (parallel 90 s runs).
8. Release path (below): push, CI on Desktop and Android, iOS and Android content builds, README, CHANGELOG, itch page, translation review.
9. The OBS video (the plan is in the memory note `vector-rumble-ai-and-video-plan`).

1. **Shockwave on explosions.** A visible ripple that spreads out from a ship's (and later a mine's or rocket's) explosion. A shader is one way, not the only one:
   the game draws with `LineBatch` lines only and has no render target or effect of its own, so a first version can be expanding, fading vector rings (a
   few circles of `LineBatch` lines, ring radius growing with time), which also keeps the vector look. A real screen-space distortion needs a render target
   plus an `Effect`, which has to build for every head (Desktop, Android, iOS), so check the MonoGame content pipeline for each before choosing it.
   Network: explosions are already told to every machine, so the ring can be drawn locally from the same event.
2. **Black holes.** Spawn at random for a period of time, then go away, and pull nearby objects toward them (ships, asteroids, shots, mines, power-ups; decide
   which). Questions to settle: whether touching one kills a ship; the pull curve and range; whether the AI knows to keep away from them (it would
   need a new thing to avoid in `AIPilot`); a Match Rules switch; and the network (the host would decide where and when, and the pull must be the same on every
   machine, as asteroids sync at about 5 Hz). Draw it as a spinning vector spiral with a faint ring showing its reach.

3. **Hop over other ships.** A jump that lifts a ship over other ships (not over walls or asteroids, which still stop it). The ship grows a little while it is up
   (a height, drawn as scale), and a soft vector shadow under it (a dim copy of the outline, offset and fading) can come with it. Questions: does it carry its
   momentum; a short cooldown; can it fire in the air, and can shots hit it (no is simpler, and makes a jump a dodge); what happens when it lands on a ship (push apart,
   or damage); whether mines and rockets care. Touches: a new input on keyboard, pad and touch (`InputBindings`, the touch layout), the collision code between ships,
   `ShipStateSync` and `RemoteShipInterpolator` (height must be sent, so a protocol version bump), a sound cue, and the AI (`AIPilot` should dodge with it and know that an airborne
   rival cannot be hit). Shadows could then be drawn for every ship, a little under it, to read the height.

4. **A real sky behind the arena (to decide).** Today `Core/Rendering/Starfield.cs` draws random single-pixel stars with parallax. Options discussed: (a) the sky as it is
   at the player's time and place, which needs a star catalogue (bright stars only, a few hundred, as a small data file), the date and time, and a latitude
   and longitude, which means asking for the location (privacy, and it would not match on a network match) or using only the time; (b) well known constellations as
   fixed line-art shapes (Orion, the Plough, Cassiopeia, the Southern Cross) drawn in the vector style at different parallax depths, with their bright stars a little bigger
   and twinkling; (c) planets as slowly moving dots, from a few lines of orbit maths for the date. Option (b) is cheap and fits the look; (a) and (c) are cheap to
   compute but need a decision on whether the sky may differ between machines in a network match (it is only a background, so it can).
   Mockup: `docs/mockups/sky.html` (constellations, planets, nebula as faint dots plus a soft glow in violet, magenta and teal, comet). Decided direction: use both nebula layers, and pick the sky for a match from a hand-made pool of about 5 (layers, hues, cloud positions, which constellations), chosen by the host and sent in the match setup so every machine shows the same one (a protocol version bump, so bundle it with other network changes); a community arena may name its sky, otherwise it is random; draw only from a fixed seed in the pool entry. Keep the nebula dim enough that no ship colour (magenta, cyan) disappears into it. Decided 2026-11-22: random only for now, no Match Rules switch (add one later only if asked). Real sky (a later option, not for v1.x): the time of day is not the hard part, the place is. On a LAN or the same WiFi everyone shares a time zone, a place and a clock to within seconds, and the sky turns only 15 degrees an hour, so nothing needs syncing; online play across time zones would have the host send one UTC time and place. Get the place without a permission prompt from the device's time zone: its offset gives roughly the longitude, its name (for example Europe/Amsterdam) roughly the latitude, from a small table; fall back to a fixed sky when the lookup fails (phones and some systems report zones differently). Do not ask for the device's location (a prompt on phones and Steam, too much for a background).

5. **Directional sound: first version built locally 2026-11-22 (`Core/Audio/PositionalSounds.cs`, `World.PlaySound`, Settings "Directional sound"); still to do: listen and tune, then lift the class into VectorEngine (it has no game types), then music and the menu and rocket cues (still in XACT).** (decided 2026-11-22: take the sounds out of XACT, build it in VectorEngine so every game gets it).** Today nothing is panned: every sound goes
   through `AudioManager.PlayCue(string cueName)` (VectorEngine, XACT sound bank: `Content/Audio/VectorRumble.xsb/.xwb/.xgs`), which takes a name only, so an explosion on the
   left of the screen is as loud in the right ear. Checked: VectorEngine has no pan, emitter or listener code. SolidOps pans in its own game code: `Core/Gameplay/Managers/AmbientManager.cs`
   plays loose WAV clips (`SoundEffect.FromStream`, `CreateInstance()`) and sets `SoundEffectInstance.Volume`, `.Pan` and `.Pitch` from the source's position.
   Plan: (1) read the XACT project (`Content/Audio/VectorRumble.xap`) for what each cue does besides playing a wave: several waves per cue (random pick), volume, pitch, instance
   limits, categories (how `AudioLevels` reaches them); (2) a positional player in VectorEngine on top of `ClipCache` and `SoundEffectInstance`, for example `Sounds.Play(cueName, position)`:
   x position to a pan of about -0.7 to 0.7 over the play area's width (very hard pans tire in headphones), optional quieter with distance (little use in a one-screen arena),
   multiplied by `AudioLevels.Sfx`, a cap on voices at once with finished instances recycled (loose instances are limited on some platforms), a master switch (Settings) for
   people who dislike panning; (3) a small data file per game mapping a cue name to clips with volume and pitch variation, replacing the XACT cue list (the 16 WAVs are in
   `Content/Audio/Waves`; the code plays `powerUpTouch`, `powerUpSpawn`, `playerSpawn`, `playerDeath`, `explosionMedium`, `explosionLarge`, `asteroidTouch`, the weapon fire cues and the
   menu cues); (4) pass the event's position from explosions, shots, pickups and spawns; menu sounds stay centred; (5) convert those first, listen, then the rest. Music stays in
   XACT for now (a separate job). SolidOps can move its `AmbientManager` pan code onto the engine player later. Check on Desktop, Android, iOS and in the Content build.
   Network: each machine plays the sounds of events it simulates, so nothing is synced.

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
