using System;
using System.Globalization;

namespace RWDE.Sim
{
    /// <summary>
    /// Engine-free stand-in for <c>UnityEngine.Vector2</c>. RWDE.Sim may not reference UnityEngine,
    /// so ported game code uses this type; member names and semantics match Unity's.
    /// </summary>
    public struct Vector2 : IEquatable<Vector2>
    {
        // UnityEngine.Vector2.kEpsilon: == treats vectors closer than this as equal.
        public const float kEpsilon = 1E-05f;

        public float x;
        public float y;

        public Vector2(float x, float y)
        {
            this.x = x;
            this.y = y;
        }

        public static Vector2 zero => new Vector2(0f, 0f);
        public static Vector2 one => new Vector2(1f, 1f);
        public static Vector2 up => new Vector2(0f, 1f);
        public static Vector2 down => new Vector2(0f, -1f);
        public static Vector2 left => new Vector2(-1f, 0f);
        public static Vector2 right => new Vector2(1f, 0f);

        public float sqrMagnitude => x * x + y * y;
        public float magnitude => (float)Math.Sqrt(x * x + y * y);

        /// <summary>This vector with length 1, or zero when it is too short to normalize (as in Unity).</summary>
        public Vector2 normalized
        {
            get
            {
                float m = magnitude;
                return m > kEpsilon ? this / m : zero;
            }
        }

        public static float Distance(Vector2 a, Vector2 b) => (a - b).magnitude;
        public static float Dot(Vector2 a, Vector2 b) => a.x * b.x + a.y * b.y;

        /// <summary><paramref name="vector"/> shortened to <paramref name="maxLength"/> if it is longer, as in Unity.</summary>
        public static Vector2 ClampMagnitude(Vector2 vector, float maxLength)
        {
            float sqr = vector.sqrMagnitude;
            if (sqr > maxLength * maxLength)
            {
                float m = (float)Math.Sqrt(sqr);
                return new Vector2(vector.x / m * maxLength, vector.y / m * maxLength);
            }
            return vector;
        }

        public static Vector2 Lerp(Vector2 a, Vector2 b, float t)
        {
            t = t < 0f ? 0f : t > 1f ? 1f : t;
            return LerpUnclamped(a, b, t);
        }

        /// <summary>The unsigned angle in degrees between two vectors (Unity's <c>Vector2.Angle</c>).</summary>
        public static float Angle(Vector2 from, Vector2 to)
        {
            float denominator = (float)Math.Sqrt(from.sqrMagnitude * to.sqrMagnitude);
            if (denominator < 1E-15f) return 0f;
            float dot = Dot(from, to) / denominator;
            dot = dot < -1f ? -1f : dot > 1f ? 1f : dot;
            return (float)Math.Acos(dot) * 57.29578f;
        }

        /// <summary>
        /// Unity's <c>Vector3.Slerp</c> on vectors in the plane, which the game also uses for 2D
        /// directions: the direction turns through the fraction <paramref name="t"/> (clamped) of the
        /// angle between them, and the length is interpolated linearly.
        /// </summary>
        public static Vector2 Slerp(Vector2 a, Vector2 b, float t)
        {
            t = t < 0f ? 0f : t > 1f ? 1f : t;
            float ma = a.magnitude, mb = b.magnitude;
            if (ma < kEpsilon || mb < kEpsilon) return LerpUnclamped(a, b, t);
            double angA = Math.Atan2(a.y, a.x);
            double delta = Math.Atan2(b.y, b.x) - angA;
            while (delta > Math.PI) delta -= 2 * Math.PI;
            while (delta < -Math.PI) delta += 2 * Math.PI;
            double ang = angA + delta * t;
            float m = ma + (mb - ma) * t;
            return new Vector2((float)Math.Cos(ang) * m, (float)Math.Sin(ang) * m);
        }

        public static Vector2 LerpUnclamped(Vector2 a, Vector2 b, float t) =>
            new Vector2(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t);

        public static Vector2 operator +(Vector2 a, Vector2 b) => new Vector2(a.x + b.x, a.y + b.y);
        public static Vector2 operator -(Vector2 a, Vector2 b) => new Vector2(a.x - b.x, a.y - b.y);
        public static Vector2 operator -(Vector2 a) => new Vector2(-a.x, -a.y);
        public static Vector2 operator *(Vector2 a, float d) => new Vector2(a.x * d, a.y * d);
        public static Vector2 operator *(float d, Vector2 a) => new Vector2(a.x * d, a.y * d);
        public static Vector2 operator /(Vector2 a, float d) => new Vector2(a.x / d, a.y / d);

        public static bool operator ==(Vector2 a, Vector2 b) => (a - b).sqrMagnitude < kEpsilon * kEpsilon;
        public static bool operator !=(Vector2 a, Vector2 b) => !(a == b);

        public bool Equals(Vector2 other) => x.Equals(other.x) && y.Equals(other.y);
        public override bool Equals(object obj) => obj is Vector2 other && Equals(other);
        public override int GetHashCode() => x.GetHashCode() ^ (y.GetHashCode() << 2);

        public override string ToString() =>
            string.Format(CultureInfo.InvariantCulture, "({0:F2}, {1:F2})", x, y);
    }
}
