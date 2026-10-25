# Test fixtures

Every file here is **invented** for tests. Nothing is copied from Rain World's install or from the
decompiled reference. The files use the game's formats (and Rain World's public Steam app id,
312520), but the content is made up: the region code `xx`, room names, sprite names and creature
lines don't exist in the game. Never add real game files here. See the IP policy in `docs/VISION.md`.

The folder name ends in `~`, so Unity doesn't import it and it never goes into a build. Tests read it
through `RWDE.Tests.Common.Fixtures`. `FakeGameInstall` copies these files into a throwaway install
layout and generates the binary files (palette PNGs, the atlas texture, WAV sounds) at test time,
so no binaries are committed.

| File | Format |
|---|---|
| `steam/libraryfolders.vdf` | Steam library list (Valve KeyValues). `{STEAM_ROOT}` and `{LIBRARY_ROOT}` are replaced with temp paths. |
| `steam/appmanifest_312520.acf` | Steam app manifest for the game. |
| `world/xx/world_xx.txt` | World file: `ROOMS`, `CREATURES` and `BAT MIGRATION BLOCKAGES` sections, `//` comments, a `(A,B)` campaign filter. |
| `modify/world/xx/world_xx.txt` | Mod `modify` file with a `[MERGE]` ... `[ENDMERGE]` block and an `[ADD]` line. |
| `world/yy/world_yy.txt`, `world/yy/properties.txt` | Spawner region `yy`: swarm rooms, room spawns, lineages, `OFFSCREEN` spawns, `(A,B)` and `(X-A)` campaign filters, an unknown room and a non-roster creature; `batsPerActiveSwarmRoom`. |
| `modify/moreslugcats/world/yy/world_yy.txt`, `modify/watcher/world/yy/world_yy.txt` | The two DLC `modify` files for region `yy`: a `[FINDLINESTART]` removal, campaign-filtered spawns with `{PreCycle}`/`{Night}` data, and a `HIDEROOM` conditional link. |
| `atlases/fixtureAtlas.txt` | Futile atlas data (TexturePacker JSON hash) for a 64x32 texture. |
| `desktop/*.json` | Desktop layouts for the terrain rasteriser: `screen`, `taskbar` (or null) and `icons`, each rectangle `[left, top, right, bottom]` in virtual-screen pixels. |
