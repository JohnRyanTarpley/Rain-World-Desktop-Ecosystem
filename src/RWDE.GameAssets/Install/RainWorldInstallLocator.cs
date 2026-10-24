using System;
using System.Collections.Generic;
using System.IO;

namespace RWDE.GameAssets.Install
{
    public enum InstallSource
    {
        SettingsOverride,
        SteamLibrary,
    }

    /// <summary>A validated Rain World install directory.</summary>
    public sealed class RainWorldInstall
    {
        public const string DataFolderName = "RainWorld_Data";

        internal RainWorldInstall(string rootPath, InstallSource source)
        {
            RootPath = rootPath;
            Source = source;
        }

        /// <summary>Folder containing <c>RainWorld.exe</c>.</summary>
        public string RootPath { get; }
        public InstallSource Source { get; }
        public string DataPath => Path.Combine(RootPath, DataFolderName);
        public string StreamingAssetsPath => Path.Combine(DataPath, "StreamingAssets");
        public string ResourcesAssetsPath => Path.Combine(DataPath, "resources.assets");

        /// <summary>Contents of <c>StreamingAssets/GameVersion.txt</c> (e.g. "v1.11.8"), or "unknown".</summary>
        public string GameVersion
        {
            get
            {
                var path = Path.Combine(StreamingAssetsPath, "GameVersion.txt");
                return File.Exists(path) ? File.ReadAllText(path).Trim() : "unknown";
            }
        }

        /// <summary>True if <paramref name="rootPath"/> looks like a Rain World install.</summary>
        public static bool IsValid(string rootPath)
        {
            if (string.IsNullOrEmpty(rootPath)) return false;
            var data = Path.Combine(rootPath, DataFolderName);
            return File.Exists(Path.Combine(data, "resources.assets")) &&
                   Directory.Exists(Path.Combine(data, "StreamingAssets"));
        }
    }

    /// <summary>The outcome of looking for the install: the install, or why none was found.</summary>
    public sealed class InstallLookup
    {
        private InstallLookup(RainWorldInstall install, string failure, IReadOnlyList<string> searched)
        {
            Install = install;
            Failure = failure;
            SearchedPaths = searched;
        }

        public RainWorldInstall Install { get; }
        public bool Found => Install != null;
        /// <summary>Human-readable reason when <see cref="Found"/> is false.</summary>
        public string Failure { get; }
        public IReadOnlyList<string> SearchedPaths { get; }

        internal static InstallLookup Success(RainWorldInstall install, IReadOnlyList<string> searched) =>
            new InstallLookup(install, null, searched);

        internal static InstallLookup NotFound(string failure, IReadOnlyList<string> searched) =>
            new InstallLookup(null, failure, searched);
    }

    /// <summary>
    /// Finds the Rain World install: the path from Settings first, then every Steam library listed in
    /// <c>steamapps/libraryfolders.vdf</c>, preferring a library that lists Rain World's app id.
    /// </summary>
    public static class RainWorldInstallLocator
    {
        public const string RainWorldAppId = "312520";
        private const string InstallFolderName = "Rain World";

        /// <param name="settingsOverride">Install path set by the user, or null/empty for auto-detect.</param>
        /// <param name="steamRoots">Steam install folders to read; null uses <see cref="DefaultSteamRoots"/>.</param>
        public static InstallLookup Locate(string settingsOverride, IEnumerable<string> steamRoots = null)
        {
            var searched = new List<string>();

            if (!string.IsNullOrWhiteSpace(settingsOverride))
            {
                var root = NormaliseOverride(settingsOverride.Trim());
                searched.Add(root);
                return RainWorldInstall.IsValid(root)
                    ? InstallLookup.Success(new RainWorldInstall(root, InstallSource.SettingsOverride), searched)
                    : InstallLookup.NotFound($"The install path in Settings is not a Rain World install: {settingsOverride}", searched);
            }

            foreach (var library in FindLibraries(steamRoots ?? DefaultSteamRoots()))
            {
                var candidate = Path.Combine(library, "steamapps", "common", InstallFolderName);
                if (searched.Contains(candidate)) continue;
                searched.Add(candidate);
                if (RainWorldInstall.IsValid(candidate))
                    return InstallLookup.Success(new RainWorldInstall(candidate, InstallSource.SteamLibrary), searched);
            }

            return InstallLookup.NotFound("Rain World was not found in any Steam library. Set the install path in Settings.", searched);
        }

        /// <summary>
        /// Library folders from each Steam root's <c>libraryfolders.vdf</c>, libraries that list Rain World first.
        /// The Steam root itself is always included as a library.
        /// </summary>
        public static List<string> FindLibraries(IEnumerable<string> steamRoots)
        {
            var preferred = new List<string>();
            var others = new List<string>();
            foreach (var steamRoot in steamRoots)
            {
                if (string.IsNullOrEmpty(steamRoot) || !Directory.Exists(steamRoot)) continue;
                var vdfPath = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
                if (File.Exists(vdfPath))
                {
                    VdfNode root;
                    try
                    {
                        root = VdfParser.Parse(File.ReadAllText(vdfPath));
                    }
                    catch (InvalidDataException)
                    {
                        root = null;
                    }

                    var folders = root?["libraryfolders"];
                    if (folders != null)
                    {
                        foreach (var entry in folders.Children)
                        {
                            // Old format: "1" "D:\\Games"; new format: "1" { "path" "D:\\Games" "apps" { ... } }.
                            var path = entry.Value.Value ?? entry.Value["path"]?.Value;
                            if (string.IsNullOrEmpty(path)) continue;
                            var hasRainWorld = entry.Value["apps"]?[RainWorldAppId] != null;
                            AddUnique(hasRainWorld ? preferred : others, path);
                        }
                    }
                }
                AddUnique(others, steamRoot);
            }

            foreach (var path in others) AddUnique(preferred, path);
            return preferred;
        }

        /// <summary>Steam roots from the registry (when available) and the usual Program Files folders.</summary>
        public static IEnumerable<string> DefaultSteamRoots()
        {
            var roots = new List<string>();
            AddUnique(roots, ReadRegistry(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath"));
            AddUnique(roots, ReadRegistry(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath"));
            AddUnique(roots, ReadRegistry(@"HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam", "InstallPath"));
            AddUnique(roots, CombineEnv("ProgramFiles(x86)", "Steam"));
            AddUnique(roots, CombineEnv("ProgramFiles", "Steam"));
            return roots;
        }

        private static string NormaliseOverride(string path)
        {
            // Accept the install folder, RainWorld_Data, or RainWorld.exe itself.
            if (File.Exists(path)) path = Path.GetDirectoryName(path);
            var trimmed = path.TrimEnd('\\', '/');
            if (string.Equals(Path.GetFileName(trimmed), RainWorldInstall.DataFolderName, StringComparison.OrdinalIgnoreCase))
                return Path.GetDirectoryName(trimmed);
            return trimmed;
        }

        private static void AddUnique(List<string> list, string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            var normalised = path.Replace('/', Path.DirectorySeparatorChar).TrimEnd(Path.DirectorySeparatorChar);
            foreach (var existing in list)
                if (string.Equals(existing, normalised, StringComparison.OrdinalIgnoreCase))
                    return;
            list.Add(normalised);
        }

        private static string CombineEnv(string variable, string child)
        {
            var value = Environment.GetEnvironmentVariable(variable);
            return string.IsNullOrEmpty(value) ? null : Path.Combine(value, child);
        }

        // Returns null on any platform other than Windows.
        private static string ReadRegistry(string keyName, string valueName)
        {
            if (!OperatingSystem.IsWindows()) return null;
            try
            {
                return Microsoft.Win32.Registry.GetValue(keyName, valueName, null) as string;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
