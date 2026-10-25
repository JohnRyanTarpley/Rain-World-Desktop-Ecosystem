using System;

namespace RWDE.Sim
{
    /// <summary>
    /// One way to move from a tile to another, ported from the game's <c>MovementConnection</c>.
    /// Unlike the game, the ends are room tiles rather than <c>WorldCoordinate</c>s: the AI map only
    /// ever joins tiles of the same room. Connections between rooms arrive with the pathfinder.
    /// </summary>
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

        public MovementConnection(MovementType type, IntVector2 start, IntVector2 destination, int distance)
        {
            this.type = type;
            StartTile = start;
            DestTile = destination;
            this.distance = distance;
        }

        public readonly MovementType type;
        public readonly int distance;
        public IntVector2 StartTile { get; }
        public IntVector2 DestTile { get; }

        public bool IsDrop =>
            type == MovementType.DropToFloor || type == MovementType.DropToClimb || type == MovementType.DropToWater;

        public bool Equals(MovementConnection other) =>
            type == other.type && StartTile == other.StartTile && DestTile == other.DestTile && distance == other.distance;

        public override bool Equals(object obj) => obj is MovementConnection other && Equals(other);

        public override int GetHashCode() =>
            ((((((int)type * 397) ^ StartTile.GetHashCode()) * 397) ^ DestTile.GetHashCode()) * 397) ^ distance;

        public static bool operator ==(MovementConnection left, MovementConnection right) => left.Equals(right);
        public static bool operator !=(MovementConnection left, MovementConnection right) => !left.Equals(right);

        public override string ToString() => type + " " + StartTile + " to " + DestTile;
    }
}
