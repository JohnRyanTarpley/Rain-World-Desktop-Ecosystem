using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Type = RWDE.Sim.CreatureTemplate.Type;

namespace RWDE.Sim.Tests.Creatures
{
    // The lizard body and AI on real rooms, checked against what the game's Lizard and LizardAI do:
    // breed setup from LizardBreedParams, standing on terrain, hunting and biting prey, fleeing threats.
    public class LizardTests
    {
        private static readonly StaticWorld Templates = StaticWorld.Init(StaticWorld.Dlc.All);

        private static readonly Type[] CoreBreeds =
        {
            Type.GreenLizard, Type.PinkLizard, Type.BlueLizard, Type.YellowLizard, Type.RedLizard, Type.SpitLizard, Type.ZoopLizard
        };

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

        // A closed box with a floor at row 1 (row 0 and the edges solid).
        private static Room Box(int width, int height)
        {
            var rows = new List<string>();
            rows.Add(new string('#', width));
            for (int i = 0; i < height - 2; i++)
                rows.Add("#" + new string('.', width - 2) + "#");
            rows.Add(new string('#', width));
            return Parse(rows.ToArray());
        }

        private static Lizard Realize(World world, Room room, Type type, IntVector2 tile, int id = 1)
        {
            var creature = new AbstractCreature(world, Get(type), room.GetWorldCoordinate(tile));
            SetId(creature, id);
            Assert.IsTrue(new CreatureRealizer().Realize(creature, room, tile, T(1, 0)), type + " realizes");
            return (Lizard)creature.realizedCreature;
        }

        private static void SetId(AbstractCreature creature, int id)
        {
            var field = typeof(AbstractCreature).GetField("ID");
            field.SetValue(creature, new EntityID(-1, id));
            creature.personality = new AbstractCreature.Personality(creature.ID);
        }

        // A plain one-chunk creature standing in for prey or a threat: a body without an AI.
        private sealed class Dummy : Creature
        {
            public Dummy(AbstractCreature abstractCreature, World world, float rad, float mass) : base(abstractCreature, world)
            {
                bodyChunks = new[] { new BodyChunk(this, 0, new Vector2(0f, 0f), rad, mass) };
                bodyChunkConnections = new BodyChunkConnection[0];
                airFriction = 0.999f;
                gravity = 0.9f;
                bounce = 0.1f;
                surfaceFriction = 0.4f;
                collisionLayer = 1;
            }

            public readonly List<PhysicalObject> collidedWith = new List<PhysicalObject>();

            public override void Collide(PhysicalObject otherObject, int myChunk, int otherChunk)
            {
                collidedWith.Add(otherObject);
                base.Collide(otherObject, myChunk, otherChunk);
            }
        }

        private static Dummy PlaceDummy(World world, Room room, Type type, IntVector2 tile, int id, float rad = 9f, float mass = 0.4f)
        {
            var creature = new AbstractCreature(world, Get(type), room.GetWorldCoordinate(tile));
            SetId(creature, id);
            var dummy = new Dummy(creature, world, rad, mass);
            dummy.PlaceInRoom(room);
            return dummy;
        }

        private static float Dist(Creature a, Creature b) => Vector2.Distance(a.mainBodyChunk.pos, b.mainBodyChunk.pos);

        // ---- Breeds ----

        [Test]
        public void TheSevenCoreBreedsRealizeWithTheirBreedParameters()
        {
            foreach (var type in CoreBreeds)
            {
                var room = Box(20, 8);
                var world = new World(1, room);
                var lizard = Realize(world, room, type, T(5, 1));
                var p = lizard.lizardParams;
                Assert.AreSame(Get(type).breedParameters, p, type.ToString());
                Assert.AreEqual(3, lizard.bodyChunks.Length);
                foreach (var chunk in lizard.bodyChunks)
                {
                    // Lizard constructor: rad 8 * bodySizeFac * bodyRadFac, mass bodyMass / 3.
                    Assert.AreEqual(8f * p.bodySizeFac * p.bodyRadFac, chunk.rad, 1e-5f, type + " chunk radius");
                    Assert.AreEqual(p.bodyMass / 3f, chunk.mass, 1e-5f, type + " chunk mass");
                }
                float length = 17f * p.bodyLengthFac * ((p.bodySizeFac + 1f) / 2f);
                Assert.AreEqual(length, lizard.bodyChunkConnections[0].distance, 1e-4f);
                Assert.AreEqual(length * (1f + p.bodyStiffnes), lizard.bodyChunkConnections[2].distance, 1e-4f);
                Assert.IsInstanceOf<LizardState>(lizard.abstractCreature.state);
                Assert.IsInstanceOf<LizardPather>(lizard.AI.pathFinder);
                Assert.AreSame(lizard.AI, lizard.abstractCreature.abstractAI.RealAI);
                Assert.Contains(lizard, room.updateList);
                Assert.Contains(lizard.abstractCreature, room.abstractRoom.creatures);
                Assert.AreEqual(LizardAI.Behavior.Idle, lizard.AI.behavior);
            }
        }

        [Test]
        public void BreedsWithoutPortedParametersStayAbstract()
        {
            var room = Box(20, 8);
            var world = new World(1, room);
            var white = new AbstractCreature(world, Get(Type.WhiteLizard), room.GetWorldCoordinate(T(5, 1)));
            Assert.IsFalse(new CreatureRealizer().Realize(white, room, T(5, 1), T(1, 0)));
            Assert.IsNull(white.realizedCreature);
            Assert.IsEmpty(room.updateList);
        }

        [Test]
        public void TheEffectColourComesFromTheCreatureId()
        {
            var room = Box(20, 8);
            var world = new World(1, room);
            var a = Realize(world, room, Type.PinkLizard, T(4, 1), 7);
            var b = Realize(world, room, Type.PinkLizard, T(8, 1), 7);
            var c = Realize(world, room, Type.PinkLizard, T(12, 1), 8);
            Assert.AreEqual(a.effectColor, b.effectColor);
            Assert.AreNotEqual(a.effectColor, c.effectColor);
        }

        // ---- Body ----

        [Test]
        public void ALizardDroppedInARoomLandsAndStaysOnItsFeet()
        {
            var room = Box(30, 12);
            var world = new World(3, room);
            var lizard = Realize(world, room, Type.GreenLizard, T(10, 8));
            for (int i = 0; i < 400; i++)
                world.Update();
            Assert.IsFalse(lizard.dead);
            Assert.IsNotNull(lizard.room);
            foreach (var chunk in lizard.bodyChunks)
            {
                Assert.Less(chunk.pos.y, 60f, "rests on the floor row");
                Assert.Greater(chunk.pos.y, 20f, "above the floor tiles");
            }
            Assert.Greater(lizard.inAllowedTerrainCounter, 0, "has footing");
        }

        [Test]
        public void ViolenceDamagesHealthAndStuns()
        {
            var room = Box(20, 8);
            var world = new World(1, room);
            var lizard = Realize(world, room, Type.GreenLizard, T(5, 1));
            var t = lizard.Template;
            lizard.Violence(null, new Vector2(0f, 0f), lizard.bodyChunks[1], CreatureTemplate.DamageType.Blunt, 1f, 0f);
            float expected = 1f / t.baseDamageResistance;
            if (t.damageRestistances[(int)CreatureTemplate.DamageType.Blunt, 0] > 0f)
                expected /= t.damageRestistances[(int)CreatureTemplate.DamageType.Blunt, 0];
            Assert.AreEqual(1f - expected, lizard.LizardState.health, 1e-5f);
            Assert.Greater(lizard.stun, 0);
        }

        [Test]
        public void AHitStraightOnTheHeadGlancesOffTheHeadShield()
        {
            var room = Box(20, 8);
            var world = new World(1, room);
            var lizard = Realize(world, room, Type.GreenLizard, T(5, 1));
            // Facing right: head to the right of the torso, jaws shut.
            lizard.bodyChunks[0].HardSetPosition(new Vector2(140f, 30f));
            lizard.bodyChunks[1].HardSetPosition(new Vector2(120f, 30f));
            lizard.bodyChunks[2].HardSetPosition(new Vector2(100f, 30f));
            lizard.JawOpen = 0f;
            var t = lizard.Template;
            float full = 1f / t.baseDamageResistance;
            if (t.damageRestistances[(int)CreatureTemplate.DamageType.Blunt, 0] > 0f)
                full /= t.damageRestistances[(int)CreatureTemplate.DamageType.Blunt, 0];
            lizard.Violence(null, new Vector2(-1f, 0f), lizard.bodyChunks[0], CreatureTemplate.DamageType.Blunt, 1f, 0f);
            Assert.AreEqual(1f - full * 0.1f, lizard.LizardState.health, 1e-5f, "head-on: a tenth of the damage");
            lizard.Violence(null, new Vector2(0f, -1f), lizard.bodyChunks[1], CreatureTemplate.DamageType.Blunt, 1f, 0f);
            Assert.AreEqual(1f - full * 1.1f, lizard.LizardState.health, 1e-5f, "body hit: full damage");
        }

        [Test]
        public void AnimationsOnlyGiveWayToMoreImportantOnesUnlessForced()
        {
            var room = Box(20, 8);
            var world = new World(1, room);
            var lizard = Realize(world, room, Type.GreenLizard, T(5, 1));
            lizard.EnterAnimation(Lizard.Animation.FightingStance, false);
            Assert.AreEqual(Lizard.Animation.FightingStance, lizard.animation);
            lizard.EnterAnimation(Lizard.Animation.HearSound, false);
            Assert.AreEqual(Lizard.Animation.FightingStance, lizard.animation, "a lesser animation waits");
            lizard.EnterAnimation(Lizard.Animation.ThreatSpotted, false);
            Assert.AreEqual(Lizard.Animation.ThreatSpotted, lizard.animation);
            lizard.EnterAnimation(Lizard.Animation.Standard, true);
            Assert.AreEqual(Lizard.Animation.Standard, lizard.animation);
            Assert.AreEqual(-1, lizard.timeToRemainInAnimation);
        }

        // ---- Hunting and fleeing ----

        [Test]
        public void APinkLizardHuntsDownPreyAndBitesIt()
        {
            var room = Box(40, 10);
            var world = new World(5, room);
            var lizard = Realize(world, room, Type.PinkLizard, T(4, 1));
            var prey = PlaceDummy(world, room, Type.Scavenger, T(28, 1), 2);
            bool hunted = false;
            bool caught = false;
            for (int i = 0; i < 4000 && !caught; i++)
            {
                world.Update();
                hunted |= lizard.AI.behavior == LizardAI.Behavior.Hunt;
                caught = lizard.grasps[0] != null && lizard.grasps[0].grabbed == prey;
            }
            Assert.IsTrue(hunted, "went hunting");
            Assert.IsTrue(caught, "caught the prey in its jaws; lizard at " + lizard.mainBodyChunk.pos + ", prey at " + prey.mainBodyChunk.pos);
            Assert.Contains(lizard.grasps[0], prey.grabbedBy);
        }

        [Test]
        public void ALizardFleesFromAVulture()
        {
            var room = Box(50, 10);
            var world = new World(9, room);
            var lizard = Realize(world, room, Type.PinkLizard, T(20, 1));
            var vulture = PlaceDummy(world, room, Type.Vulture, T(26, 1), 3, 20f, 5f);
            float start = Dist(lizard, vulture);
            bool fled = false;
            for (int i = 0; i < 600; i++)
            {
                world.Update();
                fled |= lizard.AI.behavior == LizardAI.Behavior.Flee;
            }
            Assert.IsTrue(fled, "fled");
            Assert.Greater(Dist(lizard, vulture), start + 100f, "got away");
        }

        [Test]
        public void TheSameSeedReplaysTheSameLizard()
        {
            Vector2 Run()
            {
                var room = Box(40, 10);
                var world = new World(11, room);
                var lizard = Realize(world, room, Type.BlueLizard, T(10, 1));
                for (int i = 0; i < 500; i++)
                    world.Update();
                return lizard.mainBodyChunk.pos;
            }

            Assert.AreEqual(Run(), Run());
        }

        // ---- Room objects, grasps and sight ----

        [Test]
        public void OverlappingObjectsArePushedApartAndToldOnce()
        {
            var room = Box(20, 8);
            var world = new World(1, room);
            var a = PlaceDummy(world, room, Type.Scavenger, T(5, 3), 1);
            var b = PlaceDummy(world, room, Type.Scavenger, T(5, 3), 2);
            a.mainBodyChunk.HardSetPosition(new Vector2(100f, 70f));
            b.mainBodyChunk.HardSetPosition(new Vector2(105f, 70f));
            room.Update();
            Assert.AreEqual(1, a.collidedWith.Count);
            Assert.AreSame(b, a.collidedWith[0]);
            Assert.AreSame(a, b.collidedWith[0]);
            Assert.Greater(Vector2.Distance(a.mainBodyChunk.pos, b.mainBodyChunk.pos), 5f);
        }

        [Test]
        public void AMoreDominantGraspCannotBeTakenOver()
        {
            var room = Box(20, 8);
            var world = new World(1, room);
            var first = Realize(world, room, Type.GreenLizard, T(4, 1), 1);
            var second = Realize(world, room, Type.PinkLizard, T(8, 1), 2);
            var prey = PlaceDummy(world, room, Type.Scavenger, T(6, 1), 3);
            Assert.IsTrue(first.Grab(prey, 0, 0, Creature.Grasp.Shareability.CanOnlyShareWithNonExclusive, 0.8f, true, true));
            Assert.IsFalse(second.Grab(prey, 0, 0, Creature.Grasp.Shareability.CanOnlyShareWithNonExclusive, 0.2f, true, true));
            Assert.IsTrue(second.Grab(prey, 0, 0, Creature.Grasp.Shareability.CanOnlyShareWithNonExclusive, 0.9f, true, true));
            Assert.IsNull(first.grasps[0], "the weaker hold broke");
            Assert.AreEqual(1, prey.grabbedBy.Count);
            second.ReleaseGrasp(0);
            Assert.IsEmpty(prey.grabbedBy);
            Assert.IsNull(second.grasps[0]);
        }

        [Test]
        public void WallsBlockLineOfSight()
        {
            var room = Parse(
                "##########",
                "#....#...#",
                "#....#...#",
                "##########");
            Assert.IsFalse(room.VisualContact(T(2, 1), T(7, 1)));
            Assert.IsTrue(room.VisualContact(T(1, 1), T(4, 2)));
            Assert.IsFalse(room.RayTraceTilesForTerrain(2, 1, 7, 1));
            Assert.IsTrue(room.RayTraceTilesForTerrain(1, 1, 4, 2));
        }

        [Test]
        public void ARealizedLizardCanBeAbstractizedAgain()
        {
            var room = Box(20, 8);
            var world = new World(1, room);
            var lizard = Realize(world, room, Type.YellowLizard, T(5, 1));
            var creature = lizard.abstractCreature;
            new CreatureRealizer().Abstractize(creature);
            Assert.IsNull(creature.realizedCreature);
            Assert.IsNull(creature.abstractAI.RealAI);
            Assert.IsEmpty(room.updateList);
            Assert.IsEmpty(room.abstractRoom.creatures);
        }

        [Test]
        public void ADestroyedLizardLeavesTheWorldsPopulation()
        {
            var room = Box(30, 8);
            var world = new World(3, room) { realizer = new CreatureRealizer() };
            var body = Realize(world, room, Type.GreenLizard, T(10, 1));
            var creature = body.abstractCreature;
            // Counted in the world's population as a spawned lizard that walked in would be.
            var population = (List<AbstractCreature>)typeof(World)
                .GetField("creatures", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .GetValue(world);
            population.Add(creature);
            world.Update();
            Assert.AreEqual(1, world.CreaturesInRooms.Count());

            // Room.CleanOutObjectNotInThisRoom destroys the abstract creature along with the body.
            body.Die();
            body.Destroy();
            world.Update();

            CollectionAssert.DoesNotContain(world.Creatures, creature, "no longer holds a slot under the population cap");
            Assert.AreEqual(0, world.CreaturesInRooms.Count());
            Assert.IsNull(creature.realizedCreature);
            CollectionAssert.DoesNotContain(room.updateList, body);
            CollectionAssert.DoesNotContain(room.abstractRoom.creatures, creature);
        }

        [Test]
        public void ADisplayFromAVisibleButUntrackedLizardDoesNotCrash()
        {
            // LizardAI.RecieveCommunication: on its 1-in-24 submission roll the game dereferences the sender's
            // representation without checking it. The tracker notices one creature a tick, so a lizard in
            // sight can still be untracked; the game throws there and Unity skips the rest.
            var room = Box(30, 8);
            var world = new World(4, room);
            var receiver = Realize(world, room, Type.GreenLizard, T(6, 1), 1);
            var sender = Realize(world, room, Type.GreenLizard, T(14, 1), 2);
            Assert.IsNull(receiver.AI.tracker.RepresentationForObject(sender, false), "the receiver hasn't noticed the sender");

            Assert.DoesNotThrow(() =>
            {
                for (int i = 0; i < 2000; i++)
                    receiver.AI.RecieveCommunication(sender, LizardAI.LizardCommunication.Agression);
            });
            Assert.IsNull(receiver.AI.submittedTo, "it can't submit to a lizard it doesn't track");
        }
    }
}
