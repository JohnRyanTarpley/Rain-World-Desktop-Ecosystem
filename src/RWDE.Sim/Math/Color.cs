using System;

namespace RWDE.Sim
{
    /// <summary>
    /// An RGBA colour with 0-1 channels, standing in for <c>UnityEngine.Color</c> in ported data such
    /// as <c>LizardBreedParams.standardColor</c>. The sim only stores colours for graphics to read.
    /// </summary>
    public struct Color : IEquatable<Color>
    {
        public float r;
        public float g;
        public float b;
        public float a;

        public Color(float r, float g, float b, float a = 1f)
        {
            this.r = r;
            this.g = g;
            this.b = b;
            this.a = a;
        }

        public static Color black => new Color(0f, 0f, 0f);
        public static Color white => new Color(1f, 1f, 1f);

        /// <summary>Unity's <c>Color.Lerp</c>: every channel, with <paramref name="t"/> clamped to [0, 1].</summary>
        public static Color Lerp(Color a, Color b, float t)
        {
            t = Mathf.Clamp01(t);
            return new Color(a.r + (b.r - a.r) * t, a.g + (b.g - a.g) * t, a.b + (b.b - a.b) * t, a.a + (b.a - a.a) * t);
        }

        public bool Equals(Color other) => r == other.r && g == other.g && b == other.b && a == other.a;
        public override bool Equals(object obj) => obj is Color other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(r, g, b, a);
        public static bool operator ==(Color x, Color y) => x.Equals(y);
        public static bool operator !=(Color x, Color y) => !x.Equals(y);
        public override string ToString() => "RGBA(" + r + ", " + g + ", " + b + ", " + a + ")";
    }
}
