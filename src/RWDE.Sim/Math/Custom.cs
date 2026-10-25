namespace RWDE.Sim
{
    /// <summary>The <c>RWCustom.Custom</c> helpers ported game code uses.</summary>
    public static class Custom
    {
        // Custom.DirVec: straight up when the points coincide.
        public static Vector2 DirVec(Vector2 p1, Vector2 p2) =>
            p1 == p2 ? new Vector2(0f, 1f) : new Vector2(p2.x - p1.x, p2.y - p1.y).normalized;

        // Custom.fourDirections: left, down, right, up.
        public static readonly IntVector2[] fourDirections =
        {
            new IntVector2(-1, 0), new IntVector2(0, -1), new IntVector2(1, 0), new IntVector2(0, 1)
        };

        // Custom.fourDirectionsAndZero
        public static readonly IntVector2[] fourDirectionsAndZero =
        {
            new IntVector2(0, 0), new IntVector2(-1, 0), new IntVector2(0, -1), new IntVector2(1, 0), new IntVector2(0, 1)
        };

        // Custom.eightDirections: anticlockwise (y up) from left; the first four are opposite the last four.
        public static readonly IntVector2[] eightDirections =
        {
            new IntVector2(-1, 0), new IntVector2(-1, -1), new IntVector2(0, -1), new IntVector2(1, -1),
            new IntVector2(1, 0), new IntVector2(1, 1), new IntVector2(0, 1), new IntVector2(-1, 1)
        };

        // Custom.PerpIntVec
        public static IntVector2 PerpIntVec(IntVector2 intVec) => new IntVector2(intVec.y, -intVec.x);

        // Custom.IntClamp
        public static int IntClamp(int val, int inclMin, int inclMax) => val < inclMin ? inclMin : val > inclMax ? inclMax : val;

        // Custom.DistLess
        public static bool DistLess(Vector2 a, Vector2 b, float dist) => (a - b).magnitude < dist;
    }
}
