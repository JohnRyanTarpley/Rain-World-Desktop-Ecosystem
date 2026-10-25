using System.Collections.Generic;

namespace RWDE.Sim.Tests
{
    /// <summary>An in-memory <see cref="ITerrainSource"/>: set up a snapshot, then queue diffs.</summary>
    public sealed class FakeTerrainSource : ITerrainSource
    {
        private readonly TileState[,] tiles;
        private readonly List<Pipe> pipes = new List<Pipe>();
        private readonly Queue<TerrainDiff> diffs = new Queue<TerrainDiff>();

        public FakeTerrainSource(int width, int height)
        {
            Width = width;
            Height = height;
            tiles = new TileState[width, height];
        }

        public int Width { get; }
        public int Height { get; }
        public int DiffsTaken { get; private set; }

        public TileState GetTile(int x, int y) => tiles[x, y];
        public IEnumerable<Pipe> Pipes => pipes;

        public FakeTerrainSource WithTile(int x, int y, TileState state)
        {
            tiles[x, y] = state;
            return this;
        }

        public FakeTerrainSource WithPipe(Pipe pipe)
        {
            pipes.Add(pipe);
            return this;
        }

        public void Queue(TerrainDiff diff) => diffs.Enqueue(diff);

        public bool TryTakeDiff(out TerrainDiff diff)
        {
            if (diffs.Count == 0)
            {
                diff = null;
                return false;
            }
            diff = diffs.Dequeue();
            DiffsTaken++;
            return true;
        }
    }
}
