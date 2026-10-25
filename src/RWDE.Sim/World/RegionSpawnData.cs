using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace RWDE.Sim
{
    /// <summary>
    /// One region's world file and properties, as merged by the game asset cache (base file plus the
    /// <c>moreslugcats</c>/<c>watcher</c> <c>modify</c> files, campaign filters left in). Builds the
    /// <see cref="SpawnTable"/> for a campaign setting the way <c>WorldLoader</c> reads the file for one
    /// timeline. Nothing here touches the disk: the app hands in the lines.
    /// </summary>
    public sealed class RegionSpawnData
    {
        // Region.RegionParams defaults.
        private const int DefaultBatsPerActiveSwarmRoom = 10;

        private readonly string[] worldLines;
        private readonly string[] propertiesLines;

        /// <param name="regionName">Region acronym, e.g. "su". Selects the game's per-region special cases.</param>
        /// <param name="worldLines">Merged <c>world_xx.txt</c> lines.</param>
        /// <param name="propertiesLines">Merged <c>properties.txt</c> lines, or null for the game's defaults.</param>
        public RegionSpawnData(string regionName, IEnumerable<string> worldLines, IEnumerable<string> propertiesLines = null)
        {
            RegionName = (regionName ?? "").ToUpperInvariant();
            this.worldLines = (worldLines ?? throw new ArgumentNullException(nameof(worldLines))).ToArray();
            this.propertiesLines = propertiesLines?.ToArray() ?? new string[0];
        }

        /// <summary>Upper-case region acronym, e.g. "SU".</summary>
        public string RegionName { get; }

        /// <summary>
        /// The spawn table for a campaign setting: one campaign's timeline value (e.g. "White"), or
        /// <see cref="Campaign.AllOutskirtsCreatures"/> (also used for null or blank), which merges
        /// every campaign in <paramref name="campaigns"/> (default <see cref="Campaign.All"/>).
        /// An unknown campaign name is read like a timeline the file never names.
        /// </summary>
        /// <param name="random">Resolves the world file's "cicada" (A or B at random), as the game does at load.</param>
        public SpawnTable BuildTable(string campaignSetting, IEnumerable<Campaign> campaigns = null, Random random = null)
        {
            if (!Campaign.IsAll(campaignSetting))
            {
                var campaign = Campaign.Find(campaignSetting);
                var table = Build(campaignSetting, random);
                // FliesWorldAI: in the Outskirts, a story session's third swarm room starts with one
                // batfly more than the slugcat needs to hibernate.
                if (RegionName == "SU" && campaign != null && table.fliesPerSwarmRoom.Length > 2)
                    table.fliesPerSwarmRoom[2] = campaign.FoodToHibernate + 1;
                return table;
            }

            var list = (campaigns ?? Campaign.All).ToList();
            var merged = new SpawnTable(RegionName, list);
            var seen = new HashSet<string>();
            foreach (var campaign in list)
            {
                var table = Build(campaign.Timeline, random);
                foreach (var spawner in table.spawners)
                {
                    if (!seen.Add(spawner.Signature)) continue;
                    int sameDen = merged.spawners.Count(s => s is World.Lineage && s.den == spawner.den);
                    merged.spawners.Add(spawner.Renumbered(merged.spawners.Count, sameDen > 0 ? sameDen + 1 : 0));
                }
                foreach (var room in table.swarmRooms)
                    if (!merged.swarmRooms.Contains(room)) merged.swarmRooms.Add(room);
                foreach (var name in table.unknownCreatures) merged.unknownCreatures.Add(name);
                foreach (var line in table.unreadableLines)
                    if (!merged.unreadableLines.Contains(line)) merged.unreadableLines.Add(line);
            }
            merged.fliesPerSwarmRoom = Enumerable.Repeat(BatsPerActiveSwarmRoom(null), merged.swarmRooms.Count).ToArray();
            return merged;
        }

        /// <summary><c>batsPerActiveSwarmRoom</c> from the properties file, or the game's default of 10.</summary>
        public int BatsPerActiveSwarmRoom(string timeline)
        {
            int bats = DefaultBatsPerActiveSwarmRoom;
            foreach (var raw in propertiesLines)
            {
                var line = WorldLoader.Preprocessing.PreprocessLine(raw, timeline);
                if (line == null) continue;
                var parts = Regex.Split(line, ": ");
                if (parts.Length >= 2 && parts[0] == "batsPerActiveSwarmRoom"
                    && int.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out int value))
                    bats = value;
            }
            return bats;
        }

        // The world file as WorldLoader reads it for one timeline: preprocess each line, then read the
        // CONDITIONAL LINKS, ROOMS and CREATURES blocks.
        private SpawnTable Build(string timeline, Random random)
        {
            var lines = new List<string>();
            foreach (var raw in worldLines)
            {
                var line = WorldLoader.Preprocessing.PreprocessLine(raw, timeline);
                if (line != null) lines.Add(line);
            }

            var rooms = new List<string>();
            var roomTags = new List<string[]>();
            var hidden = new HashSet<string>();
            var exclusive = new Dictionary<string, HashSet<string>>();
            var creatureLines = new List<string>();
            var section = "";
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed == "CONDITIONAL LINKS" || trimmed == "ROOMS" || trimmed == "CREATURES" || trimmed == "BAT MIGRATION BLOCKAGES")
                {
                    section = trimmed;
                    continue;
                }
                if (trimmed.StartsWith("END ", StringComparison.Ordinal))
                {
                    section = "";
                    continue;
                }
                var parts = Regex.Split(line, " : ");
                switch (section)
                {
                    case "CONDITIONAL LINKS":
                        // WorldLoader.LoadConditionalLinks: hidden and exclusive rooms leave the map.
                        if (parts.Length < 3) break;
                        var timelines = Regex.Split(parts[0], ",");
                        if (parts[1] == "HIDEROOM" && timelines.Contains(timeline)) hidden.Add(parts[2]);
                        else if (parts[1] == "EXCLUSIVEROOM")
                        {
                            if (!exclusive.TryGetValue(parts[2], out var set)) exclusive[parts[2]] = set = new HashSet<string>();
                            set.UnionWith(timelines);
                        }
                        break;
                    case "ROOMS":
                        // WorldLoader.MappingRooms
                        if (!line.Contains(" : ") || parts.Length < 2) break;
                        rooms.Add(parts[0]);
                        roomTags.Add(parts.Skip(2).ToArray());
                        break;
                    case "CREATURES":
                        creatureLines.Add(line);
                        break;
                }
            }
            foreach (var entry in exclusive)
                if (!entry.Value.Contains(timeline)) hidden.Add(entry.Key);

            var campaign = Campaign.Find(timeline);
            var table = new SpawnTable(RegionName, campaign != null ? new[] { campaign } : new Campaign[0]);
            for (int i = 0; i < rooms.Count; i++)
                if (roomTags[i].Contains("SWARMROOM") && !hidden.Contains(rooms[i]))
                    table.swarmRooms.Add(rooms[i]);

            var roomSet = new HashSet<string>(rooms);
            foreach (var line in creatureLines)
                if (!WorldLoader.AddSpawnersFromLine(line, roomSet.Contains, table.spawners, table.unknownCreatures, random))
                    table.unreadableLines.Add(line);

            table.fliesPerSwarmRoom = Enumerable.Repeat(BatsPerActiveSwarmRoom(timeline), table.swarmRooms.Count).ToArray();
            return table;
        }
    }

    /// <summary>
    /// Everything that spawns in a region for one campaign setting: the <c>CREATURES</c> block's
    /// spawners (room spawns, lineages and <c>OFFSCREEN</c> spawns) and the swarm rooms' batflies.
    /// </summary>
    public sealed class SpawnTable
    {
        internal readonly List<World.CreatureSpawner> spawners = new List<World.CreatureSpawner>();
        internal readonly List<string> swarmRooms = new List<string>();
        internal readonly SortedSet<string> unknownCreatures = new SortedSet<string>(StringComparer.Ordinal);
        internal readonly List<string> unreadableLines = new List<string>();
        internal int[] fliesPerSwarmRoom = new int[0];

        internal SpawnTable(string region, IReadOnlyList<Campaign> campaigns)
        {
            Region = region;
            Campaigns = campaigns;
        }

        public string Region { get; }

        /// <summary>The campaigns merged into this table: one, or all of them for "All Outskirts creatures".</summary>
        public IReadOnlyList<Campaign> Campaigns { get; }

        /// <summary>Every spawner, indexed by <see cref="World.CreatureSpawner.SpawnerID"/>.</summary>
        public IReadOnlyList<World.CreatureSpawner> Spawners => spawners;

        /// <summary>Rooms tagged <c>SWARMROOM</c> and not hidden for the campaign, in world-file order.</summary>
        public IReadOnlyList<string> SwarmRooms => swarmRooms;

        /// <summary>Batflies each swarm room starts with (<c>FliesWorldAI.fliesToSpawn</c>), same order as <see cref="SwarmRooms"/>.</summary>
        public IReadOnlyList<int> FliesPerSwarmRoom => fliesPerSwarmRoom;

        /// <summary>Creature names that aren't on the desktop roster (or aren't creatures); their spawns are left out.</summary>
        public IReadOnlyCollection<string> UnknownCreatures => unknownCreatures;

        /// <summary>Creature lines the game's loader could not read either (it would throw on them).</summary>
        public IReadOnlyList<string> UnreadableLines => unreadableLines;

        public World.CreatureSpawner GetSpawner(int spawnerID) =>
            spawnerID >= 0 && spawnerID < spawners.Count ? spawners[spawnerID] : null;
    }
}
