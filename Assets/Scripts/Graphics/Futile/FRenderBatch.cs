using System.Collections.Generic;
using RWDE.Sim;

namespace RWDE.Graphics
{
    /// <summary>
    /// A run of consecutive triangles sharing one atlas texture and shader, in draw order: one Unity mesh
    /// and one draw call (Futile's <c>FFacetRenderLayer</c>). Positions are in stage space (pixels).
    /// </summary>
    public sealed class FRenderBatch
    {
        public readonly List<Vector2> vertices = new List<Vector2>();
        public readonly List<Vector2> uvs = new List<Vector2>();
        public readonly List<Color> colors = new List<Color>();
        public readonly List<int> triangles = new List<int>();

        public FAtlas atlas { get; private set; }
        public FShader shader { get; private set; }

        public int TriangleCount => triangles.Count / 3;

        internal void Reset(FAtlas newAtlas, FShader newShader)
        {
            atlas = newAtlas;
            shader = newShader;
            vertices.Clear();
            uvs.Clear();
            colors.Clear();
            triangles.Clear();
        }

        public void AddVertex(Vector2 position, Vector2 uv, Color color)
        {
            vertices.Add(position);
            uvs.Add(uv);
            colors.Add(color);
        }

        public void AddTriangle(int a, int b, int c)
        {
            triangles.Add(a);
            triangles.Add(b);
            triangles.Add(c);
        }
    }
}
