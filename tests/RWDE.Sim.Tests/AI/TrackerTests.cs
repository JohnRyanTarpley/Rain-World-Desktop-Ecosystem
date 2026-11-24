using System.Linq;
using NUnit.Framework;
using Rel = RWDE.Sim.CreatureTemplate.Relationship;
using Type = RWDE.Sim.CreatureTemplate.Type;

namespace RWDE.Sim.Tests.AI
{
    // The tracker family (Tracker, RelationshipTracker, PreyTracker, ThreatTracker, AgressionTracker)
    // on a test AI, checked against what the game's modules do with the same sightings.
    public class TrackerTests
    {
        private static readonly StaticWorld Templates = StaticWorld.Init(StaticWorld.Dlc.All);

        /// <summary>A one-chunk creature that floats where it is put.</summary>
        private sealed class Dummy : Creature
        {
            public Dummy(AbstractCreature abstractCreature, World world) : base(abstractCreature, world)
            {
                bodyChunks = new[] { new BodyChunk(this, 0, new Vector2(0f, 0f), 8f, 0.3f) };
                bodyChunkConnections = new BodyChunkConnection[0];
                airFriction = 0.9f;
                gravity = 0f;
                collisionLayer = 1;
                CollideWithObjects = false;
            }
        }

        private sealed class TestAI : ArtificialIntelligence, IUseARelationshipTracker
        {
            public TestAI(AbstractCreature creature, World world) : base(creature)
            {
                AddModule(new StandardPather(this, world, creature));
                AddModule(new Tracker(this, 10, 10, 600, 0.5f, 5, 5, 10));
                AddModule(new PreyTracker(this, 5, 2f, 3f, 70f, 0.5f));
                AddModule(new ThreatTracker(this, 3));
                AddModule(new AgressionTracker(this, 0.001f, 0.001f));
                AddModule(new RelationshipTracker(this, tracker));
            }

            public override Tracker.CreatureRepresentation CreateTrackerRepresentationForCreature(AbstractCreature otherCreature) =>
                new Tracker.ElaborateCreatureRepresentation(tracker, otherCreature, 1f, 5);

            public AIModule ModuleToTrackRelationship(Rel relationship)
            {
                switch (relationship.type)
                {
                    case Rel.Type.Afraid: return threatTracker;
                    case Rel.Type.Eats:
                    case Rel.Type.Attacks: return preyTracker;
                    case Rel.Type.AgressiveRival: return agressionTracker;
                    default: return null;
                }
            }

            public Rel UpdateDynamicRelationship(RelationshipTracker.DynamicRelationship dRelation) =>
                StaticRelationship(dRelation.trackerRep.representedCreature);

            public RelationshipTracker.TrackedCreatureState CreateTrackedCreatureState(RelationshipTracker.DynamicRelationship rel) => null;
        }

        private static Room OpenRoom()
        {
            var room = new Room(40, 12);
            room.Fill(0, 0, 39, 0, Room.Tile.TerrainType.Solid);
            return room;
        }

        private static Dummy Place(World world, Room room, CreatureTemplate template, int x, int y)
        {
            var crit = new AbstractCreature(world, template, room.GetWorldCoordinate(new IntVector2(x, y)));
            var body = new Dummy(crit, world);
            body.PlaceInRoom(room);
            foreach (var c in body.bodyChunks)
            {
                c.pos = room.MiddleOfTile(x, y);
                c.lastPos = c.pos;
                c.lastLastPos = c.pos;
            }
            return body;
        }

        private static void Tick(Room room, TestAI ai, int ticks)
        {
            for (int i = 0; i < ticks; i++)
            {
                room.world.Update(); // also updates the room, as the game does each tick
                ai.Update();
            }
        }

        // The observer: a creature with sight that eats one type and fears another.
        private static (CreatureTemplate observer, CreatureTemplate prey, CreatureTemplate threat) Cast()
        {
            var observer = Templates.GetCreatureTemplate(Type.PinkLizard);
            Assert.Greater(observer.visualRadius, 0f);
            var all = Templates.CreatureTemplates.ToList();
            var prey = all.First(t => observer.CreatureRelationship(t).type == Rel.Type.Eats && observer.CreatureRelationship(t).intensity > 0.5f);
            var threat = all.First(t => observer.CreatureRelationship(t).type == Rel.Type.Afraid && observer.CreatureRelationship(t).intensity > 0.5f);
            return (observer, prey, threat);
        }

        [Test]
        public void ACreatureInSightIsTrackedAndSortedAsPrey()
        {
            var (observerT, preyT, _) = Cast();
            var room = OpenRoom();
            var world = new World(1, room);
            var me = Place(world, room, observerT, 5, 1);
            var ai = new TestAI(me.abstractCreature, world);
            ai.NewRoom(room);
            var prey = Place(world, room, preyT, 12, 1);

            Tick(room, ai, 60);

            var rep = ai.tracker.RepresentationForCreature(prey.abstractCreature, false);
            Assert.IsNotNull(rep, "seen and tracked");
            Assert.IsTrue(rep.VisualContact);
            Assert.AreEqual(Rel.Type.Eats, rep.dynamicRelationship.currentRelationship.type);
            Assert.AreSame(ai.preyTracker, rep.dynamicRelationship.trackedByModule);
            Assert.AreSame(rep, ai.preyTracker.MostAttractivePrey);
            Assert.Greater(ai.preyTracker.Utility(), 0f, "hunting is worth something");
        }

        [Test]
        public void AThreatInSightMakesTheThreatTrackerWantToFlee()
        {
            var (observerT, _, threatT) = Cast();
            var room = OpenRoom();
            var world = new World(2, room);
            var me = Place(world, room, observerT, 5, 1);
            var ai = new TestAI(me.abstractCreature, world);
            ai.NewRoom(room);
            Assert.AreEqual(0f, ai.threatTracker.Utility());
            var threat = Place(world, room, threatT, 9, 1);

            Tick(room, ai, 60);

            var rep = ai.tracker.RepresentationForCreature(threat.abstractCreature, false);
            Assert.IsNotNull(rep);
            Assert.AreSame(rep, ai.threatTracker.mostThreateningCreature);
            Assert.Greater(ai.threatTracker.Utility(), 0.5f);
            Assert.Greater(ai.threatTracker.ThreatOfTile(threat.abstractCreature.pos, false), ai.threatTracker.ThreatOfTile(room.GetWorldCoordinate(new IntVector2(38, 1)), false),
                "threat falls off with distance");
        }

        [Test]
        public void ACreatureBehindAWallIsNotNoticed()
        {
            var (observerT, preyT, _) = Cast();
            var room = OpenRoom().Fill(20, 1, 20, 11, Room.Tile.TerrainType.Solid);
            var world = new World(3, room);
            var me = Place(world, room, observerT, 5, 1);
            var ai = new TestAI(me.abstractCreature, world);
            ai.NewRoom(room);
            var prey = Place(world, room, preyT, 25, 1);

            Tick(room, ai, 100);

            Assert.IsNull(ai.tracker.RepresentationForCreature(prey.abstractCreature, false));
        }

        [Test]
        public void ACreatureThatGoesOutOfSightIsFollowedByGhostsAndFinallyForgotten()
        {
            var (observerT, preyT, _) = Cast();
            var room = OpenRoom().Fill(20, 1, 20, 11, Room.Tile.TerrainType.Solid);
            var world = new World(4, room);
            var me = Place(world, room, observerT, 5, 1);
            var ai = new TestAI(me.abstractCreature, world);
            ai.NewRoom(room);
            var prey = Place(world, room, preyT, 15, 1);
            Tick(room, ai, 40);
            var rep = (Tracker.ElaborateCreatureRepresentation)ai.tracker.RepresentationForCreature(prey.abstractCreature, false);
            Assert.IsNotNull(rep);

            // It slips behind the wall.
            foreach (var c in prey.bodyChunks)
            {
                c.pos = room.MiddleOfTile(30, 1);
                c.lastPos = c.pos;
                c.lastLastPos = c.pos;
            }
            Tick(room, ai, 20);

            Assert.IsFalse(rep.VisualContact);
            Assert.Less(rep.EstimatedChanceOfFinding, 1f);
            // The game's seeAroundCorners: for 10 ticks after the last sighting the tracker still knows where it went.
            Assert.AreEqual(new IntVector2(30, 1), rep.BestGuessForPosition().Tile);
            Assert.GreaterOrEqual(rep.ghosts.Count, 1);

            // framesToRememberCreatures is 600 here (the lizard's is -1: never forgets). Prey isn't forgotten
            // while the prey tracker follows it, as it keeps resetting the forget counter.
            Tick(room, ai, 700);
            Assert.AreSame(rep, ai.tracker.RepresentationForCreature(prey.abstractCreature, false));

            ai.preyTracker.ForgetPrey(prey.abstractCreature);
            Tick(room, ai, 700);
            Assert.IsNull(ai.tracker.RepresentationForCreature(prey.abstractCreature, false));
        }

        [Test]
        public void AngerAtARivalRisesTowardItsBaseAndSetAngerOverrides()
        {
            var (observerT, preyT, _) = Cast();
            var room = OpenRoom();
            var world = new World(5, room);
            var me = Place(world, room, observerT, 5, 1);
            var ai = new TestAI(me.abstractCreature, world);
            ai.NewRoom(room);
            var rival = Place(world, room, preyT, 7, 1);
            Tick(room, ai, 30);
            var rep = ai.tracker.RepresentationForCreature(rival.abstractCreature, false);
            Assert.IsNotNull(rep);

            ai.agressionTracker.AddCreature(rep, 0.1f, 1f);
            Tick(room, ai, 200);
            Assert.AreSame(rep, ai.agressionTracker.AgressionTarget());
            Assert.Greater(ai.agressionTracker.highestAnger, 0.1f);

            ai.agressionTracker.SetAnger(rep, 0f, 0f);
            ai.agressionTracker.Update();
            Assert.AreEqual(0f, ai.agressionTracker.Utility());
        }
    }
}
