namespace RWDE.Sim
{
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
