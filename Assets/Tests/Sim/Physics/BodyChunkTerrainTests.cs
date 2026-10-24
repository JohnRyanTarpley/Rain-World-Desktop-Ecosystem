using System;
using NUnit.Framework;
using RWDE.Sim.Physics;

namespace RWDE.Sim.Tests.Physics
{
    // Expected values are worked out by hand from the game's BodyChunk.Update and its collision checks,
    // using the game's Rock constants: rad 5, gravity 0.9, airFriction 0.999, bounce 0.4, surfaceFriction 0.4.
    // Per tick: vel.y -= 0.9; vel *= 0.999; pos += vel; then terrain collision.
    public class BodyChunkTerrainTests
    {
        private const float Tolerance = 1e-4f;

        // Tiles 0-1 solid: the floor's top surface is y = 40, so a resting rock sits at y = 45.
        private static TestRoom FloorRoom() => new TestRoom(20, 20).Fill(0, 0, 19, 1, TerrainType.Solid);

        private static void Tick(PhysicalObject obj, int ticks = 1)
        {
            for (int i = 0; i < ticks; i++)
                obj.Update(i % 2 == 0);
        }

        private static void AreEqual(float expected, float actual, string what) =>
            Assert.AreEqual(expected, actual, Tolerance, what);

        [Test]
        public void FallingChunkAcceleratesWithGravityAndAirFriction()
        {
            var rock = new TestRock(new TestRoom(20, 40), new Vector2(110f, 600f));
            var chunk = rock.firstChunk;

            Tick(rock);
            AreEqual(-0.8991f, chunk.vel.y, "vel.y after 1 tick");     // -0.9 * 0.999
            AreEqual(599.1009f, chunk.pos.y, "pos.y after 1 tick");
            Tick(rock);
            AreEqual(-1.7973009f, chunk.vel.y, "vel.y after 2 ticks"); // (-0.8991 - 0.9) * 0.999
            AreEqual(597.3035991f, chunk.pos.y, "pos.y after 2 ticks");
            Assert.AreEqual(new IntVector2(0, 0), chunk.ContactPoint);

            // Closed form after n ticks: v = -899.1 * (1 - 0.999^n), y = y0 + sum of v.
            Tick(rock, 18);
            double decay = Math.Pow(0.999, 20);
            Assert.AreEqual(-899.1 * (1 - decay), chunk.vel.y, 1e-3, "vel.y after 20 ticks");
            Assert.AreEqual(600 - 899.1 * (20 - 0.999 * (1 - decay) / 0.001), chunk.pos.y, 1e-2, "pos.y after 20 ticks");
            Assert.AreEqual(0f, chunk.vel.x);
        }

        [Test]
        public void FallingChunkLandsOnFloorAndBounces()
        {
            var rock = new TestRock(FloorRoom(), new Vector2(110f, 60f));
            var chunk = rock.firstChunk;
            chunk.vel = new Vector2(3f, -20f);

            Tick(rock);

            // Moves to (112.997, 39.1209), inside the floor; snapped to (1 + 1) * 20 + rad.
            AreEqual(112.997f, chunk.pos.x, "pos.x");
            AreEqual(45f, chunk.pos.y, "pos.y");
            AreEqual(20.8791f * 0.4f, chunk.vel.y, "vel.y bounces at speed * bounce");
            AreEqual(2.997f * 0.8f, chunk.vel.x, "vel.x scaled by surfaceFriction * 2");
            Assert.AreEqual(new IntVector2(0, -1), chunk.ContactPoint);
            Assert.AreEqual(1, rock.Impacts.Count);
            Assert.AreEqual(new IntVector2(0, -1), rock.Impacts[0].direction);
            AreEqual(20.8791f, rock.Impacts[0].speed, "impact speed");
            Assert.IsTrue(rock.Impacts[0].firstContact);
        }

        [Test]
        public void SlowLandingDoesNotBounce()
        {
            // A bounce under 1 + 9 * (1 - bounce) = 6.4 is killed.
            var rock = new TestRock(FloorRoom(), new Vector2(110f, 60f));
            var chunk = rock.firstChunk;
            chunk.vel = new Vector2(0f, -15f);

            Tick(rock);

            AreEqual(45f, chunk.pos.y, "pos.y");
            Assert.AreEqual(0f, chunk.vel.y);
            Assert.AreEqual(new IntVector2(0, -1), chunk.ContactPoint);
        }

        [Test]
        public void RestingChunkStaysPutWithFloorContact()
        {
            var rock = new TestRock(FloorRoom(), new Vector2(110f, 45f));
            var chunk = rock.firstChunk;

            for (int i = 0; i < 100; i++)
            {
                Tick(rock);
                Assert.AreEqual(110f, chunk.pos.x, "pos.x on tick " + i);
                Assert.AreEqual(45f, chunk.pos.y, "pos.y on tick " + i);
                Assert.AreEqual(0f, chunk.vel.y, "vel.y on tick " + i);
                Assert.AreEqual(new IntVector2(0, -1), chunk.ContactPoint, "contact on tick " + i);
            }
            // Gravity's 0.8991 per tick is under impactTreshhold, so resting makes no impacts.
            Assert.AreEqual(0, rock.Impacts.Count);
        }

        [Test]
        public void SlidingChunkSlowsByFloorFriction()
        {
            var rock = new TestRock(FloorRoom(), new Vector2(110f, 45f));
            var chunk = rock.firstChunk;
            chunk.vel = new Vector2(10f, 0f);

            // Each tick: vel.x *= 0.999 (air), pos.x += vel.x, then vel.x *= 0.8 (floor contact).
            Tick(rock);
            AreEqual(119.99f, chunk.pos.x, "pos.x after 1 tick");
            AreEqual(7.992f, chunk.vel.x, "vel.x after 1 tick");
            AreEqual(45f, chunk.pos.y, "pos.y after 1 tick");
            Tick(rock);
            AreEqual(127.974008f, chunk.pos.x, "pos.x after 2 ticks");
            AreEqual(6.3872064f, chunk.vel.x, "vel.x after 2 ticks");

            // Total slide: 9.99 / (1 - 0.999 * 0.8).
            Tick(rock, 198);
            Assert.AreEqual(110 + 9.99 / (1 - 0.7992), chunk.pos.x, 1e-3, "stopping point");
            AreEqual(45f, chunk.pos.y, "pos.y at rest");
            Assert.AreEqual(new IntVector2(0, -1), chunk.ContactPoint);
        }

        [Test]
        public void SlidingChunkStopsAgainstWall()
        {
            var room = FloorRoom().Fill(8, 2, 8, 5, TerrainType.Solid);
            var rock = new TestRock(room, new Vector2(150f, 45f));
            var chunk = rock.firstChunk;
            chunk.vel = new Vector2(10f, 0f);

            Tick(rock);

            // Reaches x = 159.99, inside tile 8; snapped to 8 * 20 - rad. The 7.992 * 0.4 rebound is under 6.4.
            AreEqual(155f, chunk.pos.x, "pos.x");
            AreEqual(45f, chunk.pos.y, "pos.y");
            Assert.AreEqual(0f, chunk.vel.x);
            Assert.AreEqual(new IntVector2(1, -1), chunk.ContactPoint);
            Assert.AreEqual(1, rock.Impacts.Count);
            Assert.AreEqual(new IntVector2(1, 0), rock.Impacts[0].direction);
            AreEqual(7.992f, rock.Impacts[0].speed, "impact speed");
        }

        [Test]
        public void FastChunkBouncesOffWall()
        {
            var room = new TestRoom(20, 20).Fill(8, 0, 8, 19, TerrainType.Solid);
            var rock = new TestRock(room, new Vector2(150f, 100f));
            var chunk = rock.firstChunk;
            chunk.vel = new Vector2(30f, 0f);

            Tick(rock);

            AreEqual(155f, chunk.pos.x, "pos.x");
            AreEqual(99.1009f, chunk.pos.y, "pos.y");
            AreEqual(-29.97f * 0.4f, chunk.vel.x, "vel.x rebounds");
            AreEqual(-0.8991f * 0.8f, chunk.vel.y, "vel.y scaled by wall friction");
            Assert.AreEqual(new IntVector2(1, 0), chunk.ContactPoint);
        }

        [Test]
        public void ChunkMovingLeftHitsWallOnItsLeft()
        {
            var room = FloorRoom().Fill(2, 2, 2, 5, TerrainType.Solid);
            var rock = new TestRock(room, new Vector2(70f, 45f));
            var chunk = rock.firstChunk;
            chunk.vel = new Vector2(-10f, 0f);

            Tick(rock);

            // Reaches x = 60.01; tile 2's right edge is 60, so snapped to 60 + rad.
            AreEqual(65f, chunk.pos.x, "pos.x");
            Assert.AreEqual(0f, chunk.vel.x);
            Assert.AreEqual(new IntVector2(-1, -1), chunk.ContactPoint);
            Assert.AreEqual(new IntVector2(-1, 0), rock.Impacts[0].direction);
        }

        [Test]
        public void RisingChunkHitsCeiling()
        {
            var room = new TestRoom(20, 20).Fill(0, 10, 19, 10, TerrainType.Solid);
            var rock = new TestRock(room, new Vector2(110f, 180f));
            var chunk = rock.firstChunk;
            chunk.vel = new Vector2(5f, 20f);

            Tick(rock);

            // vel.y = 19.1 * 0.999 = 19.0809 carries it to 199.0809; tile 10 starts at 200, so 200 - rad.
            AreEqual(195f, chunk.pos.y, "pos.y");
            AreEqual(-19.0809f * 0.4f, chunk.vel.y, "vel.y rebounds");
            AreEqual(4.995f * 0.8f, chunk.vel.x, "vel.x scaled by ceiling friction");
            Assert.AreEqual(new IntVector2(0, 1), chunk.ContactPoint);
            Assert.AreEqual(new IntVector2(0, 1), rock.Impacts[0].direction);
        }

        [Test]
        public void OneWayFloorHoldsFromAboveUnlessGoingThroughFloors()
        {
            var room = FloorRoom().Fill(0, 4, 19, 4, TerrainType.Floor);

            var rock = new TestRock(room, new Vector2(110f, 120f));
            Tick(rock, 100);
            AreEqual(105f, rock.firstChunk.pos.y, "rests on the platform at (4 + 1) * 20 + rad");
            Assert.AreEqual(new IntVector2(0, -1), rock.firstChunk.ContactPoint);

            var dropping = new TestRock(room, new Vector2(110f, 120f)) { GoThroughFloors = true };
            Tick(dropping, 100);
            AreEqual(45f, dropping.firstChunk.pos.y, "falls through to the solid floor");
        }

        [Test]
        public void ChunkJumpsUpThroughOneWayFloorAndLandsOnIt()
        {
            var room = FloorRoom().Fill(0, 4, 19, 4, TerrainType.Floor);
            var rock = new TestRock(room, new Vector2(110f, 60f));
            rock.firstChunk.vel = new Vector2(0f, 15f);

            Tick(rock, 200);

            AreEqual(105f, rock.firstChunk.pos.y, "pos.y");
        }

        [Test]
        public void ChunkOnSlopeSitsOnTheDiagonalAndSlidesDownhill()
        {
            // Tile (5, 2) is a slope with solid to its left and below: UpRight, high on the left.
            var room = FloorRoom().Fill(4, 2, 4, 2, TerrainType.Solid).Fill(5, 2, 5, 2, TerrainType.Slope);
            Assert.AreEqual(SlopeDirection.UpRight, room.IdentifySlope(5, 2));
            var rock = new TestRock(room, new Vector2(110f, 60f));
            var chunk = rock.firstChunk;

            Tick(rock);

            // Surface at x = 110: 20 - (110 - 100) + 40 = 50; the chunk rests 2 * rad above it.
            AreEqual(110f, chunk.pos.x, "pos.x");
            AreEqual(60f, chunk.pos.y, "pos.y");
            Assert.AreEqual(0f, chunk.vel.y);
            // vel.x += |vel.y| * clamp(0.5 - surfaceFriction) * downhill * 0.2
            AreEqual(0.8991f * 0.1f * 0.2f, chunk.vel.x, "vel.x");
            Assert.AreEqual(1, chunk.onSlope);
            Assert.AreEqual(new IntVector2(0, -1), chunk.ContactPoint);

            Tick(rock);

            AreEqual(110.017964f, chunk.pos.x, "pos.x after 2 ticks");
            AreEqual(60f - (chunk.pos.x - 110f), chunk.pos.y, "stays on the diagonal");
            AreEqual(0.017964018f * 0.6f + 0.017982f, chunk.vel.x, "vel.x after 2 ticks");
            Assert.AreEqual(1, chunk.onSlope);
        }

        [Test]
        public void ChunkLeavesRoomRangeClampedWhenNotGrabbed()
        {
            var room = new TestRoom(10, 10);
            var rock = new TestRock(room, new Vector2(100f, 100f));
            rock.firstChunk.vel = new Vector2(-2000f, 0f);

            Tick(rock);

            Assert.AreEqual(-1200f, rock.firstChunk.pos.x);
            Assert.AreEqual(0f, rock.firstChunk.vel.x);
        }

        [Test]
        public void ObjectGravityScalesWithRoomGravity()
        {
            var room = new TestRoom(10, 10) { gravity = 0.5f };
            var rock = new TestRock(room, new Vector2(100f, 100f));

            Assert.AreEqual(0.45f, rock.gravity, 1e-6f);
            Assert.AreEqual(0.9f, rock.GetLocalGravity(), 1e-6f);
        }

        [Test]
        public void IsTileSolidTreatsOneWayFloorAsSolidOnlyBelow()
        {
            var room = new TestRoom(10, 10).Fill(5, 4, 5, 4, TerrainType.Floor).Fill(6, 5, 6, 5, TerrainType.Solid);
            var rock = new TestRock(room, RoomGeometry.MiddleOfTile(5, 5));

            Assert.IsTrue(rock.IsTileSolid(0, 0, -1));
            Assert.IsTrue(rock.IsTileSolid(0, 1, 0));
            Assert.IsFalse(rock.IsTileSolid(0, -1, 0));
            rock.GoThroughFloors = true;
            Assert.IsFalse(rock.IsTileSolid(0, 0, -1));
        }

        [Test]
        public void SetPosIsAppliedOnTheNextUpdateInsteadOfVelocity()
        {
            var rock = new TestRock(new TestRoom(20, 20), new Vector2(100f, 100f));
            rock.evenUpdate = true;
            rock.firstChunk.MoveFromOutsideMyUpdate(false, new Vector2(200f, 300f));
            Assert.AreEqual(100f, rock.firstChunk.pos.x, "deferred while the object is out of step");

            Tick(rock);

            Assert.AreEqual(200f, rock.firstChunk.pos.x);
            Assert.AreEqual(300f, rock.firstChunk.pos.y);
            Assert.IsFalse(rock.firstChunk.setPos.HasValue);
        }

        [Test]
        public void TilePositionMatchesGameRounding()
        {
            Assert.AreEqual(new IntVector2(0, 0), RoomGeometry.GetTilePosition(new Vector2(0f, 19.99f)));
            Assert.AreEqual(new IntVector2(1, 1), RoomGeometry.GetTilePosition(new Vector2(20f, 20f)));
            // (int) truncates toward zero after the +20 offset, so -20 < x < 0 is tile -1 ... as in the game.
            Assert.AreEqual(new IntVector2(-1, -1), RoomGeometry.GetTilePosition(new Vector2(-0.5f, -19.5f)));
            Assert.AreEqual(new Vector2(110f, 50f), RoomGeometry.MiddleOfTile(5, 2));
        }
    }
}
