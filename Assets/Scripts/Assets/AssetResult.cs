namespace RWDE.GameAssets
{
    /// <summary>
    /// The result of an asset lookup. When <see cref="Found"/> is false, <see cref="Missing"/> says why,
    /// and callers fall back to placeholders.
    /// </summary>
    public readonly struct AssetResult<T> where T : class
    {
        private AssetResult(T value, string missing)
        {
            Value = value;
            Missing = missing;
        }

        public T Value { get; }
        public bool Found => Value != null;
        /// <summary>Why the asset is unavailable, or null when found.</summary>
        public string Missing { get; }

        public static AssetResult<T> Of(T value) => new AssetResult<T>(value, null);
        public static AssetResult<T> NotFound(string reason) => new AssetResult<T>(null, reason);

        public bool TryGet(out T value)
        {
            value = Value;
            return Found;
        }

        public override string ToString() => Found ? Value.ToString() : "Not found: " + Missing;
    }

    /// <summary>A Futile atlas on disk: the texture and its TexturePacker JSON.</summary>
    public sealed class AtlasAsset
    {
        public AtlasAsset(string name, string imagePath, string dataPath, string source)
        {
            Name = name;
            ImagePath = imagePath;
            DataPath = dataPath;
            Source = source;
        }

        public string Name { get; }
        public string ImagePath { get; }
        public string DataPath { get; }
        /// <summary>"base" for atlases extracted from the game, otherwise the mod id.</summary>
        public string Source { get; }

        public override string ToString() => $"{Name} ({Source}): {ImagePath}";
    }

    /// <summary>A loose file in the install (palette png, sound clip, ...).</summary>
    public sealed class FileAsset
    {
        public FileAsset(string name, string path)
        {
            Name = name;
            Path = path;
        }

        public string Name { get; }
        public string Path { get; }

        public override string ToString() => $"{Name}: {Path}";
    }

    /// <summary>A world file after the mods' <c>modify/</c> files have been merged in.</summary>
    public sealed class WorldFileAsset
    {
        public WorldFileAsset(string relativePath, string[] lines, string[] appliedMods, string[] warnings)
        {
            RelativePath = relativePath;
            Lines = lines;
            AppliedMods = appliedMods;
            Warnings = warnings;
        }

        /// <summary>Path under <c>world/</c>, e.g. "su/world_su.txt".</summary>
        public string RelativePath { get; }
        public string[] Lines { get; }
        public string Text => string.Join("\n", Lines) + "\n";
        /// <summary>Mods whose modify files were merged, in the order applied.</summary>
        public string[] AppliedMods { get; }
        /// <summary>Merge directives this port does not apply.</summary>
        public string[] Warnings { get; }

        public override string ToString() => $"world/{RelativePath} (+{string.Join(", ", AppliedMods)})";
    }
}
