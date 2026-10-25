using System;
using System.Collections.Generic;

namespace RWDE.Sim
{
    /// <summary>The game's <c>EntityID</c>: the spawner that made an entity (-1 for none) and a running number.</summary>
    public readonly struct EntityID : IEquatable<EntityID>
    {
        public EntityID(int spawner, int number)
        {
            this.spawner = spawner;
            this.number = number;
        }

        public readonly int spawner;
        public readonly int number;

        public bool Equals(EntityID other) => spawner == other.spawner && number == other.number;
        public override bool Equals(object obj) => obj is EntityID other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(spawner, number);
        public static bool operator ==(EntityID a, EntityID b) => a.Equals(b);
        public static bool operator !=(EntityID a, EntityID b) => !a.Equals(b);
        public override string ToString() => "ID." + spawner + "." + number;
    }

    /// <summary>
    /// A creature as the world knows it while it is offscreen, mirroring the game's
    /// <c>AbstractCreature</c>. It waits in its spawn den, roams the offscreen world, travels to an exit
    /// of a desktop room and is realized there; leaving through an exit makes it abstract again.
    /// </summary>
    public sealed class AbstractCreature
    {
        /// <summary>Where the creature is.</summary>
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

        /// <remarks>
        /// The game's <c>GeneratePopulation</c> copies the spawner's <c>nightCreature</c> and then calls
        /// <c>setCustomFlags</c>, which clears it again, so only a <c>{Night}</c> flag in the spawn data counts.
        /// </remarks>
        internal AbstractCreature(CreatureTemplate template, EntityID id, World.Den spawnDen, string spawnData)
        {
            creatureTemplate = template;
            ID = id;
            this.spawnDen = spawnDen;
            this.spawnData = spawnData;
            setCustomFlags();
        }

        public readonly CreatureTemplate creatureTemplate;
        public readonly EntityID ID;
        /// <summary>The den it was spawned in (<c>spawnDen</c>).</summary>
        public readonly World.Den spawnDen;
        /// <summary>The spawner's <c>{...}</c> attribute group, or null.</summary>
        public readonly string spawnData;

        public Place place = Place.InDen;
        /// <summary>The room it is in, or heading for; null offscreen.</summary>
        public Room room;
        /// <summary>The exit it is travelling to, entered through, or last left by.</summary>
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

        public override string ToString() => creatureTemplate.name + " " + ID + " (" + place + ")";
    }

    /// <summary>
    /// Creates and removes realized creatures. Species ports implement it; the <see cref="World"/>
    /// decides when and where.
    /// </summary>
    public interface ICreatureRealizer
    {
        /// <summary>
        /// Realizes <paramref name="creature"/> at <paramref name="tile"/> on a room edge, coming in along
        /// <paramref name="inward"/>. Returns false when the species can't be realized (not ported yet),
        /// which leaves it offscreen.
        /// </summary>
        bool Realize(AbstractCreature creature, Room room, IntVector2 tile, IntVector2 inward);

        /// <summary>
        /// Realizes a creature that never travels (a Pole Plant or Monster Kelp) somewhere in
        /// <paramref name="room"/> of the realizer's choosing. Returns false when there is no place for it.
        /// </summary>
        bool RealizePlaced(AbstractCreature creature, Room room);

        /// <summary>Removes the realized body; the creature is abstract again.</summary>
        void Abstractize(AbstractCreature creature);
    }
}
