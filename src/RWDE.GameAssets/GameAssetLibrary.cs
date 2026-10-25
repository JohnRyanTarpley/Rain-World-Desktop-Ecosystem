using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RWDE.GameAssets.Cache;
using RWDE.GameAssets.Install;
using RWDE.GameAssets.Merge;

namespace RWDE.GameAssets
{
    public sealed class GameAssetOptions
    {
        /// <summary>Install path from Settings; null or empty to auto-detect through Steam.</summary>
        public string InstallPathOverride;

        /// <summary>Cache folder; null uses <see cref="GameAssetCache.DefaultRoot"/>.</summary>
        public string CacheRoot;

        /// <summary>Steam folders to search; null uses <see cref="RainWorldInstallLocator.DefaultSteamRoots"/>.</summary>
        public IEnumerable<string> SteamRoots;

        /// <summary>DLC mods under <c>StreamingAssets/mods/</c>, in the game's load order.</summary>
        public IReadOnlyList<string> Mods = new[] { "moreslugcats", "watcher" };

        /// <summary>Region folders under <c>world/</c> to index.</summary>
        public IReadOnlyList<string> Regions = new[] { "su" };
    }

    /// <summary>
    /// Read-only access to the user's own Rain World assets: atlases (extracted base-game ones plus the
    /// mods' loose ones), palettes, sounds and world files with modify-file merging. Every lookup returns
    /// an <see cref="AssetResult{T}"/>; when the game is missing, all lookups report "not found" so the
    /// app can fall back to placeholders.
    /// </summary>
    public sealed class GameAssetLibrary
    {
        private readonly Dictionary<string, AtlasAsset> _atlases = new Dictionary<string, AtlasAsset>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, FileAsset> _palettes = new Dictionary<string, FileAsset>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, FileAsset> _sounds = new Dictionary<string, FileAsset>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _worldFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<KeyValuePair<string, string>>> _worldModifies =
            new Dictionary<string, List<KeyValuePair<string, string>>>(StringComparer.OrdinalIgnoreCase);
        private readonly string _unavailableReason;

        private GameAssetLibrary(string unavailableReason)
        {
            _unavailableReason = unavailableReason;
        }

        /// <summary>The install in use, or null if none was found.</summary>
        public RainWorldInstall Install { get; private set; }

        public GameAssetCache Cache { get; private set; }

        /// <summary>
        /// The DLC in the install (a DLC's folder exists under <c>StreamingAssets/mods/</c>), or
        /// <see cref="Dlc.None"/> when no install was found. A missing DLC is never an error.
        /// </summary>
        public Dlc InstalledDlc { get; private set; }

        /// <summary>True if a Rain World install was found.</summary>
        public bool Available => Install != null;

        /// <summary>Why the game's assets are unavailable, or null.</summary>
        public string UnavailableReason => _unavailableReason;

        /// <summary>Set if atlas extraction failed; other assets stay available.</summary>
        public string AtlasExtractionError { get; private set; }

        /// <summary><c>soundeffects/sounds.txt</c>, the game's sound id table, if present.</summary>
        public string SoundDefinitionsPath { get; private set; }

        public IEnumerable<string> AtlasNames => _atlases.Keys;
        public IEnumerable<string> PaletteNames => _palettes.Keys;
        public IEnumerable<string> SoundNames => _sounds.Keys;
        /// <summary>World file paths such as "su/world_su.txt".</summary>
        public IEnumerable<string> WorldFileNames => _worldFiles.Keys.Union(_worldModifies.Keys, StringComparer.OrdinalIgnoreCase);

        /// <summary>Finds the install, extracts the packed atlases if needed and indexes the loose files.</summary>
        public static GameAssetLibrary Open(GameAssetOptions options = null)
        {
            options = options ?? new GameAssetOptions();
            var lookup = RainWorldInstallLocator.Locate(options.InstallPathOverride, options.SteamRoots);
            if (!lookup.Found) return new GameAssetLibrary(lookup.Failure);

            var library = new GameAssetLibrary(null)
            {
                Install = lookup.Install,
                Cache = new GameAssetCache(options.CacheRoot ?? GameAssetCache.DefaultRoot),
                InstalledDlc = DlcDetector.Detect(lookup.Install),
            };

            try
            {
                library.Cache.EnsureExtracted(lookup.Install, library.InstalledDlc);
            }
            catch (Exception e) when (e is IOException || e is InvalidDataException || e is NotSupportedException ||
                                      e is UnauthorizedAccessException)
            {
                library.AtlasExtractionError = "Could not extract the game's atlases: " + e.Message;
            }

            library.BuildIndex(options);
            return library;
        }

        /// <summary>A Futile atlas by name, e.g. "rainWorld", "uiSprites", "rainworldwatcher".</summary>
        public AssetResult<AtlasAsset> GetAtlas(string name)
        {
            if (!Available) return AssetResult<AtlasAsset>.NotFound(_unavailableReason);
            name = StripPrefix(name, "Atlases/");
            if (_atlases.TryGetValue(name, out var atlas)) return AssetResult<AtlasAsset>.Of(atlas);
            return AssetResult<AtlasAsset>.NotFound(AtlasExtractionError == null
                ? $"No atlas named '{name}'"
                : $"No atlas named '{name}'. {AtlasExtractionError}");
        }

        /// <summary>A palette image by file name without extension, e.g. "palette1" or "effectColors".</summary>
        public AssetResult<FileAsset> GetPalette(string name)
        {
            if (!Available) return AssetResult<FileAsset>.NotFound(_unavailableReason);
            name = Path.GetFileNameWithoutExtension(StripPrefix(name, "palettes/"));
            return _palettes.TryGetValue(name, out var palette)
                ? AssetResult<FileAsset>.Of(palette)
                : AssetResult<FileAsset>.NotFound($"No palette named '{name}'");
        }

        /// <summary><c>palettes/palette&lt;number&gt;.png</c>, as loaded by <c>RoomCamera.LoadPalette</c>.</summary>
        public AssetResult<FileAsset> GetPalette(int number) => GetPalette("palette" + number);

        /// <summary>A sound clip by file name without extension, e.g. "batChirp1A".</summary>
        public AssetResult<FileAsset> GetSound(string name)
        {
            if (!Available) return AssetResult<FileAsset>.NotFound(_unavailableReason);
            name = Path.GetFileNameWithoutExtension(name);
            return _sounds.TryGetValue(name, out var sound)
                ? AssetResult<FileAsset>.Of(sound)
                : AssetResult<FileAsset>.NotFound($"No sound named '{name}'");
        }

        /// <summary>
        /// A world file with every mod's modify file merged in, e.g. "su/world_su.txt" (a leading
        /// "world/" is accepted). Campaign filters such as "(White,Yellow)" are left in the text.
        /// </summary>
        public AssetResult<WorldFileAsset> GetWorldFile(string relativePath)
        {
            if (!Available) return AssetResult<WorldFileAsset>.NotFound(_unavailableReason);
            var key = NormaliseWorldPath(relativePath);
            var hasBase = _worldFiles.TryGetValue(key, out var basePath);
            _worldModifies.TryGetValue(key, out var modifies);
            if (!hasBase && modifies == null) return AssetResult<WorldFileAsset>.NotFound($"No world file 'world/{key}'");

            // As in the game, a modify file with no base file merges into an empty file.
            var baseLines = hasBase ? TextLines.Split(File.ReadAllText(basePath)) : new string[0];
            if (modifies == null)
                return AssetResult<WorldFileAsset>.Of(new WorldFileAsset(key, baseLines, new string[0], new string[0]));

            var files = modifies.Select(m => new ModifyFile(m.Key, TextLines.Split(File.ReadAllText(m.Value)))).ToList();
            var merged = ModMerge.Apply(key, baseLines, files);
            return AssetResult<WorldFileAsset>.Of(new WorldFileAsset(key, merged.Lines,
                modifies.Select(m => m.Key).ToArray(), merged.Warnings.ToArray()));
        }

        private void BuildIndex(GameAssetOptions options)
        {
            var streaming = Install.StreamingAssetsPath;

            foreach (var atlas in Cache.ListAtlases())
                _atlases[atlas.Key] = new AtlasAsset(atlas.Key, atlas.Value, Path.ChangeExtension(atlas.Value, ".txt"), "base");

            foreach (var png in Files(Path.Combine(streaming, "palettes"), "*.png", SearchOption.TopDirectoryOnly))
                _palettes[Path.GetFileNameWithoutExtension(png)] = new FileAsset(Path.GetFileNameWithoutExtension(png), png);

            // loadedsoundeffects holds the clips (including ambient/); soundeffects holds the id table and rain loops.
            foreach (var folder in new[] { "soundeffects", "loadedsoundeffects" })
            foreach (var file in Files(Path.Combine(streaming, folder), "*", SearchOption.AllDirectories))
            {
                var extension = Path.GetExtension(file).ToLowerInvariant();
                if (extension != ".wav" && extension != ".ogg" && extension != ".pkf") continue;
                var name = Path.GetFileNameWithoutExtension(file);
                _sounds[name] = new FileAsset(name, file);
            }
            var soundTable = Path.Combine(streaming, "soundeffects", "sounds.txt");
            if (File.Exists(soundTable)) SoundDefinitionsPath = soundTable;

            foreach (var region in options.Regions)
                IndexWorldFolder(Path.Combine(streaming, "world", region), region);

            foreach (var mod in options.Mods)
            {
                var modRoot = Path.Combine(streaming, "mods", mod);
                if (!Directory.Exists(modRoot)) continue;

                foreach (var png in Files(Path.Combine(modRoot, "atlases"), "*.png", SearchOption.TopDirectoryOnly))
                {
                    var data = Path.ChangeExtension(png, ".txt");
                    var name = Path.GetFileNameWithoutExtension(png);
                    if (File.Exists(data)) _atlases[name] = new AtlasAsset(name, png, data, mod);
                }

                foreach (var region in options.Regions)
                {
                    // Whole-file replacements or additions, then modify files.
                    IndexWorldFolder(Path.Combine(modRoot, "world", region), region);
                    foreach (var file in Files(Path.Combine(modRoot, "modify", "world", region), "*.txt", SearchOption.TopDirectoryOnly))
                    {
                        var key = region + "/" + Path.GetFileName(file).ToLowerInvariant();
                        if (!_worldModifies.TryGetValue(key, out var list)) _worldModifies[key] = list = new List<KeyValuePair<string, string>>();
                        list.Add(new KeyValuePair<string, string>(mod, file));
                    }
                }
            }
        }

        private void IndexWorldFolder(string folder, string region)
        {
            foreach (var file in Files(folder, "*.txt", SearchOption.TopDirectoryOnly))
                _worldFiles[region + "/" + Path.GetFileName(file).ToLowerInvariant()] = file;
        }

        private static IEnumerable<string> Files(string folder, string pattern, SearchOption option)
        {
            return Directory.Exists(folder)
                ? Directory.GetFiles(folder, pattern, option).OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                : Enumerable.Empty<string>();
        }

        private static string NormaliseWorldPath(string path)
        {
            path = StripPrefix((path ?? "").Replace('\\', '/').TrimStart('/'), "world/");
            return path.ToLowerInvariant();
        }

        private static string StripPrefix(string value, string prefix)
        {
            value = value ?? "";
            return value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? value.Substring(prefix.Length) : value;
        }
    }
}
