using System.Linq;
using NUnit.Framework;
using Acc = RWDE.Sim.AItile.Accessibility;
using Move = RWDE.Sim.MovementConnection.MovementType;
using Type = RWDE.Sim.CreatureTemplate.Type;

namespace RWDE.Sim.Tests.AI
{
    // Expected values are worked out by hand from AImapper/AImap in the decompiled game.
    public class AImapTests
    {
        private static readonly StaticWorld World = StaticWorld.Init(StaticWorld.Dlc.All);

        private static CreatureTemplate Get(Type t) => World.GetCreatureTemplate(t);
        private static IntVector2 T(int x, int y) => new IntVector2(x, y);

        // Builds a room from rows of characters, top row first: '#' solid, '-' floor, '/' slope,
        // '|' vertical beam, 'w' wall behind, '.' air.
        private static Room Parse(params string[] rows)
        {
            int h = rows.Length, w = rows[0].Length;
            var diff = new TerrainDiff();
            for (int row = 0; row < h; row++)
                for (int x = 0; x < w; x++)
                {
                    int y = h - 1 - row;
                    switch (rows[row][x])
                    {
                        case '#': diff.SetTile(x, y, TileState.Solid); break;
                        case '-': diff.SetTile(x, y, new TileState(Room.Tile.TerrainType.Floor)); break;
                        case '/': diff.SetTile(x, y, new TileState(Room.Tile.TerrainType.Slope)); break;
                        case '|': diff.SetTile(x, y, new TileState(Room.Tile.TerrainType.Air, verticalBeam: true)); break;
                        case 'w': diff.SetTile(x, y, new TileState(Room.Tile.TerrainType.Air, wallbehind: true)); break;
                    }
                }
            var room = new Room(w, h);
            room.ApplyTerrainDiff(diff);
            return room;
        }

        private static bool Has(AItile tile, Move type, IntVector2 from, IntVector2 to, int distance) =>
            tile.outgoingPaths.Contains(new MovementConnection(type, from, to, distance));

        [Test]
        public void TilesAreClassifiedAsInTheGame()
        {
            var room = Parse(
                "#######",
                "#.....#",
                "#..|w.#",
                "#.....#",
                "#.....#",
                "#######");
            var map = room.aimap;
            Assert.AreEqual(Acc.Solid, map.getAItile(0, 0).acc);
            Assert.AreEqual(Acc.Floor, map.getAItile(3, 1).acc);
            Assert.AreEqual(Acc.Wall, map.getAItile(1, 2).acc, "next to a solid side");
            Assert.AreEqual(Acc.Air, map.getAItile(3, 2).acc);
            Assert.AreEqual(Acc.Climb, map.getAItile(3, 3).acc);
            Assert.AreEqual(Acc.Wall, map.getAItile(4, 3).acc, "wall behind");
            Assert.AreEqual(Acc.Ceiling, map.getAItile(3, 4).acc);
            Assert.AreEqual(Acc.Solid, map.getAItile(-1, 2).acc, "outside the room reads as solid");
            Assert.IsTrue(map.getAItile(3, 1).walkable);
            Assert.IsFalse(map.getAItile(3, 2).walkable);
        }

        [Test]
        public void TwoIconColumnsMakeATunnel()
        {
            // Two solid columns one tile apart: the gap between them is the game's corridor.
            var room = Parse(
                ".#.#.",
                ".#.#.",
                ".#.#.",
                ".#.#.",
                "#####");
            var map = room.aimap;
            Assert.AreEqual(Acc.Corridor, map.getAItile(2, 2).acc);
            Assert.IsTrue(map.getAItile(2, 2).narrowSpace);
            Assert.IsTrue(map.TileAccessibleToCreature(T(2, 2), Get(Type.PinkLizard)));
            Assert.AreEqual(new PathCost(2f, PathCost.Legality.Allowed), map.TileCostForCreature(T(2, 2), Get(Type.GreenLizard)));
            Assert.IsFalse(map.TileAccessibleToCreature(T(2, 2), Get(Type.Vulture)), "vultures keep away from terrain");
            Assert.IsTrue(Has(map.getAItile(2, 2), Move.Standard, T(2, 2), T(2, 3), 1));
        }

        [Test]
        public void LedgesGetDropsAndFloorsGetTurns()
        {
            var room = Parse(
                "......",
                "......",
                "......",
                "...###",
                "......",
                "......",
                "......",
                "######");
            var map = room.aimap;
            // (2, 4) overhangs the block's corner, so the game counts it as floor.
            Assert.AreEqual(Acc.Floor, map.getAItile(2, 5).acc);
            Assert.AreEqual(Acc.Wall, map.getAItile(2, 4).acc);
            Assert.IsTrue(Has(map.getAItile(2, 5), Move.DropToFloor, T(2, 5), T(2, 1), 4));
            Assert.IsTrue(map.getAItile(2, 1).incomingPaths.Contains(new MovementConnection(Move.DropToFloor, T(2, 5), T(2, 1), 4)));
            Assert.IsTrue(Has(map.getAItile(3, 1), Move.LizardTurn, T(3, 1), T(4, 1), 1));
            Assert.AreEqual(1, map.getAItile(4, 5).floorAltitude);
            Assert.AreEqual(T(2, 1), map.getAItile(2, 3).fallRiskTile);
        }

        [Test]
        public void PipesBecomeShortcutConnections()
        {
            var source = new FakeTerrainSource(6, 6);
            for (int x = 0; x < 6; x++) source.WithTile(x, 0, TileState.Solid);
            for (int y = 1; y < 6; y++) source.WithTile(5, y, TileState.Solid);
            var room = new Room(source);
            var map = room.aimap;
            Assert.IsFalse(map.getAItile(5, 1).outgoingPaths.Any(c => c.type == Move.ShortCut));

            room.ApplyTerrainDiff(new TerrainDiff().AddPipe(new Pipe(1, new[] { T(5, 1), T(5, 2), T(5, 3), T(5, 4) })));

            Assert.AreEqual(Acc.Floor, map.getAItile(5, 1).acc, "the entrance is open and sits on the floor");
            Assert.IsTrue(Has(map.getAItile(5, 1), Move.ShortCut, T(5, 1), T(5, 4), 3));
            Assert.IsTrue(Has(map.getAItile(5, 4), Move.ShortCut, T(5, 4), T(5, 1), 3));
            Assert.IsTrue(map.getAItile(5, 4).incomingPaths.Contains(new MovementConnection(Move.ShortCut, T(5, 1), T(5, 4), 3)));
            // Big creatures squeeze in from two and three tiles out in front of the hole.
            Assert.IsTrue(Has(map.getAItile(5, 1), Move.BigCreatureShortCutSqueeze, T(5, 1), T(3, 1), 2));
            Assert.IsTrue(Has(map.getAItile(2, 1), Move.BigCreatureShortCutSqueeze, T(2, 1), T(5, 1), 3));
            Assert.IsTrue(Has(map.getAItile(2, 2), Move.BigCreatureShortCutSqueeze, T(2, 2), T(5, 1), 3));
            Assert.IsFalse(map.getAItile(2, 0).outgoingPaths.Any(c => c.type == Move.BigCreatureShortCutSqueeze), "solid tiles get no squeeze");

            var shortcut = map.getAItile(5, 1).outgoingPaths.First(c => c.type == Move.ShortCut);
            Assert.IsTrue(map.IsConnectionAllowedForCreature(shortcut, Get(Type.BlueLizard)));
            Assert.AreEqual(new PathCost(15f * 3f + 1f, PathCost.Legality.Allowed), map.ConnectionCostForCreature(shortcut, Get(Type.BlueLizard)));

            room.ApplyTerrainDiff(new TerrainDiff().RemovePipe(1));

            Assert.AreEqual(Acc.Solid, map.getAItile(5, 1).acc);
            Assert.IsFalse(map.getAItile(5, 4).outgoingPaths.Any(c => c.type == Move.ShortCut));
            Assert.IsFalse(map.getAItile(3, 1).outgoingPaths.Any(c => c.type == Move.BigCreatureShortCutSqueeze));
        }

        [Test]
        public void TemplatesDecideWhoMayStandWhere()
        {
            var room = Parse(
                "..........",
                "..........",
                "..........",
                "#.........",
                "#.........",
                "##########");
            var map = room.aimap;
            var wall = T(1, 2);
            Assert.AreEqual(Acc.Wall, map.getAItile(wall).acc);
            Assert.IsFalse(map.TileAccessibleToCreature(wall, Get(Type.PinkLizard)));
            Assert.IsTrue(map.TileAccessibleToCreature(wall, Get(Type.BlueLizard)));
            Assert.AreEqual(new PathCost(0f, PathCost.Legality.IllegalTile), map.TileCostForCreature(wall, Get(Type.PinkLizard)));
            Assert.AreEqual(T(2, 1), map.TryForAccessibleNeighbor(T(2, 1), Get(Type.PinkLizard)));

            // Vultures keep two tiles from terrain; batflies like open air.
            Assert.AreEqual(4, map.getTerrainProximity(6, 4));
            Assert.IsTrue(map.TileAccessibleToCreature(T(6, 4), Get(Type.Vulture)));
            Assert.IsFalse(map.TileAccessibleToCreature(T(6, 1), Get(Type.Vulture)));
            Assert.AreEqual(100f / 4f, map.TileCostForCreature(T(6, 4), Get(Type.Fly)).resistance);

            // No pre-baked pathing: Monster Kelp and Overseers stand nowhere on the map.
            Assert.IsFalse(map.TileAccessibleToCreature(T(4, 1), Get(Type.TentaclePlant)));
            Assert.IsFalse(map.TileAccessibleToCreature(T(6, 4), Get(Type.Overseer)));
        }

        [Test]
        public void ReachesAreOnlyForGapsTheCreatureCantWalk()
        {
            // Two poles with air between: climbers reach across rather than letting go.
            var room = Parse(
                ".....",
                ".....",
                ".|.|.",
                ".....",
                "#####");
            var map = room.aimap;
            var reach = new MovementConnection(Move.ReachOverGap, T(1, 2), T(3, 2), 2);
            Assert.IsTrue(map.getAItile(1, 2).outgoingPaths.Contains(reach));
            Assert.IsTrue(map.IsConnectionAllowedForCreature(reach, Get(Type.PinkLizard)));
            Assert.IsFalse(map.IsConnectionAllowedForCreature(reach, Get(Type.Vulture)));
        }

        [Test]
        public void MapIsBuiltOnceAndKeptUpToDate()
        {
            var room = Parse(
                "......",
                "......",
                "######");
            var map = room.aimap;
            Assert.AreSame(map, room.aimap);
            Assert.AreEqual(Acc.Floor, map.getAItile(2, 1).acc);

            room.ApplyTerrainDiff(new TerrainDiff().SetTile(2, 1, TileState.Solid));

            Assert.AreSame(map, room.aimap);
            Assert.AreEqual(Acc.Solid, map.getAItile(2, 1).acc);
            Assert.AreEqual(Acc.Floor, map.getAItile(2, 2).acc);
        }

        [Test]
        public void DiffsFromTheTerrainSourceUpdateTheMap()
        {
            var source = new FakeTerrainSource(8, 4);
            for (int x = 0; x < 8; x++) source.WithTile(x, 0, TileState.Solid);
            var room = new Room(source);
            var map = room.aimap;
            source.Queue(new TerrainDiff().SetTile(4, 1, TileState.Solid));

            room.ApplyTerrainDiffs(source);

            Assert.AreEqual(Acc.Solid, map.getAItile(4, 1).acc);
            Assert.AreEqual(Acc.Floor, map.getAItile(4, 2).acc);
        }
    }
}
