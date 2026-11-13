# VectorEngine

The shared engine behind VectorRumble and SolidOps, and meant for future vector-style MonoGame games. It lives in this repo for now; it moves to its own repo once it has proven itself in both games. Status and decisions are tracked in [EXTRACTION.md](EXTRACTION.md).

## Projects

| Project | What it is |
|---|---|
| `VectorEngine.Core` | Everything a game needs: game base class, screens and menus, input, rendering, audio, settings, localization, particles, world simulation. Namespaces are `VectorEngine.*`. |
| `VectorEngine.Networking` | Optional LAN layer. Only this project references `MonoGame.Xna.Framework.Net`. A game that doesn't want networking doesn't reference it. |
| `VectorEngine.Tests` | xUnit tests for both. |
| `Content` | The engine's shared effects (bloom, blur), built with `Engine.mgcb`. |

## What's in Core

- **`VectorEngineGame`**: derive from it and pass `VectorEngineGameOptions` (virtual and window size, mobile/desktop orientation, VSync, the audio project name, `ScreenManagerOptions`). It creates the graphics device, screen manager and audio manager.
- **Screens**: `ScreenManager`, `GameScreen`, and the default VectorRumble-style `MenuScreen`, `MenuEntry`, `MessageBoxScreen` and `LoadingScreen`. A game restyles them with a `MenuStyle` (colours, cues, texts). Games with a different menu look write their own screens, as SolidOps does with its radial menu.
- **Input**: `InputState` and `ActionBindings`. The core `GameAction`s are Select, Cancel, Pause, Up, Down, Next, Previous and Click; a game adds its own with `bindings.Define(new ActionId("Send"), ...)`. The saved settings format keeps action names.
- **Rendering**: `Display` (virtual resolution, default 720p; call `Display.Configure` to change it), `LineBatch`, `VectorPolygon`, `BloomComponent`.
- **Simulation**: `WorldBase`, `Actor`, `WorldActor` and `CollisionResult`. A game derives its own `World` and actors. The update order is fixed: actors, collision, particle systems, a hook, then collection.
- **Effects**: `ParticleManager` presets and the older `ParticleSystem`.
- **Settings, localization, audio, diagnostics**: `SettingsManager`, `LocalizationManager`, `AudioManager` (XACT projects), `PerfOverlay`, `AllocProbe`. `Platform` says whether the game is on a mobile or desktop device.

The engine has no player-visible text of its own. Each game supplies its strings and languages.

## Networking

Packet ids 64-69 are reserved by the engine (`EnginePacketIds`: Ping 64, Pong 65). A game's own ids start at 70 (`FirstGameId`). Every packet is `[protocol version][id][body]`; a packet with another version is dropped and counted, and `PacketDispatcher.ProtocolMismatch` fires. Initialize with `MultiplayerSessionService.Initialize("MyGame", protocolVersion: 1)`. The log is `{game}-net.log`.

## Using it from a game

1. Reference `VectorEngine.Core` (and `VectorEngine.Networking` if you want LAN play). VectorRumble references both by project; SolidOps references Core through `$(EngineDir)`.
2. Derive your game from `VectorEngineGame`.
3. Derive your `World` from `WorldBase`, and your actors from `Actor`/`WorldActor`.
4. Pin MonoGame to the same version as the engine (3.8.6.1).

## Build and test

```bash
dotnet build Desktop/VectorRumble.csproj
dotnet test Engine/VectorEngine.Tests
dotnet test Tests/VectorRumble.Tests
```

## Known gaps

- Verified on Desktop only. Android works in principle; iOS, Windows and WebGL are unverified.
- The touch `VirtualGamePad` still lives in VectorRumble and is tied to its ship controls.
- `ParticleSystem` is still used by VectorRumble's menu fireworks; it will be replaced by `ParticleManager` presets.
- SolidOps' waypoint actor stays in SolidOps because it depends on its own behaviour classes.
