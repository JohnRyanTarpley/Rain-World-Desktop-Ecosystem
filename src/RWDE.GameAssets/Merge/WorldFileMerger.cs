using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace RWDE.GameAssets.Merge
{
    /// <summary>
    /// Port of the game's structured <c>world_xx.txt</c> merge (<c>ModManager.ModMerger.MergeWorldFiles</c>
    /// and its <c>WorldFile</c> model). Rooms, spawns, lineages and conditional links are merged
    /// entry by entry and the file is written back in the game's section order.
    /// </summary>
    internal static class WorldFileMerger
    {
        public static string[] Merge(string[] baseLines, IReadOnlyList<string> mergeLines)
        {
            var world = new WorldFile(baseLines);
            var mod = new WorldFile(mergeLines);

            foreach (var exclusive in mod.Exclusives)
            {
                if (!world.Exclusives.Any(e => e.ToString() == exclusive.ToString())) world.Exclusives.Add(exclusive);
                world.Hidden.RemoveAll(h => h.Key() == exclusive.Key());
            }

            foreach (var hidden in mod.Hidden)
            {
                if (!world.Hidden.Any(h => h.ToString() == hidden.ToString())) world.Hidden.Add(hidden);
                world.Exclusives.RemoveAll(e => e.Key() == hidden.Key());
            }

            foreach (var substitute in mod.Substitutes)
            {
                var index = world.Substitutes.FindIndex(s => s.Key() == substitute.Key());
                if (index >= 0) world.Substitutes[index] = substitute;
                else world.Substitutes.Add(substitute);
            }

            foreach (var modRoom in mod.Rooms)
            {
                var room = world.Rooms.FirstOrDefault(r => r.RoomName == modRoom.RoomName);
                if (room == null)
                {
                    world.Rooms.Add(modRoom);
                    continue;
                }
                room.Connections = MergeConnections(room.Connections, modRoom.Connections);
                if (!string.IsNullOrEmpty(modRoom.SpecialType)) room.SpecialType = modRoom.SpecialType;
            }

            var excludes = mod.Creatures.Where(c => c.ExcludeMode).ToList();
            var includes = mod.Creatures.Where(c => !c.ExcludeMode).ToList();

            foreach (var spawn in excludes)
            {
                var replaced = false;
                var characters = spawn.Character.Split(',');
                for (var i = world.Creatures.Count - 1; i >= 0; i--)
                {
                    var existing = world.Creatures[i];
                    if (existing.RoomName != spawn.RoomName || existing.LineageDen != spawn.LineageDen) continue;
                    if (existing.ExcludeMode)
                    {
                        replaced = true;
                        world.Creatures[i] = spawn;
                    }
                    else if (existing.Character == "" || !characters.Contains(existing.Character))
                    {
                        world.Creatures.RemoveAt(i);
                    }
                }
                if (!replaced) world.Creatures.Add(spawn);
            }

            foreach (var spawn in includes)
            {
                if (spawn.LineageDen >= 0 && spawn.RoomName == "OFFSCREEN")
                {
                    world.Creatures.Add(spawn);
                    continue;
                }
                var replaced = false;
                for (var i = world.Creatures.Count - 1; i >= 0; i--)
                {
                    var existing = world.Creatures[i];
                    if (existing.RoomName != spawn.RoomName || existing.LineageDen != spawn.LineageDen) continue;
                    if (!existing.ExcludeMode && existing.Character == spawn.Character)
                    {
                        world.Creatures[i] = spawn;
                        replaced = true;
                    }
                    if (existing.ExcludeMode && spawn.Character != "" && !existing.Character.Split(',').Contains(spawn.Character))
                        existing.Character = existing.Character + "," + spawn.Character;
                }
                if (!replaced) world.Creatures.Add(spawn);
            }

            foreach (var blockage in mod.MigrationBlockages)
                if (!world.MigrationBlockages.Contains(blockage)) world.MigrationBlockages.Add(blockage);
            foreach (var line in mod.UnknownContextLines)
                if (!world.UnknownContextLines.Contains(line)) world.UnknownContextLines.Add(line);

            return world.ToFileLines();
        }

        // Port of the connection-list merge loop in MergeWorldFiles, including its in-place edits to
        // the base room's list.
        private static List<string> MergeConnections(List<string> baseConnections, List<string> modConnections)
        {
            const string Disconnected = "DISCONNECTED";
            var result = new List<string>();
            int m = 0, b = 0;
            while (m < modConnections.Count || b < baseConnections.Count)
            {
                if (m >= modConnections.Count)
                {
                    if (baseConnections[b] != Disconnected && !result.Contains(baseConnections[b])) result.Add(baseConnections[b]);
                    b++;
                    continue;
                }
                if (b >= baseConnections.Count)
                {
                    if (modConnections[m] != Disconnected && !result.Contains(modConnections[m])) result.Add(modConnections[m]);
                    m++;
                    continue;
                }
                if (baseConnections[b] == modConnections[m] && !result.Contains(baseConnections[b]))
                {
                    result.Add(baseConnections[b]);
                    b++;
                    m++;
                    continue;
                }
                if (baseConnections[b] == Disconnected && baseConnections.Contains(modConnections[m]))
                {
                    result.Add(baseConnections[b]);
                    b++;
                    continue;
                }
                if (modConnections[m] != Disconnected && baseConnections.Contains(modConnections[m]) && !result.Contains(modConnections[m]))
                {
                    result.Add(modConnections[m]);
                    m++;
                    continue;
                }
                if (modConnections[m] == Disconnected || !result.Contains(modConnections[m]))
                {
                    var placed = false;
                    for (var k = 0; k < result.Count; k++)
                    {
                        if (result[k] == Disconnected)
                        {
                            result[k] = modConnections[m];
                            placed = true;
                            break;
                        }
                    }
                    if (!placed)
                    {
                        for (var k = b; k < baseConnections.Count; k++)
                        {
                            if (baseConnections[k] == Disconnected)
                            {
                                baseConnections[k] = modConnections[m];
                                placed = true;
                                if (k == b) result.Add(modConnections[m]);
                                break;
                            }
                        }
                    }
                    if (!placed) result.Add(modConnections[m]);
                }
                m++;
                b++;
            }
            return result;
        }

        private sealed class WorldFile
        {
            public readonly List<RoomLink> Rooms = new List<RoomLink>();
            public readonly List<RoomSpawn> Creatures = new List<RoomSpawn>();
            public readonly List<RoomConditional> Exclusives = new List<RoomConditional>();
            public readonly List<RoomConditional> Hidden = new List<RoomConditional>();
            public readonly List<SubstituteLink> Substitutes = new List<SubstituteLink>();
            public readonly List<string> MigrationBlockages = new List<string>();
            public readonly List<string> UnknownContextLines = new List<string>();

            public WorldFile(IEnumerable<string> lines)
            {
                var section = "";
                foreach (var raw in lines)
                {
                    var line = raw.Trim();
                    if (line == "" || line.StartsWith("//")) continue;
                    if (line == "ROOMS" || line == "CREATURES" || line == "BAT MIGRATION BLOCKAGES" || line == "CONDITIONAL LINKS")
                    {
                        section = line;
                        continue;
                    }
                    if (line.StartsWith("END ") && section != "")
                    {
                        section = "";
                        continue;
                    }

                    switch (section)
                    {
                        case "":
                            UnknownContextLines.Add(line);
                            break;
                        case "ROOMS":
                            Rooms.Add(new RoomLink(line));
                            break;
                        case "CREATURES":
                            AddSpawns(line);
                            break;
                        case "BAT MIGRATION BLOCKAGES":
                            MigrationBlockages.Add(line);
                            break;
                        case "CONDITIONAL LINKS":
                            AddConditional(line);
                            break;
                    }
                }
            }

            private void AddSpawns(string line)
            {
                if (!(line.StartsWith("(") && line.Contains(")")))
                {
                    Creatures.Add(new RoomSpawn("", line, false));
                    return;
                }
                var filter = line.Substring(1, line.IndexOf(')') - 1);
                if (filter.StartsWith("X-"))
                {
                    Creatures.Add(new RoomSpawn(filter.Substring(2), line, true));
                    return;
                }
                foreach (var character in filter.Split(',')) Creatures.Add(new RoomSpawn(character, line, false));
            }

            private void AddConditional(string line)
            {
                if (!line.Contains(":")) return;
                var characters = line.Split(':')[0].Trim().Split(',');
                foreach (var character in characters)
                {
                    if (line.Contains("EXCLUSIVEROOM")) Exclusives.Add(new RoomConditional(character, line, "EXCLUSIVEROOM"));
                    else if (line.Contains("HIDEROOM")) Hidden.Add(new RoomConditional(character, line, "HIDEROOM"));
                    else Substitutes.Add(new SubstituteLink(character, line));
                }
            }

            public string[] ToFileLines()
            {
                var lines = new List<string> { "CONDITIONAL LINKS" };
                lines.AddRange(Exclusives.Select(e => e.ToString()));
                lines.AddRange(Hidden.Select(h => h.ToString()));
                lines.AddRange(Substitutes.Select(s => s.ToString()));
                lines.Add("END CONDITIONAL LINKS");
                lines.Add("");
                lines.Add("ROOMS");
                lines.AddRange(Rooms.Select(r => r.ToString()));
                lines.Add("END ROOMS");
                lines.Add("");
                lines.Add("CREATURES");
                lines.AddRange(Creatures.Select(c => c.ToString()));
                lines.Add("END CREATURES");
                lines.Add("");
                lines.Add("BAT MIGRATION BLOCKAGES");
                lines.AddRange(MigrationBlockages);
                lines.Add("END BAT MIGRATION BLOCKAGES");
                if (UnknownContextLines.Count > 0)
                {
                    lines.Add("");
                    lines.AddRange(UnknownContextLines);
                }
                return lines.ToArray();
            }
        }

        /// <summary>An EXCLUSIVEROOM or HIDEROOM conditional link.</summary>
        private sealed class RoomConditional
        {
            private readonly string _character;
            private readonly string _kind;
            private readonly string _roomName = "";

            public RoomConditional(string character, string line, string kind)
            {
                _character = character;
                _kind = kind;
                var parts = line.Split(':');
                if (parts.Length >= 3) _roomName = parts[2].Trim();
            }

            public override string ToString() => _character + " : " + _kind + " : " + _roomName;
            public string Key() => _character + "-" + _roomName;
        }

        private sealed class SubstituteLink
        {
            private readonly string _character;
            private readonly string _roomName = "";
            private readonly string _targetRoom = "";
            private readonly string _substituteRoom = "";

            public SubstituteLink(string character, string line)
            {
                _character = character;
                var parts = line.Split(':');
                if (parts.Length >= 4)
                {
                    _roomName = parts[1].Trim();
                    _targetRoom = parts[2].Trim();
                    _substituteRoom = parts[3].Trim();
                }
            }

            public override string ToString() => _character + " : " + _roomName + " : " + _targetRoom + " : " + _substituteRoom;
            public string Key() => _character + "-" + _roomName + "-" + _targetRoom;
        }

        private sealed class RoomLink
        {
            public readonly string RoomName;
            public string SpecialType;
            public List<string> Connections = new List<string>();

            public RoomLink(string line)
            {
                var parts = line.Split(':');
                RoomName = parts[0].Trim();
                SpecialType = parts.Length > 2 ? parts[2].Trim() : "";
                var connections = parts.Length > 1 ? parts[1] : "";
                foreach (var connection in connections.Trim().Split(',')) Connections.Add(connection.Trim());
            }

            public override string ToString()
            {
                var text = RoomName + " : " + string.Join(", ", Connections);
                return SpecialType != "" ? text + " : " + SpecialType : text;
            }
        }

        private sealed class RoomSpawn
        {
            public string Character;
            public readonly string RoomName;
            public readonly int LineageDen;
            public readonly bool ExcludeMode;
            private readonly List<Den> _dens;
            private readonly List<Lineage> _lineages;

            public RoomSpawn(string character, string line, bool excludeMode)
            {
                Character = character;
                ExcludeMode = excludeMode;
                if (line.StartsWith("(") && line.Contains(")")) line = line.Substring(line.IndexOf(')') + 1);
                var parts = Regex.Split(line, " : ");
                if (parts[0].Trim() == "LINEAGE")
                {
                    _lineages = new List<Lineage>();
                    RoomName = parts[1].Trim();
                    int.TryParse(parts[2].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out LineageDen);
                    foreach (var spawner in SplitSpawners(parts[3])) _lineages.Add(new Lineage(spawner.Trim()));
                }
                else
                {
                    LineageDen = -1;
                    _dens = new List<Den>();
                    RoomName = parts[0].Trim();
                    foreach (var spawner in SplitSpawners(parts[1])) _dens.Add(new Den(spawner.Trim()));
                }
            }

            // Splits on commas, keeping "{...}" attribute groups (which may contain commas) whole.
            private static IEnumerable<string> SplitSpawners(string spawnString)
            {
                var pieces = spawnString.Split(',');
                if (!spawnString.Contains("{")) return pieces;
                var result = new List<string>();
                string group = null;
                foreach (var piece in pieces)
                {
                    if (group == null)
                    {
                        if (!piece.Contains("{") || piece.Contains("}")) result.Add(piece);
                        else group = piece;
                    }
                    else
                    {
                        group += "," + piece;
                        if (piece.Contains("}"))
                        {
                            result.Add(group);
                            group = null;
                        }
                    }
                }
                return result;
            }

            public override string ToString()
            {
                var text = "";
                if (Character != "") text += "(" + (ExcludeMode ? "X-" : "") + Character + ")";
                if (_lineages != null) text += "LINEAGE : ";
                text += RoomName + " : ";
                if (LineageDen >= 0) text += LineageDen + " : ";
                if (_lineages != null) text += string.Join(", ", _lineages.Select(l => l.ToString()));
                else if (_dens != null) text += string.Join(", ", _dens.Select(d => d.ToString()));
                return text;
            }
        }

        private sealed class Den
        {
            private readonly int _denNumber;
            private readonly int _spawnQuantity;
            private readonly string _creature;
            private readonly string _attributes = "";

            public Den(string den)
            {
                var parts = den.Split('-');
                int.TryParse(parts[0], NumberStyles.Any, CultureInfo.InvariantCulture, out _denNumber);
                _creature = parts.Length > 1 ? parts[1] : "";
                var inAttributes = false;
                for (var i = 2; i < parts.Length; i++)
                {
                    if (parts[i].StartsWith("{"))
                    {
                        inAttributes = true;
                        _attributes = parts[i];
                    }
                    else if (inAttributes) _attributes += "-" + parts[i];
                    else int.TryParse(parts[i], NumberStyles.Any, CultureInfo.InvariantCulture, out _spawnQuantity);
                    if (parts[i].Contains("}")) inAttributes = false;
                }
            }

            public override string ToString()
            {
                var text = _denNumber.ToString(CultureInfo.InvariantCulture) + "-" + _creature;
                if (_attributes != "") text += "-" + _attributes;
                if (_spawnQuantity > 1) text += "-" + _spawnQuantity.ToString(CultureInfo.InvariantCulture);
                return text;
            }
        }

        private sealed class Lineage
        {
            private readonly string _creature;
            private readonly string _attributes = "";
            private readonly float _chance;

            public Lineage(string lineage)
            {
                var parts = lineage.Split('-');
                _creature = parts[0];
                var inAttributes = false;
                for (var i = 1; i < parts.Length; i++)
                {
                    if (parts[i].StartsWith("{"))
                    {
                        inAttributes = true;
                        _attributes = parts[i];
                    }
                    else if (inAttributes) _attributes += "-" + parts[i];
                    else _chance = float.Parse(parts[i], NumberStyles.Any, CultureInfo.InvariantCulture);
                    if (parts[i].Contains("}")) inAttributes = false;
                }
            }

            public override string ToString()
            {
                var text = _creature;
                if (_attributes != "") text += "-" + _attributes;
                return text + "-" + _chance.ToString(CultureInfo.InvariantCulture);
            }
        }
    }
}
