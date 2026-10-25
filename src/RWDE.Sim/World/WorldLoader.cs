using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Type = RWDE.Sim.CreatureTemplate.Type;

namespace RWDE.Sim
{
    /// <summary>
    /// The parts of the game's <c>WorldLoader</c> that read a region's world file: line preprocessing
    /// with campaign filters, the <c>CREATURES</c> block (room spawns, lineages, offscreen spawns) and
    /// the creature names used in it. The sim never reads files; it is handed the merged lines
    /// (base file plus the DLC <c>modify</c> files) that the game asset cache produces.
    /// </summary>
    public static class WorldLoader
    {
        /// <summary>The spawn line room name for the region's offscreen den.</summary>
        public const string OffscreenRoom = "OFFSCREEN";

        /// <summary>Port of <c>WorldLoader.Preprocessing</c>.</summary>
        public static class Preprocessing
        {
            /// <summary>
            /// The line as the loader sees it for <paramref name="timeline"/>, or null when it is a comment,
            /// too short, or filtered out (<c>PreprocessLine</c>). A leading <c>(A,B)</c> keeps the line for
            /// those campaigns only; <c>(X-A,B)</c> for every campaign except those. A leading <c>{...}</c>
            /// tests custom conditions.
            /// </summary>
            public static string PreprocessLine(string line, string timeline)
            {
                if (line == null || line.Length < 2 || line.Substring(0, 2) == "//") return null;
                if (line[0] == '(' && line.Contains(')'))
                {
                    if (!TimelineMatch(line.Substring(1, line.IndexOf(')') - 1), timeline)) return null;
                    line = line.Substring(line.IndexOf(')') + 1);
                }
                if (line.Length > 0 && line[0] == '{' && line.Contains('}'))
                {
                    if (!PreprocessCustomConditions(line.Substring(1, line.IndexOf('}') - 1))) return null;
                    line = line.Substring(line.IndexOf('}') + 1);
                }
                return line;
            }

            /// <summary>Port of <c>TimelineMatch</c>: whether a filter's campaign list includes <paramref name="timeline"/>.</summary>
            public static bool TimelineMatch(string text, string timeline)
            {
                bool listed = false;
                bool inverted = false;
                if (text.StartsWith("X-", StringComparison.Ordinal))
                {
                    text = text.Substring(2);
                    inverted = true;
                }
                if (timeline == null) return inverted;
                foreach (var entry in text.Split(','))
                {
                    if (int.TryParse(entry.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out int number))
                    {
                        if (Campaign.FromPlayerNumber(number)?.Timeline == timeline)
                        {
                            listed = true;
                            break;
                        }
                    }
                    else if (entry.Trim() == timeline)
                    {
                        listed = true;
                        break;
                    }
                }
                return listed != inverted;
            }

            /// <summary>
            /// Port of <c>PreprocessCustomConditions</c> for a desktop with no save file: the game's
            /// ending conditions (<c>PostBathScene</c>, <c>ToysEnding</c>, <c>WeaverEnding</c>,
            /// <c>TreeSequence</c>) are all false, and <c>PrinceEncounters</c> and unknown conditions
            /// have no opinion. A line fails when a condition's value equals its <c>!</c> negation flag.
            /// </summary>
            private static bool PreprocessCustomConditions(string text)
            {
                foreach (var raw in text.Split(','))
                {
                    bool negated = raw.Contains('!');
                    var condition = raw.Replace("!", "");
                    bool? value = condition == "PostBathScene" || condition == "ToysEnding"
                                  || condition == "WeaverEnding" || condition == "TreeSequence"
                        ? false
                        : (bool?)null;
                    if (value.HasValue && value.Value == negated) return false;
                }
                return true;
            }
        }

        /// <summary>
        /// Port of <c>WorldLoader.CreatureTypeFromString</c> (with the Downpour and Watcher tables it
        /// checks first), limited to the desktop roster. Names of game creatures outside the roster
        /// return null, as unknown names do in the game. The game resolves "cicada" to A or B at
        /// random; <paramref name="random"/> does that here, and without one "cicada" returns null.
        /// </summary>
        public static Type? CreatureTypeFromString(string s, Random random = null)
        {
            switch (s.ToLowerInvariant())
            {
                // MoreSlugcatsCreatures.CreatureTypeFromString (DLC-shared names)
                case "caramel": return Type.SpitLizard;
                case "strawberry": return Type.ZoopLizard;
                case "eel": return Type.EelLizard;
                case "mirosvulture":
                case "miros vulture": return Type.MirosVulture;
                // WorldLoader.CreatureTypeFromString
                case "pink": return Type.PinkLizard;
                case "green": return Type.GreenLizard;
                case "blue": return Type.BlueLizard;
                case "yellow": return Type.YellowLizard;
                case "white": return Type.WhiteLizard;
                case "red": return Type.RedLizard;
                case "cyan": return Type.CyanLizard;
                case "vulture": return Type.Vulture;
                case "cicada a":
                case "cicadaa": return Type.CicadaA;
                case "cicada b":
                case "cicadab": return Type.CicadaB;
                case "cicada":
                    if (random == null) return null;
                    return random.NextDouble() < 0.5 ? Type.CicadaA : Type.CicadaB;
                case "tentacleplant":
                case "tentacle plant":
                case "tentacle": return Type.TentaclePlant;
                case "polemimic":
                case "pole mimic":
                case "mimic": return Type.PoleMimic;
                case "centipede":
                case "cent": return Type.Centipede;
                case "eggbug":
                case "egg bug": return Type.EggBug;
                case "bigspider":
                case "big spider": return Type.BigSpider;
                case "spitterspider":
                case "spitter spider": return Type.SpitterSpider;
                case "bigneedle":
                case "big needle":
                case "needle":
                case "needle worm": return Type.BigNeedleWorm;
                case "smallneedle":
                case "small needle": return Type.SmallNeedleWorm;
                case "dropbug":
                case "drop bug":
                case "dropwig":
                case "drop wig": return Type.DropBug;
                case "kingvulture":
                case "king vulture": return Type.KingVulture;
                case "red centipede":
                case "redcentipede":
                case "red centi":
                case "redcenti": return Type.RedCentipede;
            }
            // The default branch: a template's name or type name, compared case-sensitively.
            foreach (Type t in Enum.GetValues(typeof(Type)))
                if (s == CreatureTemplate.NameOf(t) || s == t.ToString())
                    return t;
            return null;
        }

        /// <summary>
        /// Parses one creature line that has passed <see cref="Preprocessing.PreprocessLine"/>
        /// (<c>FindingCreatures</c>). Spawners are appended to <paramref name="spawners"/>; their index
        /// in it becomes the spawner index, as in the game. Returns false for a line it can't read.
        /// </summary>
        /// <param name="roomExists">Whether a room name is in the region's <c>ROOMS</c> block.</param>
        /// <param name="unknownCreatures">Collects creature names that don't resolve.</param>
        public static bool AddSpawnersFromLine(string line, Func<string, bool> roomExists, List<World.CreatureSpawner> spawners,
            ICollection<string> unknownCreatures, Random random = null)
        {
            var parts = Regex.Split(line, " : ");
            if (parts[0] == "LINEAGE")
                return parts.Length >= 4 && AddLineageFromString(parts, roomExists, spawners, unknownCreatures, random);
            return parts.Length >= 2 && AddSpawnersFromString(parts, roomExists, spawners, unknownCreatures, random);
        }

        // Port of WorldLoader.AddSpawnersFromString: "ROOM : den-type-count{data}, ...".
        private static bool AddSpawnersFromString(string[] line, Func<string, bool> roomExists, List<World.CreatureSpawner> spawners,
            ICollection<string> unknownCreatures, Random random)
        {
            // The game keeps a spawner for an unknown room (room index -1), and GeneratePopulation then
            // finds no room for it and skips it. Dropping it here has the same result.
            var room = line[0];
            bool known = room == OffscreenRoom || roomExists(room);
            foreach (var entry in Regex.Split(ValidateSpacedDelimiter(line[1], ","), ", "))
            {
                var bits = entry.Split('-');
                if (bits.Length < 2 || !int.TryParse(bits[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int den))
                    return false;
                var type = CreatureTypeFromString(bits[1], random);
                int amount = 1;
                string data = "";
                bool inData = false;
                for (int k = 2; k < bits.Length; k++)
                {
                    if (bits[k].Length > 0 && bits[k][0] == '{')
                    {
                        data = bits[k];
                        inData = true;
                    }
                    else if (inData)
                    {
                        data = data + "-" + bits[k];
                    }
                    else if (!int.TryParse(bits[k], NumberStyles.Integer, CultureInfo.InvariantCulture, out amount))
                    {
                        amount = 1;
                    }
                    if (bits[k].Contains('}')) inData = false;
                }
                if (type == null)
                {
                    unknownCreatures?.Add(bits[1]);
                    continue;
                }
                if (!known) continue;
                spawners.Add(new World.SimpleSpawner(spawners.Count, new World.Den(room, den), type.Value, data == "" ? null : data, amount));
            }
            return true;
        }

        // Port of WorldLoader.AddLineageFromString: "LINEAGE : ROOM : den : type-chance, ...".
        private static bool AddLineageFromString(string[] s, Func<string, bool> roomExists, List<World.CreatureSpawner> spawners,
            ICollection<string> unknownCreatures, Random random)
        {
            if (s[1] != OffscreenRoom && !roomExists(s[1])) return true;
            if (!int.TryParse(s[2], NumberStyles.Any, CultureInfo.InvariantCulture, out int den)) return false;

            var entries = Regex.Split(ValidateSpacedDelimiter(s[3], ","), ", ");
            var types = new Type?[entries.Length];
            var chances = new float[entries.Length];
            var data = new string[entries.Length];
            for (int j = 0; j < entries.Length; j++)
            {
                var bits = Regex.Split(entries[j], "-");
                if (bits[0].ToLowerInvariant() != "none")
                {
                    types[j] = CreatureTypeFromString(bits[0], random);
                    if (types[j] == null) unknownCreatures?.Add(bits[0]);
                }
                data[j] = "";
                bool inData = false;
                for (int k = 1; k < bits.Length; k++)
                {
                    // The game indexes bits[k][0] inside a try block; an empty piece fails the whole lineage.
                    if (bits[k].Length == 0) return false;
                    if (bits[k][0] == '{')
                    {
                        data[j] = bits[k];
                        inData = true;
                    }
                    else if (inData)
                    {
                        data[j] = data[j] + "-" + bits[k];
                    }
                    else if (!float.TryParse(bits[k], NumberStyles.Any, CultureInfo.InvariantCulture, out chances[j]))
                    {
                        chances[j] = 0f;
                    }
                    if (bits[k].Contains('}')) inData = false;
                }
                if (data[j] == "") data[j] = null;
            }

            // Lineages sharing a den get a conflict number in their save key.
            bool conflict = false;
            int conflictNumber = 1;
            var denCoord = new World.Den(s[1], den);
            foreach (var spawner in spawners)
            {
                if (spawner is World.Lineage other && other.den == denCoord)
                {
                    conflictNumber++;
                    conflict = true;
                }
            }
            var lineage = new World.Lineage(spawners.Count, denCoord, types, chances, data, conflict ? conflictNumber : 0);
            if (s.Length > 4) lineage.nightCreature = true;
            spawners.Add(lineage);
            return true;
        }

        // Custom.ValidateSpacedDelimiter
        private static string ValidateSpacedDelimiter(string s, string delimiter) => s.EndsWith(delimiter, StringComparison.Ordinal) ? s + " " : s;
    }
}
