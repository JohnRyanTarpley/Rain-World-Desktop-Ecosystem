namespace RWDE.Graphics
{
    /// <summary>Engine-free stand-in for <c>UnityEngine.Rect</c>: x/y is the bottom-left corner.</summary>
    public struct Rect
    {
        public float x;
        public float y;
        public float width;
        public float height;

        public Rect(float x, float y, float width, float height)
        {
            this.x = x;
            this.y = y;
            this.width = width;
            this.height = height;
        }

        public float xMin => x;
        public float yMin => y;
        public float xMax => x + width;
        public float yMax => y + height;

        public override string ToString() => $"(x:{x}, y:{y}, width:{width}, height:{height})";
    }
}
