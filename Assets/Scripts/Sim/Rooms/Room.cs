using System;
using System.Collections.Generic;

namespace RWDE.Sim
{
    /// <summary>
    /// The desktop as one Rain World room: a grid of 20 px tiles plus the shortcuts (pipes) through it.
    /// Names and coordinate rules follow the game's <c>Room</c> so ported code reads the same:
    /// tile (0, 0) is bottom-left, y points up, and tile (x, y) covers pixels [20x, 20x + 20).
    /// </summary>
    public sealed class Room
    {
        /// <summary>Tile edge length in pixels. The game hardcodes 20f throughout <c>Room</c>.</summary>
        public const float TileSize = 20f;

        /// <summary>One tile. Mirrors the game's <c>Room.Tile</c>.</summary>
        public sealed class Tile
        {
            // Room.Tile.TerrainType, in the game's order (the level files store these as ints).
            public enum TerrainType
            {
                Air,
                Solid,
                Slope,
                Floor,
                ShortcutEntrance
            }

            public TerrainType Terrain;
            public bool verticalBeam;
            public bool horizontalBeam;
            public bool wallbehind;
            /// <summary>0 = no shortcut; 1 = shortcut path (entrances included). The game uses 2-5 for exits and dens.</summary>
            public int shortCut;
            /// <summary>0 = dry, 1 = deep water, 2 = water surface. The desktop has no water, so this stays 0.</summary>
            public int waterInt;
            public int X;
            public int Y;

            public Tile(int x, int y, TerrainType tType, bool vBeam, bool hBeam, bool wbhnd, int sc, int wtr)
            {
                X = x;
                Y = y;
                Terrain = tType;
                verticalBeam = vBeam;
                horizontalBeam = hBeam;
                wallbehind = wbhnd;
                shortCut = sc;
                waterInt = wtr;
            }

            public bool AnyBeam => verticalBeam || horizontalBeam;
            public bool DeepWater => waterInt == 1;
            public bool WaterSurface => waterInt == 2;
            public bool AnyWater => waterInt != 0;
            public bool Solid => IsSolid();

            public bool IsSolid() => Terrain == TerrainType.Solid;

            public override string ToString() => "tile " + X + "," + Y + ": " + Terrain + (shortCut != 0 ? " sc" + shortCut : "");
        }

        private readonly Tile.TerrainType[,] baseTerrain;
        private readonly Dictionary<int, Pipe> pipes = new Dictionary<int, Pipe>();
        private readonly Dictionary<IntVector2, Pipe> pipeEntrances = new Dictionary<IntVector2, Pipe>();
        // How many pipe paths run through each tile; pipes may cross between their entrances.
        private readonly Dictionary<IntVector2, int> pipeCover = new Dictionary<IntVector2, int>();

        /// <summary>An all-air room of <paramref name="width"/> by <paramref name="height"/> tiles.</summary>
        public Room(int width, int height)
        {
            if (width < 1 || height < 1)
                throw new ArgumentOutOfRangeException(nameof(width), "A room needs at least one tile.");
            Width = width;
            Height = height;
            Tiles = new Tile[width, height];
            baseTerrain = new Tile.TerrainType[width, height];
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                    Tiles[x, y] = new Tile(x, y, Tile.TerrainType.Air, false, false, false, 0, 0);
        }

        /// <summary>A room holding <paramref name="source"/>'s current tiles and pipes. Later changes come through <see cref="ApplyTerrainDiffs"/>.</summary>
        public Room(ITerrainSource source) : this(source.Width, source.Height)
        {
            var snapshot = new TerrainDiff();
            for (int x = 0; x < Width; x++)
                for (int y = 0; y < Height; y++)
                {
                    var state = source.GetTile(x, y);
                    if (!state.Equals(TileState.Air)) snapshot.SetTile(x, y, state);
                }
            foreach (var pipe in source.Pipes) snapshot.AddPipe(pipe);
            ApplyTerrainDiff(snapshot);
        }

        public int Width { get; }
        public int Height { get; }
        public int TileWidth => Width;
        public int TileHeight => Height;
        public float PixelWidth => Width * TileSize;
        public float PixelHeight => Height * TileSize;
        public FloatRect RoomRect => new FloatRect(0f, 0f, PixelWidth, PixelHeight);

        /// <summary>Indexed [x, y]. Use <see cref="GetTile(int, int)"/> for positions that may be outside the room.</summary>
        public readonly Tile[,] Tiles;

        /// <summary>When set, <see cref="GetTile(int, int)"/> returns this for every position outside the room.</summary>
        public Tile DefaultTile;

        /// <summary>One entry per shortcut entrance, ordered as the game's <c>ShortcutMapper</c> scans: top row first, left to right.</summary>
        public ShortcutData[] shortcuts = new ShortcutData[0];

        /// <summary>The entrance tile of each entry in <see cref="shortcuts"/>, at the same index.</summary>
        public IntVector2[] shortcutsIndex = new IntVector2[0];

        public IEnumerable<Pipe> Pipes => pipes.Values;

        public bool TryGetPipe(int id, out Pipe pipe) => pipes.TryGetValue(id, out pipe);

        // ---- Room <-> tile coordinates (Room.GetTilePosition, MiddleOfTile, TileRect) ----

        public IntVector2 GetTilePosition(Vector2 pos) => StaticGetTilePosition(pos);

        /// <summary>
        /// The tile containing pixel <paramref name="pos"/>. Kept exactly as the game computes it: the
        /// +20/-1 floors positions in [-20, 0) to tile -1, but further out the cast truncates again,
        /// so all of [-40, 0) maps to -1.
        /// </summary>
        public static IntVector2 StaticGetTilePosition(Vector2 pos) =>
            new IntVector2((int)((pos.x + 20f) / 20f) - 1, (int)((pos.y + 20f) / 20f) - 1);

        public Vector2 MiddleOfTile(int x, int y) => new Vector2(10f + x * 20f, 10f + y * 20f);
        public Vector2 MiddleOfTile(IntVector2 pos) => MiddleOfTile(pos.x, pos.y);
        public Vector2 MiddleOfTile(Vector2 pos) => MiddleOfTile(GetTilePosition(pos));

        public FloatRect TileRect(IntVector2 pos) =>
            FloatRect.MakeFromVector2(MiddleOfTile(pos) - new Vector2(10f, 10f), MiddleOfTile(pos) + new Vector2(10f, 10f));

        // The game's spelling.
        public bool IsPositionInsideBoundries(IntVector2 pos) =>
            pos.x >= 0 && pos.x < TileWidth && pos.y >= 0 && pos.y < TileHeight;

        // ---- Tile access (Room.GetTile) ----

        public Tile GetTile(Vector2 pos) => GetTile(GetTilePosition(pos));
        public Tile GetTile(IntVector2 pos) => GetTile(pos.x, pos.y);

        /// <summary>
        /// The tile at (<paramref name="x"/>, <paramref name="y"/>). Outside the room this is
        /// <see cref="DefaultTile"/> if set, otherwise the nearest edge tile extended outwards, with
        /// shortcuts stripped (an entrance reads as solid), as in the game.
        /// </summary>
        public Tile GetTile(int x, int y)
        {
            if (x < Width && y < Height && x > -1 && y > -1)
                return Tiles[x, y];
            if (DefaultTile != null)
                return DefaultTile;
            var edge = Tiles[Clamp(x, 0, Width - 1), Clamp(y, 0, Height - 1)];
            if (edge.Terrain != Tile.TerrainType.ShortcutEntrance && edge.shortCut == 0)
                return edge;
            return new Tile(edge.X, edge.Y,
                edge.Terrain == Tile.TerrainType.ShortcutEntrance ? Tile.TerrainType.Solid : edge.Terrain,
                edge.verticalBeam, edge.horizontalBeam, edge.wallbehind, 0, edge.waterInt);
        }

        // ---- Shortcuts (Room.shortcutData, Room.ShorcutEntranceHoleDirection) ----

        /// <summary>
        /// The shortcut starting at entrance <paramref name="pos"/>, or a DeadEnd when there is none.
        /// Unlike the game, tile (0, 0) can hold an entrance: a desktop pipe may end in the corner.
        /// </summary>
        public ShortcutData shortcutData(IntVector2 pos)
        {
            int i = Array.IndexOf(shortcutsIndex, pos);
            return i < 0 ? ShortcutData.DeadEnd(pos) : shortcuts[i];
        }

        public ShortcutData shortcutData(Vector2 pos) => shortcutData(GetTilePosition(pos));

        private static readonly IntVector2[] fourDirections =
        {
            new IntVector2(-1, 0), new IntVector2(0, -1), new IntVector2(1, 0), new IntVector2(0, 1)
        };

        /// <summary>
        /// The direction from an entrance to the first non-solid neighbour (left, down, right, up, in
        /// the game's <c>Custom.fourDirections</c> order), i.e. the way a creature comes out. Zero when
        /// every neighbour is solid. Keeps the game's spelling.
        /// </summary>
        public IntVector2 ShorcutEntranceHoleDirection(IntVector2 pos)
        {
            foreach (var d in fourDirections)
                if (GetTile(pos.x + d.x, pos.y + d.y).Terrain != Tile.TerrainType.Solid)
                    return d;
            return new IntVector2(0, 0);
        }

        // ---- Terrain diffs, applied between ticks ----

        /// <summary>Takes every pending diff from <paramref name="source"/> and applies them in order. Call at the start of a tick.</summary>
        public TerrainChanges ApplyTerrainDiffs(ITerrainSource source)
        {
            var changes = new TerrainChanges();
            var before = new Dictionary<IntVector2, TileSnapshot>();
            while (source.TryTakeDiff(out var diff))
                Apply(diff, changes, before);
            CollectChangedTiles(changes, before);
            return changes;
        }

        /// <summary>
        /// Applies one diff. Throws <see cref="ArgumentException"/>, leaving the room untouched, when
        /// the diff puts a tile or pipe outside the room or lets two pipes share an entrance.
        /// </summary>
        public TerrainChanges ApplyTerrainDiff(TerrainDiff diff)
        {
            var changes = new TerrainChanges();
            var before = new Dictionary<IntVector2, TileSnapshot>();
            Apply(diff, changes, before);
            CollectChangedTiles(changes, before);
            return changes;
        }

        private readonly struct TileSnapshot
        {
            public TileSnapshot(Tile t)
            {
                Terrain = t.Terrain;
                VerticalBeam = t.verticalBeam;
                HorizontalBeam = t.horizontalBeam;
                Wallbehind = t.wallbehind;
                ShortCut = t.shortCut;
            }

            public readonly Tile.TerrainType Terrain;
            public readonly bool VerticalBeam, HorizontalBeam, Wallbehind;
            public readonly int ShortCut;

            public bool Same(Tile t) =>
                Terrain == t.Terrain && VerticalBeam == t.verticalBeam && HorizontalBeam == t.horizontalBeam
                && Wallbehind == t.wallbehind && ShortCut == t.shortCut;
        }

        private void Apply(TerrainDiff diff, TerrainChanges changes, Dictionary<IntVector2, TileSnapshot> before)
        {
            if (diff == null) throw new ArgumentNullException(nameof(diff));
            Validate(diff);

            foreach (var change in diff.Tiles)
            {
                var pos = change.Pos;
                Touch(pos, before);
                var tile = Tiles[pos.x, pos.y];
                baseTerrain[pos.x, pos.y] = change.State.Terrain;
                tile.verticalBeam = change.State.VerticalBeam;
                tile.horizontalBeam = change.State.HorizontalBeam;
                tile.wallbehind = change.State.Wallbehind;
                RefreshOverlay(pos);
            }

            bool pipesChanged = false;
            foreach (var id in diff.RemovedPipes)
            {
                if (pipes.TryGetValue(id, out var old))
                {
                    RemovePipe(old, before);
                    changes.removedPipes.Add(old);
                    pipesChanged = true;
                }
            }
            foreach (var pipe in diff.AddedPipes)
            {
                if (pipes.TryGetValue(pipe.Id, out var old))
                {
                    RemovePipe(old, before);
                    changes.removedPipes.Add(old);
                }
                AddPipe(pipe, before);
                changes.addedPipes.Add(pipe);
                pipesChanged = true;
            }

            if (pipesChanged) RebuildShortcuts();
        }

        // Checks the whole diff against the room before anything changes.
        private void Validate(TerrainDiff diff)
        {
            foreach (var change in diff.Tiles)
                if (!IsPositionInsideBoundries(change.Pos))
                    throw new ArgumentException("Tile change outside the room: " + change.Pos + ".");

            var after = new Dictionary<int, Pipe>(pipes);
            foreach (var id in diff.RemovedPipes) after.Remove(id);
            foreach (var pipe in diff.AddedPipes)
            {
                if (pipe == null) throw new ArgumentException("Null pipe in terrain diff.");
                foreach (var p in pipe.Path)
                    if (!IsPositionInsideBoundries(p))
                        throw new ArgumentException(pipe + " leaves the room at " + p + ".");
                after[pipe.Id] = pipe;
            }

            // An entrance may not lie anywhere on another pipe, or the game's one-shortcut-per-entrance rule breaks.
            foreach (var pipe in after.Values)
                foreach (var end in new[] { pipe.A, pipe.B })
                    foreach (var other in after.Values)
                        if (other.Id != pipe.Id && Contains(other.Path, end))
                            throw new ArgumentException(pipe + " has an entrance at " + end + ", which is on " + other + ".");
        }

        private static bool Contains(IReadOnlyList<IntVector2> path, IntVector2 pos)
        {
            for (int i = 0; i < path.Count; i++)
                if (path[i] == pos) return true;
            return false;
        }

        private void AddPipe(Pipe pipe, Dictionary<IntVector2, TileSnapshot> before)
        {
            pipes[pipe.Id] = pipe;
            pipeEntrances[pipe.A] = pipe;
            pipeEntrances[pipe.B] = pipe;
            foreach (var p in pipe.Path)
            {
                Touch(p, before);
                pipeCover.TryGetValue(p, out int n);
                pipeCover[p] = n + 1;
                RefreshOverlay(p);
            }
        }

        private void RemovePipe(Pipe pipe, Dictionary<IntVector2, TileSnapshot> before)
        {
            pipes.Remove(pipe.Id);
            pipeEntrances.Remove(pipe.A);
            pipeEntrances.Remove(pipe.B);
            foreach (var p in pipe.Path)
            {
                Touch(p, before);
                int n = pipeCover[p] - 1;
                if (n > 0) pipeCover[p] = n;
                else pipeCover.Remove(p);
                RefreshOverlay(p);
            }
        }

        // A tile's terrain is the source's, except that pipe entrances read as ShortcutEntrance;
        // every tile on a pipe path, entrances included, has shortCut 1 (the level files' flag 3).
        private void RefreshOverlay(IntVector2 pos)
        {
            var tile = Tiles[pos.x, pos.y];
            tile.Terrain = pipeEntrances.ContainsKey(pos) ? Tile.TerrainType.ShortcutEntrance : baseTerrain[pos.x, pos.y];
            tile.shortCut = pipeCover.ContainsKey(pos) ? 1 : 0;
        }

        private void RebuildShortcuts()
        {
            var entrances = new List<IntVector2>(pipeEntrances.Keys);
            entrances.Sort((a, b) => a.y != b.y ? b.y.CompareTo(a.y) : a.x.CompareTo(b.x));
            shortcuts = new ShortcutData[entrances.Count];
            shortcutsIndex = new IntVector2[entrances.Count];
            for (int i = 0; i < entrances.Count; i++)
            {
                var start = entrances[i];
                var pipe = pipeEntrances[start];
                shortcuts[i] = new ShortcutData(ShortcutData.Type.Normal, start, pipe.OtherEnd(start), -1, pipe.PathFrom(start), pipe.Id);
                shortcutsIndex[i] = start;
            }
        }

        private void Touch(IntVector2 pos, Dictionary<IntVector2, TileSnapshot> before)
        {
            if (!before.ContainsKey(pos)) before[pos] = new TileSnapshot(Tiles[pos.x, pos.y]);
        }

        private void CollectChangedTiles(TerrainChanges changes, Dictionary<IntVector2, TileSnapshot> before)
        {
            foreach (var entry in before)
                if (!entry.Value.Same(Tiles[entry.Key.x, entry.Key.y]))
                    changes.changedTiles.Add(entry.Key);
        }

        private static int Clamp(int v, int min, int max) => v < min ? min : v > max ? max : v;
    }
}
