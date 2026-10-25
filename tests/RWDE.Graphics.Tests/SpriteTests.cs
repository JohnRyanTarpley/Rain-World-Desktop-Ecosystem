using NUnit.Framework;
using RWDE.Sim;

namespace RWDE.Graphics.Tests
{
    public class SpriteTests
    {
        private const float Tolerance = 1e-4f;
        private FStage _stage;

        [SetUp]
        public void SetUp()
        {
            TestAtlases.Install();
            _stage = new FStage();
        }

        private static void AssertVertex(FRenderBatch batch, int index, float x, float y)
        {
            Assert.AreEqual(x, batch.vertices[index].x, Tolerance, "x of vertex " + index);
            Assert.AreEqual(y, batch.vertices[index].y, Tolerance, "y of vertex " + index);
        }

        [Test]
        public void SpriteIsAQuadCentredOnItsPosition()
        {
            var sprite = new FSprite("square") { x = 100f, y = 50f };
            _stage.AddChild(sprite);

            var batch = _stage.BuildBatches()[0];

            Assert.AreEqual(4, batch.vertices.Count);
            Assert.AreEqual(new[] { 0, 1, 2, 0, 2, 3 }, batch.triangles.ToArray());
            AssertVertex(batch, 0, 84f, 66f);
            AssertVertex(batch, 1, 116f, 66f);
            AssertVertex(batch, 2, 116f, 34f);
            AssertVertex(batch, 3, 84f, 34f);
            Assert.AreEqual(sprite.element.uvTopLeft, batch.uvs[0]);
            Assert.AreEqual(sprite.element.uvBottomLeft, batch.uvs[3]);
        }

        [Test]
        public void AnchorMovesTheQuad()
        {
            var sprite = new FSprite("square");
            sprite.SetAnchor(0f, 0f);
            _stage.AddChild(sprite);

            var batch = _stage.BuildBatches()[0];

            AssertVertex(batch, 0, 0f, 32f);
            AssertVertex(batch, 2, 32f, 0f);
        }

        [Test]
        public void RotationIsClockwiseInDegrees()
        {
            var sprite = new FSprite("square") { rotation = 90f };
            _stage.AddChild(sprite);

            var batch = _stage.BuildBatches()[0];

            // The top-left corner (-16, 16) turns a quarter clockwise to (16, 16).
            AssertVertex(batch, 0, 16f, 16f);
            AssertVertex(batch, 1, 16f, -16f);
        }

        [Test]
        public void ContainersTransformTheirChildren()
        {
            var container = new FContainer { x = 10f, scale = 2f };
            var sprite = new FSprite("square") { x = 5f, scaleY = 0.5f };
            container.AddChild(sprite);
            _stage.AddChild(container);

            var batch = _stage.BuildBatches()[0];

            // Centre at 10 + 2 * 5 = 20; half-size 2 * 16 = 32 wide and 2 * 0.5 * 16 = 16 high.
            AssertVertex(batch, 0, -12f, 16f);
            AssertVertex(batch, 2, 52f, -16f);
        }

        [Test]
        public void TrimmedElementIsDrawnWhereItSatInTheUntrimmedImage()
        {
            var sprite = new FSprite("dot");

            var r = sprite.localRect;

            Assert.AreEqual(-5f, r.x, Tolerance);
            Assert.AreEqual(-5f, r.y, Tolerance);
            Assert.AreEqual(8f, r.width, Tolerance);
            Assert.AreEqual(12f, sprite.textureRect.width, Tolerance);
        }

        [Test]
        public void WidthAndHeightSetTheScale()
        {
            var sprite = new FSprite("square") { width = 64f, height = 16f };

            Assert.AreEqual(2f, sprite.scaleX, Tolerance);
            Assert.AreEqual(0.5f, sprite.scaleY, Tolerance);
        }

        [Test]
        public void AlphaMultipliesDownTheTree()
        {
            var container = new FContainer { alpha = 0.5f };
            var sprite = new FSprite("square") { color = new Color(1f, 0.5f, 0.25f, 0.8f), alpha = 0.5f };
            container.AddChild(sprite);
            _stage.AddChild(container);

            var c = _stage.BuildBatches()[0].colors[0];

            Assert.AreEqual(0.5f, c.g, Tolerance);
            Assert.AreEqual(0.25f, c.b, Tolerance);
            Assert.AreEqual(0.8f * 0.5f * 0.5f, c.a, Tolerance);
        }

        [Test]
        public void HiddenNodesAreNotDrawn()
        {
            var hiddenContainer = new FContainer { isVisible = false };
            hiddenContainer.AddChild(new FSprite("square"));
            _stage.AddChild(hiddenContainer);
            _stage.AddChild(new FSprite("square") { isVisible = false });

            Assert.AreEqual(0, _stage.BuildBatches().Count);
        }

        [Test]
        public void ConsecutiveSpritesOnOneAtlasShareABatch()
        {
            _stage.AddChild(new FSprite("square"));
            _stage.AddChild(new FSprite("dot"));

            var batches = _stage.BuildBatches();

            Assert.AreEqual(1, batches.Count);
            Assert.AreEqual(4, batches[0].TriangleCount);
            Assert.AreEqual(new[] { 4, 5, 6, 4, 6, 7 }, batches[0].triangles.GetRange(6, 6).ToArray());
        }

        [Test]
        public void AtlasOrShaderChangesSplitBatchesInDrawOrder()
        {
            _stage.AddChild(new FSprite("square"));
            _stage.AddChild(new FSprite("other"));
            _stage.AddChild(new FSprite("dot"));
            _stage.AddChild(new FSprite("dot") { shader = FShader.ByName("LizardSkin") });

            var batches = _stage.BuildBatches();

            Assert.AreEqual(4, batches.Count);
            Assert.AreEqual("testAtlas", batches[0].atlas.name);
            Assert.AreEqual("otherAtlas", batches[1].atlas.name);
            Assert.AreEqual("testAtlas", batches[2].atlas.name);
            Assert.AreSame(FShader.defaultShader, batches[2].shader);
            Assert.AreSame(FShader.ByName("LizardSkin"), batches[3].shader);
        }

        [Test]
        public void BuildingAgainReflectsChanges()
        {
            var sprite = new FSprite("square");
            _stage.AddChild(sprite);
            _stage.BuildBatches();

            sprite.x = 10f;
            sprite.SetElementByName("other");
            var batch = _stage.BuildBatches()[0];

            Assert.AreEqual("otherAtlas", batch.atlas.name);
            Assert.AreEqual(4, batch.vertices.Count);
            AssertVertex(batch, 0, 8f, 2f);
        }

        [Test]
        public void SetElementByNameThrowsForUnknownNames()
        {
            var sprite = new FSprite("square");

            Assert.Throws<FutileException>(() => sprite.SetElementByName("noSuchElement"));
            Assert.Throws<FutileException>(() => new FSprite("noSuchElement"));
        }

        [Test]
        public void ChildOrderControlsDrawOrder()
        {
            var a = new FSprite("square");
            var b = new FSprite("square");
            var c = new FSprite("square");
            var container = new FContainer();
            container.AddChild(a);
            container.AddChild(b);
            container.AddChild(c);

            container.AddChild(a);
            Assert.AreEqual(new FNode[] { b, c, a }, Children(container), "AddChild of a child moves it to the front");

            a.MoveToBack();
            Assert.AreEqual(new FNode[] { a, b, c }, Children(container));

            c.MoveBehindOtherNode(b);
            Assert.AreEqual(new FNode[] { a, c, b }, Children(container));

            a.MoveInFrontOfOtherNode(b);
            Assert.AreEqual(new FNode[] { c, b, a }, Children(container));

            container.AddChildAtIndex(a, 1);
            Assert.AreEqual(new FNode[] { c, a, b }, Children(container));
        }

        [Test]
        public void ANodeLivesInOneContainer()
        {
            var sprite = new FSprite("square");
            var first = new FContainer();
            var second = new FContainer();
            first.AddChild(sprite);

            second.AddChild(sprite);

            Assert.AreEqual(0, first.GetChildCount());
            Assert.AreSame(second, sprite.container);

            sprite.RemoveFromContainer();
            Assert.IsNull(sprite.container);
            Assert.AreEqual(0, second.GetChildCount());
        }

        [Test]
        public void RemoveAllChildrenDetachesThem()
        {
            var sprite = new FSprite("square");
            var container = new FContainer();
            container.AddChild(sprite);

            container.RemoveAllChildren();

            Assert.IsNull(sprite.container);
            Assert.AreEqual(0, container.GetChildCount());
        }

        private static FNode[] Children(FContainer container)
        {
            var nodes = new FNode[container.GetChildCount()];
            for (int i = 0; i < nodes.Length; i++) nodes[i] = container.GetChildAt(i);
            return nodes;
        }
    }
}
