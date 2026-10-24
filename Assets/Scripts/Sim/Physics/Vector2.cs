using System;

namespace RWDE.Sim.Physics
{
    /// <summary>
    /// Engine-free stand-in for <c>UnityEngine.Vector2</c> (RWDE.Sim may not reference UnityEngine).
    /// Same float maths and the same epsilons as Unity's, so ported game code gives the game's numbers.
    /// </summary>
    public struct Vector2 : IEquatable<Vector2>
    {
        // UnityEngine.Vector2.kEpsilon and kEpsilonNormalSqrt
        public const float kEpsilon = 1E-05f;
        public const float kEpsilonNormalSqrt = 1E-15f;

        public float x;
        public float y;

        public Vector2(float x, float y)
        {
            this.x = x;
            this.y = y;
        }

        public static Vector2 zero => new Vector2(0f, 0f);

        public float sqrMagnitude => x * x + y * y;
        public float magnitude => (float)Math.Sqrt(x * x + y * y);

        public Vector2 normalized
        {
            get
            {
                float mag = magnitude;
                return mag > kEpsilon ? this / mag : zero;
            }
        }

        public static float Dot(Vector2 a, Vector2 b) => a.x * b.x + a.y * b.y;

        public static float Distance(Vector2 a, Vector2 b) => (a - b).magnitude;

        public static Vector2 ClampMagnitude(Vector2 vector, float maxLength)
        {
            float sqr = vector.sqrMagnitude;
            if (sqr > maxLength * maxLength)
            {
                float mag = (float)Math.Sqrt(sqr);
                return new Vector2(vector.x / mag * maxLength, vector.y / mag * maxLength);
            }
            return vector;
        }

        public static Vector2 operator +(Vector2 a, Vector2 b) => new Vector2(a.x + b.x, a.y + b.y);
        public static Vector2 operator -(Vector2 a, Vector2 b) => new Vector2(a.x - b.x, a.y - b.y);
        public static Vector2 operator -(Vector2 a) => new Vector2(-a.x, -a.y);
        public static Vector2 operator *(Vector2 a, float d) => new Vector2(a.x * d, a.y * d);
        public static Vector2 operator *(float d, Vector2 a) => new Vector2(a.x * d, a.y * d);
        public static Vector2 operator /(Vector2 a, float d) => new Vector2(a.x / d, a.y / d);

        // Unity compares vectors approximately.
        public static bool operator ==(Vector2 a, Vector2 b) => (a - b).sqrMagnitude < kEpsilon * kEpsilon;
        public static bool operator !=(Vector2 a, Vector2 b) => !(a == b);

        public bool Equals(Vector2 other) => x.Equals(other.x) && y.Equals(other.y);
        public override bool Equals(object obj) => obj is Vector2 other && Equals(other);
        public override int GetHashCode() => x.GetHashCode() ^ (y.GetHashCode() << 2);
        public override string ToString() => $"({x:F2}, {y:F2})";
    }
}
