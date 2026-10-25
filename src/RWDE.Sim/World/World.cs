using System;
using System.Collections.Generic;
using System.Linq;

namespace RWDE.Sim
{
    /// <summary>
    /// The abstract world around the desktop: the offscreen population of <see cref="AbstractCreature"/>s
    /// spawned from the region's <see cref="SpawnTable"/>, and their arrivals into and departures from
    /// the desktop rooms through screen-edge and sky exits. Mirrors the parts of the game's <c>World</c>,
    /// <c>WorldLoader.GeneratePopulation</c>, <c>AbstractCreature</c> and <c>AbstractCreatureAI</c> that
    /// move creatures between dens, the offscreen world and a room.
    /// </summary>
    /// <remarks>
    /// The desktop has no other rooms, so the rest of the region collapses into one offscreen space,
    /// like the game's offscreen den: every spawn waits in its den there, roams, and its next room is
    /// always a desktop room. Every random choice comes from one <see cref="random"/>, seeded from config,
    /// so a run can be replayed.
    /// </remarks>
    public sealed partial class World
    {
        /// <summary>
        /// Travel distance from the offscreen world to a room's exit. The game's offscreen den nodes have
        /// connectivity 1 to everything and <c>World.RegionTransportationDistanceBetweenNodes</c> returns 1
        /// for the offscreen den.
        /// </summary>
        private const int OffscreenTravelDistance = 1;

        /// <summary>
        /// <c>AbstractRoom.CreatureAttractionToFloat(Neutral)</c>: the offscreen world has no room
        /// attractions, so every creature finds it neutral.
        /// </summary>
        private const float OffscreenAttraction = 0.5f;

        /// <summary>Creatures stay in their dens this close to the rain, in ticks (<c>WantToStayInDenUntilEndOfCycle</c>, story value 60 s).</summary>
        private const int StayInDenBeforeRain = 60 * 40;

        private readonly StaticWorld staticWorld;
        private readonly List<Room> rooms = new List<Room>();
        private readonly List<AbstractCreature> creatures = new List<AbstractCreature>();
        private readonly Dictionary<string, int> lineageCounters = new Dictionary<string, int>();
        private SpawnTable table;
        private int nextID;
        private int[] swarmRoomFlies = new int[0];

        public World(StaticWorld staticWorld, SpawnTable table, int seed)
        {
            this.staticWorld = staticWorld ?? throw new ArgumentNullException(nameof(staticWorld));
            this.table = table ?? throw new ArgumentNullException(nameof(table));
            random = new Random(seed);
        }

        /// <summary>
        /// A world for <paramref name="campaignSetting"/> (one campaign's timeline value, or
        /// <see cref="Campaign.AllOutskirtsCreatures"/>), with its population generated.
        /// </summary>
        /// <param name="campaigns">The campaigns "All Outskirts creatures" merges; default every campaign.</param>
        public static World Create(StaticWorld staticWorld, RegionSpawnData data, string campaignSetting, int seed,
            IEnumerable<Campaign> campaigns = null)
        {
            var world = new World(staticWorld, new SpawnTable(data.RegionName, new Campaign[0]), seed);
            world.table = data.BuildTable(campaignSetting, campaigns, world.random);
            world.GeneratePopulation();
            return world;
        }

        /// <summary>The world's only source of randomness.</summary>
        public readonly Random random;

        /// <summary>Creates and removes realized bodies. Without one, nothing enters a room.</summary>
        public ICreatureRealizer realizer;

        /// <summary>Most creatures in the desktop rooms at once; arrivals wait offscreen beyond it.</summary>
        public int populationCap = int.MaxValue;

        /// <summary>Ticks until the rain (<c>RainCycle.TimeUntilRain</c>); set by the rain cycle.</summary>
        public int timeUntilRain = int.MaxValue;

        /// <summary>The pre-cycle is running (<c>RainCycle.maxPreTimer &gt; 0</c>); <c>{PreCycle}</c> creatures only come out then.</summary>
        public bool preCycleActive;

        /// <summary>It is night (<c>RainCycle.dayNightCounter &gt; 600</c>); <c>{Night}</c> creatures only come out then.</summary>
        public bool nightActive;

        public SpawnTable SpawnTable => table;
        public IReadOnlyList<Room> Rooms => rooms;
        public IReadOnlyList<AbstractCreature> Creatures => creatures;

        /// <summary>Batflies counted offscreen in each swarm room (<see cref="SpawnTable.SwarmRooms"/> order).</summary>
        public IReadOnlyList<int> SwarmRoomFlies => swarmRoomFlies;

        /// <summary>Batflies from the <c>CREATURES</c> block, counted offscreen in their dens.</summary>
        public int DenFlies { get; private set; }

        public IEnumerable<AbstractCreature> CreaturesInRooms => creatures.Where(c => c.place == AbstractCreature.Place.InRoom);

        public void AddRoom(Room room)
        {
            if (room == null) throw new ArgumentNullException(nameof(room));
            if (!rooms.Contains(room)) rooms.Add(room);
        }

        /// <summary>Removes a room (a monitor went away). Creatures in it, or on their way to it, go offscreen.</summary>
        public void RemoveRoom(Room room)
        {
            if (!rooms.Remove(room)) return;
            foreach (var c in creatures)
            {
                if (c.room != room) continue;
                if (c.place == AbstractCreature.Place.InRoom) realizer?.Abstractize(c);
                if (c.place == AbstractCreature.Place.InRoom || c.place == AbstractCreature.Place.Travelling) GoOffscreen(c, c.exit);
            }
        }

        /// <summary>Switches the spawn table (e.g. a new campaign setting); it applies from the next <see cref="GeneratePopulation"/>.</summary>
        public void SetSpawnTable(SpawnTable newTable) => table = newTable ?? throw new ArgumentNullException(nameof(newTable));

        /// <summary>The lineage counter for <paramref name="lineage"/> (the game keeps it in the save's <c>RegionState</c>).</summary>
        public int LineageCounter(Lineage lineage) => lineageCounters.TryGetValue(lineage.denString, out int n) ? n : 0;

        /// <summary>
        /// Port of <c>WorldLoader.GeneratePopulation</c> with fresh spawns, plus the swarm rooms' batflies
        /// (<c>FliesWorldAI</c> constructor). Offscreen creatures are replaced; creatures in a room stay.
        /// Types whose template doesn't exist (their DLC is off) are left out.
        /// </summary>
        public void GeneratePopulation()
        {
            creatures.RemoveAll(c => c.place != AbstractCreature.Place.InRoom);
            DenFlies = 0;

            foreach (var spawner in table.Spawners)
            {
                if (spawner is SimpleSpawner simple)
                {
                    var template = staticWorld.GetCreatureTemplate(simple.creatureType);
                    if (template == null || simple.amount <= 0) continue;
                    if (template.quantified)
                    {
                        // AbstractRoom.AddQuantifiedCreature: counted, not simulated.
                        DenFlies += simple.amount;
                        continue;
                    }
                    for (int i = 0; i < simple.amount; i++)
                        Spawn(template, simple, simple.spawnDataString);
                }
                else if (spawner is Lineage lineage)
                {
                    lineageCounters.TryGetValue(lineage.denString, out int counter);
                    var type = lineage.CurrentType(counter);
                    if (type == null)
                    {
                        // A NONE step rolls to move on; the den stays empty this cycle.
                        lineageCounters[lineage.denString] = lineage.ChanceToProgress(counter, random);
                        continue;
                    }
                    lineageCounters[lineage.denString] = counter;
                    var template = staticWorld.GetCreatureTemplate(type.Value);
                    if (template != null) Spawn(template, lineage, lineage.CurrentSpawnData(counter));
                }
            }

            swarmRoomFlies = staticWorld.GetCreatureTemplate(CreatureTemplate.Type.Fly) == null
                ? new int[table.SwarmRooms.Count]
                : table.FliesPerSwarmRoom.ToArray();
        }

        private void Spawn(CreatureTemplate template, CreatureSpawner spawner, string spawnData)
        {
            var creature = new AbstractCreature(template, new EntityID(spawner.SpawnerID, nextID++), spawner.den, spawnData);
            // AbstractCreature constructor: a den spawn waits 1 tick in the offscreen den and 100-999
            // ticks in a room's den; Pole Plants and Monster Kelp don't wait.
            if (template.type == CreatureTemplate.Type.TentaclePlant || template.type == CreatureTemplate.Type.PoleMimic)
                creature.remainInDenCounter = 0;
            else
                creature.remainInDenCounter = spawner.den.Offscreen ? 1 : RandomRange(100, 1000);
            creatures.Add(creature);
        }

        /// <summary>Advances the abstract world one tick (1/40 s).</summary>
        public void Update()
        {
            foreach (var c in creatures.ToArray())
            {
                switch (c.place)
                {
                    case AbstractCreature.Place.InDen:
                        InDenUpdate(c);
                        break;
                    case AbstractCreature.Place.Offscreen:
                        c.timeSpentHere++;
                        c.timeBuffer++;
                        MigrationBehavior(c);
                        break;
                    case AbstractCreature.Place.Travelling:
                        c.timeSpentHere++;
                        c.timeBuffer++;
                        FollowPath(c);
                        break;
                    case AbstractCreature.Place.InRoom:
                        c.timeSpentHere++;
                        break;
                }
            }
        }

        /// <summary>
        /// A realized creature left <paramref name="exit"/>'s room through it: its body is removed and it
        /// is abstract and offscreen again, free to come back later.
        /// </summary>
        public void CreatureLeftRoom(AbstractCreature creature, RoomExit exit)
        {
            if (creature.place != AbstractCreature.Place.InRoom)
                throw new InvalidOperationException(creature + " is not in a room.");
            realizer?.Abstractize(creature);
            GoOffscreen(creature, exit);
        }

        /// <summary>Removes a creature from the world for good (killed, or cleared by a reset).</summary>
        public void RemoveCreature(AbstractCreature creature)
        {
            if (creature.place == AbstractCreature.Place.InRoom) realizer?.Abstractize(creature);
            creatures.Remove(creature);
        }

        // Port of AbstractCreature.InDenUpdate.
        private void InDenUpdate(AbstractCreature c)
        {
            bool preCycleOk = !c.preCycle || preCycleActive;
            if (!(c.remainInDenCounter > -1 && (!c.nightCreature || nightActive || c.ignoreCycle) && preCycleOk)) return;
            if (WantToStayInDenUntilEndOfCycle(c))
            {
                c.remainInDenCounter = -1;
                return;
            }
            c.remainInDenCounter--;
            if (c.remainInDenCounter < 0)
            {
                c.remainInDenCounter = -1;
                c.place = AbstractCreature.Place.Offscreen;
                c.timeSpentHere = 0;
            }
        }

        // Port of AbstractCreature.WantToStayInDenUntilEndOfCycle for an unhurt creature.
        private bool WantToStayInDenUntilEndOfCycle(AbstractCreature c)
        {
            if (timeUntilRain < StayInDenBeforeRain && !c.nightCreature && !c.ignoreCycle) return true;
            if (c.preCycle && !preCycleActive) return true;
            return false;
        }

        // Port of AbstractCreatureAI.MigrationBehavior. When the roll says "move to another room", the
        // game picks a random or more attractive neighbour; offscreen, the only other rooms are the
        // desktop rooms.
        private void MigrationBehavior(AbstractCreature c)
        {
            var template = c.creatureTemplate;
            if (c.timeSpentHere <= RandomRange(0, template.abstractedLaziness)) return;
            if (!TimeInfluencedRandomRoll(template.roamBetweenRoomsChance * Mathf.Lerp(2f, 0.1f, OffscreenAttraction), 1)) return;
            if (populationCap <= CreaturesInRooms.Count()) return;

            if (Stationary(template))
            {
                if (rooms.Count == 0 || realizer == null) return;
                var room = rooms[RandomRange(0, rooms.Count)];
                if (realizer.RealizePlaced(c, room))
                {
                    c.place = AbstractCreature.Place.InRoom;
                    c.room = room;
                    c.exit = null;
                    c.timeSpentHere = 0;
                }
                return;
            }

            // AbstractCreatureAI.TryToGoToRoom: a random node of a type the creature maps.
            var exits = new List<RoomExit>();
            foreach (var room in rooms)
                foreach (var exit in RoomExit.FindExits(room))
                    if (exit.EntryTiles(template).Count > 0)
                        exits.Add(exit);
            if (exits.Count == 0) return;

            var target = exits[RandomRange(0, exits.Count)];
            c.place = AbstractCreature.Place.Travelling;
            c.room = target.Room;
            c.exit = target;
            c.timeBuffer = 0;
        }

        // Port of the timing in AbstractCreatureAI.FollowPath: a step of length d takes
        // d / (offScreenSpeed * offscreenSpeedFac) ticks.
        private void FollowPath(AbstractCreature c)
        {
            if (!rooms.Contains(c.room))
            {
                GoOffscreen(c, null);
                return;
            }
            if (c.timeBuffer <= OffscreenTravelDistance / (c.creatureTemplate.offScreenSpeed * OffscreenSpeedFac)) return;
            if (populationCap <= CreaturesInRooms.Count()) return;

            // The terrain may have changed on the way; enter through what is left of the exit.
            var tiles = c.exit.EntryTiles(c.creatureTemplate);
            if (tiles.Count == 0 || realizer == null)
            {
                GoOffscreen(c, c.exit);
                return;
            }
            var tile = tiles[RandomRange(0, tiles.Count)];
            if (!realizer.Realize(c, c.room, tile, c.exit.Inward))
            {
                GoOffscreen(c, c.exit);
                return;
            }
            c.place = AbstractCreature.Place.InRoom;
            c.entryTile = tile;
            c.timeSpentHere = 0;
        }

        private static void GoOffscreen(AbstractCreature c, RoomExit exit)
        {
            c.place = AbstractCreature.Place.Offscreen;
            c.room = null;
            c.exit = exit;
            c.timeSpentHere = 0;
            c.timeBuffer = 0;
        }

        /// <summary>
        /// <c>AbstractCreatureAI.offscreenSpeedFac</c> for a creature following nobody:
        /// <c>Lerp(0.1, 0.9, (1 - attraction)^2)</c>, with the offscreen world's neutral attraction.
        /// </summary>
        private static float OffscreenSpeedFac => Mathf.Lerp(0.1f, 0.9f, Mathf.Pow(1f - OffscreenAttraction, 2f));

        /// <summary>Creatures that never travel offscreen and are placed in a room instead (Pole Plants, Monster Kelp).</summary>
        private static bool Stationary(CreatureTemplate t) => t.abstractImmobile || t.offScreenSpeed <= 0f;

        // AbstractCreatureAI.TimeInfluencedRandomRoll
        private bool TimeInfluencedRandomRoll(float stat, int time)
        {
            for (int i = 0; i < time; i++)
                if (random.NextDouble() < stat)
                    return true;
            return false;
        }

        // UnityEngine.Random.Range(int, int): max exclusive, and min when the range is empty.
        private int RandomRange(int min, int max) => max <= min ? min : random.Next(min, max);
    }
}
