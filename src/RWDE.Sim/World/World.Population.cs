using System;
using System.Collections.Generic;
using System.Linq;

namespace RWDE.Sim
{
    /// <summary>
    /// The abstract world's population: the offscreen <see cref="AbstractCreature"/>s spawned from the
    /// region's <see cref="SpawnTable"/>, and their arrivals into and departures from the desktop rooms
    /// through screen-edge and sky exits. Mirrors the parts of the game's <c>World</c>,
    /// <c>WorldLoader.GeneratePopulation</c>, <c>AbstractCreature</c> and <c>AbstractCreatureAI</c> that
    /// move creatures between dens, the offscreen world and a room.
    /// </summary>
    /// <remarks>
    /// The desktop has no rooms besides its monitors, so the rest of the region collapses into one
    /// offscreen space, like the game's offscreen den: every spawn waits in its den there, roams, and
    /// its next room is always a desktop room. Every random choice comes from <see cref="random"/>.
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
        private readonly List<AbstractCreature> creatures = new List<AbstractCreature>();
        private readonly Dictionary<string, int> lineageCounters = new Dictionary<string, int>();
        private SpawnTable table;
        private int nextID;
        private int[] swarmRoomFlies = new int[0];

        /// <summary>A world whose population comes from <paramref name="table"/>; call <see cref="GeneratePopulation"/> to spawn it.</summary>
        public World(StaticWorld staticWorld, SpawnTable table, int seed, params Room[] rooms) : this(seed, rooms)
        {
            this.staticWorld = staticWorld ?? throw new ArgumentNullException(nameof(staticWorld));
            this.table = table ?? throw new ArgumentNullException(nameof(table));
        }

        /// <summary>
        /// A world for <paramref name="campaignSetting"/> (one campaign's timeline value, or
        /// <see cref="Campaign.AllOutskirtsCreatures"/>), with its population generated.
        /// </summary>
        /// <param name="campaigns">The campaigns "All Outskirts creatures" merges; null for every campaign.</param>
        public static World Create(StaticWorld staticWorld, RegionSpawnData data, string campaignSetting, int seed,
            IEnumerable<Campaign> campaigns, params Room[] rooms)
        {
            var world = new World(staticWorld, new SpawnTable(data.RegionName, new Campaign[0]), seed, rooms);
            world.table = data.BuildTable(campaignSetting, campaigns, world.random);
            world.GeneratePopulation();
            return world;
        }

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

        /// <summary>The spawn table, or null for a world without a spawner.</summary>
        public SpawnTable SpawnTable => table;

        /// <summary>The realized rooms, in index order.</summary>
        public IEnumerable<Room> Rooms => abstractRooms.Select(r => r.realizedRoom);

        /// <summary>Every spawned creature, offscreen or in a room.</summary>
        public IReadOnlyList<AbstractCreature> Creatures => creatures;

        /// <summary>Batflies counted offscreen in each swarm room (<see cref="SpawnTable.SwarmRooms"/> order).</summary>
        public IReadOnlyList<int> SwarmRoomFlies => swarmRoomFlies;

        /// <summary>Batflies from the <c>CREATURES</c> block, counted offscreen in their dens.</summary>
        public int DenFlies { get; private set; }

        public IEnumerable<AbstractCreature> CreaturesInRooms => creatures.Where(c => c.place == AbstractCreature.Place.InRoom);

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
            if (table == null || staticWorld == null)
                throw new InvalidOperationException("This world was made without a spawn table.");
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
            var creature = new AbstractCreature(this, template, new EntityID(spawner.SpawnerID, nextID++), spawner.den, spawnData);
            // AbstractCreature constructor: a den spawn waits 1 tick in the offscreen den and 100-999
            // ticks in a room's den; Pole Plants and Monster Kelp don't wait.
            if (template.type == CreatureTemplate.Type.TentaclePlant || template.type == CreatureTemplate.Type.PoleMimic)
                creature.remainInDenCounter = 0;
            else
                creature.remainInDenCounter = spawner.den.Offscreen ? 1 : RandomRange(100, 1000);
            creatures.Add(creature);
        }

        // Advances the population one tick (1/40 s); called from Update.
        private void UpdatePopulation()
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
        /// A realized creature left its room through <paramref name="exit"/>: its body is removed and it
        /// is abstract and offscreen again, free to come back later.
        /// </summary>
        public void CreatureLeftRoom(AbstractCreature creature, RoomExit exit)
        {
            if (creature.place != AbstractCreature.Place.InRoom || !creatures.Contains(creature))
                throw new InvalidOperationException(creature + " is not a spawned creature in a room.");
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
                if (realizer == null) return;
                var room = abstractRooms[RandomRange(0, abstractRooms.Length)].realizedRoom;
                if (realizer.RealizePlaced(c, room))
                {
                    c.place = AbstractCreature.Place.InRoom;
                    c.pos = new WorldCoordinate(room.abstractRoom.index, -1, -1, -1);
                    c.exit = null;
                    c.timeSpentHere = 0;
                }
                return;
            }

            // AbstractCreatureAI.TryToGoToRoom: a random node of a type the creature maps.
            var exits = new List<RoomExit>();
            foreach (var room in Rooms)
                foreach (var exit in RoomExit.FindExits(room))
                    if (exit.EntryTiles(template).Count > 0)
                        exits.Add(exit);
            if (exits.Count == 0) return;

            var target = exits[RandomRange(0, exits.Count)];
            c.place = AbstractCreature.Place.Travelling;
            c.exit = target;
            c.abstractAI.destination = new WorldCoordinate(target.Room.abstractRoom.index, -1, -1, -1);
            c.timeBuffer = 0;
        }

        // Port of the timing in AbstractCreatureAI.FollowPath: a step of length d takes
        // d / (offScreenSpeed * offscreenSpeedFac) ticks.
        private void FollowPath(AbstractCreature c)
        {
            if (c.timeBuffer <= OffscreenTravelDistance / (c.creatureTemplate.offScreenSpeed * OffscreenSpeedFac)) return;
            if (populationCap <= CreaturesInRooms.Count()) return;

            // The terrain may have changed on the way; enter through what is left of the exit.
            var room = c.exit.Room;
            var tiles = c.exit.EntryTiles(c.creatureTemplate);
            if (tiles.Count == 0 || realizer == null)
            {
                GoOffscreen(c, c.exit);
                return;
            }
            var tile = tiles[RandomRange(0, tiles.Count)];
            if (!realizer.Realize(c, room, tile, c.exit.Inward))
            {
                GoOffscreen(c, c.exit);
                return;
            }
            c.place = AbstractCreature.Place.InRoom;
            c.pos = new WorldCoordinate(room.abstractRoom.index, tile.x, tile.y, -1);
            c.abstractAI.destination = c.pos;
            c.entryTile = tile;
            c.timeSpentHere = 0;
        }

        private static void GoOffscreen(AbstractCreature c, RoomExit exit)
        {
            c.place = AbstractCreature.Place.Offscreen;
            c.pos = AbstractCreature.OffscreenPos;
            c.abstractAI.destination = c.pos;
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
