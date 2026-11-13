# VectorEngine extraction: shared agent memory

READ THIS FIRST. Update it as you work. It is the single source of truth, so you should not need to re-derive anything below.
Branch: `feature/vector-engine` in BOTH repos. Never touch master/main/develop.
Repos: /Users/dominique/Development/CartBlanche/VectorRumble (engine lives in `Engine/`) and /Users/dominique/Development/CartBlanche/SolidOps.

## Decisions (made by the user, final)
1. Baseline = SolidOps version of any file that exists in both. Where it doesn't exist in SolidOps, use VectorRumble's.
2. Namespaces: `VectorEngine.*`. Engine path: `VectorRumble/Engine/`, referenced via ProjectReference from both games.
3. Default virtual resolution 720p (1280x720), set via `Display.Configure(w,h)` at startup (1080p possible later). User will move SolidOps to 720p themselves, later and separately. Do not change SolidOps' 800x480 as part of extraction.
4. `GameAction`: core enum + game-defined extras. Saved settings / XML bindings format must NOT change.
5. Networking = optional separate assembly `VectorEngine.Networking` (only one referencing MonoGame.Xna.Framework.Net). Packet ids 64-90 keep their numbers. Add a protocol version byte (one-time wire break accepted).
6. Menu style is game-specific. Engine default = traditional VectorRumble-style menu (`MenuScreen`/`MenuEntry` with a `MenuStyle` object). Games override with their own style (e.g. SolidOps radial menu stays in SolidOps).
7. Platforms: Desktop first, then Android. Ignore WebGL/Windows/iOS verification for now.
8. Pin MonoGame to 3.8.6.1 in both repos (engine gets its own Directory.Build.props defining MonoGameVersion).
9. CI: LEAVE ALONE on both repos for now. Do not edit .github/workflows. Do not trigger publishes.
10. Namespace rename risk: resource lookups break at runtime (e.g. `Core.Localization.Resources` in Resources.Designer.cs). Verify explicitly, a clean build is not proof.

## Rules for every agent
- Commit small, one subsystem per commit, on the feature branch only. Build both games (Desktop) and run tests after each step.
- Preserve gameplay: copy physics constants verbatim; keep actor update order; engine owns a seedable Random; no fixed timestep (opt-in later).
- Seams = options/style objects/interfaces so each game keeps its look. Do not unify game-specific constants.
- Before editing a file, add a CLAIM line below. Do not touch files someone else claimed. Remove/mark DONE when finished.
- Record discoveries in "Findings" (short bullets) so others do not re-investigate.
- Do not paste large code or reports here. Link to paths.

## Extraction order (mark status)
- [x] 0 Scaffold DONE: Engine/VectorEngine.Core + VectorEngine.Tests, Engine/Directory.Build.props, ProjectReference in both games, SolidOps pinned to 3.8.6.1, golden runs recorded (see Findings)
- [x] 1 DONE (partial by design): Collision, CollectCollection moved. CollisionResult + WorldActor depend on Actor, so they move in step 5
- [x] 2 Rendering DONE except ParticleManager: LineBatch, VectorPolygon, ParticleSystem, Bloom + shaders (Engine/Content/Engine.mgcb). ParticleManager (SO-only, 840 lines, ScreenManager-coupled) deferred until a second game wants it
- [x] 3 DONE: AudioManager (+AudioLevels, ClipCache), settings storage + SettingsManager<T> (app name injected), LocalizationManager, Display (configurable, default 720p), IdleFade, Accelerometer
- [ ] 4 ScreenManager (ScreenManagerOptions), GameScreen, InputState, ActionBindings, MenuScreen/MenuStyle, MessageBox, LoadingScreen, VectorEngineGame base class
- [ ] 5 WorldBase + Actor core, wall-hit policy replacing `actors[0]` hack
- [ ] 6 VectorEngine.Networking
- [ ] 7 Cleanup: namespaces, docs

## Engine vs game map (summary)
ENGINE: Collision*, WorldActor, CollectCollection (SO impl), Actor core, WorldBase, LineBatch, VectorPolygon, ParticleSystem, ParticleManager, Bloom*, shaders (BloomExtract/Combine, GaussianBlur), Starfield, PerfOverlay, Display, ScreenManager, GameScreen, InputState, ActionBindings (core), IdleFade, Accelerometer, MenuScreen/MenuEntry, MessageBox, LoadingScreen, AudioManager (+AudioLevels, ClipCache), settings storage + SettingsManager<T>, LocalizationManager, AllocProbe, generic networking.
ENGINE-LATER: VectorEngine.AI (NavGraph, steering, Waypoints), generic replay container, DeterminismCheck via IDeterministicSimulation, GameCamera.
GAME: Ship/weapons/arena/powerups (VR); Player/Enemy/Level/stealth/Trophies/replay content/ZoneSurface.fx/Radial menus (SO); Strings/Resources resx; About/Credits/Background screens; PreviewPainter; game bootstrap; MatchRules; game packets.

## Known seams / hazards
- LineBatch, ParticleManager, BloomComponent take `ScreenManager` today -> inject GraphicsDevice/SpriteBatch/IRenderContext.
- Two `Particle` types in SO (Core.Gameplay vs Core.Effects): rename one.
- SO Actor has waypoint/dwell AI -> move to `WaypointActor : Actor`.
- World projectile wall-hit uses `actors[0]` assumed ship -> ICollisionPolicy keeping exact Touch semantics.
- `internal` types need public/InternalsVisibleTo (also for VectorRumble.Tests).
- BloomComponent uses Path.Combine for content paths -> use '/'.
- ParticleSystem uses static Random -> allow seeded.
- ScreenManager pulls SolidOpsSettings, SolidOpsGame.IsMobile, HintDevice, AllocProbe -> interfaces.
- XACT AudioEngine not on all platforms; consider IAudioService.
- JSON settings under trimming (iOS): source-gen or preserve.
- SolidOps Core.csproj has a dead MonoGame.Xna.Framework.Net reference: drop from engine core.
- Neither repo shares git history; no 3-way merge ancestor.
- .fx compile needs Wine on macOS (MGFXC_WINE_PATH).

## Claims (agent / files / status)
(none yet)

## Findings log
- Step 0: both games Desktop-build clean with the engine referenced. VR tests 354 pass; Engine tests 1; SolidOps.Tests 2.
- Golden runs: `VectorRumble/Tests/VectorRumble.Tests/GoldenWorldTests.cs` and `SolidOps/Tests/SolidOps.Tests/GoldenWorldTests.cs`, baselines in each `Golden/world_collisions.txt` (12 bouncing actors, 600 frames @1/60, snapshot every 10). VR also has wall bounces (box + pillar arena); SO is actor-vs-actor only (Level.Walls is private-set). Re-record only deliberately: `GOLDEN_RECORD=1 dotnet test ...`. Run these after EVERY extraction step.
- Harness trick (VR): ShipManager.ships is null until content loads, so the test sets it to an empty array via reflection.
- SolidOps references engine via `EngineDir` MSBuild property (default ../../VectorRumble/Engine/). No CI change made.
- `dotnet sln add` fails on both .sln files (duplicate project names Desktop/Android both "VectorRumble"/"SolidOps"; pre-existing). Engine and test projects are NOT in either .sln yet; they build via ProjectReference. Fix the .sln by hand later.
- SolidOps.Tests needs InternalsVisibleTo (added in Core/Properties/AssemblyInfo.cs).
- Verify commands: `dotnet build VectorRumble/Desktop/VectorRumble.csproj`; `dotnet test VectorRumble/Tests/VectorRumble.Tests`; `dotnet test VectorRumble/Engine/VectorEngine.Tests`; `dotnet build SolidOps/Platforms/Desktop/SolidOps.csproj`; `dotnet test SolidOps/Tests/SolidOps.Tests`.
- Step 1: engine namespaces are VectorEngine.Simulation / VectorEngine.Collections. Each game exposes them via a global using (VR: Core/EngineUsings.cs; SO: Core/GlobalUsings.cs). CollectCollection<T> no longer takes a World (nobody read it): `new CollectCollection<T>()`. SO's O(n) Collect used. Golden runs unchanged. Engine tests: 8.
- Step 2: engine namespace VectorEngine.Rendering (global using in both games). LineBatch(GraphicsDevice, IRenderContext? = null, maxVertexCount = 8192): VR default, SO passes 4096. SO ScreenManager implements IRenderContext; SO code uses `lineBatch.Context.BaseScreenSize`. VR call sites renamed to SO's API (DrawLine->DrawLineList, DrawPolygon->DrawEmptyPolygon, Draw->DrawExplosions). Game shapes moved to the games: VR `Shapes` (rocket, mine, asteroid), SO `SolidOpsShapes` (rocket, timer circle). Polygons with <2 points are now skipped silently (SO used to throw).
- ParticleSystem.random is now `ParticleSystem.Random` (static, settable, visual only; games also use it for non-sim colour picks). The Particle struct is nested (SO has a separate Core.Effects.Particle class that would clash).
- Bloom: BloomComponent(Game, SpriteBatch? = null, contentFolder = "Effects") with RenderSize (null = back buffer; VR sets 1280x720) and OutputTarget (null = back buffer; VR sets its game target). Preset 6 renamed "Neon" (nothing read the old name). Includes SO's blur-weight cache.
- Shaders: Engine/Content/Engine.mgcb + Effects/{BloomCombine,BloomExtract,GaussianBlur}.fx + Macros.fxh. Each head adds it as a 2nd MonoGameContentReference. Desktop heads verified (xnb built from engine mgcb, games launch). Android/iOS/WebGL/Windows heads edited the same way but NOT built. SO keeps its own Macros.fxh for ZoneSurface.fx.
- Line endings: many files are CRLF. Scripts that rewrite files must preserve them (git diff --stat should show small diffs).
- Smoke run: both Desktop games stay up 8s at the menu, no crash. Gameplay not visually verified.
- Step 3: new engine namespaces VectorEngine.Audio / .Settings / .Localization / .Input (Display is in .Rendering); both games have global usings for them. SO's empty Core.Audio / Core.Inputs namespaces are gone (their usings removed).
- AudioManager is SO's version: no `#if AUDIO` guard (VR defined AUDIO everywhere, so behaviour is unchanged). VoiceSynth stays in SO (VoiceNote/VoiceProfile are game types).
- Settings: storages take the app name: `new DesktopSettingsStorage("SolidOps")`; storage no longer uses a static folder property. VR has no settings storage yet (nothing to wire).
- LocalizationManager.GetSupportedCultures(ResourceManager) now takes the game's resources; SO passes `Core.Localization.Resources.ResourceManager`. The .resx files and Resources.Designer.cs stay in SO unrenamed, so no runtime resource-name change happened (decision 10 still matters for later steps). Accelerometer's 3 error texts are now English literals (were SO resx entries ErrorAccelerometer*, now unused there).
- Display: `BaseWidth/BaseHeight` are now static properties (were consts), default 1280x720, `Display.Configure(w,h)`. VR uses it; SO does NOT yet (SO's ScreenManager still has its own 800x480 BaseScreenSize: step 4).
- Engine tests: 16. VR tests 354. SO golden 2. Both games smoke-run 8s.
