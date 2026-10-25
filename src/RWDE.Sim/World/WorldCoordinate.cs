using System;

namespace RWDE.Sim
{
    /// <summary>
    /// A position in the world, ported from the game's <c>WorldCoordinate</c>: a room index plus a tile,
    /// an abstract node (a room exit) or both. -1 marks a part as undefined. The game's unknown-room
    /// names (rooms of other regions) have no desktop counterpart and are left out.
    /// </summary>
    public struct WorldCoordinate : IEquatable<WorldCoordinate>
    {
        public int x;
        public int y;
        public int room;
        public int abstractNode;

        public WorldCoordinate(int room, int x, int y, int abstractNode)
        {
            this.x = x;
            this.y = y;
            this.room = room;
            this.abstractNode = abstractNode;
        }

        /// <summary>Kept as the game has it: only (-1, -1) counts as no tile.</summary>
        public bool TileDefined => x != -1 || y != -1;
        public bool NodeDefined => abstractNode > -1;

        public IntVector2 Tile
        {
            get => new IntVector2(x, y);
            set
            {
                x = value.x;
                y = value.y;
            }
        }

        public bool CompareDisregardingTile(WorldCoordinate other) => room == other.room && abstractNode == other.abstractNode;

        public bool CompareDisregardingNode(WorldCoordinate other) => room == other.room && x == other.x && y == other.y;

        public WorldCoordinate WashTileData() => new WorldCoordinate(room, -1, -1, abstractNode);
        public WorldCoordinate WashNode() => new WorldCoordinate(room, x, y, -1);

        public bool Equals(WorldCoordinate other) =>
            room == other.room && x == other.x && y == other.y && abstractNode == other.abstractNode;

        public bool Equals(WorldCoordinate? other) => other.HasValue && Equals(other.Value);
        public override bool Equals(object obj) => obj is WorldCoordinate other && Equals(other);
        public override int GetHashCode() => (((room * 397) ^ x) * 397 ^ y) * 397 ^ abstractNode;

        public static bool operator ==(WorldCoordinate a, WorldCoordinate b) => a.Equals(b);
        public static bool operator !=(WorldCoordinate a, WorldCoordinate b) => !a.Equals(b);

        public static WorldCoordinate operator +(WorldCoordinate a, IntVector2 b) =>
            new WorldCoordinate(a.room, a.x + b.x, a.y + b.y, a.abstractNode);

        public override string ToString() => "WC ~ r: " + room + " x: " + x + " y: " + y + " n:" + abstractNode;
    }
}
