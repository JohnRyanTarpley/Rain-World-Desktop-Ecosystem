using RWDE.Sim;

namespace RWDE.Graphics
{
    /// <summary>
    /// An element drawn on a free quad, as the game's <c>CustomFSprite</c>: the module places the four
    /// corners (top-left, top-right, bottom-right, bottom-left) and may colour each one.
    /// </summary>
    public class CustomFSprite : FSprite
    {
        public Vector2[] vertices = new Vector2[4];
        public Color[] verticeColors = { Futile.white, Futile.white, Futile.white, Futile.white };

        /// <exception cref="FutileException">No loaded atlas has an element named <paramref name="imageName"/>.</exception>
        public CustomFSprite(string imageName) : base(imageName)
        {
        }

        public void MoveVertice(int ind, Vector2 pos)
        {
            vertices[ind] = pos;
        }

        /// <summary>Kept for ported code; the quad is rebuilt every frame anyway.</summary>
        public void Refresh()
        {
        }

        protected override void PopulateRenderLayer(FRenderBatch batch)
        {
            // CustomFSprite.PopulateRenderLayer: triangles (0, 1, 2) and (0, 2, 3), raw corner colours.
            var m = _concatenatedMatrix;
            int first = batch.vertices.Count;
            batch.AddVertex(m.Apply(vertices[0]), _element.uvTopLeft, verticeColors[0]);
            batch.AddVertex(m.Apply(vertices[1]), _element.uvTopRight, verticeColors[1]);
            batch.AddVertex(m.Apply(vertices[2]), _element.uvBottomRight, verticeColors[2]);
            batch.AddVertex(m.Apply(vertices[3]), _element.uvBottomLeft, verticeColors[3]);
            batch.AddTriangle(first, first + 1, first + 2);
            batch.AddTriangle(first, first + 2, first + 3);
        }
    }
}
