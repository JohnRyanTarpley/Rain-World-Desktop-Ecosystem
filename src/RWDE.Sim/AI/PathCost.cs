using System;

namespace RWDE.Sim
{
    /// <summary>
    /// The cost of a tile or movement for one creature, ported from the game's <c>PathCost</c>.
    /// Costs compare by legality first and resistance second, so any allowed path beats any illegal one.
    /// </summary>
    public struct PathCost : IComparable<PathCost>, IEquatable<PathCost>
    {
        // PathCost.Legality, in the game's order (comparisons use it).
        public enum Legality
        {
            Allowed,
            Unwanted,
            IllegalConnection,
            IllegalTile,
            SolidTile,
            Unallowed
        }

        public Legality legality;
        public float resistance;

        public PathCost(float resistance, Legality legality)
        {
            this.resistance = resistance;
            this.legality = legality;
        }

        public bool Allowed => legality <= Legality.Unwanted;
        public bool Considerable => legality != Legality.Unallowed;

        /// <summary>Resistances add up; the result is as illegal as the worse of the two.</summary>
        public static PathCost operator +(PathCost a, PathCost b) =>
            new PathCost(a.resistance + b.resistance, (Legality)Math.Max((int)a.legality, (int)b.legality));

        public static PathCost operator *(PathCost a, float b) => new PathCost(a.resistance * b, a.legality);

        public static bool operator ==(PathCost a, PathCost b) => a.Equals(b);
        public static bool operator !=(PathCost a, PathCost b) => !a.Equals(b);

        public static bool operator >(PathCost a, PathCost b) =>
            a.legality == b.legality ? a.resistance > b.resistance : a.legality > b.legality;

        public static bool operator >=(PathCost a, PathCost b) =>
            a.legality == b.legality ? a.resistance >= b.resistance : a.legality > b.legality;

        public static bool operator <(PathCost a, PathCost b) =>
            a.legality == b.legality ? a.resistance < b.resistance : a.legality < b.legality;

        public static bool operator <=(PathCost a, PathCost b) =>
            a.legality == b.legality ? a.resistance <= b.resistance : a.legality < b.legality;

        public int CompareTo(PathCost other) => this < other ? -1 : this > other ? 1 : 0;

        public bool Equals(PathCost other) => legality == other.legality && resistance == other.resistance;
        public override bool Equals(object obj) => obj is PathCost other && Equals(other);
        public override int GetHashCode() => ((int)legality * 397) ^ resistance.GetHashCode();

        public override string ToString() => "pc{" + resistance + ", " + legality + "}";
    }

    /// <summary>A template's cost for one tile accessibility. Mirrors the game's <c>TileTypeResistance</c>.</summary>
    public readonly struct TileTypeResistance
    {
        public TileTypeResistance(AItile.Accessibility accessibility, float resistance, PathCost.Legality legality)
        {
            this.accessibility = accessibility;
            cost = new PathCost(resistance, legality);
        }

        public readonly AItile.Accessibility accessibility;
        public readonly PathCost cost;
    }

    /// <summary>A template's cost for one movement type. Mirrors the game's <c>TileConnectionResistance</c>.</summary>
    public readonly struct TileConnectionResistance
    {
        public TileConnectionResistance(MovementConnection.MovementType movementType, float resistance, PathCost.Legality legality)
        {
            this.movementType = movementType;
            cost = new PathCost(resistance, legality);
        }

        public readonly MovementConnection.MovementType movementType;
        public readonly PathCost cost;
    }
}
