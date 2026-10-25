using System.Collections.Generic;

namespace RWDE.Sim
{
    /// <summary>
    /// A creature as the world sees it, ported from the game's <c>AbstractCreature</c>: its template,
    /// world, position and abstract AI, which the pathfinder reads, plus what the spawner needs. A
    /// spawned creature waits in its spawn den offscreen, roams the offscreen world, travels to an exit
    /// of a desktop room and is realized there; leaving through an exit makes it abstract again.
    /// </summary>
    public class AbstractCreature
    {
        /// <summary>Where a spawned creature is.</summary>
        public enum Place
        {
            /// <summary>In its spawn den offscreen (the game's <c>InDen</c>).</summary>
            InDen,
            /// <summary>Out of its den, somewhere offscreen.</summary>
            Offscreen,
            /// <summary>Offscreen, on its way to <see cref="exit"/>.</summary>
            Travelling,
            /// <summary>In a desktop room (realized).</summary>
            InRoom
        }

        /// <summary>Nowhere in the world's rooms: offscreen.</summary>
        public static readonly WorldCoordinate OffscreenPos = new WorldCoordinate(-1, -1, -1, -1);

        public AbstractCreature(World world, CreatureTemplate creatureTemplate, WorldCoordinate pos)
        {
            this.world = world;
            this.creatureTemplate = creatureTemplate;
            this.pos = pos;
            abstractAI = new AbstractCreatureAI(world, this);
        }

        /// <remarks>
        /// The game's <c>GeneratePopulation</c> copies the spawner's <c>nightCreature</c> and then calls
        /// <c>setCustomFlags</c>, which clears it again, so only a <c>{Night}</c> flag in the spawn data counts.
        /// </remarks>
        internal AbstractCreature(World world, CreatureTemplate creatureTemplate, EntityID id, World.Den spawnDen, string spawnData)
            : this(world, creatureTemplate, OffscreenPos)
        {
            ID = id;
            this.spawnDen = spawnDen;
            this.spawnData = spawnData;
            place = Place.InDen;
            setCustomFlags();
        }

        public World world;
        public readonly CreatureTemplate creatureTemplate;
        /// <summary>Where the creature is: its room and tile, and the node it last entered through or is heading for.</summary>
        public WorldCoordinate pos;
        public AbstractCreatureAI abstractAI;

        public readonly EntityID ID = new EntityID(-1, -1);
        /// <summary>The den it was spawned in (<c>spawnDen</c>).</summary>
        public readonly World.Den spawnDen = new World.Den(null, -1);
        /// <summary>The spawner's <c>{...}</c> attribute group, or null.</summary>
        public readonly string spawnData;

        /// <summary>Where it is; a creature made outside the spawner counts as in its room.</summary>
        public Place place = Place.InRoom;
        /// <summary>The exit it is travelling to, entered through, or last left by; null for one placed in its room.</summary>
        public RoomExit exit;
        /// <summary>The tile it was realized at.</summary>
        public IntVector2 entryTile;

        /// <summary>Ticks left in the den; -1 when not waiting (<c>remainInDenCounter</c>).</summary>
        public int remainInDenCounter = -1;
        /// <summary>Ticks in the current room or offscreen stretch (<c>AbstractWorldEntity.timeSpentHere</c>).</summary>
        public int timeSpentHere;
        /// <summary>Ticks banked toward offscreen travel (<c>AbstractCreatureAI.timeBuffer</c>).</summary>
        public int timeBuffer;

        // Flags from the spawn data (setCustomFlags).
        public bool nightCreature;
        public bool ignoreCycle;
        public bool preCycle;
        public bool superSizeMe;
        public bool tentacleImmune;
        public bool lavaImmune;
        public bool voidCreature;
        public bool Winterized;
        public bool rippleCreature;
        public readonly List<string> unrecognizedFlags = new List<string>();

        public AbstractRoom Room => world.GetAbstractRoom(pos);

        public bool InDen => place == Place.InDen;
        public bool Realized => place == Place.InRoom;

        /// <summary>
        /// Port of <c>AbstractCreature.setCustomFlags</c> for the spawn-data flags. Region-wide flags
        /// (<c>globalCreatureFlags</c>) and blizzard and arena flags don't apply to the Outskirts on the
        /// desktop. <c>Seed</c> and <c>RotType</c> are kept for the realized creature to read from
        /// <see cref="spawnData"/>.
        /// </summary>
        public void setCustomFlags()
        {
            unrecognizedFlags.Clear();
            nightCreature = false;
            ignoreCycle = false;
            superSizeMe = false;
            tentacleImmune = false;
            voidCreature = false;
            if (spawnData == null || spawnData.Length < 2 || spawnData[0] != '{') return;

            foreach (var flag in spawnData.Substring(1, spawnData.Length - 2).Split(',', '|'))
            {
                if (flag.Length == 0) continue;
                switch (flag.Split(':')[0])
                {
                    case "Night":
                        nightCreature = true;
                        ignoreCycle = false;
                        break;
                    case "Lavasafe": lavaImmune = true; break;
                    case "Ignorecycle": ignoreCycle = true; break;
                    case "AlternateForm": superSizeMe = true; break;
                    case "TentacleImmune": tentacleImmune = true; break;
                    case "Winter": Winterized = true; break;
                    case "PreCycle": preCycle = true; break;
                    case "Voidsea":
                        lavaImmune = true;
                        voidCreature = true;
                        break;
                    case "Ripple": rippleCreature = true; break;
                    case "Seed":
                    case "RotType":
                        break;
                    default:
                        // Numbers such as a Pole Plant's length or a centipede's size are read by the creature itself.
                        unrecognizedFlags.Add(flag);
                        break;
                }
            }
        }

        public override string ToString() => creatureTemplate.name + " " + ID + " at " + pos + " (" + place + ")";
    }

    /// <summary>
    /// The offscreen side of a creature's AI, ported from the game's <c>AbstractCreatureAI</c>. So far
    /// only its destination, its node path and the link to the realized AI.
    /// </summary>
    public class AbstractCreatureAI
    {
        public AbstractCreatureAI(World world, AbstractCreature parent)
        {
            this.world = world;
            this.parent = parent;
            destination = parent.pos;
        }

        public World world;
        public readonly AbstractCreature parent;
        public WorldCoordinate destination;
        /// <summary>The nodes the creature plans to pass through offscreen.</summary>
        public readonly List<WorldCoordinate> path = new List<WorldCoordinate>();
        /// <summary>The realized AI, while the creature is in a room.</summary>
        public ArtificialIntelligence RealAI;
    }
}
