using System;

namespace RWDE.Sim
{
    /// <summary>An integer rectangle of tiles, edges included. Mirrors the game's <c>RWCustom.IntRect</c>.</summary>
    public struct IntRect
    {
        public int left;
        public int bottom;
        public int right;
        public int top;

        public IntRect(int left, int bottom, int right, int top)
        {
            this.left = left;
            this.bottom = bottom;
            this.right = right;
            this.top = top;
        }

        public int Height => Math.Abs(top - bottom);
        public int Width => Math.Abs(right - left);

        public override string ToString() => "(" + left + ", " + bottom + ") to (" + right + ", " + top + ")";
    }
}
