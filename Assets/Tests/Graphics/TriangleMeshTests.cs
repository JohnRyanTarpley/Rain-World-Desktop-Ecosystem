using NUnit.Framework;
using RWDE.Sim;

namespace RWDE.Graphics.Tests
{
    public class TriangleMeshTests
    {
        private const float Tolerance = 1e-5f;
        private FStage _stage;

        [SetUp]
        public void SetUp()
        {
            TestAtlases.Install();
            _stage = new FStage();
        }

        [Test]
        public void LongMeshHasTwoVerticesPerSegment()
        {
            var mesh = TriangleMesh.MakeLongMesh(3, false, false);

            // (segments - 1) * 4 + 2 triangles over 12 vertices.
            Assert.AreEqual(10, mesh.triangles.Length);
            Assert.AreEqual(12, mesh.vertices.Length);
            Assert.AreEqual(Futile.WhiteElementName, mesh.element.name);
            Assert.AreEqual(new Vector2(0f, 0f), mesh.UVvertices[0]);
            Assert.AreEqual(new Vector2(1f, 0.2f), mesh.UVvertices[3]);
            Assert.AreEqual(new Vector2(1f, 1f), mesh.UVvertices[11]);
        }

        [Test]
        public void PointyLongMeshEndsInOneVertex()
        {
            var mesh = TriangleMesh.MakeLongMesh(3, true, false);

            Assert.AreEqual(9, mesh.triangles.Length);
            Assert.AreEqual(11, mesh.vertices.Length);
            Assert.AreEqual(0.5f, mesh.UVvertices[10].x, Tolerance);
        }

        [Test]
        public void GridTrianglesCoverEveryQuad()
        {
            var tris = TriangleMesh.GridTriangles(2, 1);

            Assert.AreEqual(4, tris.Length);
            Assert.AreEqual(new[] { 0, 1, 2 }, new[] { tris[0].a, tris[0].b, tris[0].c });
            Assert.AreEqual(new[] { 2, 1, 3 }, new[] { tris[1].a, tris[1].b, tris[1].c });
            Assert.AreEqual(new[] { 4, 3, 5 }, new[] { tris[3].a, tris[3].b, tris[3].c });
        }

        [Test]
        public void OneToOneLongMeshSpreadsUvsAlongItsLength()
        {
            var mesh = TriangleMesh.MakeOneToOneLongMesh(3, false);

            Assert.AreEqual(6, mesh.vertices.Length);
            Assert.AreEqual(new Vector2(1f, 0.5f), mesh.UVvertices[3]);
            Assert.AreEqual(new Vector2(0f, 1f), mesh.UVvertices[4]);
        }

        [Test]
        public void MovedVerticesAreDrawnInTheMeshSpace()
        {
            var mesh = new TriangleMesh("square", new[] { new TriangleMesh.Triangle(0, 1, 2) }, false) { x = 100f };
            mesh.MoveVertice(0, new Vector2(0f, 0f));
            mesh.MoveVertice(1, new Vector2(10f, 0f));
            mesh.MoveVertice(2, new Vector2(0f, 10f));
            mesh.color = new Color(0.2f, 0.4f, 0.6f);
            mesh.alpha = 0.5f;
            _stage.AddChild(mesh);

            var batch = _stage.BuildBatches()[0];

            Assert.AreEqual(3, batch.vertices.Count);
            Assert.AreEqual(new Vector2(110f, 0f), batch.vertices[1]);
            Assert.AreEqual(new Vector2(100f, 10f), batch.vertices[2]);
            Assert.AreEqual(0.4f, batch.colors[0].g, Tolerance);
            Assert.AreEqual(0.5f, batch.colors[0].a, Tolerance);
        }

        [Test]
        public void CustomColorMeshUsesPerVertexColours()
        {
            var mesh = new TriangleMesh("square", new[] { new TriangleMesh.Triangle(0, 1, 2), new TriangleMesh.Triangle(2, 1, 3) }, true);
            mesh.color = new Color(0f, 0f, 1f);
            mesh.verticeColors[3] = new Color(1f, 0f, 0f);
            mesh.alpha = 0.25f;
            _stage.AddChild(mesh);

            var batch = _stage.BuildBatches()[0];

            Assert.AreEqual(6, batch.vertices.Count, "three vertices per triangle");
            Assert.AreEqual(1f, batch.colors[0].b, Tolerance, "color sets every vertex colour");
            Assert.AreEqual(1f, batch.colors[5].r, Tolerance, "vertex 3 is the last of triangle 1");
            Assert.AreEqual(0.25f, batch.colors[5].a, Tolerance, "alpha sets every vertex's alpha");
        }

        [Test]
        public void CustomFSpriteDrawsItsFourCorners()
        {
            var sprite = new CustomFSprite("square");
            sprite.MoveVertice(0, new Vector2(0f, 10f));
            sprite.MoveVertice(1, new Vector2(20f, 10f));
            sprite.MoveVertice(2, new Vector2(20f, 0f));
            sprite.MoveVertice(3, new Vector2(0f, 0f));
            sprite.verticeColors[2] = new Color(0f, 1f, 0f);
            _stage.AddChild(sprite);

            var batch = _stage.BuildBatches()[0];

            Assert.AreEqual(new Vector2(20f, 0f), batch.vertices[2]);
            Assert.AreEqual(sprite.element.uvBottomRight, batch.uvs[2]);
            Assert.AreEqual(new Color(0f, 1f, 0f), batch.colors[2]);
            Assert.AreEqual(new[] { 0, 1, 2, 0, 2, 3 }, batch.triangles.ToArray());
        }
    }
}
