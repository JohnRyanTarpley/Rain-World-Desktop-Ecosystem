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

        // Custom.eightDirectionsAndZero: zero, then anticlockwise (y up) from left.
        public static readonly IntVector2[] eightDirectionsAndZero =
        {
            new IntVector2(0, 0), new IntVector2(-1, 0), new IntVector2(-1, -1), new IntVector2(0, -1), new IntVector2(1, -1),
            new IntVector2(1, 0), new IntVector2(1, 1), new IntVector2(0, 1), new IntVector2(-1, 1)
        };

        // Custom.PerpIntVec
        public static IntVector2 PerpIntVec(IntVector2 intVec) => new IntVector2(intVec.y, -intVec.x);

        // Custom.IntClamp
        public static int IntClamp(int val, int inclMin, int inclMax) => val < inclMin ? inclMin : val > inclMax ? inclMax : val;

        // Custom.DistLess
        public static bool DistLess(Vector2 a, Vector2 b, float dist) => (a - b).magnitude < dist;
        public static bool DistLess(IntVector2 a, IntVector2 b, float dist) => DistLess(a.ToVector2(), b.ToVector2(), dist);

        // Custom.LerpMap with an exponent on the mapped fraction.
        public static float LerpMap(float val, float fromA, float toA, float fromB, float toB, float exponent) =>
            fromB + (toB - fromB) * Mathf.Pow(Mathf.InverseLerp(fromA, toA, val), exponent);

        // Custom.PushFromHalf: pushes a 0-1 value away from 0.5.
        public static float PushFromHalf(float val, float pushExponent)
        {
            if (val == 0.5f) return 0.5f;
            if (val < 0.5f) return LerpMap(val, 0f, 0.5f, 0f, 0.5f, pushExponent);
            return LerpMap(val, 1f, 0.5f, 1f, 0.5f, pushExponent);
        }

        // Custom.eightDirectionsDiagonalsLast
        public static readonly IntVector2[] eightDirectionsDiagonalsLast =
        {
            new IntVector2(-1, 0), new IntVector2(0, -1), new IntVector2(1, 0), new IntVector2(0, 1),
            new IntVector2(-1, -1), new IntVector2(1, -1), new IntVector2(1, 1), new IntVector2(-1, 1)
        };

        // Custom.LerpMap
        public static float LerpMap(float val, float fromA, float toA, float fromB, float toB) =>
            fromB + (toB - fromB) * Mathf.InverseLerp(fromA, toA, val);

        // Custom.LerpAndTick: lerps toward the target, then steps by a fixed tick without overshooting.
        public static float LerpAndTick(float from, float to, float lerp, float tick)
        {
            float v = Mathf.Lerp(from, to, lerp);
            if (v < to) return System.Math.Min(v + tick, to);
            return System.Math.Max(v - tick, to);
        }

        // Custom.SCurve
        public static float SCurve(float x, float k)
        {
            x = x * 2f - 1f;
            if (x < 0f)
            {
                x = Mathf.Abs(1f + x);
                return k * x / (k - x + 1f) * 0.5f;
            }
            k = -1f - k;
            return 0.5f + k * x / (k - x + 1f) * 0.5f;
        }

        // Custom.DegToVec: 0 degrees points up, 90 to the right.
        public static Vector2 DegToVec(float ang) =>
            new Vector2(Mathf.Sin(ang * (Mathf.PI / 180f)), Mathf.Cos(ang * (Mathf.PI / 180f)));

        // Custom.VecToDeg
        public static float VecToDeg(Vector2 v) => Mathf.Atan2(v.x, v.y) / (Mathf.PI * 2f) * 360f;

        // Custom.AimFromOneVectorToAnother
        public static float AimFromOneVectorToAnother(Vector2 p1, Vector2 p2) => VecToDeg(p2 - p1);

        // Custom.RNV: a random unit vector, drawn from the given generator (the game uses UnityEngine.Random).
        public static Vector2 RNV(System.Random random) => DegToVec(random.Value() * 360f);

        // Custom.PerpendicularVector
        public static Vector2 PerpendicularVector(Vector2 v)
        {
            v = v.normalized;
            return new Vector2(-v.y, v.x);
        }

        public static Vector2 PerpendicularVector(Vector2 v1, Vector2 v2) => PerpendicularVector(v1 - v2);

        // Custom.DistanceToLine
        public static float DistanceToLine(Vector2 V, Vector2 l2, Vector2 l1) =>
            ((l2.y - l1.y) * V.x - (l2.x - l1.x) * V.y + l2.x * l1.y - l2.y * l1.x)
            / Mathf.Sqrt(Mathf.Pow(l2.y - l1.y, 2f) + Mathf.Pow(l2.x - l1.x, 2f));

        // Custom.Angle: the signed angle in degrees.
        public static float Angle(Vector2 A, Vector2 B) => Vector2.Angle(A, B) * Mathf.Sign(DistanceToLine(A, B, -B));

        // Custom.IntVectorsOpposite
        public static bool IntVectorsOpposite(IntVector2 a, IntVector2 b) => a.x == -b.x && a.y == -b.y;

        // Custom.ManhattanDistance
        public static int ManhattanDistance(IntVector2 a, IntVector2 b) => System.Math.Abs(a.x - b.x) + System.Math.Abs(a.y - b.y);

        public static int ManhattanDistance(WorldCoordinate a, WorldCoordinate b) =>
            a.room != b.room ? int.MaxValue : System.Math.Abs(a.x - b.x) + System.Math.Abs(a.y - b.y);

        // Custom.WorldCoordFloatDist
        public static float WorldCoordFloatDist(WorldCoordinate a, WorldCoordinate b) =>
            a.room != b.room || !a.TileDefined || !b.TileDefined ? float.MaxValue : a.Tile.FloatDist(b.Tile);

        // Custom.MakeWorldCoordinate
        public static WorldCoordinate MakeWorldCoordinate(IntVector2 pos, int room) => new WorldCoordinate(room, pos.x, pos.y, -1);

        public static Vector2 IntVector2ToVector2(IntVector2 v) => new Vector2(v.x, v.y);

        // Custom.RandomDeviation and the variations built on it.
        private static float RandomDeviation(System.Random random, float k) =>
            SCurve(random.Value() * 0.5f, k) * 2f * (random.Value() < 0.5f ? 1f : -1f);

        public static float ClampedRandomVariation(System.Random random, float baseValue, float maxDeviation, float k) =>
            Mathf.Clamp(baseValue + RandomDeviation(random, k) * maxDeviation, 0f, 1f);

        public static float WrappedRandomVariation(System.Random random, float baseValue, float maxDeviation, float k)
        {
            float v = baseValue + RandomDeviation(random, k) * maxDeviation + 1f;
            return v - Mathf.Floor(v);
        }

        // Custom.HSL2RGB
        public static Color HSL2RGB(float h, float sl, float l)
        {
            float r = l, g = l, b = l;
            float v = l <= 0.5 ? l * (1f + sl) : l + sl - l * sl;
            if (v > 0f)
            {
                float m = l + l - v;
                float sv = (v - m) / v;
                h *= 6f;
                int sextant = (int)h;
                float fract = h - sextant;
                float vsf = v * sv * fract;
                float mid1 = m + vsf;
                float mid2 = v - vsf;
                switch (sextant)
                {
                    case 0: r = v; g = mid1; b = m; break;
                    case 1: r = mid2; g = v; b = m; break;
                    case 2: r = m; g = v; b = mid1; break;
                    case 3: r = m; g = mid2; b = v; break;
                    case 4: r = mid1; g = m; b = v; break;
                    case 5: r = v; g = m; b = mid2; break;
                }
            }
            return new Color(r, g, b);
        }

        // Custom.InsideRect: edges included.
        public static bool InsideRect(IntVector2 vec, IntRect rect) => InsideRect(vec.x, vec.y, rect);

        public static bool InsideRect(int x, int y, IntRect rect) =>
            x >= rect.left && x <= rect.right && y >= rect.bottom && y <= rect.top;
    }
}
