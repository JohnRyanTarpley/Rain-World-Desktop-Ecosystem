using System;

namespace RWDE.Sim.Physics
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
    }

    /// <summary>The <c>RWCustom.Custom</c> helpers ported game code uses.</summary>
    public static class Custom
    {
        // Custom.DirVec: straight up when the points coincide.
        public static Vector2 DirVec(Vector2 p1, Vector2 p2) =>
            p1 == p2 ? new Vector2(0f, 1f) : new Vector2(p2.x - p1.x, p2.y - p1.y).normalized;

        // Custom.DistLess
        public static bool DistLess(Vector2 a, Vector2 b, float dist) => (a - b).magnitude < dist;
    }
}
