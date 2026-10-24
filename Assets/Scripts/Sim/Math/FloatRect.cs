using System;
using System.Globalization;

namespace RWDE.Sim
{
    /// <summary>An axis-aligned rectangle in room pixels. Mirrors the game's <c>FloatRect</c>.</summary>
    public struct FloatRect
    {
        public float left;
        public float bottom;
        public float right;
        public float top;

        public FloatRect(float left, float bottom, float right, float top)
        {
            this.left = left;
            this.bottom = bottom;
            this.right = right;
            this.top = top;
        }

        public Vector2 Center => new Vector2((left + right) / 2f, (bottom + top) / 2f);
        public float Width => Math.Abs(right - left);
        public float Height => Math.Abs(top - bottom);

        public static FloatRect MakeFromVector2(Vector2 lowerLeft, Vector2 upperRight) =>
            new FloatRect(lowerLeft.x, lowerLeft.y, upperRight.x, upperRight.y);

        /// <summary>True when <paramref name="v2"/> is strictly inside; points on an edge are outside, as in the game.</summary>
        public bool Vector2Inside(Vector2 v2) => v2.x > left && v2.x < right && v2.y > bottom && v2.y < top;

        // Like the game, these change this rect and also return it.
        public FloatRect Grow(float grow)
        {
            left -= grow;
            bottom -= grow;
            right += grow;
            top += grow;
            return this;
        }

        public FloatRect Shrink(float shrink)
        {
            left += shrink;
            bottom += shrink;
            right -= shrink;
            top -= shrink;
            return this;
        }

        public override string ToString() =>
            string.Format(CultureInfo.InvariantCulture, "rect: {0}, {1}, {2}, {3}", left, bottom, right, top);
    }
}
