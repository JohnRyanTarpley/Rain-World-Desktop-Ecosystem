using System.Linq;
using NUnit.Framework;
using RWDE.Sim;

namespace RWDE.Graphics.Tests
{
    public class PlaceholderGraphicsTests
    {
        /// <summary>A two-chunk sim object joined by one connection.</summary>
        private sealed class TwoChunkCreature : PhysicalObject
        {
            public TwoChunkCreature(Room room, Vector2 head, Vector2 tail)
            {
                this.room = room;
                bodyChunks = new[] { new BodyChunk(this, 0, head, 8f, 0.2f), new BodyChunk(this, 1, tail, 5f, 0.1f) };
                bodyChunkConnections = new[]
                {
                    new BodyChunkConnection(bodyChunks[0], bodyChunks[1], 20f, BodyChunkConnection.Type.Normal, 1f, -1f),
                };
            }
        }

        private Room _room;
        private RoomCamera _camera;

        [SetUp]
        public void SetUp()
        {
            TestAtlases.Install();
            _room = new Room(10, 10);
            var registry = new GraphicsModuleRegistry();
            registry.Register<PhysicalObject>(o => new PlaceholderGraphics(o));
            _camera = new RoomCamera(new FStage(), _room, registry);
        }

        [Test]
        public void OneShapePerChunkAndConnection()
        {
            var creature = new TwoChunkCreature(_room, new Vector2(100f, 100f), new Vector2(100f, 80f));
            _camera.UpdateGraphicsModules(new PhysicalObject[] { creature });
            var module = (PlaceholderGraphics)_camera.GraphicsModuleOf(creature);
            var leaser = _camera.SpriteLeasers[0];

            Assert.AreEqual(3, leaser.sprites.Length);
            Assert.IsTrue(leaser.sprites.All(s => s.element.name == Futile.WhiteElementName));
            Assert.IsTrue(leaser.sprites.All(s => s.container == _camera.ReturnFContainer("Midground")));
            Assert.Less(_camera.ReturnFContainer("Midground").GetChildIndex(leaser.sprites[module.ConnectionSprite(0)]),
                _camera.ReturnFContainer("Midground").GetChildIndex(leaser.sprites[module.ChunkSprite(0)]),
                "connections draw behind the chunks");
        }

        [Test]
        public void ChunksAreDrawnAtTheirInterpolatedPositionAndSize()
        {
            var creature = new TwoChunkCreature(_room, new Vector2(100f, 100f), new Vector2(100f, 80f));
            creature.bodyChunks[0].lastPos = new Vector2(60f, 100f);
            creature.bodyChunks[1].lastPos = new Vector2(100f, 80f);
            var module = new PlaceholderGraphics(creature);
            var leaser = _camera.NewObjectInRoom(module);
            _camera.pos = _camera.lastPos = new Vector2(10f, 0f);

            _camera.DrawUpdate(0.5f);

            var head = leaser.sprites[module.ChunkSprite(0)];
            Assert.AreEqual(new Vector2(70f, 100f), head.GetPosition());
            Assert.AreEqual(16f, head.width, 1e-4f);
            Assert.AreEqual(16f, head.height, 1e-4f);
            Assert.AreEqual(10f, leaser.sprites[module.ChunkSprite(1)].width, 1e-4f);
        }

        [Test]
        public void ConnectionsSpanTheirChunks()
        {
            var creature = new TwoChunkCreature(_room, new Vector2(130f, 50f), new Vector2(100f, 50f));
            creature.bodyChunks[0].lastPos = creature.bodyChunks[0].pos;
            creature.bodyChunks[1].lastPos = creature.bodyChunks[1].pos;
            var module = new PlaceholderGraphics(creature);
            var leaser = _camera.NewObjectInRoom(module);

            _camera.DrawUpdate(1f);

            var bar = leaser.sprites[module.ConnectionSprite(0)];
            Assert.AreEqual(new Vector2(115f, 50f), bar.GetPosition());
            Assert.AreEqual(30f, bar.height, 1e-4f);
            Assert.AreEqual(PlaceholderGraphics.ConnectionWidth, bar.width, 1e-4f);
            Assert.AreEqual(-90f, bar.rotation, 1e-3f, "chunk1 to chunk2 points left");
            Assert.IsTrue(bar.isVisible);

            creature.bodyChunkConnections[0].active = false;
            _camera.DrawUpdate(1f);
            Assert.IsFalse(bar.isVisible, "inactive connections are hidden");
        }

        [Test]
        public void ShapesTakeThePalettesBlack()
        {
            var leaser = _camera.NewObjectInRoom(new PlaceholderGraphics(new TwoChunkCreature(_room, Vector2.zero, new Vector2(0f, 20f))));

            _camera.ApplyPalette(new RoomPalette { blackColor = new Color(0.2f, 0.1f, 0.05f) });

            Assert.IsTrue(leaser.sprites.All(s => s.color == new Color(0.2f, 0.1f, 0.05f)));
        }

        [Test]
        public void ShapesBatchIntoOneDrawCall()
        {
            _camera.UpdateGraphicsModules(new PhysicalObject[]
            {
                new TwoChunkCreature(_room, new Vector2(50f, 50f), new Vector2(50f, 30f)),
                new TwoChunkCreature(_room, new Vector2(90f, 50f), new Vector2(90f, 30f)),
            });
            _camera.DrawUpdate(1f);

            var batches = _camera.stage.BuildBatches();

            Assert.AreEqual(1, batches.Count);
            Assert.AreEqual(12, batches[0].TriangleCount, "six quads");
        }

        [Test]
        public void TheShippedGraphicsAssemblyHasNoPlaceholderFallback()
        {
            var placeholders = typeof(GraphicsModule).Assembly.GetTypes()
                .Where(t => t.Name.Contains("Placeholder") || t.Name.Contains("Fallback"))
                .Select(t => t.FullName)
                .ToArray();

            CollectionAssert.IsEmpty(placeholders, "placeholder shapes are test-only; the app requires a Rain World install");
        }
    }
}
