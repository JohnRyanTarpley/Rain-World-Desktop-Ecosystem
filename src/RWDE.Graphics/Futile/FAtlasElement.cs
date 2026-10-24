using RWDE.Sim;

namespace RWDE.Graphics
{
    /// <summary>One named image inside an atlas texture, as Futile's <c>FAtlasElement</c>.</summary>
    public sealed class FAtlasElement
    {
        /// <summary>The game's element name, e.g. "Circle20" or "LizardHead0.0" (file extension stripped).</summary>
        public string name;
        public int indexInAtlas;
        public FAtlas atlas;
        public int atlasIndex;

        /// <summary>The element's area in the texture, in UV space (origin bottom-left).</summary>
        public Rect uvRect;
        public Vector2 uvTopLeft;
        public Vector2 uvTopRight;
        public Vector2 uvBottomRight;
        public Vector2 uvBottomLeft;

        /// <summary>The trimmed image's rect inside the untrimmed source, in points (y measured from the top).</summary>
        public Rect sourceRect;
        /// <summary>The untrimmed size in points.</summary>
        public Vector2 sourceSize;
        public Vector2 sourcePixelSize;
        public bool isTrimmed;

        public override string ToString() => name + (atlas != null ? " (" + atlas.name + ")" : "");
    }
}
