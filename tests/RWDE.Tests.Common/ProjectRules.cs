using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;

namespace RWDE.Tests.Common
{
    /// <summary>
    /// The department dependency graph (see the build-release context), checked against the
    /// .csproj files under <c>src/</c>. A cycle, or a platform or package dependency in Sim, is a
    /// review blocker.
    /// </summary>
    public static class ProjectRules
    {
        public const string Sim = "RWDE.Sim";
        public const string GameAssets = "RWDE.GameAssets";
        public const string Shell = "RWDE.Shell";
        public const string Graphics = "RWDE.Graphics";
        public const string Audio = "RWDE.Audio";
        public const string Interaction = "RWDE.Interaction";
        public const string UI = "RWDE.UI";
        public const string App = "RWDE.App";

        /// <summary>Which RWDE projects each department project may reference.</summary>
        public static readonly IReadOnlyDictionary<string, string[]> Allowed = new Dictionary<string, string[]>
        {
            { Sim, new string[0] },
            { GameAssets, new string[0] },
            { Shell, new[] { Sim } },
            { Graphics, new[] { Sim, GameAssets } },
            { Audio, new[] { Sim, GameAssets } },
            { Interaction, new[] { Sim, Shell } },
            { UI, new[] { Sim, Shell } },
            { App, new[] { Sim, GameAssets, Shell, Graphics, Audio, Interaction, UI } },
        };

        /// <summary>
        /// Department projects that must stay portable: plain <c>net8.0</c> (no Windows APIs) and no
        /// NuGet packages, so ported game code never depends on a platform or a renderer.
        /// </summary>
        public static readonly string[] Portable = { Sim };

        public sealed class Project
        {
            public string Path;
            public string Name;
            public string TargetFramework = "net8.0";
            public string[] References = new string[0];
            public string[] Packages = new string[0];
        }

        /// <summary>Reads every .csproj under <c>src/</c>.</summary>
        public static List<Project> LoadAll() => LoadAll(TestPaths.SourceRoot);

        public static List<Project> LoadAll(string root) =>
            Directory.GetFiles(root, "*.csproj", SearchOption.AllDirectories).Select(Parse).ToList();

        /// <summary>Returns a message per rule <paramref name="projectName"/> breaks; empty when it is clean.</summary>
        public static List<string> Violations(string projectName, IList<Project> projects)
        {
            var errors = new List<string>();
            var project = projects.FirstOrDefault(p => p.Name == projectName);
            if (project == null)
            {
                errors.Add(projectName + ": no project with this name under src/");
                return errors;
            }
            if (!Allowed.TryGetValue(projectName, out var allowed))
            {
                errors.Add(projectName + ": not a known department project; add it to ProjectRules.Allowed");
                return errors;
            }

            foreach (var reference in project.References)
            {
                if (!allowed.Contains(reference))
                    errors.Add(projectName + " must not reference " + reference);
            }
            if (Portable.Contains(projectName))
            {
                if (project.TargetFramework != "net8.0")
                    errors.Add(projectName + " must target plain net8.0, not " + project.TargetFramework);
                foreach (var package in project.Packages)
                    errors.Add(projectName + " must not use NuGet packages (found " + package + ")");
            }

            var cycle = FindCycle(projectName, projects);
            if (cycle != null)
                errors.Add("dependency cycle: " + string.Join(" -> ", cycle));
            return errors;
        }

        /// <summary>Fails the current test when <paramref name="projectName"/>'s .csproj breaks the graph.</summary>
        public static void AssertFollowsRules(string projectName)
        {
            var errors = Violations(projectName, LoadAll());
            if (errors.Count > 0)
                Assert.Fail(string.Join("\n", errors));
        }

        /// <summary>A reference path that leads from <paramref name="start"/> back to itself, or null.</summary>
        public static List<string> FindCycle(string start, IList<Project> projects)
        {
            var refs = projects.ToDictionary(p => p.Name, p => p.References);
            var path = new List<string> { start };
            return Walk(start, start, refs, path, new HashSet<string>()) ? path : null;
        }

        static bool Walk(string start, string current, Dictionary<string, string[]> refs, List<string> path, HashSet<string> seen)
        {
            if (!refs.TryGetValue(current, out var next)) return false;
            foreach (var n in next)
            {
                path.Add(n);
                if (n == start) return true;
                if (seen.Add(n) && Walk(start, n, refs, path, seen)) return true;
                path.RemoveAt(path.Count - 1);
            }
            return false;
        }

        static Project Parse(string path)
        {
            var xml = XDocument.Load(path);
            string[] Includes(string item) => xml.Descendants(item)
                .Select(e => (string)e.Attribute("Include"))
                .Where(s => !string.IsNullOrEmpty(s))
                .ToArray();

            return new Project
            {
                Path = path,
                Name = System.IO.Path.GetFileNameWithoutExtension(path),
                TargetFramework = xml.Descendants("TargetFramework").Select(e => e.Value.Trim()).FirstOrDefault(),
                References = Includes("ProjectReference")
                    .Select(r => System.IO.Path.GetFileNameWithoutExtension(r.Replace('\\', '/')))
                    .ToArray(),
                Packages = Includes("PackageReference"),
            };
        }
    }
}
