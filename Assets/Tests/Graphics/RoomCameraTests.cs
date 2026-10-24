using System.Collections.Generic;
using NUnit.Framework;
using RWDE.Sim;

namespace RWDE.Graphics.Tests
{
    public class RoomCameraTests
    {
        /// <summary>A one-chunk sim object.</summary>
        private class TestCreature : PhysicalObject
        {
            public TestCreature(Room room, Vector2 pos)
            {
                this.room = room;
                bodyChunks = new[] { new BodyChunk(this, 0, pos, 5f, 0.1f) };
                bodyChunkConnections = new BodyChunkConnection[0];
            }
        }

        private sealed class OtherCreature : TestCreature
        {
            public OtherCreature(Room room, Vector2 pos) : base(room, pos)
            {
            }
        }

        private sealed class TestRock : PhysicalObject
        {
            public TestRock()
            {
                bodyChunks = new BodyChunk[0];
                bodyChunkConnections = new BodyChunkConnection[0];
            }
        }

        /// <summary>A body sprite drawn at the interpolated chunk position, plus an eye on top.</summary>
        private class TestGraphics : GraphicsModule
        {
            public const int BodySprite = 0;
            public const int EyeSprite = 1;

            public readonly List<string> Calls;
            public int Updates;

            public TestGraphics(PhysicalObject ow, List<string> calls) : base(ow)
            {
                Calls = calls;
            }

            public override void Update()
            {
                Updates++;
            }

            public override void InitiateSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam)
            {
                Calls.Add("InitiateSprites");
                sLeaser.sprites = new FSprite[2];
                sLeaser.sprites[BodySprite] = new FSprite("square");
                sLeaser.sprites[EyeSprite] = new FSprite("dot");
                AddToContainer(sLeaser, rCam, null);
            }

            public override void DrawSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos)
            {
                Calls.Add("DrawSprites " + owner.GetHashCode());
                var chunk = owner.firstChunk;
                sLeaser.sprites[BodySprite].SetPosition(Vector2.Lerp(chunk.lastPos, chunk.pos, timeStacker) - camPos);
                base.DrawSprites(sLeaser, rCam, timeStacker, camPos);
            }

            public override void ApplyPalette(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, RoomPalette palette)
            {
                Calls.Add("ApplyPalette");
                sLeaser.sprites[BodySprite].color = palette.blackColor;
            }
        }

        private sealed class OtherGraphics : TestGraphics
        {
            public OtherGraphics(PhysicalObject ow, List<string> calls) : base(ow, calls)
            {
            }
        }

        private Room _room;
        private List<string> _calls;
        private GraphicsModuleRegistry _registry;
        private RoomCamera _camera;

        [SetUp]
        public void SetUp()
        {
            TestAtlases.Install();
            _room = new Room(10, 10);
            _calls = new List<string>();
            _registry = new GraphicsModuleRegistry();
            _registry.Register<TestCreature>(c => new TestGraphics(c, _calls));
            _camera = new RoomCamera(new FStage(), _room, _registry);
        }

        [Test]
        public void LayersAreTheGamesInOrder()
        {
            Assert.AreEqual(15, _camera.stage.GetChildCount());
            Assert.AreSame(_camera.ReturnFContainer("Midground"), _camera.stage.GetChildAt(3));
            Assert.AreSame(_camera.ReturnFContainer("HUD2"), _camera.stage.GetChildAt(14));
            Assert.Throws<System.ArgumentException>(() => _camera.ReturnFContainer("NoSuchLayer"));
        }

        [Test]
        public void NewObjectInitiatesSpritesThenAppliesThePalette()
        {
            _camera.currentPalette = new RoomPalette { blackColor = new Color(0.1f, 0.1f, 0.1f) };
            var module = new TestGraphics(new TestCreature(_room, Vector2.zero), _calls);

            var leaser = _camera.NewObjectInRoom(module);

            Assert.AreEqual(new[] { "InitiateSprites", "ApplyPalette" }, _calls.ToArray());
            Assert.AreSame(_camera.ReturnFContainer("Midground"), leaser.sprites[0].container);
            Assert.AreEqual(new Color(0.1f, 0.1f, 0.1f), leaser.sprites[0].color);
        }

        [Test]
        public void DrawUpdateInterpolatesWithTimeStacker()
        {
            var creature = new TestCreature(_room, new Vector2(100f, 100f));
            creature.firstChunk.lastPos = new Vector2(80f, 100f);
            var leaser = _camera.NewObjectInRoom(new TestGraphics(creature, _calls));
            _camera.pos = new Vector2(10f, 0f);
            _camera.lastPos = new Vector2(10f, 0f);

            _camera.DrawUpdate(0.25f);

            Assert.AreEqual(new Vector2(75f, 100f), leaser.sprites[0].GetPosition());
        }

        [Test]
        public void DrawablesAreDrawnBackToFront()
        {
            var first = new TestCreature(_room, Vector2.zero);
            var second = new TestCreature(_room, Vector2.zero);
            _camera.NewObjectInRoom(new TestGraphics(first, _calls));
            _camera.NewObjectInRoom(new TestGraphics(second, _calls));
            _calls.Clear();

            _camera.DrawUpdate(1f);

            Assert.AreEqual(new[] { "DrawSprites " + second.GetHashCode(), "DrawSprites " + first.GetHashCode() }, _calls.ToArray());
        }

        [Test]
        public void SpritesBatchIntoTheStage()
        {
            _camera.NewObjectInRoom(new TestGraphics(new TestCreature(_room, new Vector2(50f, 50f)), _calls));
            _camera.NewObjectInRoom(new TestGraphics(new TestCreature(_room, new Vector2(90f, 50f)), _calls));
            _camera.DrawUpdate(1f);

            var batches = _camera.stage.BuildBatches();

            Assert.AreEqual(1, batches.Count, "both creatures' sprites share the test atlas");
            Assert.AreEqual(8, batches[0].TriangleCount);
        }

        [Test]
        public void UpdateGraphicsModulesCreatesOneModulePerRegisteredObject()
        {
            var creature = new TestCreature(_room, Vector2.zero);
            var objects = new PhysicalObject[] { creature, new TestRock() };

            _camera.UpdateGraphicsModules(objects);
            _camera.UpdateGraphicsModules(objects);

            Assert.AreEqual(1, _camera.SpriteLeasers.Count, "the rock has no registered module");
            var module = (TestGraphics)_camera.GraphicsModuleOf(creature);
            Assert.AreSame(creature, module.owner);
            Assert.AreEqual(2, module.Updates, "Update runs every tick");
        }

        [Test]
        public void TheMostDerivedRegistrationWins()
        {
            _registry.Register<OtherCreature>(c => new OtherGraphics(c, _calls));
            var other = new OtherCreature(_room, Vector2.zero);

            _camera.UpdateGraphicsModules(new PhysicalObject[] { other, new TestCreature(_room, Vector2.zero) });

            Assert.IsInstanceOf<OtherGraphics>(_camera.GraphicsModuleOf(other));
            Assert.IsNull(_registry.Create(new TestRock()));
        }

        [Test]
        public void ObjectsThatLeaveLoseTheirSprites()
        {
            var creature = new TestCreature(_room, Vector2.zero);
            _camera.UpdateGraphicsModules(new PhysicalObject[] { creature });
            var leaser = _camera.SpriteLeasers[0];

            _camera.UpdateGraphicsModules(new PhysicalObject[0]);
            _camera.DrawUpdate(1f);

            Assert.IsNull(leaser.sprites[0].container);
            Assert.AreEqual(0, _camera.SpriteLeasers.Count);
            Assert.IsNull(_camera.GraphicsModuleOf(creature));
        }

        [Test]
        public void DeletedOwnersAreRemovedOnTheNextDraw()
        {
            var creature = new TestCreature(_room, Vector2.zero);
            var leaser = _camera.NewObjectInRoom(new TestGraphics(creature, _calls));

            creature.Destroy();
            _camera.DrawUpdate(1f);

            Assert.IsNull(leaser.sprites[1].container);
            Assert.AreEqual(0, _camera.SpriteLeasers.Count);
        }

        [Test]
        public void OwnersInAnotherRoomAreRemoved()
        {
            var leaser = _camera.NewObjectInRoom(new TestGraphics(new TestCreature(new Room(5, 5), Vector2.zero), _calls));

            _camera.DrawUpdate(1f);

            Assert.IsTrue(leaser.deleteMeNextFrame);
            Assert.AreEqual(0, _camera.ReturnFContainer("Midground").GetChildCount());
        }

        [Test]
        public void ApplyPaletteRecoloursEveryDrawable()
        {
            var leaser = _camera.NewObjectInRoom(new TestGraphics(new TestCreature(_room, Vector2.zero), _calls));

            _camera.ApplyPalette(new RoomPalette { blackColor = new Color(0.3f, 0.2f, 0.1f) });

            Assert.AreEqual(new Color(0.3f, 0.2f, 0.1f), leaser.sprites[0].color);
        }

        [Test]
        public void AddToContainerMovesSpritesBetweenLayers()
        {
            var leaser = _camera.NewObjectInRoom(new TestGraphics(new TestCreature(_room, Vector2.zero), _calls));

            leaser.AddSpritesToContainer(_camera.ReturnFContainer("Foreground"), _camera);

            Assert.AreEqual(0, _camera.ReturnFContainer("Midground").GetChildCount());
            Assert.AreEqual(2, _camera.ReturnFContainer("Foreground").GetChildCount());
        }
    }
}
