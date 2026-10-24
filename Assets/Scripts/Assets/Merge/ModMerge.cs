using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RWDE.GameAssets.Merge
{
    /// <summary>Result of merging one file: the merged lines and anything this port could not apply.</summary>
    public sealed class MergeResult
    {
        public MergeResult(string[] lines, IReadOnlyList<string> warnings)
        {
            Lines = lines;
            Warnings = warnings;
        }

        public string[] Lines { get; }
        public IReadOnlyList<string> Warnings { get; }
        public string Text => string.Join("\n", Lines) + "\n";
    }

    /// <summary>
    /// Applies mods' <c>modify/</c> files to a base file in the game's order
    /// (<c>ModManager.ModMerger.ExecutePendingMerge</c>): every mod's <c>[MERGE]</c> block first, in load
    /// order, then every mod's line edits, in load order.
    /// </summary>
    public static class ModMerge
    {
        public static MergeResult Apply(string relativePath, string[] baseLines, IEnumerable<ModifyFile> modifyFiles)
        {
            var files = modifyFiles.ToList();
            var warnings = new List<string>();
            var lines = baseLines;

            foreach (var file in files.Where(f => f.MergeLines != null))
                lines = ApplyMerge(relativePath, lines, file.MergeLines, warnings, file.ModId);

            foreach (var file in files.Where(f => f.HasModifications))
                lines = file.ApplyModifications(lines);

            foreach (var file in files.Where(f => f.SkippedFileDestinations > 0))
                warnings.Add($"{file.ModId}: [FILEDESTINATION] sections in {relativePath} write to other files and are not applied");

            return new MergeResult(lines, warnings);
        }

        // Port of PendingApply.ApplyMerges: the merge strategy depends on the file name.
        private static string[] ApplyMerge(string relativePath, string[] lines, List<string> mergeLines, List<string> warnings, string modId)
        {
            var fileName = Path.GetFileName(relativePath.ToLowerInvariant());
            if (fileName.EndsWith("default alignments.txt") || fileName.EndsWith("sounds.txt") || fileName.EndsWith("locks.txt") ||
                fileName.EndsWith("properties.txt") || (fileName.Contains("properties-") && fileName.EndsWith(".txt")))
                return TextLines.Normalise(MergeUniqueItems(fileName, lines, mergeLines));
            if (fileName.EndsWith("mpmusic.txt") || fileName.EndsWith("regions.txt") || fileName.EndsWith("egates.txt"))
                return TextLines.Normalise(lines.Concat(mergeLines).Distinct());
            if (fileName.Contains("world_") && fileName.EndsWith(".txt"))
                return TextLines.Normalise(WorldFileMerger.Merge(lines, mergeLines));
            if (fileName.EndsWith("_settings.txt") || fileName.Contains("_settingstemplate_") || fileName.Contains("_settings-"))
                return TextLines.Normalise(MergeRoomSettings(lines, mergeLines));
            if (fileName.Contains("map_") && !fileName.Contains("map_image_") && fileName.EndsWith(".txt"))
            {
                warnings.Add($"{modId}: map file [MERGE] for {relativePath} is not ported; base map kept");
                return lines;
            }
            if (fileName.Contains("strings.txt"))
            {
                warnings.Add($"{modId}: strings file [MERGE] for {relativePath} is not ported; base text kept");
                return lines;
            }
            // Any other file: the merge block replaces the file (WriteMergedFile).
            return TextLines.Normalise(mergeLines);
        }

        // Port of MergeUniqueItemsUnordered: "key: value" lines, later keys win.
        private static IEnumerable<string> MergeUniqueItems(string fileName, string[] lines, List<string> mergeLines)
        {
            var items = new OrderedLines();
            var isSounds = fileName.Contains("sounds.txt");
            var isProperties = fileName.Contains("properties.txt");
            var subregions = new List<string>();
            var templates = new List<string>();

            foreach (var text in lines.Concat(mergeLines))
            {
                if (text.StartsWith("//")) continue;
                var colon = text.IndexOf(':');
                if (colon < 0) continue;
                var key = text.Substring(0, colon);
                if (isSounds)
                {
                    var slash = text.IndexOf('/');
                    if (slash >= 0 && slash < colon) key = text.Substring(0, slash);
                }
                if (isProperties)
                {
                    key = SecondColonKey(text, "Room_Attr:") ?? SecondColonKey(text, "Broken Shelters:") ?? key;
                    if (text.StartsWith("Subregion:"))
                    {
                        var subregion = text.Substring("Subregion:".Length).Trim();
                        if (!subregions.Contains(subregion)) subregions.Add(subregion);
                        continue;
                    }
                    if (text.StartsWith("Room Setting Templates:"))
                    {
                        foreach (var item in text.Substring("Room Setting Templates:".Length).Trim().Split(','))
                            if (!templates.Contains(item.Trim())) templates.Add(item.Trim());
                        continue;
                    }
                }
                items[key] = text;
            }

            if (templates.Count > 0) items["Room Setting Templates"] = "Room Setting Templates: " + string.Join(", ", templates);
            for (var i = 0; i < subregions.Count; i++) items["Subregion_" + i] = "Subregion: " + subregions[i];
            return items.Values;
        }

        private static string SecondColonKey(string text, string prefix)
        {
            if (!text.StartsWith(prefix)) return null;
            var rest = text.Substring(prefix.Length);
            return rest.Contains(":") ? text.Substring(0, prefix.Length + rest.IndexOf(':')) : null;
        }

        // Port of MergeRoomSettings (without the palette renumbering the game does for mod palette clashes).
        private static IEnumerable<string> MergeRoomSettings(string[] lines, List<string> mergeLines)
        {
            var items = new OrderedLines();
            foreach (var text in lines.Concat(mergeLines))
            {
                var colon = text.IndexOf(':');
                if (colon < 0) continue;
                var key = text.Substring(0, colon);
                if ((key == "Effects" || key == "PlacedObjects" || key == "AmbientSounds" || key == "Triggers") && items.ContainsKey(key))
                {
                    // Union of comma-separated entries. The game slices the old line with the new line's colon
                    // index, which is the same index because the key is the same.
                    var union = new List<string>();
                    foreach (var entry in items[key].Substring(colon + 1).Split(',').Concat(text.Substring(colon + 1).Split(',')))
                        if (entry.Trim() != string.Empty && !union.Contains(entry)) union.Add(entry);
                    items[key] = key + ":" + string.Join(",", union) + ", ";
                    continue;
                }
                if (key == "FadePalette" && items.ContainsKey(key))
                {
                    if (text.Substring(colon + 1).Split(',').Length >= items[key].Substring(colon + 1).Split(',').Length)
                        items[key] = text;
                }
                else
                {
                    items[key] = text;
                }
            }
            return items.Values;
        }

        /// <summary>A string dictionary that keeps first-insertion order, like the game's Dictionary use.</summary>
        private sealed class OrderedLines
        {
            private readonly List<string> _keys = new List<string>();
            private readonly Dictionary<string, string> _values = new Dictionary<string, string>();

            public string this[string key]
            {
                get => _values[key];
                set
                {
                    if (!_values.ContainsKey(key)) _keys.Add(key);
                    _values[key] = value;
                }
            }

            public bool ContainsKey(string key) => _values.ContainsKey(key);
            public IEnumerable<string> Values => _keys.Select(k => _values[k]);
        }
    }
}
