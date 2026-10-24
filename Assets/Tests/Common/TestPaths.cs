using System;
using System.IO;

namespace RWDE.Tests.Common
{
    /// <summary>
    /// Locates the Unity project and the shared fixture folder without touching UnityEngine,
    /// so engine-free test assemblies (RWDE.Sim.Tests) can use it too.
    /// </summary>
    public static class TestPaths
    {
        static string projectRoot;

        /// <summary>The folder that holds <c>Assets/</c> and <c>ProjectSettings/</c>.</summary>
        public static string ProjectRoot
        {
            get
            {
                if (projectRoot == null)
                    projectRoot = FindProjectRoot(Directory.GetCurrentDirectory())
                        ?? FindProjectRoot(AppDomain.CurrentDomain.BaseDirectory)
                        ?? throw new DirectoryNotFoundException(
                            "Could not find the Unity project root (a folder with Assets/ and ProjectSettings/) above "
                            + Directory.GetCurrentDirectory());
                return projectRoot;
            }
        }

        /// <summary>
        /// Invented fixture data in Rain World's file formats. The trailing <c>~</c> keeps Unity from
        /// importing the folder, so it never ends up in a build.
        /// </summary>
        public static string FixturesRoot => Path.Combine(ProjectRoot, "Assets", "Tests", "Common", "Fixtures~");

        public static string ScriptsRoot => Path.Combine(ProjectRoot, "Assets", "Scripts");

        static string FindProjectRoot(string start)
        {
            for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, "Assets"))
                    && Directory.Exists(Path.Combine(dir.FullName, "ProjectSettings")))
                    return dir.FullName;
            }
            return null;
        }
    }
}
