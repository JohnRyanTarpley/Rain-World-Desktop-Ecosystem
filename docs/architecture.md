# Architecture

How the code is split into projects, which department owns each one, and which projects may
depend on which. Read [VISION.md](VISION.md) first for what the app does and why.

## Overview

`rwde.exe` is a plain .NET 8 Windows app with no game engine. It ports Rain World's simulation
(rooms, physics, creature AI, spawning) into an engine-free library, turns the Windows desktop
into a Rain World room, and draws the game's own sprites with Direct3D 11 into a borderless
overlay window through DirectComposition. Every game asset is read at runtime from the user's
own Rain World install; see [CONTRIBUTING.md](../CONTRIBUTING.md) for the IP policy.

```
RWDE.App (rwde.exe)  ──►  every project below
RWDE.Graphics, RWDE.Audio        ──►  RWDE.Sim, RWDE.GameAssets
RWDE.Interaction, RWDE.UI        ──►  RWDE.Sim, RWDE.Shell
RWDE.Shell                       ──►  RWDE.Sim
RWDE.Sim, RWDE.GameAssets        ──►  nothing
```

Each arrow is an allowed project reference; see [Dependency rules](#dependency-rules).

## Departments

Work is split into departments. Each owns a set of projects and the code inside them.

| Department | Owns | Responsibility |
|---|---|---|
| **ecosystem-sim** | `src/RWDE.Sim/`, `tests/RWDE.Sim.Tests/`, `src/RWDE.App/OutskirtsSpawnData.cs` | Rooms and tiles, body-chunk physics, the abstract world and spawning, creatures and their AI, the rain cycle. |
| **desktop-shell** | `src/RWDE.Shell/`, `src/RWDE.App/`, `tests/RWDE.Shell.Tests/`, `tests/RWDE.App.Tests/` | The overlay window, Win32 interop, desktop scanning into terrain, lifecycle (quit, stop), and the app's composition root and fixed 40 Hz tick. |
| **rendering-animation** | `src/RWDE.Graphics/`, `src/RWDE.Audio/`, their test projects | The Futile-style sprite layer, per-creature graphics modules, palettes, shaders, the Direct3D 11 renderer, and creature sound. |
| **interaction-ui** | `src/RWDE.Interaction/`, `src/RWDE.UI/`, their test projects | Cursor hit-testing, grab/drag/throw, food placement, the settings window and the `Settings` object. |
| **build-release** | `src/RWDE.GameAssets/`, `tests/RWDE.GameAssets.Tests/`, `tests/RWDE.Tests.Common/`, `tests/Fixtures/`, the solution and build files, CI, packaging, docs | Finding the Rain World install and reading its assets, the shared test setup, the dependency rules, CI, releases and the IP policy. |
| **code-review** | Nothing | Read-only review sweeps (game fidelity, vision conformance, robustness, IP). It reports findings to the owning department and never changes code. |

## Projects

| Project | Target | Department | What it holds |
|---|---|---|---|
| `RWDE.Sim` | `net8.0` | ecosystem-sim | The ported game simulation. `Math/` has engine-free stand-ins for Unity types (`Vector2`, `Mathf`, ...) that keep Unity's names and semantics. `Rooms/` has `Room`, tiles, pipes and `ITerrainSource`. `Physics/` has `BodyChunk` and `PhysicalObject`. `AI/` has `AImap`, the path finders and the AI modules. `World/` has the abstract world, world-file loading and spawning. `Creatures/` has creature templates and one folder per species. |
| `RWDE.GameAssets` | `net8.0` | build-release | `Install/` finds the Rain World install (Steam `libraryfolders.vdf` or a Settings path) and its installed DLC. `Cache/` extracts the packed base-game atlases into the per-user cache and decodes PNGs. `Unity/` reads Unity's asset file format (a file reader, not a Unity dependency). `Merge/` applies mod `modify` files to world files. `GameAssetLibrary` is the read-only API: `GetAtlas`, `GetPalette`, `GetSound`, `GetWorldFile`, `InstalledDlc`. |
| `RWDE.Shell` | `net8.0-windows` | desktop-shell | `Overlay/` is the frameless, never-focused overlay window and its desktop/topmost modes. `Desktop/` scans the desktop icons. `Terrain/` rasterises icons, the taskbar and screen edges into the sim's tile grid (implements `ITerrainSource`). `Lifecycle/` handles single instance, hold-Escape quit and `--stop`. `Native/` holds all Win32 P/Invoke. |
| `RWDE.Graphics` | `net8.0-windows` | rendering-animation | `Futile/` is a small port of the Futile sprite layer (`FSprite`, `TriangleMesh`, `FContainer`, `FAtlas`). `Drawing/` has `GraphicsModule`, `RoomCamera` and room palettes. `Direct3D/` is the renderer (Vortice packages). |
| `RWDE.Audio` | `net8.0-windows` | rendering-animation | Creature sound playback. |
| `RWDE.Interaction` | `net8.0-windows` | interaction-ui | Cursor input turned into sim commands. |
| `RWDE.UI` | `net8.0-windows` | interaction-ui | `Configuration/` has the serialisable `Settings` and `SettingsStore` (JSON under `%APPDATA%/RainWorldDesktop/`); the WinForms settings window. |
| `RWDE.App` | `net8.0-windows`, `WinExe` | desktop-shell | `rwde.exe`: boots every system, requires a Rain World install, and drives the fixed 40 Hz sim tick. |

### Tests

Every `src/RWDE.<Name>` project has a `tests/RWDE.<Name>.Tests` NUnit 3 project.
`tests/Directory.Build.props` adds NUnit and a reference to `RWDE.Tests.Common` to each of them.

| Path | What |
|---|---|
| `tests/RWDE.Tests.Common/` | Shared helpers: `ProjectRules` (the dependency rules), `FakeGameInstall` (a throwaway install layout built from fixtures at test time), `TempDirectory`, `TestPaths`, writers for game file formats. |
| `tests/RWDE.Tests.Common.Tests/` | Tests for those helpers. |
| `tests/Fixtures/` | Invented files in Rain World's formats. See its [README](../tests/Fixtures/README.md). |

## Dependency rules

Project references enforce department boundaries. A project may reference only these:

| Project | May reference |
|---|---|
| `RWDE.Sim` | nothing |
| `RWDE.GameAssets` | nothing |
| `RWDE.Shell` | Sim |
| `RWDE.Graphics` | Sim, GameAssets |
| `RWDE.Audio` | Sim, GameAssets |
| `RWDE.Interaction` | Sim, Shell |
| `RWDE.UI` | Sim, Shell |
| `RWDE.App` | everything |

`RWDE.Sim` also must target plain `net8.0` and use no NuGet packages, so ported game code never
depends on Windows or a renderer. The sim defines interfaces such as `ITerrainSource` and
`ICreatureRealizer`, and the other projects implement them.

`tests/RWDE.Tests.Common/ProjectRules.cs` reads every `.csproj` under `src/` and checks these
rules. Each test project's `*BoundaryTests` runs the check for its project, so a broken rule or a
reference cycle fails `dotnet test`. A new department project must be added to
`ProjectRules.Allowed`.

## How the pieces fit at runtime

This is the target design. The app is still being built: today `RWDE.App` finds the install,
opens the overlay and runs a test scene (`DesktopProbe`) instead of the full loop.

1. **Start.** `RWDE.App` loads `Settings`, then asks `RWDE.GameAssets` for the Rain World
   install. With no install it shows a message saying where it looked and exits.
2. **Assets.** On first run, `RWDE.GameAssets` extracts the packed base-game atlases into
   `%APPDATA%/RainWorldDesktop/cache/`. Loose files (palettes, sounds, world files, DLC atlases)
   are read from the install directly.
3. **Terrain.** `RWDE.Shell` scans the desktop (icons, taskbar, monitors) and turns it into the
   game's 20 px tile grid. It rescans about once a second and sends a `TerrainDiff` to the sim.
4. **Simulation.** `RWDE.Sim` loads the Outskirts world file and spawns creatures from the
   game's own spawn data, then ticks rooms, physics and AI at 40 Hz.
5. **Drawing.** `RWDE.Graphics` reads creature state (never writes it) and draws each creature's
   graphics module with the game's sprites and palettes, interpolating between sim ticks.
6. **Input and settings.** `RWDE.Interaction` turns cursor input into sim commands, and
   `RWDE.UI` edits `Settings`, which the other departments read.

## Build output

`Directory.Build.props` sends every project's `bin/` and `obj/` to `/artifacts`. CI
(`.github/workflows/ci.yml`) runs `.github/scripts/check-project.mjs`, then `dotnet test` on
`windows-latest`, then publishes a self-contained single-file `rwde.exe` as a build artifact.
