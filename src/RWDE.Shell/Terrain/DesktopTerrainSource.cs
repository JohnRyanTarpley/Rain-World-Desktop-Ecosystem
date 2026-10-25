using System;
using System.Collections.Generic;
using RWDE.Sim;

namespace RWDE.Shell.Terrain
{
    /// <summary>
    /// The desktop as the sim's <see cref="ITerrainSource"/>: a rasterised <see cref="DesktopLayout"/>
    /// in room tiles (y up, 20 px). For now it is a fixed snapshot with no pipes and no diffs;
    /// tunnels, monitor pipes and live rescans add to it later.
    /// </summary>
    public sealed class DesktopTerrainSource : ITerrainSource
    {
        private static readonly Pipe[] NoPipes = new Pipe[0];

        public DesktopTerrainSource(DesktopLayout layout)
        {
            Layout = layout ?? throw new ArgumentNullException(nameof(layout));
            Terrain = TerrainRasterizer.Rasterize(layout);
        }

        /// <summary>Reads the desktop once from <paramref name="source"/> and rasterises it.</summary>
        public DesktopTerrainSource(IDesktopLayoutSource source)
            : this((source ?? throw new ArgumentNullException(nameof(source))).Read()) { }

        public DesktopLayout Layout { get; }
        public RasterizedDesktop Terrain { get; }
        public RoomCoordinates Coordinates => Terrain.Coordinates;

        /// <summary>Exits to the offscreen world (left and right edges) and the sky (top edge).</summary>
        public IReadOnlyList<EdgeOpening> Openings => Terrain.Openings;

        public int Width => Terrain.Width;
        public int Height => Terrain.Height;

        public TileState GetTile(int x, int y) => Terrain[x, y];

        public IEnumerable<Pipe> Pipes => NoPipes;

        public bool TryTakeDiff(out TerrainDiff diff)
        {
            diff = null;
            return false;
        }
    }
}
