using System;

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
}
