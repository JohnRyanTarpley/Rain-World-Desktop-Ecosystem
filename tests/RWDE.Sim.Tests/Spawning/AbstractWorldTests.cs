using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Type = RWDE.Sim.CreatureTemplate.Type;

namespace RWDE.Sim.Tests.Spawning
{
    // The offscreen population and its traffic through the desktop room's exits. Timings follow
    // AbstractCreature.InDenUpdate, AbstractCreatureAI.MigrationBehavior and FollowPath in the game.
    public class AbstractWorldTests
    {
        private static readonly StaticWorld All = StaticWorld.Init(StaticWorld.Dlc.All);

        private sealed class FakeRealizer : ICreatureRealizer
        {
            public bool Accept = true;
            public readonly List<(AbstractCreature creature, IntVector2 tile, IntVector2 inward)> Realized = new List<(AbstractCreature, IntVector2, IntVector2)>();
            public readonly List<AbstractCreature> Placed = new List<AbstractCreature>();
            public readonly List<AbstractCreature> Abstracted = new List<AbstractCreature>();
            /// <summary>The creature's position when each Realize call was made, accepted or not.</summary>
            public readonly List<WorldCoordinate> PosWhenRealized = new List<WorldCoordinate>();
            public IntVector2 PlaceAt = new IntVector2(6, 1);

            public bool Realize(AbstractCreature creature, Room room, IntVector2 tile, IntVector2 inward)
            {
                PosWhenRealized.Add(creature.pos);
                if (Accept) Realized.Add((creature, tile, inward));
                return Accept;
            }

            public bool RealizePlaced(AbstractCreature creature, Room room, out IntVector2 tile)
            {
                tile = PlaceAt;
                if (Accept) Placed.Add(creature);
                return Accept;
            }

            public void Abstractize(AbstractCreature creature) => Abstracted.Add(creature);
        }

        // A 16x10 room: solid floor, solid side walls above y = 3 and a solid ceiling with an opening
        // at x = 4..11. The open edge tiles are left and right at y = 1..3 and the sky opening.
        private static Room DesktopRoom()
        {
            const int w = 16, h = 10;
            var diff = new TerrainDiff();
            for (int x = 0; x < w; x++)
            {
                diff.SetTile(x, 0, TileState.Solid);
                if (x < 4 || x > 11) diff.SetTile(x, h - 1, TileState.Solid);
            }
            for (int y = 4; y < h; y++)
            {
                diff.SetTile(0, y, TileState.Solid);
                diff.SetTile(w - 1, y, TileState.Solid);
            }
            var room = new Room(w, h);
            room.ApplyTerrainDiff(diff);
            return room;
        }

        private static SpawnTable Table(params string[] creatureLines)
        {
            var lines = new List<string> { "ROOMS", "YY_A01 : YY_A02 : SWARMROOM", "YY_A02 : YY_A01", "END ROOMS", "CREATURES" };
            lines.AddRange(creatureLines);
            lines.Add("END CREATURES");
            return new RegionSpawnData("yy", lines).BuildTable("White");
        }

        private static (World world, FakeRealizer realizer, Room room) Setup(int seed, params string[] creatureLines)
        {
            var room = DesktopRoom();
            var world = new World(All, Table(creatureLines), seed, room);
            var realizer = new FakeRealizer();
            world.realizer = realizer;
            world.GeneratePopulation();
            return (world, realizer, room);
        }

        private static int RunUntil(World world, System.Func<bool> done, int maxTicks = 20000)
        {
            for (int t = 0; t < maxTicks; t++)
            {
                if (done()) return t;
                world.Update();
            }
            return -1;
        }

        [Test]
        public void PopulationComesFromTheSpawnTable()
        {
            var (world, _, _) = Setup(1, "YY_A01 : 2-Green, 3-Pink-2", "OFFSCREEN : 0-Scavenger-3", "YY_A02 : 4-Mimic-{12}");

            var types = world.Creatures.Select(c => c.creatureTemplate.type).ToList();
            Assert.AreEqual(1, types.Count(t => t == Type.GreenLizard));
            Assert.AreEqual(2, types.Count(t => t == Type.PinkLizard));
            Assert.AreEqual(3, types.Count(t => t == Type.Scavenger));
            Assert.IsTrue(world.Creatures.All(c => c.InDen));

            // AbstractCreature constructor: 1 tick in the offscreen den, 100-999 in a room den, none for plants.
            foreach (var c in world.Creatures)
            {
                if (c.creatureTemplate.type == Type.PoleMimic) Assert.AreEqual(0, c.remainInDenCounter);
                else if (c.spawnDen.Offscreen) Assert.AreEqual(1, c.remainInDenCounter);
                else Assert.That(c.remainInDenCounter, Is.InRange(100, 999));
            }
            Assert.AreEqual("{12}", world.Creatures.Single(c => c.creatureTemplate.type == Type.PoleMimic).spawnData);
            Assert.AreEqual(world.Creatures.Count, world.Creatures.Select(c => c.ID).Distinct().Count());
        }

        [Test]
        public void SpawnsWhoseDlcIsOffAreLeftOut()
        {
            var table = Table("YY_A01 : 2-Caramel, 3-Green", "OFFSCREEN : 0-MirosVulture-2");
            var world = new World(StaticWorld.Init(StaticWorld.Dlc.BaseOnly), table, 1, DesktopRoom());
            world.GeneratePopulation();

            CollectionAssert.AreEqual(new[] { Type.GreenLizard }, world.Creatures.Select(c => c.creatureTemplate.type));
        }

        [Test]
        public void SwarmRoomsAndDenFliesAreCounted()
        {
            var (world, _, _) = Setup(1, "YY_A02 : 3-Fly-4");

            CollectionAssert.AreEqual(new[] { 10 }, world.SwarmRoomFlies);
            Assert.AreEqual(4, world.DenFlies);
            Assert.IsEmpty(world.Creatures);
        }

        [Test]
        public void NoneLineageStepRollsOnAndSpawnsNextCycle()
        {
            var (world, _, _) = Setup(1, "LINEAGE : YY_A02 : 2 : NONE-1, EggBug-0");
            var lineage = (World.Lineage)world.SpawnTable.Spawners[0];

            Assert.IsEmpty(world.Creatures);
            Assert.AreEqual(1, world.LineageCounter(lineage));

            world.GeneratePopulation();
            Assert.AreEqual(Type.EggBug, world.Creatures.Single().creatureTemplate.type);
            Assert.AreEqual(lineage.SpawnerID, world.Creatures.Single().ID.spawner);
        }

        [Test]
        public void GroundCreatureWalksInAtAScreenEdge()
        {
            var (world, realizer, room) = Setup(3, "OFFSCREEN : 0-Green-3");

            Assert.AreNotEqual(-1, RunUntil(world, () => realizer.Realized.Count == 3));
            foreach (var (creature, tile, inward) in realizer.Realized)
            {
                Assert.IsTrue(creature.Realized);
                Assert.AreSame(room, creature.Room.realizedRoom);
                Assert.AreEqual(room.GetWorldCoordinate(tile), creature.pos);
                Assert.AreEqual(1, tile.y, "a lizard can only stand on the floor tiles of the edge openings");
                Assert.That(tile.x, Is.EqualTo(0).Or.EqualTo(room.Width - 1));
                Assert.AreEqual(tile.x == 0 ? new IntVector2(1, 0) : new IntVector2(-1, 0), inward);
                Assert.AreEqual(tile, creature.entryTile);
            }
        }

        [Test]
        public void SkyExitsTakeOnlyCreaturesThatMapThem()
        {
            var room = DesktopRoom();
            var exits = RoomExit.FindExits(room);
            var sky = exits.Single(e => e.Side == RoomExit.Edge.Top);

            Assert.AreEqual(3, exits.Count);
            Assert.AreEqual(AbstractRoomNode.Type.SkyExit, sky.NodeType);
            CollectionAssert.AreEqual(Enumerable.Range(4, 8).Select(x => new IntVector2(x, 9)), sky.Tiles);
            Assert.IsFalse(sky.UsableBy(All.GetCreatureTemplate(Type.GreenLizard)));
            Assert.IsTrue(sky.UsableBy(All.GetCreatureTemplate(Type.Vulture)));
            var left = exits.Single(e => e.Side == RoomExit.Edge.Left);
            CollectionAssert.AreEqual(new[] { new IntVector2(0, 1), new IntVector2(0, 2), new IntVector2(0, 3) }, left.Tiles);
            Assert.AreEqual(AbstractRoomNode.Type.Exit, left.NodeType);
        }

        [Test]
        public void VultureComesInFromTheSky()
        {
            var (world, realizer, room) = Setup(5, "OFFSCREEN : 0-Vulture-2");

            Assert.AreNotEqual(-1, RunUntil(world, () => realizer.Realized.Count == 2));
            foreach (var (creature, tile, inward) in realizer.Realized)
            {
                Assert.IsTrue(creature.exit.UsableBy(creature.creatureTemplate));
                Assert.IsTrue(room.aimap.TileAccessibleToCreature(tile, creature.creatureTemplate));
                if (creature.exit.Side == RoomExit.Edge.Top) Assert.AreEqual(new IntVector2(0, -1), inward);
            }
        }

        [Test]
        public void SameSeedReplaysTheSameArrivals()
        {
            string Run(int seed)
            {
                var (world, realizer, _) = Setup(seed, "OFFSCREEN : 0-Green-4", "YY_A01 : 2-Pink-3", "OFFSCREEN : 0-Vulture-1");
                var log = new List<string>();
                for (int t = 0; t < 6000; t++)
                {
                    int before = realizer.Realized.Count;
                    world.Update();
                    for (int i = before; i < realizer.Realized.Count; i++)
                        log.Add(t + ":" + realizer.Realized[i].creature.ID + "@" + realizer.Realized[i].tile);
                }
                return string.Join(" ", log);
            }

            Assert.AreEqual(Run(11), Run(11));
            Assert.AreNotEqual(Run(11), Run(12));
        }

        [Test]
        public void LeavingThroughAnExitMakesTheCreatureAbstractAgain()
        {
            var (world, realizer, _) = Setup(2, "OFFSCREEN : 0-Green");
            RunUntil(world, () => realizer.Realized.Count == 1);
            var lizard = world.Creatures.Single();
            var exit = lizard.exit;

            world.CreatureLeftRoom(lizard, exit);

            Assert.AreEqual(AbstractCreature.Place.Offscreen, lizard.place);
            Assert.IsNull(lizard.Room);
            Assert.AreEqual(AbstractCreature.OffscreenPos, lizard.pos);
            Assert.AreSame(exit, lizard.exit);
            CollectionAssert.AreEqual(new[] { lizard }, realizer.Abstracted);
            Assert.Throws<System.InvalidOperationException>(() => world.CreatureLeftRoom(lizard, exit));

            // It roams offscreen and comes back.
            Assert.AreNotEqual(-1, RunUntil(world, () => realizer.Realized.Count == 2));
        }

        [Test]
        public void PopulationCapHoldsArrivalsOffscreen()
        {
            var (world, realizer, _) = Setup(2, "OFFSCREEN : 0-Green-3");
            world.populationCap = 1;

            for (int t = 0; t < 4000; t++) world.Update();

            Assert.AreEqual(1, realizer.Realized.Count);
            Assert.AreEqual(1, world.CreaturesInRooms.Count());
        }

        [Test]
        public void NothingEntersWithoutARealizerOrWhenItRefuses()
        {
            var (world, realizer, _) = Setup(2, "OFFSCREEN : 0-Green-2");
            realizer.Accept = false;
            for (int t = 0; t < 2000; t++) world.Update();
            Assert.IsEmpty(world.CreaturesInRooms);

            world.realizer = null;
            for (int t = 0; t < 2000; t++) world.Update();
            Assert.IsEmpty(world.CreaturesInRooms);
        }

        [Test]
        public void PreCycleCreaturesWaitForThePreCycle()
        {
            var (world, _, _) = Setup(2, "OFFSCREEN : 0-Salamander-{PreCycle}");
            var salamander = world.Creatures.Single();
            Assert.IsTrue(salamander.preCycle);

            for (int t = 0; t < 100; t++) world.Update();
            Assert.IsTrue(salamander.InDen);

            var (world2, _, _) = Setup(2, "OFFSCREEN : 0-Salamander-{PreCycle}");
            world2.preCycleActive = true;
            world2.Update();
            world2.Update();
            Assert.IsFalse(world2.Creatures.Single().InDen);
        }

        [Test]
        public void NightCreaturesWaitForTheNight()
        {
            var (world, _, _) = Setup(2, "OFFSCREEN : 0-MirosVulture-{Night}");
            Assert.IsTrue(world.Creatures.Single().nightCreature);

            for (int t = 0; t < 100; t++) world.Update();
            Assert.IsTrue(world.Creatures.Single().InDen);

            world.nightActive = true;
            world.Update();
            world.Update();
            Assert.IsFalse(world.Creatures.Single().InDen);
        }

        [Test]
        public void CreaturesStayInTheirDensWhenTheRainIsNear()
        {
            var (world, _, _) = Setup(2, "OFFSCREEN : 0-Green");
            world.timeUntilRain = 60 * 40 - 1;
            world.Update();
            world.Update();

            var lizard = world.Creatures.Single();
            Assert.IsTrue(lizard.InDen);
            Assert.AreEqual(-1, lizard.remainInDenCounter);
        }

        [Test]
        public void PlantsArePlacedInTheRoomInsteadOfWalkingIn()
        {
            var (world, realizer, room) = Setup(2, "YY_A02 : 4-Mimic-{12}, 5-TentaclePlant");

            Assert.AreNotEqual(-1, RunUntil(world, () => realizer.Placed.Count == 2));
            Assert.IsEmpty(realizer.Realized);
            Assert.IsTrue(world.Creatures.All(c => c.Realized && c.Room.realizedRoom == room && c.exit == null));

            // The plant is where the realizer put it, not at a coordinate with no tile.
            foreach (var plant in world.Creatures)
            {
                Assert.AreEqual(room.GetWorldCoordinate(realizer.PlaceAt), plant.pos);
                Assert.AreEqual(plant.pos, plant.abstractAI.destination);
            }
        }

        [Test]
        public void ACreatureIsInItsRoomBeforeItIsRealized()
        {
            // A realizer that builds the creature's AI reads its position, so it must be the entry tile.
            var (world, realizer, room) = Setup(3, "OFFSCREEN : 0-Green");
            RunUntil(world, () => realizer.Realized.Count == 1);
            var (lizard, tile, _) = realizer.Realized.Single();

            Assert.AreEqual(room.GetWorldCoordinate(tile), realizer.PosWhenRealized.Single());
            Assert.AreEqual(lizard.pos, lizard.abstractAI.destination);

            // Refused, it goes back offscreen.
            var (refused, refuser, _) = Setup(3, "OFFSCREEN : 0-Green");
            refuser.Accept = false;
            RunUntil(refused, () => refuser.PosWhenRealized.Count == 1);
            refused.Update();
            var green = refused.Creatures.Single();
            Assert.AreNotEqual(AbstractCreature.OffscreenPos, refuser.PosWhenRealized.Single());
            Assert.AreEqual(AbstractCreature.OffscreenPos, green.pos);
            Assert.AreEqual(AbstractCreature.OffscreenPos, green.abstractAI.destination);
        }

        [Test]
        public void InfantCentipedesComeOutOfTheirDenIntoTheRoom()
        {
            // Their template never roams between rooms, so the generic roam roll would hold them offscreen for good.
            var (world, realizer, _) = Setup(2, "YY_A02 : 4-Small Centipede-3");
            Assert.That(All.GetCreatureTemplate(Type.SmallCentipede).roamBetweenRoomsChance, Is.LessThan(0f));

            Assert.AreNotEqual(-1, RunUntil(world, () => realizer.Realized.Count == 3, 3000));

            // One that leaves doesn't wander back in.
            var centipede = world.Creatures.First();
            world.CreatureLeftRoom(centipede, centipede.exit);
            for (int t = 0; t < 20000; t++) world.Update();
            Assert.AreEqual(AbstractCreature.Place.Offscreen, centipede.place);
            Assert.AreEqual(3, realizer.Realized.Count);
        }

        [Test]
        public void AnInfantCentipedeTurnedAwayTriesAgain()
        {
            // The realizer refuses the first try, as when the exit is shut on the way; that trip never
            // got in, so it doesn't count as leaving.
            var (world, realizer, _) = Setup(2, "YY_A02 : 4-Small Centipede");
            realizer.Accept = false;
            Assert.AreNotEqual(-1, RunUntil(world, () => realizer.PosWhenRealized.Count > 0, 3000));

            realizer.Accept = true;
            Assert.AreNotEqual(-1, RunUntil(world, () => realizer.Realized.Count == 1, 3000));
        }

        [Test]
        public void ScavengersComeInOnTheirOwnMigrationTimer()
        {
            var (world, realizer, _) = Setup(4, "OFFSCREEN : 0-Scavenger-3");
            Assert.That(All.GetCreatureTemplate(Type.Scavenger).roamBetweenRoomsChance, Is.LessThan(0f));
            foreach (var scav in world.Creatures)
                Assert.That(scav.dontMigrate, Is.InRange(400, 4799), "ScavengerAbstractAI: Random.Range(400, 4800)");

            int first = RunUntil(world, () => realizer.Realized.Count > 0, 60000);
            Assert.That(first, Is.GreaterThanOrEqualTo(400), "no scavenger sets off before its dontMigrate runs out");
            Assert.AreNotEqual(-1, RunUntil(world, () => realizer.Realized.Count == 3, 60000));
        }

        [Test]
        public void PersonalityComesFromTheID()
        {
            var a = new AbstractCreature.Personality(new EntityID(0, 7));
            var b = new AbstractCreature.Personality(new EntityID(3, 7));
            var c = new AbstractCreature.Personality(new EntityID(0, 8));

            Assert.AreEqual(a, b, "the game seeds it from the ID's number");
            Assert.AreNotEqual(a, c);
            foreach (var p in new[] { a, c })
                foreach (var trait in new[] { p.energy, p.bravery, p.sympathy, p.dominance, p.nervous, p.aggression })
                    Assert.That(trait, Is.InRange(0f, 1f));
        }

        [Test]
        public void SwitchingTablesStartsLineagesOver()
        {
            // White's lineage in the den has moved on to step 1; Red's lineage in the same den has only step 0.
            var data = new RegionSpawnData("yy", new[]
            {
                "ROOMS", "YY_A01 : YY_A02", "YY_A02 : YY_A01", "END ROOMS", "CREATURES",
                "(White)LINEAGE : YY_A02 : 2 : NONE-1, EggBug-0", "(Red)LINEAGE : YY_A02 : 2 : Red-0", "END CREATURES"
            });
            var world = World.Create(All, data, "White", 1, null, DesktopRoom());
            Assert.AreEqual(1, world.LineageCounter((World.Lineage)world.SpawnTable.Spawners[0]));

            world.SetSpawnTable(data.BuildTable("Red"));
            world.GeneratePopulation();

            Assert.AreEqual(Type.RedLizard, world.Creatures.Single().creatureTemplate.type);
        }

        [Test]
        public void ArrivalsComeIntoEveryRoomOfTheWorld()
        {
            var left = DesktopRoom();
            var right = DesktopRoom();
            var world = new World(All, Table("OFFSCREEN : 0-Green-12"), 4, left, right);
            var realizer = new FakeRealizer();
            world.realizer = realizer;
            world.GeneratePopulation();

            Assert.AreNotEqual(-1, RunUntil(world, () => realizer.Realized.Count == 12));
            CollectionAssert.AreEquivalent(new[] { 0, 1 }, world.Creatures.Select(c => c.pos.room).Distinct());
        }

        [Test]
        public void PathfindingCreaturesOutsideTheSpawnerAreLeftAlone()
        {
            var room = DesktopRoom();
            var world = new World(1, room);
            var lizard = new AbstractCreature(world, All.GetCreatureTemplate(Type.GreenLizard), room.GetWorldCoordinate(new IntVector2(3, 1)));

            world.Update();

            Assert.IsTrue(lizard.Realized);
            Assert.IsNull(world.SpawnTable);
            Assert.IsEmpty(world.Creatures);
            Assert.Throws<System.InvalidOperationException>(() => world.GeneratePopulation());
            Assert.Throws<System.InvalidOperationException>(() => world.CreatureLeftRoom(lizard, null));
        }

        [Test]
        public void ClosedExitTurnsTravellersBack()
        {
            var (world, realizer, room) = Setup(2, "OFFSCREEN : 0-Green");
            var lizard = world.Creatures.Single();
            RunUntil(world, () => lizard.place == AbstractCreature.Place.Travelling);

            // Desktop icons fill both edge openings while the lizard is on its way.
            var diff = new TerrainDiff();
            for (int y = 1; y <= 3; y++)
            {
                diff.SetTile(0, y, TileState.Solid);
                diff.SetTile(room.Width - 1, y, TileState.Solid);
            }
            room.ApplyTerrainDiff(diff);
            for (int t = 0; t < 2000; t++) world.Update();

            Assert.IsEmpty(realizer.Realized);
            Assert.AreNotEqual(AbstractCreature.Place.InRoom, lizard.place);
        }

        [Test]
        public void NewPopulationReplacesOffscreenCreaturesOnly()
        {
            var (world, realizer, _) = Setup(2, "OFFSCREEN : 0-Green");
            RunUntil(world, () => realizer.Realized.Count == 1);
            var inRoom = world.Creatures.Single();

            world.GeneratePopulation();

            Assert.AreEqual(2, world.Creatures.Count);
            CollectionAssert.Contains(world.Creatures, inRoom);
            Assert.AreEqual(1, world.Creatures.Count(c => c.InDen));
        }

        [Test]
        public void CreateBuildsTheCampaignTableAndPopulation()
        {
            var data = new RegionSpawnData("yy", new[]
            {
                "ROOMS", "YY_A01 : YY_A02", "END ROOMS", "CREATURES",
                "(White)YY_A01 : 2-Green", "(Red)YY_A01 : 3-Red", "END CREATURES"
            });

            var white = World.Create(All, data, "White", 1, null, DesktopRoom());
            var all = World.Create(All, data, Campaign.AllOutskirtsCreatures, 1, null, DesktopRoom());

            CollectionAssert.AreEqual(new[] { Type.GreenLizard }, white.Creatures.Select(c => c.creatureTemplate.type));
            CollectionAssert.AreEquivalent(new[] { Type.GreenLizard, Type.RedLizard }, all.Creatures.Select(c => c.creatureTemplate.type));

            white.SetSpawnTable(data.BuildTable("Red"));
            white.GeneratePopulation();
            CollectionAssert.AreEqual(new[] { Type.RedLizard }, white.Creatures.Select(c => c.creatureTemplate.type));
        }

        [Test]
        public void SpawnDataFlagsAreRead()
        {
            var (world, _, _) = Setup(1, "OFFSCREEN : 0-Green-{Ignorecycle,AlternateForm|Winter,17}");
            var c = world.Creatures.Single();

            Assert.IsTrue(c.ignoreCycle);
            Assert.IsTrue(c.superSizeMe);
            Assert.IsTrue(c.Winterized);
            CollectionAssert.AreEqual(new[] { "17" }, c.unrecognizedFlags);
        }
    }
}
