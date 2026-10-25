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
    }
}
