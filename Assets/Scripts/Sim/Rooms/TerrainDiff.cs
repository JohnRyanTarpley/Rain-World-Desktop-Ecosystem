using System.Collections.Generic;

namespace RWDE.Sim
{
    /// <summary>
    /// A batch of terrain changes from the terrain source. The room applies them in this order:
    /// tile changes, then pipe removals, then pipe additions. Adding a pipe whose id already exists
    /// replaces that pipe.
    /// </summary>
    public sealed class TerrainDiff
    {
        public readonly struct TileChange
        {
            public TileChange(IntVector2 pos, TileState state)
            {
                Pos = pos;
                State = state;
            }

            public IntVector2 Pos { get; }
            public TileState State { get; }
        }

        private readonly List<TileChange> tiles = new List<TileChange>();
        private readonly List<int> removedPipes = new List<int>();
        private readonly List<Pipe> addedPipes = new List<Pipe>();

        public IReadOnlyList<TileChange> Tiles => tiles;
        public IReadOnlyList<int> RemovedPipes => removedPipes;
        public IReadOnlyList<Pipe> AddedPipes => addedPipes;

        public bool IsEmpty => tiles.Count == 0 && removedPipes.Count == 0 && addedPipes.Count == 0;

        public TerrainDiff SetTile(int x, int y, TileState state)
        {
            tiles.Add(new TileChange(new IntVector2(x, y), state));
            return this;
        }

        public TerrainDiff RemovePipe(int id)
        {
            removedPipes.Add(id);
            return this;
        }

        public TerrainDiff AddPipe(Pipe pipe)
        {
            addedPipes.Add(pipe);
            return this;
        }
    }

    /// <summary>
    /// What applying terrain diffs actually changed in a room. Systems that run after the diffs use
    /// it to react: AIMap and path invalidation, pushing creatures out of new solid tiles, and
    /// ejecting creatures from removed pipes.
    /// </summary>
    public sealed class TerrainChanges
    {
        internal readonly List<IntVector2> changedTiles = new List<IntVector2>();
        internal readonly List<Pipe> removedPipes = new List<Pipe>();
        internal readonly List<Pipe> addedPipes = new List<Pipe>();

        /// <summary>Tiles whose terrain, beams, wall or shortcut flag differ from before, each listed once.</summary>
        public IReadOnlyList<IntVector2> ChangedTiles => changedTiles;

        /// <summary>Pipes that are gone, including the old version of each replaced pipe.</summary>
        public IReadOnlyList<Pipe> RemovedPipes => removedPipes;

        /// <summary>Pipes that are new, including the new version of each replaced pipe.</summary>
        public IReadOnlyList<Pipe> AddedPipes => addedPipes;

        public bool Any => changedTiles.Count > 0 || removedPipes.Count > 0 || addedPipes.Count > 0;
    }
}
