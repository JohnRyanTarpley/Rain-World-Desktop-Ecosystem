using System;
using System.Collections.Generic;
using System.Globalization;
using RWDE.Sim;

namespace RWDE.Graphics
{
    /// <summary>
    /// A texture and the elements packed into it, as Futile's <c>FAtlas</c>. The element data is the
    /// TexturePacker JSON the game ships (<c>Atlases/rainWorld.txt</c> and friends). The texture itself is
    /// an opaque handle: the Unity renderer stores a <c>UnityEngine.Texture</c> there.
    /// </summary>
    public sealed class FAtlas
    {
        // Futile.resourceScaleInverse: Rain World runs Futile at resource scale 1.
        private const float ResourceScaleInverse = 1f;

        private readonly List<FAtlasElement> _elements = new List<FAtlasElement>();

        private FAtlas(string name, object texture, int textureWidth, int textureHeight, bool isSingleImage)
        {
            this.name = name;
            this.texture = texture;
            textureSize = new Vector2(textureWidth, textureHeight);
            this.isSingleImage = isSingleImage;
        }

        public readonly string name;
        /// <summary>The engine texture, or null in tests.</summary>
        public readonly object texture;
        public readonly Vector2 textureSize;
        public readonly bool isSingleImage;

        /// <summary>Load order of this atlas in its <see cref="FAtlasManager"/>.</summary>
        public int index { get; internal set; }

        public IReadOnlyList<FAtlasElement> elements => _elements;

        /// <summary>An atlas from TexturePacker JSON (FAtlas.LoadAtlasData).</summary>
        /// <exception cref="FutileException">The JSON has no "frames" object.</exception>
        /// <exception cref="NotSupportedException">A frame is rotated, which Futile does not support.</exception>
        public static FAtlas FromTexturePackerJson(string name, string json, object texture, int textureWidth, int textureHeight)
        {
            if (textureWidth <= 0 || textureHeight <= 0) throw new ArgumentException("Atlas texture size must be positive");
            var atlas = new FAtlas(name, texture, textureWidth, textureHeight, false);
            if (!(AtlasJson.Parse(json) is AtlasJson.JsonObject root) || !(root.Get("frames") is AtlasJson.JsonObject frames))
                throw new FutileException("The atlas " + name + " was not a proper JSON file. Make sure to select \"Unity3D\" in TexturePacker.");

            var size = atlas.textureSize;
            int index = 0;
            foreach (var key in frames.Keys)
            {
                var frame = (AtlasJson.JsonObject)frames.Get(key);
                var element = new FAtlasElement { indexInAtlas = index++ };

                // Futile.shouldRemoveAtlasElementFileExtensions is on in Rain World.
                int dot = key.LastIndexOf(".", StringComparison.Ordinal);
                element.name = dot >= 0 ? key.Substring(0, dot) : key;
                element.isTrimmed = (bool)frame.Get("trimmed");
                if ((bool)frame.Get("rotated"))
                    throw new NotSupportedException("Futile no longer supports TexturePacker's \"rotated\" flag. Please disable it when creating the " + name + " atlas.");

                var f = (AtlasJson.JsonObject)frame.Get("frame");
                float fx = Number(f, "x"), fy = Number(f, "y"), fw = Number(f, "w"), fh = Number(f, "h");
                // TexturePacker measures y from the top; UVs are from the bottom.
                var uv = new Rect(fx / size.x, (size.y - fy - fh) / size.y, fw / size.x, fh / size.y);
                element.uvRect = uv;
                element.uvTopLeft = new Vector2(uv.xMin, uv.yMax);
                element.uvTopRight = new Vector2(uv.xMax, uv.yMax);
                element.uvBottomRight = new Vector2(uv.xMax, uv.yMin);
                element.uvBottomLeft = new Vector2(uv.xMin, uv.yMin);

                var sourceSize = (AtlasJson.JsonObject)frame.Get("sourceSize");
                element.sourcePixelSize = new Vector2(Number(sourceSize, "w"), Number(sourceSize, "h"));
                element.sourceSize = element.sourcePixelSize * ResourceScaleInverse;

                var s = (AtlasJson.JsonObject)frame.Get("spriteSourceSize");
                element.sourceRect = new Rect(Number(s, "x") * ResourceScaleInverse, Number(s, "y") * ResourceScaleInverse,
                    Number(s, "w") * ResourceScaleInverse, Number(s, "h") * ResourceScaleInverse);
                atlas._elements.Add(element);
            }
            return atlas;
        }

        /// <summary>A whole texture as one element named after the atlas (FAtlas.CreateAtlasFromSingleImage).</summary>
        public static FAtlas FromSingleImage(string name, object texture, int textureWidth, int textureHeight)
        {
            var atlas = new FAtlas(name, texture, textureWidth, textureHeight, true);
            var uv = new Rect(0f, 0f, 1f, 1f);
            atlas._elements.Add(new FAtlasElement
            {
                name = name,
                uvRect = uv,
                uvTopLeft = new Vector2(uv.xMin, uv.yMax),
                uvTopRight = new Vector2(uv.xMax, uv.yMax),
                uvBottomRight = new Vector2(uv.xMax, uv.yMin),
                uvBottomLeft = new Vector2(uv.xMin, uv.yMin),
                sourceSize = new Vector2(textureWidth * ResourceScaleInverse, textureHeight * ResourceScaleInverse),
                sourcePixelSize = new Vector2(textureWidth, textureHeight),
                sourceRect = new Rect(0f, 0f, textureWidth * ResourceScaleInverse, textureHeight * ResourceScaleInverse),
            });
            return atlas;
        }

        private static float Number(AtlasJson.JsonObject obj, string key) =>
            float.Parse(Convert.ToString(obj.Get(key), CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture);

        public override string ToString() => name;
    }
}
