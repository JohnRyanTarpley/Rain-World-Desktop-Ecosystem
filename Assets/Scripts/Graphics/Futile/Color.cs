using System;
using System.Globalization;

namespace RWDE.Graphics
{
    /// <summary>
    /// Engine-free stand-in for <c>UnityEngine.Color</c>, so the sprite layer and ported graphics modules
    /// stay free of UnityEngine (and testable without it). Member names and semantics match Unity's.
    /// </summary>
    public struct Color : IEquatable<Color>
    {
        public float r;
        public float g;
        public float b;
        public float a;

        public Color(float r, float g, float b, float a)
        {
            this.r = r;
            this.g = g;
            this.b = b;
            this.a = a;
        }

        public Color(float r, float g, float b) : this(r, g, b, 1f)
        {
        }

        public static Color white => new Color(1f, 1f, 1f, 1f);
        public static Color black => new Color(0f, 0f, 0f, 1f);
        public static Color clear => new Color(0f, 0f, 0f, 0f);
        public static Color red => new Color(1f, 0f, 0f, 1f);
        public static Color green => new Color(0f, 1f, 0f, 1f);
        public static Color blue => new Color(0f, 0f, 1f, 1f);
        public static Color grey => new Color(0.5f, 0.5f, 0.5f, 1f);
        public static Color gray => grey;

        /// <summary>Linear interpolation with <paramref name="t"/> clamped to [0, 1], as in Unity.</summary>
        public static Color Lerp(Color a, Color b, float t)
        {
            t = t < 0f ? 0f : t > 1f ? 1f : t;
            return LerpUnclamped(a, b, t);
        }

        public static Color LerpUnclamped(Color a, Color b, float t) =>
            new Color(a.r + (b.r - a.r) * t, a.g + (b.g - a.g) * t, a.b + (b.b - a.b) * t, a.a + (b.a - a.a) * t);

        public static Color operator +(Color a, Color b) => new Color(a.r + b.r, a.g + b.g, a.b + b.b, a.a + b.a);
        public static Color operator -(Color a, Color b) => new Color(a.r - b.r, a.g - b.g, a.b - b.b, a.a - b.a);
        public static Color operator *(Color a, Color b) => new Color(a.r * b.r, a.g * b.g, a.b * b.b, a.a * b.a);
        public static Color operator *(Color a, float f) => new Color(a.r * f, a.g * f, a.b * f, a.a * f);
        public static Color operator *(float f, Color a) => a * f;
        public static Color operator /(Color a, float f) => new Color(a.r / f, a.g / f, a.b / f, a.a / f);

        /// <summary>Approximate equality, as Unity's (compares as a Vector4 with a 1e-5 tolerance).</summary>
        public static bool operator ==(Color lhs, Color rhs)
        {
            float dr = lhs.r - rhs.r, dg = lhs.g - rhs.g, db = lhs.b - rhs.b, da = lhs.a - rhs.a;
            return dr * dr + dg * dg + db * db + da * da < 9.99999944E-11f;
        }

        public static bool operator !=(Color lhs, Color rhs) => !(lhs == rhs);

        public bool Equals(Color other) => r.Equals(other.r) && g.Equals(other.g) && b.Equals(other.b) && a.Equals(other.a);
        public override bool Equals(object obj) => obj is Color other && Equals(other);
        public override int GetHashCode() => r.GetHashCode() ^ (g.GetHashCode() << 2) ^ (b.GetHashCode() >> 2) ^ (a.GetHashCode() >> 1);

        public override string ToString() => string.Format(CultureInfo.InvariantCulture, "RGBA({0:F3}, {1:F3}, {2:F3}, {3:F3})", r, g, b, a);
    }
}
