using System.IO;

namespace RWDE.Tests.Common
{
    /// <summary>
    /// Reads the invented fixture files under <c>Assets/Tests/Common/Fixtures~/</c>.
    /// Paths use forward slashes, relative to that folder, e.g. <c>"world/xx/world_xx.txt"</c>.
    /// </summary>
    public static class Fixtures
    {
        public static string PathOf(string relativePath)
        {
            var path = Path.Combine(TestPaths.FixturesRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path))
                throw new FileNotFoundException("Fixture not found: " + relativePath, path);
            return path;
        }

        public static string ReadText(string relativePath) => File.ReadAllText(PathOf(relativePath));

        public static string[] ReadLines(string relativePath) => File.ReadAllLines(PathOf(relativePath));

        /// <summary>Copies a fixture to <paramref name="destination"/>, creating parent folders.</summary>
        public static void CopyTo(string relativePath, string destination)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            File.Copy(PathOf(relativePath), destination, true);
        }
    }
}
