using System;

namespace RWDE.Sim.Physics
{
    /// <summary>A tile coordinate or direction, as <c>RWCustom.IntVector2</c> in the game.</summary>
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

        public static Vector2 ToVector2(IntVector2 a) => new Vector2(a.x, a.y);

        public bool Equals(IntVector2 other) => this == other;
        public override bool Equals(object obj) => obj is IntVector2 other && Equals(other);
        public override int GetHashCode() => x * 397 ^ y;
        public override string ToString() => $"({x}, {y})";
    }
}
