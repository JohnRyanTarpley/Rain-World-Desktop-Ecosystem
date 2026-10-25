using System;

namespace RWDE.Shell.Terrain
{
    public enum RoomEdge
    {
        Left,
        Right,
        Bottom,
        Top
    }

    /// <summary>
    /// A run of open (non-solid) tiles along one edge of the room. Outside the room the sim extends
    /// each edge tile outwards, so creatures leave and arrive through these runs: the left and right
    /// edges are exits to the offscreen world, and the top edge is the sky.
    /// </summary>
    public readonly struct EdgeOpening : IEquatable<EdgeOpening>
    {
        public EdgeOpening(RoomEdge edge, int from, int to)
        {
            Edge = edge;
            From = from;
            To = to;
        }

        public RoomEdge Edge { get; }

        /// <summary>First open tile along the edge: a y on the left and right edges, an x on the bottom and top.</summary>
        public int From { get; }

        /// <summary>Last open tile along the edge, inclusive.</summary>
        public int To { get; }

        public int Length => To - From + 1;

        /// <summary>True for openings in the top edge, where flying creatures come and go.</summary>
        public bool IsSky => Edge == RoomEdge.Top;

        public bool Equals(EdgeOpening other) => Edge == other.Edge && From == other.From && To == other.To;
        public override bool Equals(object obj) => obj is EdgeOpening other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Edge, From, To);
        public override string ToString() => Edge + " " + From + ".." + To;
    }
}
