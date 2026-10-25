using System;
using System.Collections.Generic;
using RWDE.Shell.Desktop;
using RWDE.Sim;

namespace RWDE.Shell.Terrain
{
    /// <summary>The tiles of a rasterised desktop, indexed [x, y] with y up, plus its edge openings.</summary>
    public sealed class RasterizedDesktop
    {
        private readonly TileState[,] tiles;

        internal RasterizedDesktop(RoomCoordinates coordinates, TileState[,] tiles, List<EdgeOpening> openings, List<TunnelTiles> tunnels)
        {
            Coordinates = coordinates;
            this.tiles = tiles;
            Openings = openings;
            Tunnels = tunnels;
        }

        public RoomCoordinates Coordinates { get; }
        public int Width => Coordinates.Width;
        public int Height => Coordinates.Height;

        public TileState this[int x, int y] => tiles[x, y];

        /// <summary>Open runs along the edges, ordered left, right, bottom, top, each from low to high.</summary>
        public IReadOnlyList<EdgeOpening> Openings { get; }

        /// <summary>The icon tunnels carved into the tiles, ordered left to right, then top to bottom.</summary>
        public IReadOnlyList<TunnelTiles> Tunnels { get; }
    }

    /// <summary>A vertical one-tile crawl passage: Air tiles in column <see cref="X"/> from <see cref="Bottom"/> to <see cref="Top"/>, inclusive, between Solid walls.</summary>
    public readonly struct TunnelTiles : IEquatable<TunnelTiles>
    {
        public TunnelTiles(int x, int bottom, int top)
        {
            X = x;
            Bottom = bottom;
            Top = top;
        }

        public int X { get; }
        public int Bottom { get; }
        public int Top { get; }

        public bool Equals(TunnelTiles other) => X == other.X && Bottom == other.Bottom && Top == other.Top;
        public override bool Equals(object obj) => obj is TunnelTiles other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Bottom, Top);
        public override string ToString() => "x " + X + ", y " + Bottom + ".." + Top;
    }

    /// <summary>
    /// Turns a <see cref="DesktopLayout"/> into the sim's 20 px tile grid:
    /// <list type="bullet">
    /// <item>each desktop icon becomes a block of Solid tiles;</item>
    /// <item>two icon columns side by side get a one-tile tunnel down the line between them (see
    /// <see cref="IconTunnelDetector"/>) that never cuts the floor or the taskbar, while a single
    /// column stays a solid pillar;</item>
    /// <item>the taskbar becomes Solid tiles, so its top is the floor creatures walk on;</item>
    /// <item>the bottom tile row is always Solid, so the bottom of the screen is ground and never a
    /// drop out of the room, even when the taskbar is on another edge;</item>
    /// <item>everything else is Air, which leaves the left and right edges open as exits and the top
    /// edge open as sky wherever no icon or taskbar touches them.</item>
    /// </list>
    /// The display mode plays no part: the taskbar is the same terrain in desktop and topmost mode.
    /// </summary>
    public static class TerrainRasterizer
    {
        public static RasterizedDesktop Rasterize(DesktopLayout layout)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            var coords = new RoomCoordinates(layout.Screen);
            var tiles = new TileState[coords.Width, coords.Height];

            // The floor row and the taskbar are ground that tunnels never cut through.
            var ground = new bool[coords.Width, coords.Height];
            for (int x = 0; x < coords.Width; x++)
                ground[x, 0] = true;
            if (layout.Taskbar.HasValue && coords.TryGetTiles(layout.Taskbar.Value, out var barMin, out var barMax))
                for (int x = barMin.x; x <= barMax.x; x++)
                    for (int y = barMin.y; y <= barMax.y; y++)
                        ground[x, y] = true;

            for (int x = 0; x < coords.Width; x++)
                for (int y = 0; y < coords.Height; y++)
                    if (ground[x, y])
                        tiles[x, y] = TileState.Solid;
            foreach (var icon in layout.Icons)
                Fill(tiles, coords, icon);

            var tunnels = Carve(tiles, ground, coords, IconTunnelDetector.Detect(layout.Icons));

            return new RasterizedDesktop(coords, tiles, FindOpenings(tiles, coords.Width, coords.Height), tunnels);
        }

        /// <summary>
        /// Clears the tile column under each tunnel's seam to Air for the height of its icons and makes the
        /// columns either side Solid, so it is the game's one-tile crawl passage. All tunnels are worked out
        /// in tile space before any is carved:
        /// <list type="bullet">
        /// <item>a tunnel whose icons reach the floor or the taskbar stops short of them; one crossing
        /// them stays solid;</item>
        /// <item>a tunnel in the first or last tile column, with no room for a wall on its outer side,
        /// stays solid rather than opening onto the screen edge;</item>
        /// <item>tunnels in the same column that touch become one;</item>
        /// <item>a wall is never built on another tunnel's tiles.</item>
        /// </list>
        /// A tunnel closed at one end or both is still carved. A dead end is an ordinary crawl passage in
        /// the game, and one nothing can reach is an air pocket no creature can get to, the same as solid
        /// terrain to every creature; game rooms have those too.
        /// </summary>
        private static List<TunnelTiles> Carve(TileState[,] tiles, bool[,] ground, RoomCoordinates coords, List<IconTunnel> detected)
        {
            int width = coords.Width, height = coords.Height;

            var spans = new List<TunnelTiles>();
            foreach (var tunnel in detected)
            {
                var seam = new ScreenRect(tunnel.SeamX, tunnel.Top, tunnel.SeamX + 1, tunnel.Bottom);
                if (!coords.TryGetTiles(seam, out var min, out var max)) continue;
                int x = min.x, bottom = min.y, top = max.y;
                if (x == 0 || x == width - 1) continue;
                while (bottom <= top && ground[x, bottom]) bottom++;
                while (top >= bottom && ground[x, top]) top--;
                bool crossesGround = false;
                for (int y = bottom; y <= top; y++)
                    crossesGround |= ground[x, y];
                if (bottom <= top && !crossesGround)
                    spans.Add(new TunnelTiles(x, bottom, top));
            }

            // Same column and touching or overlapping: one tunnel.
            spans.Sort((a, b) => a.X != b.X ? a.X.CompareTo(b.X) : a.Bottom.CompareTo(b.Bottom));
            var merged = new List<TunnelTiles>();
            foreach (var span in spans)
            {
                int last = merged.Count - 1;
                if (last >= 0 && merged[last].X == span.X && span.Bottom <= merged[last].Top + 1)
                    merged[last] = new TunnelTiles(span.X, merged[last].Bottom, Math.Max(merged[last].Top, span.Top));
                else
                    merged.Add(span);
            }

            var passage = new bool[width, height];
            foreach (var span in merged)
                for (int y = span.Bottom; y <= span.Top; y++)
                    passage[span.X, y] = true;
            foreach (var span in merged)
                for (int y = span.Bottom; y <= span.Top; y++)
                {
                    tiles[span.X, y] = TileState.Air;
                    foreach (int x in new[] { span.X - 1, span.X + 1 })
                        if (!ground[x, y] && !passage[x, y])
                            tiles[x, y] = TileState.Solid;
                }

            merged.Sort((a, b) => a.X != b.X ? a.X.CompareTo(b.X) : b.Top.CompareTo(a.Top));
            return merged;
        }

        private static void Fill(TileState[,] tiles, RoomCoordinates coords, ScreenRect rect)
        {
            if (!coords.TryGetTiles(rect, out var min, out var max)) return;
            for (int x = min.x; x <= max.x; x++)
                for (int y = min.y; y <= max.y; y++)
                    tiles[x, y] = TileState.Solid;
        }

        private static List<EdgeOpening> FindOpenings(TileState[,] tiles, int width, int height)
        {
            var openings = new List<EdgeOpening>();
            AddRuns(openings, RoomEdge.Left, height, i => tiles[0, i]);
            AddRuns(openings, RoomEdge.Right, height, i => tiles[width - 1, i]);
            AddRuns(openings, RoomEdge.Bottom, width, i => tiles[i, 0]);
            AddRuns(openings, RoomEdge.Top, width, i => tiles[i, height - 1]);
            return openings;
        }

        private static void AddRuns(List<EdgeOpening> into, RoomEdge edge, int count, Func<int, TileState> tile)
        {
            int start = -1;
            for (int i = 0; i <= count; i++)
            {
                bool open = i < count && tile(i).Terrain != Room.Tile.TerrainType.Solid;
                if (open && start < 0) start = i;
                else if (!open && start >= 0)
                {
                    into.Add(new EdgeOpening(edge, start, i - 1));
                    start = -1;
                }
            }
        }
    }
}
