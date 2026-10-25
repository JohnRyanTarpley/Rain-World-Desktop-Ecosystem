using System;
using System.Collections.Generic;
using RWDE.GameAssets;
using RWDE.GameAssets.Install;

namespace RWDE.Graphics
{
    /// <summary>
    /// The Futile atlases the game loads at startup (<c>RainWorld.LoadResources</c> and
    /// <c>RainWorld.LoadModResources</c>), so every element name a ported graphics module asks for resolves
    /// as it does in the game. A DLC's atlases are loaded only when that DLC is installed; their absence
    /// is normal and not reported.
    /// </summary>
    /// <remarks>
    /// The single-texture "atlases" from <c>Resources</c> (LightMask0, maze, glyphs, ...) are not in the
    /// asset cache and are left out, as are the Expedition, Jolly Co-op and MMF atlases.
    /// </remarks>
    public static class GameAtlases
    {
        /// <summary>One atlas and the DLC it needs (<see cref="Dlc.None"/> for the base game).</summary>
        public readonly struct Entry
        {
            public Entry(string name, Dlc neededDlc)
            {
                Name = name;
                NeededDlc = neededDlc;
            }

            public string Name { get; }
            /// <summary>The atlas loads if any of these DLC is installed; <see cref="Dlc.None"/> means always.</summary>
            public Dlc NeededDlc { get; }

            public bool IsAvailable(Dlc installed) => NeededDlc == Dlc.None || (NeededDlc & installed) != 0;

            public override string ToString() => NeededDlc == Dlc.None ? Name : $"{Name} ({NeededDlc})";
        }

        /// <summary>Every atlas, in the game's load order.</summary>
        public static readonly IReadOnlyList<Entry> All = new[]
        {
            // RainWorld.LoadResources
            new Entry("rainWorld", Dlc.None),
            new Entry("uiSprites", Dlc.None),
            new Entry("shelterGate", Dlc.None),
            new Entry("regionGate", Dlc.None),
            new Entry("waterSprites", Dlc.None),
            // RainWorld.LoadModResources: ModManager.DLCShared is Downpour or The Watcher.
            new Entry("rainWorldMSC", Dlc.Downpour | Dlc.Watcher),
            new Entry("uiSpritesMSC", Dlc.Downpour | Dlc.Watcher),
            // ModManager.Watcher
            new Entry("rainworldwatcher", Dlc.Watcher),
            new Entry("uispriteswatcher", Dlc.Watcher),
            new Entry("au_buildings", Dlc.Watcher),
        };

        /// <summary>The atlases to load for an install with <paramref name="installed"/> DLC, in load order.</summary>
        public static IEnumerable<Entry> For(Dlc installed)
        {
            foreach (var entry in All)
            {
                if (entry.IsAvailable(installed)) yield return entry;
            }
        }

        /// <summary>What <see cref="LoadAll"/> loaded and which expected atlases it could not.</summary>
        public sealed class LoadReport
        {
            public List<FAtlas> Loaded { get; } = new List<FAtlas>();
            /// <summary>One message per atlas that should be there but isn't, or failed to load.</summary>
            public List<string> Problems { get; } = new List<string>();
        }

        /// <summary>Loads every atlas for <see cref="GameAssetLibrary.InstalledDlc"/> through <paramref name="load"/>.</summary>
        public static LoadReport LoadAll(GameAssetLibrary assets, Func<AtlasAsset, FAtlas> load) =>
            LoadAll(assets.InstalledDlc, assets.GetAtlas, load);

        /// <summary>
        /// Looks up each atlas for <paramref name="installed"/> with <paramref name="find"/> and loads it with
        /// <paramref name="load"/> (e.g. <c>FutileRenderer.LoadAtlas</c>). A missing atlas or one that fails
        /// to load is reported and the rest still load; a programming error such as a duplicate element
        /// name (<see cref="FutileException"/>) is thrown.
        /// </summary>
        public static LoadReport LoadAll(Dlc installed, Func<string, AssetResult<AtlasAsset>> find, Func<AtlasAsset, FAtlas> load)
        {
            var report = new LoadReport();
            foreach (var entry in For(installed))
            {
                var found = find(entry.Name);
                if (!found.TryGet(out var asset))
                {
                    report.Problems.Add($"Missing atlas {entry}: {found.Missing}");
                    continue;
                }
                try
                {
                    report.Loaded.Add(load(asset));
                }
                catch (Exception e) when (e is System.IO.IOException || e is System.IO.InvalidDataException ||
                                          e is UnauthorizedAccessException || e is NotSupportedException)
                {
                    report.Problems.Add($"Could not load atlas {entry}: {e.Message}");
                }
            }
            return report;
        }
    }
}
