using RWDE.Sim;

namespace RWDE.Graphics
{
    /// <summary>
    /// Free-form triangles over shared vertices, as the game's <c>TriangleMesh</c>: bodies, tails, tongues
    /// and limbs are strips whose vertices the graphics module moves every frame. Vertices are in the
    /// mesh's local space; UVs are raw texture coordinates, as in the game.
    /// </summary>
    public class TriangleMesh : FSprite
    {
        public struct Triangle
        {
            public int a;
            public int b;
            public int c;

            public Triangle(int a, int b, int c)
            {
                this.a = a;
                this.b = b;
                this.c = c;
            }

            public int GetAt(int g) => g == 2 ? c : g == 1 ? b : a;
        }

        public Triangle[] triangles;
        public Vector2[] vertices;
        /// <summary>Per-vertex colours, used instead of <see cref="FSprite.color"/> when <see cref="customColor"/> is set.</summary>
        public Color[] verticeColors;
        public Vector2[] UVvertices;
        public bool customColor;

        /// <exception cref="FutileException">No loaded atlas has an element named <paramref name="imageName"/>.</exception>
        public TriangleMesh(string imageName, Triangle[] tris, bool customColor, bool atlasedImage = false)
        {
            this.customColor = customColor;
            triangles = tris;
            // The vertex count is one more than the highest index used, and at least 3.
            int highest = 2;
            foreach (var t in triangles)
            {
                if (t.a > highest) highest = t.a;
                if (t.b > highest) highest = t.b;
                if (t.c > highest) highest = t.c;
            }
            vertices = new Vector2[highest + 1];
            UVvertices = new Vector2[highest + 1];
            if (customColor)
            {
                verticeColors = new Color[highest + 1];
                for (int i = 0; i < verticeColors.Length; i++) verticeColors[i] = Futile.white;
            }
            _element = Futile.atlasManager.GetElementWithName(imageName);
            if (atlasedImage) SetAtlasedImage(_element);
        }

        public override Color color
        {
            get => _color;
            set
            {
                if (customColor)
                {
                    for (int i = 0; i < verticeColors.Length; i++) verticeColors[i] = value;
                }
                else
                {
                    base.color = value;
                }
            }
        }

        public override float alpha
        {
            get => base.alpha;
            set
            {
                if (customColor)
                {
                    for (int i = 0; i < verticeColors.Length; i++) verticeColors[i].a = value;
                }
                else
                {
                    base.alpha = value;
                }
            }
        }

        public void MoveVertice(int ind, Vector2 pos)
        {
            vertices[ind] = pos;
        }

        /// <summary>Kept for ported code; the mesh is rebuilt every frame anyway.</summary>
        public void Refresh()
        {
        }

        // TriangleMesh.SetAtlasedImage: sample the middle of an element, for flat-coloured meshes on a shared atlas.
        private void SetAtlasedImage(FAtlasElement atlasElement)
        {
            var middle = (atlasElement.uvTopLeft + atlasElement.uvBottomRight) / 2f;
            for (int i = 0; i < UVvertices.Length; i++) UVvertices[i] = middle;
        }

        protected override void PopulateRenderLayer(FRenderBatch batch)
        {
            // TriangleMesh.PopulateRenderLayer: three vertices per triangle.
            var m = _concatenatedMatrix;
            var flat = AlphaColor(_color);
            for (int i = 0; i < triangles.Length; i++)
            {
                int first = batch.vertices.Count;
                for (int j = 0; j < 3; j++)
                {
                    int v = triangles[i].GetAt(j);
                    batch.AddVertex(m.Apply(vertices[v]), UVvertices[v], customColor ? verticeColors[v] : flat);
                }
                batch.AddTriangle(first, first + 1, first + 2);
            }
        }

        /// <summary>A strip of quads, two vertices per segment, optionally ending in a point (TriangleMesh.MakeLongMesh).</summary>
        public static TriangleMesh MakeLongMesh(int segments, bool pointyTip, bool customColor) =>
            MakeLongMesh(segments, pointyTip, customColor, Futile.WhiteElementName);

        public static TriangleMesh MakeLongMesh(int segments, bool pointyTip, bool customColor, string texture)
        {
            var tris = new Triangle[(segments - 1) * 4 + (pointyTip ? 1 : 2)];
            for (int i = 0; i < segments - 1; i++)
            {
                int first = i * 4;
                for (int j = 0; j < 4; j++) tris[first + j] = new Triangle(first + j, first + j + 1, first + j + 2);
            }
            int last = (segments - 1) * 4;
            tris[last] = new Triangle(last, last + 1, last + 2);
            if (!pointyTip) tris[last + 1] = new Triangle(last + 1, last + 2, last + 3);

            var mesh = new TriangleMesh(texture, tris, customColor);
            float step = 1f / ((segments - 1) * 2 + 1);
            for (int k = 0; k < mesh.UVvertices.Length; k++)
                mesh.UVvertices[k] = new Vector2(k % 2 == 0 ? 0f : 1f, k / 2 * step);
            if (pointyTip) mesh.UVvertices[mesh.UVvertices.Length - 1].x = 0.5f;
            return mesh;
        }

        // TriangleMesh.MakeLongMeshAtlased
        public static TriangleMesh MakeLongMeshAtlased(int segments, bool pointyTip, bool customColor)
        {
            var mesh = MakeLongMesh(segments, pointyTip, customColor, "RainWorld_White");
            mesh.SetAtlasedImage(mesh._element);
            return mesh;
        }

        /// <summary>A strip with UVs spread evenly along its length (TriangleMesh.MakeOneToOneLongMesh).</summary>
        public static TriangleMesh MakeOneToOneLongMesh(int segments, bool customColor, string texture = Futile.WhiteElementName)
        {
            var mesh = new TriangleMesh(texture, GridTriangles(segments - 1, 1), customColor);
            for (int i = 0; i < mesh.UVvertices.Length; i += 2)
            {
                float v = (float)i / (mesh.UVvertices.Length - 2);
                mesh.UVvertices[i] = new Vector2(0f, v);
                mesh.UVvertices[i + 1] = new Vector2(1f, v);
            }
            return mesh;
        }

        /// <summary>Triangles for a grid of quads, <paramref name="nQuadWidth"/> + 1 vertices per row (TriangleMesh.GridTriangles).</summary>
        public static Triangle[] GridTriangles(int nQuadLen, int nQuadWidth)
        {
            var tris = new Triangle[nQuadLen * nQuadWidth * 2];
            for (int i = 0; i < nQuadLen; i++)
            {
                int row = i * (nQuadWidth + 1);
                int firstTri = i * nQuadWidth * 2;
                for (int j = 0; j < nQuadWidth; j++)
                {
                    int v = row + j;
                    int t = firstTri + j * 2;
                    tris[t] = new Triangle(v, v + 1, v + 1 + nQuadWidth);
                    tris[t + 1] = new Triangle(v + 1 + nQuadWidth, v + 1, v + 2 + nQuadWidth);
                }
            }
            return tris;
        }
    }
}
