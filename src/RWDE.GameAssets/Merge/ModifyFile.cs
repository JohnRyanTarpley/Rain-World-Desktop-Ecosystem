using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace RWDE.GameAssets.Merge
{
    /// <summary>
    /// One mod's <c>modify/</c> file, parsed the way the game's <c>ModManager.ModMerger.PendingApply</c>
    /// parses it: a <c>[MERGE]</c> block plus line edits (<c>[ADD]</c>, <c>[FIND]</c>/<c>[REPLACE]</c> and friends).
    /// <c>[FILEDESTINATION]</c> sections, which write to other files, are kept but not applied.
    /// </summary>
    public sealed class ModifyFile
    {
        private enum ReplaceType
        {
            Substitute,
            Before,
            After,
        }

        private sealed class ReplaceOperation
        {
            public string FindMatch;
            public string FindMatchRegex;
            public string FindMatchStart;
            public string FindMatchEnd;
            public int FindLineNumber = -1;
            public bool FindLine;
            public string ReplaceWith = "";
            public ReplaceType Type;
            public int Occurrence;
            public int OccurrencesSeen;
        }

        private readonly List<string> _addAtEnd = new List<string>();
        private readonly Dictionary<int, List<string>> _addAtLine = new Dictionary<int, List<string>>();
        private readonly List<ReplaceOperation> _replacements = new List<ReplaceOperation>();

        public ModifyFile(string modId, string[] lines)
        {
            ModId = modId;
            Parse(lines);
        }

        public string ModId { get; }

        /// <summary>Lines of the <c>[MERGE]</c> block, or null if the file has none.</summary>
        public List<string> MergeLines { get; private set; }

        /// <summary>True if the file has line edits (the game treats every modify file this way).</summary>
        public bool HasModifications => _addAtEnd.Count > 0 || _addAtLine.Count > 0 || _replacements.Count > 0;

        /// <summary>Number of <c>[FILEDESTINATION]</c> sections, which this port does not apply.</summary>
        public int SkippedFileDestinations { get; private set; }

        /// <summary>Applies the line edits to <paramref name="source"/> (port of <c>ApplyModifications</c>).</summary>
        public string[] ApplyModifications(string[] source)
        {
            foreach (var op in _replacements) op.OccurrencesSeen = 0;
            var result = new List<string>();
            for (var i = 0; i < source.Length; i++)
            {
                if (_addAtLine.TryGetValue(i, out var inserted)) result.AddRange(inserted);
                var text = source[i];
                foreach (var op in _replacements)
                {
                    if (op.Occurrence == 0)
                        text = ApplyEveryOccurrence(op, text, i);
                    else if (op.OccurrencesSeen >= 0)
                        text = ApplyNthOccurrence(op, text, source[i]);
                }
                if (text.Trim() != "") result.Add(text);
            }
            result.AddRange(_addAtEnd);
            return TextLines.Normalise(result);
        }

        private static string ApplyEveryOccurrence(ReplaceOperation op, string text, int lineIndex)
        {
            if (op.FindMatch != null)
            {
                if (!op.FindLine) return ApplySubstring(text, op.FindMatch, op.ReplaceWith, op.Type);
                return text.Contains(op.FindMatch) ? ApplyLine(text, op.ReplaceWith, op.Type) : text;
            }
            if (op.FindMatchRegex != null)
            {
                if (!op.FindLine) return ApplySubstringRegex(text, op.FindMatchRegex, op.ReplaceWith, op.Type);
                return Regex.Matches(text, op.FindMatchRegex).Count > 0 ? ApplyLine(text, op.ReplaceWith, op.Type) : text;
            }
            if (op.FindMatchStart != null)
                return text.StartsWith(op.FindMatchStart, StringComparison.Ordinal) ? ApplyLine(text, op.ReplaceWith, op.Type) : text;
            if (op.FindMatchEnd != null)
                return text.EndsWith(op.FindMatchEnd, StringComparison.Ordinal) ? ApplyLine(text, op.ReplaceWith, op.Type) : text;
            return op.FindLineNumber == lineIndex + 1 ? ApplyLine(text, op.ReplaceWith, op.Type) : text;
        }

        private static string ApplyNthOccurrence(ReplaceOperation op, string text, string originalLine)
        {
            if (!op.FindLine)
            {
                MatchCollection seen;
                if (op.FindMatch != null) seen = Regex.Matches(originalLine, "\\b(" + op.FindMatch + ")\\b");
                else if (op.FindMatchRegex != null) seen = Regex.Matches(originalLine, op.FindMatchRegex);
                else return text;

                if (op.OccurrencesSeen + seen.Count < op.Occurrence)
                {
                    op.OccurrencesSeen += seen.Count;
                    return text;
                }

                var nth = op.Occurrence - op.OccurrencesSeen;
                var current = op.FindMatch != null
                    ? Regex.Matches(text, "\\b(" + op.FindMatch + ")\\b")
                    : Regex.Matches(text, op.FindMatchRegex);
                if (nth > current.Count) return text;
                var match = current[nth - 1];

                if (op.FindMatch != null)
                {
                    var before = text.Substring(0, match.Index);
                    var after = text.Substring(match.Index + op.FindMatch.Length);
                    switch (op.Type)
                    {
                        case ReplaceType.Substitute: text = before + op.ReplaceWith + after; break;
                        case ReplaceType.Before: text = before + op.ReplaceWith + op.FindMatch + after; break;
                        case ReplaceType.After: text = before + op.FindMatch + op.ReplaceWith + after; break;
                    }
                }
                else
                {
                    var regex = new Regex(op.FindMatchRegex);
                    var before = text.Substring(0, match.Index);
                    var rest = text.Substring(match.Index);
                    switch (op.Type)
                    {
                        case ReplaceType.Substitute: text = before + regex.Replace(rest, op.ReplaceWith, 1); break;
                        case ReplaceType.Before: text = before + regex.Replace(rest, op.ReplaceWith + match.Value, 1); break;
                        case ReplaceType.After: text = before + regex.Replace(rest, match.Value + op.ReplaceWith, 1); break;
                    }
                }
                op.OccurrencesSeen = -1;
                return text;
            }

            bool matches;
            if (op.FindMatch != null) matches = text.Contains(op.FindMatch);
            else if (op.FindMatchRegex != null) matches = Regex.Matches(text, op.FindMatchRegex).Count > 0;
            else if (op.FindMatchStart != null) matches = text.StartsWith(op.FindMatchStart, StringComparison.Ordinal);
            else if (op.FindMatchEnd != null) matches = text.EndsWith(op.FindMatchEnd, StringComparison.Ordinal);
            else return text;

            if (!matches) return text;
            op.OccurrencesSeen++;
            if (op.OccurrencesSeen < op.Occurrence) return text;
            op.OccurrencesSeen = -1;
            return ApplyLine(text, op.ReplaceWith, op.Type);
        }

        private static string ApplySubstring(string line, string find, string replaceWith, ReplaceType type)
        {
            switch (type)
            {
                case ReplaceType.Substitute: return line.Replace(find, replaceWith);
                case ReplaceType.Before: return line.Replace(find, replaceWith + find);
                default: return line.Replace(find, find + replaceWith);
            }
        }

        private static string ApplySubstringRegex(string line, string find, string replaceWith, ReplaceType type)
        {
            switch (type)
            {
                case ReplaceType.Substitute: return Regex.Replace(line, find, replaceWith);
                case ReplaceType.Before: return Regex.Replace(line, find, replaceWith + "$+");
                default: return Regex.Replace(line, find, "$+" + replaceWith);
            }
        }

        private static string ApplyLine(string line, string replaceWith, ReplaceType type)
        {
            switch (type)
            {
                case ReplaceType.Substitute: return replaceWith;
                case ReplaceType.Before: return replaceWith + line;
                default: return line + replaceWith;
            }
        }

        private static bool MatchAndRemoveStart(string line, string match, out string result)
        {
            if (line.StartsWith(match, StringComparison.Ordinal))
            {
                result = line.Substring(match.Length).Replace("\\n", "\n");
                return true;
            }
            result = null;
            return false;
        }

        private void Parse(string[] lines)
        {
            string find = null, findStart = null, findEnd = null, findRegex = null;
            var lineNumber = -1;
            var findLine = false;
            var inMerge = false;
            var inDestination = false;

            foreach (var line in lines)
            {
                if (inDestination)
                {
                    if (line.Contains("[ENDFILEDESTINATION]")) inDestination = false;
                    continue;
                }
                if (line.StartsWith("[FILEDESTINATION]", StringComparison.Ordinal))
                {
                    inDestination = true;
                    SkippedFileDestinations++;
                    continue;
                }
                if (inMerge)
                {
                    if (line.Contains("[ENDMERGE]"))
                    {
                        MergeLines.Add(line.Replace("[ENDMERGE]", ""));
                        inMerge = false;
                    }
                    else
                    {
                        MergeLines.Add(line);
                    }
                    continue;
                }

                string rest;
                if (MatchAndRemoveStart(line, "[MERGE]", out rest))
                {
                    inMerge = true;
                    if (MergeLines == null) MergeLines = new List<string>();
                    rest = rest.Trim();
                    if (rest.Length > 0) MergeLines.Add(rest);
                }
                else if (MatchAndRemoveStart(line, "[ADD]", out rest))
                {
                    _addAtEnd.Add(rest);
                }
                else if (line.StartsWith("[ADD_", StringComparison.Ordinal) && line.Contains("]"))
                {
                    if (TryParseIndex(line, "[ADD_", out var index))
                    {
                        if (!_addAtLine.TryGetValue(index, out var list)) _addAtLine[index] = list = new List<string>();
                        list.Add(line.Substring(line.IndexOf(']') + 1).Replace("\\n", "\n"));
                    }
                }
                else if (MatchAndRemoveStart(line, "[FIND]", out rest))
                {
                    SetFind(rest, null, null, null, false);
                }
                else if (MatchAndRemoveStart(line, "[FINDLINE]", out rest))
                {
                    SetFind(rest, null, null, null, true);
                }
                else if (MatchAndRemoveStart(line, "[FINDREGEX]", out rest))
                {
                    SetFind(null, rest, null, null, false);
                }
                else if (MatchAndRemoveStart(line, "[FINDLINEREGEX]", out rest))
                {
                    SetFind(null, rest, null, null, true);
                }
                else if (MatchAndRemoveStart(line, "[FINDLINESTART]", out rest))
                {
                    SetFind(null, null, rest, null, true);
                }
                else if (MatchAndRemoveStart(line, "[FINDLINEEND]", out rest))
                {
                    SetFind(null, null, null, rest, true);
                }
                else if (line.StartsWith("[TARGETLINE]", StringComparison.Ordinal))
                {
                    if (int.TryParse(line.Substring("[TARGETLINE]".Length).Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out lineNumber) && lineNumber > 0)
                    {
                        find = findRegex = findStart = findEnd = null;
                        findLine = true;
                    }
                    else
                    {
                        lineNumber = -1;
                    }
                }
                else if (HasTarget() && TryReplaceDirective(line, out var type, out var occurrence, out var replaceWith))
                {
                    _replacements.Add(new ReplaceOperation
                    {
                        FindMatch = find,
                        FindMatchRegex = findRegex,
                        FindMatchStart = findStart,
                        FindMatchEnd = findEnd,
                        FindLineNumber = lineNumber,
                        FindLine = findLine,
                        ReplaceWith = replaceWith,
                        Type = type,
                        Occurrence = occurrence,
                    });
                }
            }

            void SetFind(string f, string regex, string start, string end, bool wholeLine)
            {
                find = f;
                findRegex = regex;
                findStart = start;
                findEnd = end;
                lineNumber = -1;
                findLine = wholeLine;
            }

            bool HasTarget() => find != null || findRegex != null || findStart != null || findEnd != null || lineNumber > 0;
        }

        private static bool TryReplaceDirective(string line, out ReplaceType type, out int occurrence, out string replaceWith)
        {
            occurrence = 0;
            replaceWith = null;
            type = ReplaceType.Substitute;
            if (MatchAndRemoveStart(line, "[REPLACE]", out replaceWith)) return true;
            if (MatchAndRemoveStart(line, "[ADDBEFORE]", out replaceWith))
            {
                type = ReplaceType.Before;
                return true;
            }
            if (MatchAndRemoveStart(line, "[ADDAFTER]", out replaceWith))
            {
                type = ReplaceType.After;
                return true;
            }
            if (!line.Contains("]")) return false;

            string prefix;
            if (line.StartsWith("[REPLACE_", StringComparison.Ordinal)) prefix = "[REPLACE_";
            else if (line.StartsWith("[ADDBEFORE_", StringComparison.Ordinal))
            {
                prefix = "[ADDBEFORE_";
                type = ReplaceType.Before;
            }
            else if (line.StartsWith("[ADDAFTER_", StringComparison.Ordinal))
            {
                prefix = "[ADDAFTER_";
                type = ReplaceType.After;
            }
            else return false;

            if (!TryParseIndex(line, prefix, out occurrence)) return false;
            replaceWith = line.Substring(line.IndexOf(']') + 1).Replace("\\n", "\n");
            return true;
        }

        private static bool TryParseIndex(string line, string prefix, out int value)
        {
            var close = line.IndexOf(']');
            value = 0;
            return close > prefix.Length - 1 &&
                   int.TryParse(line.Substring(prefix.Length, close - prefix.Length), NumberStyles.Any, CultureInfo.InvariantCulture, out value);
        }
    }

    internal static class TextLines
    {
        /// <summary>Splits text into lines the way <c>File.ReadAllLines</c> does.</summary>
        public static string[] Split(string text)
        {
            if (string.IsNullOrEmpty(text)) return new string[0];
            var lines = new List<string>(text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'));
            if (lines.Count > 0 && lines[lines.Count - 1] == "") lines.RemoveAt(lines.Count - 1);
            return lines.ToArray();
        }

        /// <summary>
        /// The game writes each step to disk and reads it back, so a line holding a newline becomes
        /// two lines. This does the same round trip in memory.
        /// </summary>
        public static string[] Normalise(IEnumerable<string> lines) => Split(string.Join("\n", lines) + "\n");
    }
}
