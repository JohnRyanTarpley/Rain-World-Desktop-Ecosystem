# Rain-World-Desktop-Ecosystem
A "virtual pet" like environment that brings the vibrant ecosystem of Rain World to your desktop!

See [docs/VISION.md](docs/VISION.md) for the project vision.

## Requirements
- Windows 10 or 11 (64-bit)
- An installed copy of Rain World. The app loads the game's own sprites, palettes and sounds
  from your install at runtime; nothing from the game is in this repo.

## Build and run
Everything installs from a terminal:

```powershell
winget install Microsoft.DotNet.SDK.8
dotnet test RWDE.sln                      # build and run the tests
dotnet run --project src/RWDE.App         # start it in the background
```

Stop it by holding Escape for 8 seconds, or from a terminal:

```powershell
dotnet run --project src/RWDE.App -- --stop
```

Ctrl+Alt+Shift+R switches between drawing on the desktop layer (behind your windows, the
default) and on top of every window. The log of the latest run is at
`%LOCALAPPDATA%\RainWorldDesktop\rwde.log`.

## Layout
| Folder | What |
|---|---|
| `src/RWDE.Sim` | The ported game simulation. Plain `net8.0`, no packages. |
| `src/RWDE.GameAssets` | Finds the Rain World install and reads its assets. |
| `src/RWDE.Shell` | The overlay window, desktop scanning, Win32. |
| `src/RWDE.Graphics` | Futile-style sprite layer and the Direct3D 11 renderer. |
| `src/RWDE.Audio`, `RWDE.Interaction`, `RWDE.UI` | Not started yet. |
| `src/RWDE.App` | `rwde.exe`, the composition root. |
| `tests/` | NUnit tests per project, plus invented fixtures in Rain World's formats. |
