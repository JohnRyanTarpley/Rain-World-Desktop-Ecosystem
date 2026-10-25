using System;
using System.Collections.Generic;
using System.Globalization;
using Type = RWDE.Sim.CreatureTemplate.Type;

namespace RWDE.Sim
{
    public sealed partial class World
    {
        /// <summary>
        /// A spawn den: a room from the world file (or <see cref="WorldLoader.OffscreenRoom"/>) and its
        /// den node number. The game stores this as a <c>WorldCoordinate</c> with only room and node set.
        /// </summary>
        public readonly struct Den : IEquatable<Den>
        {
            public Den(string room, int abstractNode)
            {
                this.room = room;
                this.abstractNode = abstractNode;
            }

            public readonly string room;
            public readonly int abstractNode;

            public bool Offscreen => room == WorldLoader.OffscreenRoom;

            public bool Equals(Den other) => room == other.room && abstractNode == other.abstractNode;
            public override bool Equals(object obj) => obj is Den other && Equals(other);
            public override int GetHashCode() => HashCode.Combine(room, abstractNode);
            public static bool operator ==(Den a, Den b) => a.Equals(b);
            public static bool operator !=(Den a, Den b) => !a.Equals(b);

            /// <summary>Like <c>WorldCoordinate.SaveToString</c>: "ROOM.node".</summary>
            public override string ToString() => room + "." + abstractNode.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>The game's <c>World.CreatureSpawner</c>: one entry of the world file's <c>CREATURES</c> block.</summary>
        public abstract class CreatureSpawner
        {
            protected CreatureSpawner(int inRegionSpawnerIndex, Den den)
            {
                this.inRegionSpawnerIndex = inRegionSpawnerIndex;
                this.den = den;
            }

            /// <summary>Position in the parsed spawner list; renumbered when tables are merged.</summary>
            public int inRegionSpawnerIndex;
            public readonly Den den;
            public bool nightCreature;

            /// <summary><c>region * 1000 + inRegionSpawnerIndex</c>; the desktop has one region, number 0.</summary>
            public int SpawnerID => inRegionSpawnerIndex;

            /// <summary>Text that is equal for two spawners exactly when they spawn the same thing in the same den.</summary>
            internal abstract string Signature { get; }

            /// <summary>A copy at a new index; a lineage also takes a new den conflict number.</summary>
            internal abstract CreatureSpawner Renumbered(int index, int conflictNumber);
        }

        /// <summary>The game's <c>World.SimpleSpawner</c>: <c>amount</c> creatures of one type in a den.</summary>
        public sealed class SimpleSpawner : CreatureSpawner
        {
            public SimpleSpawner(int inRegionSpawnerIndex, Den den, Type creatureType, string spawnDataString, int amount)
                : base(inRegionSpawnerIndex, den)
            {
                this.creatureType = creatureType;
                this.spawnDataString = spawnDataString;
                this.amount = amount;
            }

            public readonly Type creatureType;
            /// <summary>The <c>{...}</c> attribute group, or null.</summary>
            public readonly string spawnDataString;
            public readonly int amount;

            internal override string Signature => "S|" + den + "|" + creatureType + "|" + spawnDataString + "|" + amount;

            internal override CreatureSpawner Renumbered(int index, int conflictNumber) =>
                new SimpleSpawner(index, den, creatureType, spawnDataString, amount) { nightCreature = nightCreature };

            public override string ToString() => den + ": " + amount + " " + creatureType + (spawnDataString ?? "");
        }

        /// <summary>
        /// The game's <c>World.Lineage</c>: a den whose creature type moves along a list of types. The
        /// current position is a counter kept per <see cref="denString"/> (in the game's save file; in
        /// the <see cref="World"/> on the desktop).
        /// </summary>
        public sealed class Lineage : CreatureSpawner
        {
            public Lineage(int inRegionSpawnerIndex, Den den, Type?[] creatureTypes, float[] progressionChances, string[] spawnData, int conflictNumber)
                : base(inRegionSpawnerIndex, den)
            {
                this.creatureTypes = creatureTypes;
                this.progressionChances = progressionChances;
                this.spawnData = spawnData;
                denString = den.ToString();
                if (conflictNumber > 0) denString = denString + ";" + conflictNumber.ToString(CultureInfo.InvariantCulture);
            }

            /// <summary>Types along the lineage; null for <c>NONE</c> (the game stores -1).</summary>
            public readonly Type?[] creatureTypes;
            /// <summary>Chance, at each step, of moving on to the next type.</summary>
            public readonly float[] progressionChances;
            public readonly string[] spawnData;
            /// <summary>The save key for this lineage's counter.</summary>
            public readonly string denString;

            /// <summary>The type at step <paramref name="counter"/>, or null for <c>NONE</c> or a counter out of range (<c>CurrentType</c>).</summary>
            public Type? CurrentType(int counter) => counter < 0 || counter >= creatureTypes.Length ? null : creatureTypes[counter];

            /// <summary>The spawn data at step <paramref name="counter"/> (<c>CurrentSpawnData</c>).</summary>
            public string CurrentSpawnData(int counter) =>
                counter < 0 || counter >= creatureTypes.Length || creatureTypes[counter] == null ? null : spawnData[counter];

            /// <summary>
            /// Port of <c>ChanceToProgress</c>: rolls to move the counter one step on. The last step
            /// never progresses.
            /// </summary>
            public int ChanceToProgress(int counter, Random random)
            {
                if (counter >= 0 && counter < progressionChances.Length - 1 && random.NextDouble() < progressionChances[counter])
                    counter++;
                return counter;
            }

            internal override string Signature
            {
                get
                {
                    var text = "L|" + den + "|" + nightCreature;
                    for (int i = 0; i < creatureTypes.Length; i++)
                        text += "|" + (creatureTypes[i]?.ToString() ?? "NONE") + "-" + spawnData[i] + "-"
                                + progressionChances[i].ToString(CultureInfo.InvariantCulture);
                    return text;
                }
            }

            internal override CreatureSpawner Renumbered(int index, int conflictNumber) =>
                new Lineage(index, den, creatureTypes, progressionChances, spawnData, conflictNumber) { nightCreature = nightCreature };

            public override string ToString() => "lineage " + denString;
        }
    }
}
