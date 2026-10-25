using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using RWDE.GameAssets.Install;

namespace RWDE.GameAssets.Cache
{
    /// <summary>
    /// The per-user cache of assets extracted from the game, under
    /// <c>%APPDATA%/RainWorldDesktop/cache/</c>. Extraction runs on first use and again whenever the
    /// game version or the packed asset file changes. Nothing here is ever written into the repo.
    /// </summary>
    public sealed class GameAssetCache
    {
        /// <summary>Bump when the extracted layout changes, so old caches are rebuilt.</summary>
        internal const int CacheFormatVersion = 1;

        private const string StampFileName = "cache-stamp.txt";

        public GameAssetCache(string cacheRoot)
        {
            RootPath = cacheRoot;
        }

        public string RootPath { get; }
        public string AtlasDirectory => Path.Combine(RootPath, "atlases");
        private string StampPath => Path.Combine(RootPath, StampFileName);

        /// <summary><c>%APPDATA%/RainWorldDesktop/cache</c>.</summary>
        public static string DefaultRoot =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RainWorldDesktop", "cache");

        /// <summary>Names of the extracted atlases skipped last time, with the reason.</summary>
        public IReadOnlyList<string> SkippedAtlases { get; private set; } = new string[0];

        /// <summary>True if the last call to <see cref="EnsureExtracted"/> extracted (rather than reused the cache).</summary>
        public bool ExtractedThisRun { get; private set; }

        /// <summary>
        /// Extracts the packed atlases unless the cache already matches this install. Atlases of a DLC
        /// that isn't installed are left out (see <see cref="DlcDetector.IsPackedAtlasAvailable"/>).
        /// </summary>
        public void EnsureExtracted(RainWorldInstall install, Dlc installedDlc)
        {
            var stamp = BuildStamp(install, installedDlc);
            ExtractedThisRun = false;
            if (File.Exists(StampPath) && File.ReadAllText(StampPath) == stamp && Directory.Exists(AtlasDirectory))
            {
                SkippedAtlases = new string[0];
                return;
            }

            if (File.Exists(StampPath)) File.Delete(StampPath);
            if (Directory.Exists(AtlasDirectory)) Directory.Delete(AtlasDirectory, recursive: true);

            var report = AtlasExtractor.Extract(install.ResourcesAssetsPath, AtlasDirectory,
                name => DlcDetector.IsPackedAtlasAvailable(name, installedDlc));
            SkippedAtlases = report.Skipped;
            ExtractedThisRun = true;
            // Written last: an interrupted extraction leaves no stamp and is redone next launch.
            File.WriteAllText(StampPath, stamp);
        }

        /// <summary>Atlas name (as packed, e.g. "rainWorld") to the extracted png path.</summary>
        public Dictionary<string, string> ListAtlases()
        {
            var atlases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!Directory.Exists(AtlasDirectory)) return atlases;
            foreach (var png in Directory.GetFiles(AtlasDirectory, "*.png").OrderBy(p => p, StringComparer.Ordinal))
            {
                if (File.Exists(Path.ChangeExtension(png, ".txt")))
                    atlases[Path.GetFileNameWithoutExtension(png)] = png;
            }
            return atlases;
        }

        internal static string BuildStamp(RainWorldInstall install, Dlc installedDlc)
        {
            var assets = new FileInfo(install.ResourcesAssetsPath);
            return string.Join("\n",
                "format=" + CacheFormatVersion.ToString(CultureInfo.InvariantCulture),
                "gameVersion=" + install.GameVersion,
                "resourcesSize=" + assets.Length.ToString(CultureInfo.InvariantCulture),
                "resourcesModifiedUtc=" + assets.LastWriteTimeUtc.Ticks.ToString(CultureInfo.InvariantCulture),
                "dlc=" + installedDlc,
                "");
        }
    }
}
