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

        internal RasterizedDesktop(RoomCoordinates coordinates, TileState[,] tiles, List<EdgeOpening> openings)
        {
            Coordinates = coordinates;
            this.tiles = tiles;
            Openings = openings;
        }

        public RoomCoordinates Coordinates { get; }
        public int Width => Coordinates.Width;
        public int Height => Coordinates.Height;

        public TileState this[int x, int y] => tiles[x, y];

        /// <summary>Open runs along the edges, ordered left, right, bottom, top, each from low to high.</summary>
        public IReadOnlyList<EdgeOpening> Openings { get; }
    }

    /// <summary>
    /// Turns a <see cref="DesktopLayout"/> into the sim's 20 px tile grid:
    /// <list type="bullet">
    /// <item>each desktop icon becomes a block of Solid tiles;</item>
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

            for (int x = 0; x < coords.Width; x++)
                tiles[x, 0] = TileState.Solid;

            if (layout.Taskbar.HasValue)
                Fill(tiles, coords, layout.Taskbar.Value);
            foreach (var icon in layout.Icons)
                Fill(tiles, coords, icon);

            return new RasterizedDesktop(coords, tiles, FindOpenings(tiles, coords.Width, coords.Height));
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
