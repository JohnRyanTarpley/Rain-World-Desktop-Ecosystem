using System.Collections.Generic;

namespace RWDE.Sim.Tests
{
    public static class RoomFill
    {
        /// <summary>Sets every tile in the inclusive rectangle through a terrain diff, as the shell would.</summary>
        public static Room Fill(this Room room, int x0, int y0, int x1, int y1, Room.Tile.TerrainType terrain)
        {
            var diff = new TerrainDiff();
            for (int x = x0; x <= x1; x++)
                for (int y = y0; y <= y1; y++)
                    diff.SetTile(x, y, new TileState(terrain));
            room.ApplyTerrainDiff(diff);
            return room;
        }
    }

    /// <summary>One chunk with the game's <c>Rock</c> constants (Rock constructor).</summary>
    public sealed class TestRock : PhysicalObject
    {
        public readonly List<(int chunk, IntVector2 direction, float speed, bool firstContact)> Impacts =
            new List<(int, IntVector2, float, bool)>();

        public TestRock(Room room, Vector2 pos)
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
        public TestPair(Room room, Vector2 pos1, float mass1, Vector2 pos2, float mass2,
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
