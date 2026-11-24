namespace RWDE.Sim
{
    /// <summary>
    /// <c>UnityEngine.Random</c>'s calls over a <see cref="System.Random"/>, so ported code can keep the
    /// game's shape while drawing from the world's seeded generator.
    /// </summary>
    public static class RandomExtensions
    {
        /// <summary><c>Random.value</c>: a float in [0, 1).</summary>
        public static float Value(this System.Random random) => (float)random.NextDouble();

        /// <summary><c>Random.Range(int, int)</c>: <paramref name="max"/> excluded, and <paramref name="min"/> when the range is empty.</summary>
        public static int Range(this System.Random random, int min, int max) => max <= min ? min : random.Next(min, max);

        /// <summary><c>Random.Range(float, float)</c>.</summary>
        public static float Range(this System.Random random, float min, float max) => min + (max - min) * (float)random.NextDouble();
    }
}
