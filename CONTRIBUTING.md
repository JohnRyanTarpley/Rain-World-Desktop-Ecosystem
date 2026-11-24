# Contributing

Read [docs/VISION.md](docs/VISION.md) for the goal and [docs/architecture.md](docs/architecture.md)
for the project map. The [README](README.md) covers requirements and building.

## IP policy: never commit Rain World content

Rain World's art, audio, data and code belong to Videocult and Akupara Games. This repository is
MIT-licensed and must contain only original work.

**Never commit:**
- Sprites, atlases, palettes, sounds, music, shaders or any other file from the game's install
  (`RainWorld_Data/`, `StreamingAssets/`, the DLC `mods/` folders).
- World files, room files or other game data, even a single region or a trimmed copy.
- The game's assemblies (`Assembly-CSharp.dll` or any other `.dll` from the install).
- Decompiled game code, whole files or pasted excerpts.
- Files from the per-user asset cache (`%APPDATA%/RainWorldDesktop/cache/`).

**How the app uses game content instead:**
- Everything is loaded at runtime from the user's own Rain World install, found through Steam's
  `libraryfolders.vdf` or a path set in Settings, the same way mods use the game. A Rain World
  install is required; there is no placeholder-art fallback.
- Content that the game packs into its asset files (the Futile atlases in `resources.assets`,
  including Downpour's and The Watcher's shared `rainWorldMSC` and `uiSpritesMSC` when installed) is
  extracted on the user's machine into the per-user cache, never into the repository. Loose files,
  such as the atlases in a DLC's mod folder, are read from the install where they are.

**Porting game behaviour:**
- Read the decompiled game code locally to understand behaviour, then write your own
  implementation. Keep the decompiled reference outside the repository; never copy its files in.
- Port the game's logic and constants faithfully, and cite the source: any constant or behaviour
  ported from the game carries a comment naming the game class and member it comes from, e.g.
  `// LizardBreedParams.tongueAttackRange`.
- Machine-specific paths (your install, your decompiled reference) don't go in the repository.

**Tests use invented data.** Fixtures in `tests/Fixtures/` use the game's file formats with
made-up content (region `xx`, invented room and sprite names). Binary fixtures such as PNGs and
WAVs are generated at test time by `FakeGameInstall`. Never add real game files as fixtures.

**Safety nets.** `.gitignore` excludes `GameData/`, `Decompiled/` and `*.dll`.
`.github/scripts/check-project.mjs` fails CI when a tracked file looks like game content
(assemblies, decompiled code, `GameData/`, `RainWorld_Data/`, `StreamingAssets/`). An original
native library that must be committed is added with `git add -f` and listed in that script's
`allowedBinaries`. These checks catch mistakes; they don't replace the policy.

If game content is committed by accident, don't just delete it in a later commit: it stays in the
history. Tell the maintainers so the history can be rewritten before it is pushed or merged.

## Workflow

- Work on a branch and open a pull request against `main`.
- Stay inside your department's projects (see the ownership table in
  [docs/architecture.md](docs/architecture.md)). A change that needs a new project reference must
  follow the dependency rules there; breaking them is a review blocker.
- Before pushing, run:

  ```powershell
  node .github/scripts/check-project.mjs   # solution membership and IP checks
  dotnet test RWDE.sln                     # build and every test
  ```

## Conventions

- **Tests:** NUnit 3 with classic asserts, in `tests/RWDE.<Name>.Tests/`. New behaviour comes
  with tests.
- **New projects:** add the `.csproj` under `src/` or `tests/`, run `dotnet sln RWDE.sln add <path>`
  (CI fails otherwise), and add a new `src/` project to `ProjectRules.Allowed` in
  `tests/RWDE.Tests.Common/ProjectRules.cs`.
- **Unity stand-ins:** ported code uses the engine-free types in `RWDE.Sim` (`Vector2`, `Mathf`)
  and `RWDE.Graphics` (`Color`), which keep Unity's names and semantics so ports stay close to
  the original.
- **Win32:** P/Invoke lives only in `src/RWDE.Shell/Native/`.
- **Binary files we create ourselves** (`*.png`, `*.wav`, `*.psd` and the others in
  `.gitattributes`) go through Git LFS.
