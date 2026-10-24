using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace RWDE.Tests.Common
{
    /// <summary>
    /// The department dependency graph (see the build-release context), checked against the
    /// asmdefs under <c>Assets/Scripts/</c>. A cycle or a Sim → engine reference is a review blocker.
    /// </summary>
    public static class AsmdefRules
    {
        public const string Sim = "RWDE.Sim";
        public const string GameAssets = "RWDE.GameAssets";
        public const string Shell = "RWDE.Shell";
        public const string Graphics = "RWDE.Graphics";
        public const string Audio = "RWDE.Audio";
        public const string Interaction = "RWDE.Interaction";
        public const string UI = "RWDE.UI";
        public const string App = "RWDE.App";

        /// <summary>Which RWDE assemblies each department assembly may reference.</summary>
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

        /// <summary>Department assemblies that must not reference UnityEngine.</summary>
        public static readonly string[] EngineFree = { Sim };

        public sealed class Asmdef
        {
            public string Path;
            public string Name;
            public string Guid;
            public string[] References;
            public bool NoEngineReferences;
        }

        /// <summary>Reads every asmdef under <c>Assets/Scripts/</c>, resolving GUID references to names.</summary>
        public static List<Asmdef> LoadAll() => LoadAll(TestPaths.ScriptsRoot);

        public static List<Asmdef> LoadAll(string root)
        {
            var all = Directory.GetFiles(root, "*.asmdef", SearchOption.AllDirectories).Select(Parse).ToList();
            var byGuid = all.Where(a => a.Guid != null).ToDictionary(a => a.Guid, a => a.Name);
            foreach (var a in all)
            {
                a.References = a.References
                    .Select(r => r.StartsWith("GUID:", StringComparison.Ordinal)
                        && byGuid.TryGetValue(r.Substring(5), out var name) ? name : r)
                    .ToArray();
            }
            return all;
        }

        /// <summary>Returns a message per rule <paramref name="assemblyName"/> breaks; empty when it is clean.</summary>
        public static List<string> Violations(string assemblyName, IList<Asmdef> asmdefs)
        {
            var errors = new List<string>();
            var asmdef = asmdefs.FirstOrDefault(a => a.Name == assemblyName);
            if (asmdef == null)
            {
                errors.Add(assemblyName + ": no asmdef with this name under Assets/Scripts");
                return errors;
            }
            if (!Allowed.TryGetValue(assemblyName, out var allowed))
            {
                errors.Add(assemblyName + ": not a known department assembly; add it to AsmdefRules.Allowed");
                return errors;
            }

            foreach (var reference in asmdef.References)
            {
                if (reference.StartsWith("RWDE.", StringComparison.Ordinal) && !allowed.Contains(reference))
                    errors.Add(assemblyName + " must not reference " + reference);
            }
            if (EngineFree.Contains(assemblyName) && !asmdef.NoEngineReferences)
                errors.Add(assemblyName + " must set \"noEngineReferences\": true");

            var cycle = FindCycle(assemblyName, asmdefs);
            if (cycle != null)
                errors.Add("dependency cycle: " + string.Join(" -> ", cycle));
            return errors;
        }

        /// <summary>Fails the current test when <paramref name="assemblyName"/>'s asmdef breaks the graph.</summary>
        public static void AssertFollowsRules(string assemblyName)
        {
            var errors = Violations(assemblyName, LoadAll());
            if (errors.Count > 0)
                Assert.Fail(string.Join("\n", errors));
        }

        /// <summary>A reference path that leads from <paramref name="start"/> back to itself, or null.</summary>
        public static List<string> FindCycle(string start, IList<Asmdef> asmdefs)
        {
            var refs = asmdefs.ToDictionary(a => a.Name, a => a.References);
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

        static Asmdef Parse(string path)
        {
            var json = File.ReadAllText(path);
            var metaPath = path + ".meta";
            string guid = null;
            if (File.Exists(metaPath))
            {
                var m = Regex.Match(File.ReadAllText(metaPath), @"^guid:\s*([0-9a-f]{32})", RegexOptions.Multiline);
                if (m.Success) guid = m.Groups[1].Value;
            }

            var refsBlock = Regex.Match(json, "\"references\"\\s*:\\s*\\[(.*?)\\]", RegexOptions.Singleline);
            return new Asmdef
            {
                Path = path,
                Name = Regex.Match(json, "\"name\"\\s*:\\s*\"([^\"]*)\"").Groups[1].Value,
                Guid = guid,
                References = refsBlock.Success
                    ? Regex.Matches(refsBlock.Groups[1].Value, "\"([^\"]*)\"").Cast<Match>().Select(x => x.Groups[1].Value).ToArray()
                    : new string[0],
                NoEngineReferences = Regex.IsMatch(json, "\"noEngineReferences\"\\s*:\\s*true"),
            };
        }
    }
}
