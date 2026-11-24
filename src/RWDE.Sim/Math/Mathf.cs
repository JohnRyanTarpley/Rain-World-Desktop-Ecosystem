using System;

namespace RWDE.Sim
{
    /// <summary>The <c>UnityEngine.Mathf</c> functions ported game code uses, with Unity's semantics.</summary>
    public static class Mathf
    {
        public static float Abs(float f) => Math.Abs(f);
        public static float Min(float a, float b) => a < b ? a : b;
        public static float Max(float a, float b) => a > b ? a : b;
        public static float Sqrt(float f) => (float)Math.Sqrt(f);

        public static float Clamp(float value, float min, float max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        public static float Clamp01(float value) => Clamp(value, 0f, 1f);

        public static float InverseLerp(float a, float b, float value) => a != b ? Clamp01((value - a) / (b - a)) : 0f;

        /// <summary>Interpolates from <paramref name="a"/> to <paramref name="b"/>, with <paramref name="t"/> clamped to [0, 1].</summary>
        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);

        public static float Pow(float f, float p) => (float)Math.Pow(f, p);

        public const float PI = (float)Math.PI;
        public static int Max(int a, int b) => a > b ? a : b;
        public static int Min(int a, int b) => a < b ? a : b;
        public static float Sin(float f) => (float)Math.Sin(f);
        public static float Cos(float f) => (float)Math.Cos(f);
        public static float Atan2(float y, float x) => (float)Math.Atan2(y, x);
        public static float Floor(float f) => (float)Math.Floor(f);

        /// <summary>1 for zero and positive values, -1 for negative ones (Unity's <c>Mathf.Sign</c>).</summary>
        public static float Sign(float f) => f >= 0f ? 1f : -1f;

        /// <summary>Rounds half to even, as Unity's <c>Mathf.RoundToInt</c> does.</summary>
        public static int RoundToInt(float f) => (int)Math.Round(f);
    }
}
