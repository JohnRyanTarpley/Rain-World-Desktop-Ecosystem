using System;
using System.IO;

namespace RWDE.Tests.Common
{
    /// <summary>Locates the repo root and the shared fixture folder from wherever the tests run.</summary>
    public static class TestPaths
    {
        static string repoRoot;

        /// <summary>The folder that holds <c>src/</c> and <c>tests/</c>.</summary>
        public static string RepoRoot
        {
            get
            {
                if (repoRoot == null)
                    repoRoot = FindRepoRoot(Directory.GetCurrentDirectory())
                        ?? FindRepoRoot(AppDomain.CurrentDomain.BaseDirectory)
                        ?? throw new DirectoryNotFoundException(
                            "Could not find the repo root (a folder with src/ and tests/Fixtures/) above "
                            + Directory.GetCurrentDirectory());
                return repoRoot;
            }
        }

        /// <summary>Invented fixture data in Rain World's file formats. It is never part of a build.</summary>
        public static string FixturesRoot => Path.Combine(RepoRoot, "tests", "Fixtures");

        public static string SourceRoot => Path.Combine(RepoRoot, "src");

        static string FindRepoRoot(string start)
        {
            for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, "src"))
                    && Directory.Exists(Path.Combine(dir.FullName, "tests", "Fixtures")))
                    return dir.FullName;
            }
            return null;
        }
    }
}
