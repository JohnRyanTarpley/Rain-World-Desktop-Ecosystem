using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Acc = RWDE.Sim.AItile.Accessibility;
using Move = RWDE.Sim.MovementConnection.MovementType;
using Type = RWDE.Sim.CreatureTemplate.Type;

namespace RWDE.Sim.Tests.AI
{
    // Routes are checked against what the game's PathFinder/StandardPather do on the same terrain: the
    // creature takes the cheapest legal connections the search priced, through tunnels and pipes alike.
    public class PathFinderTests
    {
        private static readonly StaticWorld Templates = StaticWorld.Init(StaticWorld.Dlc.All);

        private static CreatureTemplate Get(Type t) => Templates.GetCreatureTemplate(t);
        private static IntVector2 T(int x, int y) => new IntVector2(x, y);

        // Rows of characters, top row first: '#' solid, '.' air.
        private static Room Parse(params string[] rows)
        {
            int h = rows.Length, w = rows[0].Length;
            var diff = new TerrainDiff();
            for (int row = 0; row < h; row++)
                for (int x = 0; x < w; x++)
                    if (rows[row][x] == '#')
                        diff.SetTile(x, h - 1 - row, TileState.Solid);
            var room = new Room(w, h);
            room.ApplyTerrainDiff(diff);
            return room;
        }

        private sealed class TestAI : ArtificialIntelligence
        {
            public TestAI(AbstractCreature creature, World world) : base(creature)
            {
                AddModule(new StandardPather(this, world, creature));
            }

            public StandardPather Pather => (StandardPather)pathFinder;
        }

        // A creature in `room` whose AI starts out heading for `destination`: the pathfinder takes its
        // first destination from the abstract AI, as in the game.
        private static TestAI Spawn(World world, Room room, Type type, IntVector2 tile, WorldCoordinate destination)
        {
            var creature = new AbstractCreature(world, Get(type), room.GetWorldCoordinate(tile));
            creature.abstractAI.destination = destination;
            var ai = new TestAI(creature, world);
            ai.NewRoom(room);
            return ai;
        }

        // Ticks the world and the AI, moving the creature one connection per tick once the search has
        // priced its tile (or, just after entering a room, while it steps away from the entrance). Stops
        // after a connection that leaves the room, or when `arrived` says so.
        private static List<MovementConnection> Walk(World world, TestAI ai, Func<WorldCoordinate, bool> arrived, int maxTicks = 3000)
        {
            var taken = new List<MovementConnection>();
            var creature = ai.creature;
            for (int tick = 0; tick < maxTicks && !arrived(creature.pos); tick++)
            {
                world.Update();
                ai.Update();
                var pather = ai.Pather;
                if (!pather.DoneMappingAccessibility)
                    continue;
                if (pather.forbiddenEntranceCounter <= 0 && !pather.CoordinateAtCurrentPathingGeneration(creature.pos))
                    continue;
                var step = pather.FollowPath(creature.pos, true);
                if (step == default)
                    continue;
                taken.Add(step);
                if (!step.destinationCoord.TileDefined)
                    break;
                creature.pos = new WorldCoordinate(step.destinationCoord.room, step.destinationCoord.x, step.destinationCoord.y, creature.pos.abstractNode);
            }
            return taken;
        }

        private static Func<WorldCoordinate, bool> At(int room, int x, int y) => pos => pos.room == room && pos.x == x && pos.y == y;

        private static void AssertEveryStepIsLegal(Room room, CreatureTemplate crit, IEnumerable<MovementConnection> steps)
        {
            foreach (var step in steps.Where(s => s.destinationCoord.TileDefined))
            {
                Assert.IsTrue(room.aimap.IsConnectionAllowedForCreature(step, crit), "illegal step " + step);
                Assert.AreEqual(room.abstractRoom.index, step.destinationCoord.room);
            }
        }

        // ---- Icon tunnels (terrain) ----

        // Two icon columns side by side rasterise as two solid columns with a one-tile gap: the game's
        // corridor. Here three of them form a shaft through a slab between a lower and an upper floor.
        private static readonly string[] Shaft =
        {
            "############",
            "#..........#",
            "#..........#",
            "#####.######",
            "#####.######",
            "#####.######",
            "#..........#",
            "############"
        };

        [Test]
        public void ALizardClimbsThroughAnIconTunnel()
        {
            var room = Parse(Shaft);
            var world = new World(1, room);
            Assert.AreEqual(Acc.Corridor, room.aimap.getAItile(5, 3).acc);

            var ai = Spawn(world, room, Type.PinkLizard, T(1, 1), room.GetWorldCoordinate(T(10, 5)));
            var steps = Walk(world, ai, At(0, 10, 5));

            Assert.AreEqual(T(10, 5), ai.creature.pos.Tile, "arrived");
            var visited = steps.Select(s => s.DestTile).ToList();
            CollectionAssert.IsSubsetOf(new[] { T(5, 2), T(5, 3), T(5, 4), T(5, 5) }, visited, "went up the tunnel");
            AssertEveryStepIsLegal(room, ai.creature.creatureTemplate, steps);
            Assert.IsTrue(ai.Pather.CoordinateReachableAndGetbackable(room.GetWorldCoordinate(T(5, 3))));
        }


        // ---- Repathing when the terrain changes ----

        [Test]
        public void BlockingTheTunnelInUseSendsTheCreatureThroughTheOtherOne()
        {
            var room = Parse(
                "##############",
                "#............#",
                "#............#",
                "###.######.###",
                "###.######.###",
                "###.######.###",
                "#............#",
                "##############");
            var world = new World(1, room);
            var ai = Spawn(world, room, Type.PinkLizard, T(5, 1), room.GetWorldCoordinate(T(6, 5)));

            // The left tunnel is the shorter way. Let the creature reach its foot.
            var before = Walk(world, ai, At(0, 3, 1));
            Assert.AreEqual(T(3, 1), ai.creature.pos.Tile, "headed for the left tunnel");
            int version = room.TerrainVersion;

            // An icon moves into the left tunnel.
            room.ApplyTerrainDiff(new TerrainDiff().SetTile(3, 3, TileState.Solid));
            Assert.AreEqual(version + 1, room.TerrainVersion);

            var after = Walk(world, ai, At(0, 6, 5));
            Assert.AreEqual(T(6, 5), ai.creature.pos.Tile, "arrived the other way");
            var visited = after.Select(s => s.DestTile).ToList();
            CollectionAssert.Contains(visited, T(10, 3), "through the right tunnel");
            CollectionAssert.DoesNotContain(visited, T(3, 3));
            AssertEveryStepIsLegal(room, ai.creature.creatureTemplate, after);
        }

        [Test]
        public void ADestinationCutOffByTheTerrainIsNoLongerReachable()
        {
            var room = Parse(Shaft);
            var world = new World(1, room);
            var ai = Spawn(world, room, Type.PinkLizard, T(1, 1), room.GetWorldCoordinate(T(10, 5)));
            for (int i = 0; i < 300; i++)
            {
                world.Update();
                ai.Update();
            }
            Assert.IsTrue(ai.Pather.CoordinateReachable(room.GetWorldCoordinate(T(10, 5))));

            room.ApplyTerrainDiff(new TerrainDiff().SetTile(5, 3, TileState.Solid));
            for (int i = 0; i < 300; i++)
            {
                world.Update();
                ai.Update();
            }
            Assert.IsFalse(ai.Pather.CoordinateReachable(room.GetWorldCoordinate(T(10, 5))));
            Assert.IsTrue(ai.Pather.lookingForImpossiblePath);
        }

        // ---- Pipes inside a room ----

        [Test]
        public void APipeInsideARoomIsAPathOption()
        {
            // A wall splits the room; a pipe runs through it from (4, 1) to (6, 1).
            var room = Parse(
                "###########",
                "#...###...#",
                "#...###...#",
                "#...###...#",
                "###########");
            room.ApplyTerrainDiff(new TerrainDiff().AddPipe(new Pipe(1, new[] { T(4, 1), T(5, 1), T(6, 1) })));
            var world = new World(1, room);
            var ai = Spawn(world, room, Type.PinkLizard, T(1, 1), room.GetWorldCoordinate(T(9, 1)));

            var steps = Walk(world, ai, At(0, 9, 1));

            Assert.AreEqual(T(9, 1), ai.creature.pos.Tile, "arrived");
            Assert.IsTrue(steps.Any(s => s.type == Move.ShortCut && s.StartTile == T(4, 1) && s.DestTile == T(6, 1)), "took the pipe");
            AssertEveryStepIsLegal(room, ai.creature.creatureTemplate, steps);
        }

        // ---- Pipes between monitor rooms ----

        // Two monitor rooms side by side. A pipe joins them: in the left room it runs from an entrance in
        // the right wall at (9, 1) to the room exit at (10, 1); in the right room from (1, 1) to (0, 1).
        // The right room is wider, and destinations go past the left room's edge: the game's FollowPath
        // treats any step onto the destination's tile as free without comparing rooms, so a destination
        // tile that also exists in the left room would pull the creature there.
        private static (Room left, Room right) Monitors(bool withPipe)
        {
            var left = Parse(
                "###########",
                "#........##",
                "#........##",
                "#........##",
                "###########");
            var right = Parse(
                "##############",
                "##...........#",
                "##...........#",
                "##...........#",
                "##############");
            if (withPipe) AddMonitorPipe(left, right);
            return (left, right);
        }

        private static void AddMonitorPipe(Room left, Room right)
        {
            left.ApplyTerrainDiff(new TerrainDiff().AddPipe(new Pipe(1, new[] { T(9, 1), T(10, 1) }, roomLink: 7)));
            right.ApplyTerrainDiff(new TerrainDiff().AddPipe(new Pipe(1, new[] { T(1, 1), T(0, 1) }, roomLink: 7)));
        }

        [Test]
        public void APipeBetweenMonitorsJoinsTheirRooms()
        {
            var (left, right) = Monitors(true);
            var world = new World(1, left, right);

            Assert.AreEqual(ShortcutData.Type.RoomExit, left.shortcutData(T(9, 1)).shortCutType);
            Assert.AreEqual(2, left.GetTile(10, 1).shortCut, "the room exit tile");
            CollectionAssert.AreEqual(new[] { T(10, 1) }, left.exitAndDenIndex);
            Assert.AreEqual(0, left.shortcutData(T(9, 1)).destNode);
            Assert.AreEqual(1, left.abstractRoom.nodes.Length);
            Assert.AreEqual(AbstractRoomNode.Type.Exit, left.abstractRoom.nodes[0].type);
            CollectionAssert.AreEqual(new[] { 1 }, left.abstractRoom.connections);
            CollectionAssert.AreEqual(new[] { 0 }, right.abstractRoom.connections);
            Assert.AreEqual(0, right.abstractRoom.ExitIndex(0));
            Assert.AreEqual(2, world.TotalShortCutLengthBetweenTwoConnectedRooms(0, 1), "one step each side");
            Assert.AreEqual(new WorldCoordinate(1, 1, 1, -1), right.LocalCoordinateOfNode(0));
            Assert.Throws<ArgumentException>(() => left.ApplyTerrainDiff(new TerrainDiff().AddPipe(new Pipe(2, new[] { T(1, 3), T(0, 3) }, roomLink: 7))),
                "one exit per link in a room");

            // Unplugging the far half leaves the near exit leading nowhere.
            right.ApplyTerrainDiff(new TerrainDiff().RemovePipe(1));
            CollectionAssert.AreEqual(new[] { -1 }, left.abstractRoom.connections);
            Assert.AreEqual(0, right.abstractRoom.nodes.Length);
        }

        [Test]
        public void ALizardRoutesThroughAPipeIntoTheNextMonitorRoom()
        {
            var (left, right) = Monitors(true);
            var world = new World(1, left, right);
            var crit = Get(Type.PinkLizard);
            var ai = Spawn(world, left, Type.PinkLizard, T(2, 1), right.GetWorldCoordinate(T(12, 1)));

            var inLeft = Walk(world, ai, pos => false);

            Assert.AreEqual(new WorldCoordinate(1, 12, 1, 0), ai.Pather.GetDestination, "a destination in another room gets the node to aim for");

            // It walks to the entrance, then takes the shortcut into the exit node.
            var last = inLeft[inLeft.Count - 1];
            Assert.AreEqual(Move.ShortCut, last.type);
            Assert.AreEqual(T(9, 1), last.StartTile);
            Assert.AreEqual(new WorldCoordinate(0, -1, -1, 0), last.destinationCoord);
            Assert.AreEqual(1, last.distance);
            Assert.AreEqual(T(9, 1), ai.creature.pos.Tile);
            CollectionAssert.AreEqual(new[] { T(3, 1), T(4, 1), T(5, 1), T(6, 1), T(7, 1), T(8, 1), T(9, 1) },
                inLeft.Take(inLeft.Count - 1).Select(s => s.DestTile), "straight along the floor");
            AssertEveryStepIsLegal(left, crit, inLeft);
            Assert.IsTrue(ai.Pather.CoordinateReachableAndGetbackable(new WorldCoordinate(1, -1, -1, 0)), "the far node, through the pipe");

            // The pipe carries it across (shortcut travel's job) and spits it out of the far entrance.
            int entranceNode = right.abstractRoom.ExitIndex(left.abstractRoom.index);
            ai.creature.pos = new WorldCoordinate(1, -1, -1, entranceNode);
            ai.NewRoom(right);
            ai.creature.pos = new WorldCoordinate(1, 1, 1, entranceNode);

            var inRight = Walk(world, ai, At(1, 12, 1));

            Assert.AreEqual(new WorldCoordinate(1, 12, 1, entranceNode), ai.creature.pos, "arrived");
            AssertEveryStepIsLegal(right, crit, inRight);
            Assert.IsFalse(inRight.Any(s => !s.destinationCoord.TileDefined), "didn't turn back into the pipe");
        }

        [Test]
        public void APipePluggedInLaterOpensTheWayToTheOtherRoom()
        {
            var (left, right) = Monitors(false);
            var world = new World(1, left, right);
            var ai = Spawn(world, left, Type.PinkLizard, T(2, 1), left.GetWorldCoordinate(T(2, 1)));
            // As in the game, a new pathfinder ignores SetDestination until its first update has taken up
            // the destination it started with.
            world.Update();
            ai.Update();
            ai.SetDestination(right.GetWorldCoordinate(T(12, 1)));
            Assert.AreEqual(new WorldCoordinate(1, 12, 1, 0), ai.Pather.GetDestination, "node 0 by default while the room has none");
            for (int i = 0; i < 100; i++)
            {
                world.Update();
                ai.Update();
            }
            Assert.IsTrue(ai.Pather.lookingForImpossiblePath, "no way there yet");
            Assert.IsFalse(ai.Pather.CoordinateReachable(new WorldCoordinate(1, -1, -1, 0)));

            AddMonitorPipe(left, right);
            var steps = Walk(world, ai, pos => false);

            Assert.AreEqual(Move.ShortCut, steps[steps.Count - 1].type);
            Assert.AreEqual(new WorldCoordinate(0, -1, -1, 0), steps[steps.Count - 1].destinationCoord, "into the new pipe");
        }

        [Test]
        public void ARouteThroughAMiddleMonitorUsesItsNodeCostsAndRepathsWhenItsTerrainChanges()
        {
            // Three monitors in a row: left -(link 7)- middle -(link 8)- right.
            var (left, right) = Monitors(false);
            var middle = Parse(
                "##############",
                "##..........##",
                "##..........##",
                "##..........##",
                "##############");
            left.ApplyTerrainDiff(new TerrainDiff().AddPipe(new Pipe(1, new[] { T(9, 1), T(10, 1) }, roomLink: 7)));
            middle.ApplyTerrainDiff(new TerrainDiff()
                .AddPipe(new Pipe(1, new[] { T(1, 1), T(0, 1) }, roomLink: 7))
                .AddPipe(new Pipe(2, new[] { T(12, 1), T(13, 1) }, roomLink: 8)));
            right.ApplyTerrainDiff(new TerrainDiff().AddPipe(new Pipe(1, new[] { T(1, 1), T(0, 1) }, roomLink: 8)));
            var world = new World(1, left, middle, right);
            var crit = Get(Type.PinkLizard);
            CollectionAssert.AreEqual(new[] { 0, 2 }, middle.abstractRoom.connections);
            Assert.Greater(middle.abstractRoom.nodes[0].ConnectionCost(1, crit), 0, "the middle room's floor joins its exits");

            var ai = Spawn(world, left, Type.PinkLizard, T(2, 1), right.GetWorldCoordinate(T(12, 1)));
            var farNode = new WorldCoordinate(2, -1, -1, 0);
            for (int i = 0; i < 300; i++)
            {
                world.Update();
                ai.Update();
            }
            Assert.IsTrue(ai.Pather.CoordinateReachable(farNode), "through both pipes");
            Assert.IsFalse(ai.Pather.lookingForImpossiblePath);

            // A column of icons fills the middle monitor top to bottom, cutting its exits apart.
            middle.ApplyTerrainDiff(new TerrainDiff().SetTile(6, 1, TileState.Solid).SetTile(6, 2, TileState.Solid).SetTile(6, 3, TileState.Solid));
            Assert.AreEqual(-1, middle.abstractRoom.nodes[0].ConnectionCost(1, crit));
            for (int i = 0; i < 300; i++)
            {
                world.Update();
                ai.Update();
            }
            Assert.IsFalse(ai.Pather.CoordinateReachable(farNode), "the left room's pathfinder saw the change");
            Assert.IsTrue(ai.Pather.lookingForImpossiblePath);
        }

        // ---- Pre-processed exit data (CreatureSpecificAImap, node connectivity) ----

        [Test]
        public void ExitDistancesAndNodeConnectivityFollowTheFloor()
        {
            // A one-tile-high floor with a room exit at each end: node 0 enters at (1, 1), node 1 at (10, 1).
            var room = Parse(
                "############",
                "#..........#",
                "############");
            room.ApplyTerrainDiff(new TerrainDiff()
                .AddPipe(new Pipe(1, new[] { T(1, 1), T(0, 1) }, roomLink: 1))
                .AddPipe(new Pipe(2, new[] { T(10, 1), T(11, 1) }, roomLink: 2)));
            var crit = Get(Type.PinkLizard);
            var owner = crit.doPreBakedPathing ? crit : crit.preBakedPathingAncestor;
            var map = room.aimap;

            CollectionAssert.AreEqual(new[] { T(0, 1), T(11, 1) }, room.exitAndDenIndex);
            Assert.AreEqual(1, map.ExitDistanceForCreature(T(1, 1), 0, crit), "the entrance itself is step 1");
            Assert.AreEqual(5, map.ExitDistanceForCreature(T(5, 1), 0, crit));
            Assert.AreEqual(6, map.ExitDistanceForCreature(T(5, 1), 1, crit));
            Assert.AreEqual(-1, map.ExitDistanceForCreature(T(5, 0), 0, crit), "solid");
            Assert.IsTrue(map.ExitReachableFromTile(T(3, 1), 1, crit));

            // Walking from node 0's entrance to node 1's: 9 floor steps, each priced as the preprocessor prices them.
            float cost = 0f;
            for (int i = 0; i < 9; i++)
                cost += owner.ConnectionResistance(Move.Standard).resistance * 1 + owner.AccessibilityResistance(Acc.Floor).resistance;
            var nodes = room.abstractRoom.nodes;
            Assert.AreEqual((int)cost, nodes[0].ConnectionCost(1, crit));
            Assert.AreEqual(10, nodes[0].ConnectionLength(1, crit));
            Assert.AreEqual((int)cost, nodes[1].ConnectionCost(0, crit));
            Assert.AreEqual(-1, nodes[0].ConnectionCost(0, crit));

            // Every tile that leads to an exit seeds the accessibility map at generation 0, so scores 1.
            Assert.AreEqual(1f, map.AccessibilityForCreature(T(5, 1), crit));
            Assert.AreEqual(1f, map.AccessibilityForCreature(T(1, 1), crit));
            Assert.Less(map.AccessibilityForCreature(T(5, 2), crit), 1f, "reached only through connections from the floor");

            // An icon dropped on the floor cuts the nodes apart.
            room.ApplyTerrainDiff(new TerrainDiff().SetTile(5, 1, TileState.Solid));
            Assert.AreEqual(0, map.ExitDistanceForCreature(T(3, 1), 1, crit), "out of reach");
            Assert.AreEqual(3, map.ExitDistanceForCreature(T(3, 1), 0, crit));
            Assert.AreEqual(-1, room.abstractRoom.nodes[0].ConnectionCost(1, crit));
        }

        [Test]
        public void QuickPathFinderFindsTheTunnelPath()
        {
            var room = Parse(Shaft);
            var finder = new QuickPathFinder(T(1, 1), T(10, 5), room.aimap, Get(Type.PinkLizard));
            while (finder.status == 0)
                finder.Update();

            Assert.AreEqual(1, finder.status);
            var path = finder.ReturnPath().tiles;
            Assert.AreEqual(T(1, 1), path[0]);
            Assert.AreEqual(T(10, 5), path[path.Length - 1]);
            CollectionAssert.IsSubsetOf(new[] { T(5, 2), T(5, 3), T(5, 4) }, path);
            for (int i = 1; i < path.Length; i++)
                Assert.IsTrue(room.aimap.getAItile(path[i - 1]).outgoingPaths.Any(c => c.DestTile == path[i]), "each tile follows a connection");
            Assert.Greater(QuickConnectivity.Check(room, Get(Type.PinkLizard), T(1, 1), T(10, 5), 200), 0);
        }
    }
}
