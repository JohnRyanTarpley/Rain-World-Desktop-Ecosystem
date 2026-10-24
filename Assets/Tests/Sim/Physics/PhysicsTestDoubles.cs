using System.Collections.Generic;
using RWDE.Sim.Physics;

namespace RWDE.Sim.Tests.Physics
{
    /// <summary>A tile grid that is all air until tiles are filled in.</summary>
    public sealed class TestRoom : IPhysicsRoom
    {
        private readonly TerrainType[,] tiles;

        public TestRoom(int width, int height)
        {
            Width = width;
            Height = height;
            tiles = new TerrainType[width, height];
        }

        public int Width { get; }
        public int Height { get; }
        public float gravity { get; set; } = 1f;
        public float PixelWidth => Width * 20f;
        public float PixelHeight => Height * 20f;

        /// <summary>Sets every tile in the inclusive rectangle.</summary>
        public TestRoom Fill(int x0, int y0, int x1, int y1, TerrainType terrain)
        {
            for (int x = x0; x <= x1; x++)
                for (int y = y0; y <= y1; y++)
                    tiles[x, y] = terrain;
            return this;
        }

        // Room.GetTile without a DefaultTile: outside the room reads the nearest edge tile,
        // with a shortcut entrance reading as solid.
        public TerrainType GetTerrain(int x, int y)
        {
            if (x >= 0 && y >= 0 && x < Width && y < Height)
                return tiles[x, y];
            var edge = tiles[System.Math.Max(0, System.Math.Min(x, Width - 1)), System.Math.Max(0, System.Math.Min(y, Height - 1))];
            return edge == TerrainType.ShortcutEntrance ? TerrainType.Solid : edge;
        }
    }

    /// <summary>One chunk with the game's <c>Rock</c> constants (Rock constructor).</summary>
    public sealed class TestRock : PhysicalObject
    {
        public readonly List<(int chunk, IntVector2 direction, float speed, bool firstContact)> Impacts =
            new List<(int, IntVector2, float, bool)>();

        public TestRock(IPhysicsRoom room, Vector2 pos)
        {
            this.room = room;
            bodyChunks = new[] { new BodyChunk(this, 0, pos, 5f, 0.07f) };
            bodyChunkConnections = new BodyChunkConnection[0];
            airFriction = 0.999f;
            gravity = 0.9f;
            bounce = 0.4f;
            surfaceFriction = 0.4f;
            collisionLayer = 2;
            waterFriction = 0.98f;
            buoyancy = 0.4f;
        }

        public override void TerrainImpact(int chunk, IntVector2 direction, float speed, bool firstContact)
        {
            Impacts.Add((chunk, direction, speed, firstContact));
        }
    }

    /// <summary>Two chunks and one connection, with no gravity and no air friction unless set.</summary>
    public sealed class TestPair : PhysicalObject
    {
        public TestPair(IPhysicsRoom room, Vector2 pos1, float mass1, Vector2 pos2, float mass2,
            float distance, BodyChunkConnection.Type type, float elasticity, float weightSymmetry)
        {
            this.room = room;
            bodyChunks = new[] { new BodyChunk(this, 0, pos1, 5f, mass1), new BodyChunk(this, 1, pos2, 5f, mass2) };
            bodyChunkConnections = new[] { new BodyChunkConnection(bodyChunks[0], bodyChunks[1], distance, type, elasticity, weightSymmetry) };
            airFriction = 1f;
            gravity = 0f;
        }
    }
}
