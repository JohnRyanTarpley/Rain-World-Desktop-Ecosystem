using System;

namespace RWDE.Sim
{
    /// <summary>
    /// One way to move between two <see cref="WorldCoordinate"/>s, ported from the game's
    /// <c>MovementConnection</c>: tile to tile inside a room, or to and from a room's abstract nodes
    /// (the pathfinder's shortcut and between-room connections).
    /// </summary>
    /// <remarks>
    /// The game stamps AI-map connections with the room's index. A desktop <see cref="Room"/> can join
    /// a <see cref="World"/> after its map is built, so AI-map connections carry room -1 and the
    /// pathfinder moves them into its room with <see cref="InRoom"/>.
    /// </remarks>
    public readonly struct MovementConnection : IEquatable<MovementConnection>
    {
        // MovementConnection.MovementType, in the game's order (templates index their costs by it).
        public enum MovementType
        {
            Standard,
            ReachOverGap,
            ReachUp,
            DoubleReachUp,
            ReachDown,
            SemiDiagonalReach,
            DropToFloor,
            DropToClimb,
            DropToWater,
            LizardTurn,
            OpenDiagonal,
            Slope,
            CeilingSlope,
            ShortCut,
            NPCTransportation,
            BigCreatureShortCutSqueeze,
            OutsideRoom,
            SideHighway,
            SkyHighway,
            SeaHighway,
            RegionTransportation,
            BetweenRooms,
            OffScreenMovement,
            OffScreenUnallowed
        }

        public MovementConnection(MovementType type, WorldCoordinate startCoord, WorldCoordinate destinationCoord, int distance)
        {
            this.type = type;
            this.startCoord = startCoord;
            this.destinationCoord = destinationCoord;
            this.distance = distance;
        }

        /// <summary>A connection between two tiles of the room whose AI map holds it (room -1, no node).</summary>
        public MovementConnection(MovementType type, IntVector2 start, IntVector2 destination, int distance)
            : this(type, new WorldCoordinate(-1, start.x, start.y, -1), new WorldCoordinate(-1, destination.x, destination.y, -1), distance)
        {
        }

        public readonly MovementType type;
        public readonly WorldCoordinate startCoord;
        public readonly WorldCoordinate destinationCoord;
        public readonly int distance;
        public IntVector2 StartTile => startCoord.Tile;
        public IntVector2 DestTile => destinationCoord.Tile;

        public bool IsDrop =>
            type == MovementType.DropToFloor || type == MovementType.DropToClimb || type == MovementType.DropToWater;

        /// <summary>This connection with both ends placed in room <paramref name="room"/>.</summary>
        public MovementConnection InRoom(int room)
        {
            var s = startCoord;
            var d = destinationCoord;
            s.room = room;
            d.room = room;
            return new MovementConnection(type, s, d, distance);
        }

        public bool Equals(MovementConnection other) =>
            type == other.type && startCoord == other.startCoord && destinationCoord == other.destinationCoord && distance == other.distance;

        public override bool Equals(object obj) => obj is MovementConnection other && Equals(other);

        public override int GetHashCode() =>
            ((((((int)type * 397) ^ startCoord.GetHashCode()) * 397) ^ destinationCoord.GetHashCode()) * 397) ^ distance;

        public static bool operator ==(MovementConnection left, MovementConnection right) => left.Equals(right);
        public static bool operator !=(MovementConnection left, MovementConnection right) => !left.Equals(right);

        public override string ToString() => type + " " + startCoord + " to " + destinationCoord;
    }
}
