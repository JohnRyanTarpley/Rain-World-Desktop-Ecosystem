using NUnit.Framework;
using RWDE.Sim.Physics;
using Type = RWDE.Sim.Physics.PhysicalObject.BodyChunkConnection.Type;

namespace RWDE.Sim.Tests.Physics
{
    // Expected values follow PhysicalObject.BodyChunkConnection.Update: each chunk moves (and gains velocity)
    // by (distance - currentDistance) * elasticity, chunk1 taking weightSymmetry of it and chunk2 the rest.
    public class BodyChunkConnectionTests
    {
        private const float Tolerance = 1e-4f;

        private static TestPair Pair(float x2, float mass1, float mass2, Type type, float elasticity, float weightSymmetry) =>
            new TestPair(new TestRoom(20, 20), new Vector2(100f, 100f), mass1, new Vector2(x2, 100f), mass2,
                20f, type, elasticity, weightSymmetry);

        [Test]
        public void WeightSymmetryDefaultsToMassShare()
        {
            var pair = Pair(130f, 1f, 3f, Type.Normal, 1f, -1f);

            Assert.AreEqual(0.75f, pair.bodyChunkConnections[0].weightSymmetry, 1e-6f);
        }

        [Test]
        public void NormalConnectionPullsStretchedChunksBackByMass()
        {
            var pair = Pair(130f, 1f, 3f, Type.Normal, 1f, -1f);

            pair.Update(true);

            // 10 px too long: the light chunk moves 7.5, the heavy one 2.5.
            Assert.AreEqual(107.5f, pair.bodyChunks[0].pos.x, Tolerance);
            Assert.AreEqual(7.5f, pair.bodyChunks[0].vel.x, Tolerance);
            Assert.AreEqual(127.5f, pair.bodyChunks[1].pos.x, Tolerance);
            Assert.AreEqual(-2.5f, pair.bodyChunks[1].vel.x, Tolerance);
            Assert.AreEqual(20f, Vector2.Distance(pair.bodyChunks[0].pos, pair.bodyChunks[1].pos), Tolerance);
        }

        [Test]
        public void ElasticityCorrectsOnlyPartOfTheError()
        {
            var pair = Pair(130f, 1f, 1f, Type.Normal, 0.5f, 0.5f);

            pair.Update(true);

            Assert.AreEqual(102.5f, pair.bodyChunks[0].pos.x, Tolerance);
            Assert.AreEqual(127.5f, pair.bodyChunks[1].pos.x, Tolerance);
        }

        [Test]
        public void PullOnlyActsWhenStretched()
        {
            var slack = Pair(110f, 1f, 1f, Type.Pull, 1f, 0.5f);
            slack.Update(true);
            Assert.AreEqual(100f, slack.bodyChunks[0].pos.x);
            Assert.AreEqual(110f, slack.bodyChunks[1].pos.x);

            var taut = Pair(130f, 1f, 1f, Type.Pull, 1f, 0.5f);
            taut.Update(true);
            Assert.AreEqual(105f, taut.bodyChunks[0].pos.x, Tolerance);
            Assert.AreEqual(125f, taut.bodyChunks[1].pos.x, Tolerance);
        }

        [Test]
        public void PushOnlyActsWhenCompressed()
        {
            var apart = Pair(130f, 1f, 1f, Type.Push, 1f, 0.5f);
            apart.Update(true);
            Assert.AreEqual(100f, apart.bodyChunks[0].pos.x);
            Assert.AreEqual(130f, apart.bodyChunks[1].pos.x);

            var close = Pair(110f, 1f, 1f, Type.Push, 0.5f, 0.5f);
            close.Update(true);
            Assert.AreEqual(97.5f, close.bodyChunks[0].pos.x, Tolerance);
            Assert.AreEqual(112.5f, close.bodyChunks[1].pos.x, Tolerance);
        }

        [Test]
        public void InactiveConnectionDoesNothing()
        {
            var pair = Pair(130f, 1f, 1f, Type.Normal, 1f, 0.5f);
            pair.bodyChunkConnections[0].active = false;

            pair.Update(true);

            Assert.AreEqual(100f, pair.bodyChunks[0].pos.x);
            Assert.AreEqual(130f, pair.bodyChunks[1].pos.x);
        }

        [Test]
        public void ConnectionSetsRotationChunks()
        {
            var pair = Pair(130f, 1f, 1f, Type.Normal, 1f, 0.5f);

            Assert.AreSame(pair.bodyChunks[1], pair.bodyChunks[0].rotationChunk);
            Assert.AreSame(pair.bodyChunks[0], pair.bodyChunks[1].rotationChunk);
            Assert.AreEqual(-1f, pair.bodyChunks[0].Rotation.x, 1e-6f);
            Assert.AreEqual(1f, pair.bodyChunks[1].Rotation.x, 1e-6f);
        }

        [Test]
        public void ConnectedChunksFallAndRestOnFloorAtTheirDistance()
        {
            // A two-chunk body (the game's gravity 0.9 and air friction 0.999) dropped onto a floor.
            var room = new TestRoom(20, 20).Fill(0, 0, 19, 1, TerrainType.Solid);
            var pair = new TestPair(room, new Vector2(100f, 120f), 1f, new Vector2(120f, 120f), 1f,
                20f, Type.Normal, 1f, 0.5f);
            pair.SetLocalGravity(0.9f);
            pair.SetLocalAirFriction(0.999f);

            for (int i = 0; i < 200; i++)
                pair.Update(i % 2 == 0);

            foreach (var chunk in pair.bodyChunks)
            {
                Assert.AreEqual(45f, chunk.pos.y, Tolerance);
                Assert.AreEqual(new IntVector2(0, -1), chunk.ContactPoint);
            }
            Assert.AreEqual(20f, Vector2.Distance(pair.bodyChunks[0].pos, pair.bodyChunks[1].pos), Tolerance);
        }
    }
}
