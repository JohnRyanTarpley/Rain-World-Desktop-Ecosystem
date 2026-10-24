using System.Collections.Generic;

namespace RWDE.Sim
{
    /// <summary>
    /// The sim's only view of desktop terrain, implemented by desktop-shell. Coordinates are room
    /// tiles as in the game: (0, 0) is the bottom-left tile, y points up, a tile is 20 px.
    /// </summary>
    /// <remarks>
    /// A <see cref="Room"/> reads the snapshot (<see cref="GetTile"/> and <see cref="Pipes"/>) once
    /// when it is built. After that it only takes diffs, at the start of each tick. Every change
    /// made after the snapshot must therefore arrive as a diff, in the order it happened.
    /// </remarks>
    public interface ITerrainSource
    {
        /// <summary>Room width in tiles.</summary>
        int Width { get; }

        /// <summary>Room height in tiles.</summary>
        int Height { get; }

        /// <summary>The current state of tile (<paramref name="x"/>, <paramref name="y"/>).</summary>
        TileState GetTile(int x, int y);

        /// <summary>The pipes that exist now.</summary>
        IEnumerable<Pipe> Pipes { get; }

        /// <summary>
        /// Hands over the oldest change not yet taken, or returns false when there is none. Called
        /// from the sim thread between ticks only.
        /// </summary>
        bool TryTakeDiff(out TerrainDiff diff);
    }
}
