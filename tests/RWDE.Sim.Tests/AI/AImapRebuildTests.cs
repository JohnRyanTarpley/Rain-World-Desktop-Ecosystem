using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Terrain = RWDE.Sim.Room.Tile.TerrainType;

namespace RWDE.Sim.Tests.AI
{
    /// <summary>
    /// The local rebuild must leave the map exactly as a full build of the same terrain would,
    /// connection order included, while leaving far-away tiles alone.
    /// </summary>
    public class AImapRebuildTests
    {
        private const int W = 40;
        private const int H = 18;

        private static IntVector2 T(int x, int y) => new IntVector2(x, y);

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        public void LocalRebuildMatchesFullBuild(int seed)
        {
            var rng = new Random(seed);
            var initial = RandomTerrain(rng);
            var live = new Room(W, H);
            var reference = new Room(W, H);
            live.ApplyTerrainDiff(initial);
            reference.ApplyTerrainDiff(initial);
            var map = live.aimap;

            var pipes = new Dictionary<int, Pipe>();
            for (int step = 0; step < 40; step++)
            {
                var diff = RandomDiff(rng, pipes);
                live.ApplyTerrainDiff(diff);
                reference.ApplyTerrainDiff(diff);

                // A fresh room with the same terrain builds its map from scratch.
                var fresh = new Room(W, H);
                fresh.ApplyTerrainDiff(Snapshot(reference));
                AssertSameMap(fresh.aimap, map, "seed " + seed + ", step " + step);
            }
        }

        [Test]
        public void FarAwayColumnsAreLeftAlone()
        {
            var room = new Room(W, H);
            var floor = new TerrainDiff();
            for (int x = 0; x < W; x++) floor.SetTile(x, 0, TileState.Solid);
            room.ApplyTerrainDiff(floor);
            var map = room.aimap;
            var near = map.getAItile(12, 1);
            var far = map.getAItile(30, 1);

            room.ApplyTerrainDiff(new TerrainDiff().SetTile(10, 1, TileState.Solid));

            Assert.AreNotSame(near, map.getAItile(12, 1), "columns near the change are rebuilt");
            Assert.AreSame(far, map.getAItile(30, 1), "columns far from it keep their tiles");
            Assert.AreEqual(AItile.Accessibility.Floor, map.getAItile(10, 2).acc);
        }

        [Test]
        public void FloorChangesReachFourColumnsSideways()
        {
            // Smoothed floor altitude looks 4 columns to each side, so a new ledge lowers it there too.
            var room = new Room(20, 14);
            var floor = new TerrainDiff();
            for (int x = 0; x < 20; x++) floor.SetTile(x, 0, TileState.Solid);
            room.ApplyTerrainDiff(floor);
            var map = room.aimap;
            Assert.AreEqual(10, map.getAItile(14, 10).smoothedFloorAltitude);

            room.ApplyTerrainDiff(new TerrainDiff().SetTile(10, 6, TileState.Solid));

            Assert.AreEqual(4 + 4 / 2, map.getAItile(14, 10).smoothedFloorAltitude);
            var fresh = new Room(20, 14);
            fresh.ApplyTerrainDiff(Snapshot(room));
            AssertSameMap(fresh.aimap, map, "after the ledge");
        }

        [Test]
        public void ShortcutToAFarEntranceSurvivesARebuildAtThatEnd()
        {
            // A pipe along the top row; terrain changes near one end must keep the other end's connection
            // in the rebuilt end's incoming list, in the same place.
            var room = new Room(W, H);
            var setup = new TerrainDiff();
            for (int x = 0; x < W; x++)
            {
                setup.SetTile(x, 0, TileState.Solid);
                setup.SetTile(x, H - 1, TileState.Solid);
            }
            setup.AddPipe(new Pipe(7, Enumerable.Range(2, 34).Select(x => T(x, H - 1)).ToArray()));
            room.ApplyTerrainDiff(setup);
            var map = room.aimap;
            var fromFarEnd = new MovementConnection(MovementConnection.MovementType.ShortCut, T(35, H - 1), T(2, H - 1), 33);
            Assert.IsTrue(map.getAItile(2, H - 1).incomingPaths.Contains(fromFarEnd));

            room.ApplyTerrainDiff(new TerrainDiff().SetTile(3, H - 2, TileState.Solid));

            Assert.IsTrue(map.getAItile(2, H - 1).incomingPaths.Contains(fromFarEnd));
            var fresh = new Room(W, H);
            fresh.ApplyTerrainDiff(Snapshot(room));
            AssertSameMap(fresh.aimap, map, "after the change");
        }

        // ---- Random terrain ----

        private static TileState RandomTile(Random rng)
        {
            double r = rng.NextDouble();
            var terrain = r < 0.35 ? Terrain.Solid : r < 0.40 ? Terrain.Floor : r < 0.45 ? Terrain.Slope : Terrain.Air;
            return new TileState(terrain, rng.NextDouble() < 0.1, rng.NextDouble() < 0.05, rng.NextDouble() < 0.1);
        }

        private static TerrainDiff RandomTerrain(Random rng)
        {
            var diff = new TerrainDiff();
            for (int x = 0; x < W; x++)
                for (int y = 0; y < H; y++)
                    diff.SetTile(x, y, RandomTile(rng));
            return diff;
        }

        // Tile edits clustered in one spot, sometimes with a pipe added, replaced or removed. Vertical
        // pipes stay below the top row and use distinct columns; one horizontal pipe may run along the
        // top row, so pipes never share an entrance and some shortcuts join far-apart columns.
        private static TerrainDiff RandomDiff(Random rng, Dictionary<int, Pipe> pipes)
        {
            var diff = new TerrainDiff();
            int cx = rng.Next(W), cy = rng.Next(H);
            int n = rng.Next(1, 4);
            for (int i = 0; i < n; i++)
            {
                int x = Math.Clamp(cx + rng.Next(-2, 3), 0, W - 1);
                int y = Math.Clamp(cy + rng.Next(-2, 3), 0, H - 1);
                diff.SetTile(x, y, RandomTile(rng));
            }

            double r = rng.NextDouble();
            if (r < 0.15 && pipes.Count > 0)
            {
                int id = pipes.Keys.ElementAt(rng.Next(pipes.Count));
                pipes.Remove(id);
                diff.RemovePipe(id);
            }
            else if (r < 0.45)
            {
                Pipe pipe;
                if (rng.NextDouble() < 0.3)
                {
                    int x0 = rng.Next(0, 5), x1 = rng.Next(W - 5, W);
                    pipe = new Pipe(0, Enumerable.Range(x0, x1 - x0 + 1).Select(x => T(x, H - 1)).ToArray());
                }
                else
                {
                    // One pipe per column: its id is the column, so a new one replaces the old.
                    int x = rng.Next(W);
                    int id = x + 1;
                    int y0 = rng.Next(0, H - 3), y1 = rng.Next(y0 + 1, H - 1);
                    pipe = new Pipe(id, Enumerable.Range(y0, y1 - y0 + 1).Select(y => T(x, y)).ToArray());
                }
                pipes[pipe.Id] = pipe;
                diff.AddPipe(pipe);
            }
            return diff;
        }

        private static TerrainDiff Snapshot(Room room)
        {
            var diff = new TerrainDiff();
            var entrances = new HashSet<IntVector2>(room.Pipes.SelectMany(p => new[] { p.A, p.B }));
            for (int x = 0; x < room.Width; x++)
                for (int y = 0; y < room.Height; y++)
                {
                    var t = room.Tiles[x, y];
                    // Entrances show ShortcutEntrance; the source's terrain under them is unknown here,
                    // and it doesn't matter, since the pipe overlay hides it.
                    var terrain = entrances.Contains(T(x, y)) ? Terrain.Air : t.Terrain;
                    diff.SetTile(x, y, new TileState(terrain, t.verticalBeam, t.horizontalBeam, t.wallbehind));
                }
            foreach (var p in room.Pipes) diff.AddPipe(p);
            return diff;
        }

        // ---- Comparison ----

        private static void AssertSameMap(AImap expected, AImap actual, string context)
        {
            for (int x = 0; x < expected.width; x++)
                for (int y = 0; y < expected.height; y++)
                {
                    string at = context + " at " + x + "," + y;
                    var e = expected.map[x, y];
                    var a = actual.map[x, y];
                    Assert.AreEqual(e.acc, a.acc, at + " acc");
                    Assert.AreEqual(e.walkable, a.walkable, at + " walkable");
                    Assert.AreEqual(e.narrowSpace, a.narrowSpace, at + " narrowSpace");
                    Assert.AreEqual(e.floorAltitude, a.floorAltitude, at + " floorAltitude");
                    Assert.AreEqual(e.smoothedFloorAltitude, a.smoothedFloorAltitude, at + " smoothedFloorAltitude");
                    Assert.AreEqual(e.fallRiskTile, a.fallRiskTile, at + " fallRiskTile");
                    CollectionAssert.AreEqual(e.outgoingPaths, a.outgoingPaths, at + " outgoing");
                    CollectionAssert.AreEqual(e.incomingPaths, a.incomingPaths, at + " incoming");
                    Assert.AreEqual(expected.terrainProximity[x, y], actual.terrainProximity[x, y], at + " terrainProximity");
                }
        }
    }
}
