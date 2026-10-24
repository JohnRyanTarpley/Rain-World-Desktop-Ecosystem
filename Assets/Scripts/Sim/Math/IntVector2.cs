using System;

namespace RWDE.Sim
{
    /// <summary>An integer 2D vector, used for tile positions. Mirrors the game's <c>RWCustom.IntVector2</c>.</summary>
    public struct IntVector2 : IEquatable<IntVector2>
    {
        public int x;
        public int y;

        public IntVector2(int x, int y)
        {
            this.x = x;
            this.y = y;
        }

        public static IntVector2 operator +(IntVector2 a, IntVector2 b) => new IntVector2(a.x + b.x, a.y + b.y);
        public static IntVector2 operator -(IntVector2 a, IntVector2 b) => new IntVector2(a.x - b.x, a.y - b.y);
        public static IntVector2 operator *(IntVector2 a, int b) => new IntVector2(a.x * b, a.y * b);
        public static bool operator ==(IntVector2 a, IntVector2 b) => a.x == b.x && a.y == b.y;
        public static bool operator !=(IntVector2 a, IntVector2 b) => !(a == b);

        public Vector2 ToVector2() => new Vector2(x, y);
        public static Vector2 ToVector2(IntVector2 a) => new Vector2(a.x, a.y);

        /// <summary>Truncates toward zero, as the game's <c>IntVector2.FromVector2</c> does.</summary>
        public static IntVector2 FromVector2(Vector2 v) => new IntVector2((int)v.x, (int)v.y);

        public float FloatDist(IntVector2 other) => Vector2.Distance(ToVector2(), other.ToVector2());

        public bool Equals(IntVector2 other) => x == other.x && y == other.y;
        public override bool Equals(object obj) => obj is IntVector2 other && Equals(other);
        public override int GetHashCode() => (x * 397) ^ y;

        public override string ToString() => "x: " + x + ", y: " + y;
    }
}
