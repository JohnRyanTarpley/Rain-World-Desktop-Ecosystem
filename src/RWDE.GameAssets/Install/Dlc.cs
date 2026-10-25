using System;
using System.Collections.Generic;
using System.IO;

namespace RWDE.GameAssets.Install
{
    /// <summary>Rain World's DLC. Only the base game is required; each DLC is optional.</summary>
    [Flags]
    public enum Dlc
    {
        None = 0,
        /// <summary>Downpour, <c>StreamingAssets/mods/moreslugcats</c>.</summary>
        Downpour = 1,
        /// <summary>The Watcher, <c>StreamingAssets/mods/watcher</c>.</summary>
        Watcher = 2,
    }

    /// <summary>
    /// Which DLC an install has. A DLC is installed when its folder exists under
    /// <c>StreamingAssets/mods/</c>; Steam ownership and Rain World's own Remix enable state are ignored.
    /// </summary>
    public static class DlcDetector
    {
        public const string DownpourFolder = "moreslugcats";
        public const string WatcherFolder = "watcher";

        /// <summary>
        /// Atlases packed in <c>resources.assets</c> that belong to a DLC, and the DLC that need them.
        /// The game loads these under <c>ModManager.DLCShared</c>, i.e. with Downpour or The Watcher.
        /// </summary>
        private static readonly Dictionary<string, Dlc> PackedAtlasOwners = new Dictionary<string, Dlc>(StringComparer.OrdinalIgnoreCase)
        {
            ["rainWorldMSC"] = Dlc.Downpour | Dlc.Watcher,
            ["uiSpritesMSC"] = Dlc.Downpour | Dlc.Watcher,
        };

        public static string FolderName(Dlc dlc)
        {
            switch (dlc)
            {
                case Dlc.Downpour: return DownpourFolder;
                case Dlc.Watcher: return WatcherFolder;
                default: throw new ArgumentOutOfRangeException(nameof(dlc), dlc, "Expected a single DLC");
            }
        }

        public static Dlc Detect(RainWorldInstall install)
        {
            var mods = Path.Combine(install.StreamingAssetsPath, "mods");
            var installed = Dlc.None;
            foreach (var dlc in new[] { Dlc.Downpour, Dlc.Watcher })
            {
                if (Directory.Exists(Path.Combine(mods, FolderName(dlc)))) installed |= dlc;
            }
            return installed;
        }

        /// <summary>True if a packed atlas should be extracted: it is base-game, or a DLC that uses it is installed.</summary>
        public static bool IsPackedAtlasAvailable(string atlasName, Dlc installed)
        {
            return !PackedAtlasOwners.TryGetValue(atlasName, out var owners) || (owners & installed) != 0;
        }
    }
}
