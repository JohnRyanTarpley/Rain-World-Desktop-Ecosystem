namespace RWDE.Sim
{
    /// <summary>
    /// One node of an <see cref="AbstractRoom"/>, ported from the game's <c>AbstractRoomNode</c>: a
    /// place where creatures enter and leave the room. Desktop rooms have room exits only, one per pipe
    /// to another monitor room; screen-edge and sky exits arrive with the abstract world.
    /// </summary>
    /// <remarks>
    /// The game bakes each node's <c>connectivity</c> (the cost of walking to every other node, per
    /// pre-baked creature) into the level files. Desktop terrain changes, so the costs are worked out on
    /// demand from the room's <see cref="AImap"/> (see <see cref="CreatureSpecificAImap"/>) and
    /// <see cref="ConnectionCost"/> reads them from there.
    /// </remarks>
    public sealed partial class AbstractRoomNode
    {
        // AbstractRoomNode.Type, in the game's registration order.
        public enum Type
        {
            Exit,
            Den,
            RegionTransportation,
            SideExit,
            SkyExit,
            SeaExit,
            BatHive,
            GarbageHoles
        }

        private readonly AbstractRoom room;
        private readonly int index;

        internal AbstractRoomNode(AbstractRoom room, int index, Type type, int shortCutLength, bool submerged, int entranceWidth)
        {
            this.room = room;
            this.index = index;
            this.type = type;
            this.shortCutLength = shortCutLength;
            this.submerged = submerged;
            this.entranceWidth = entranceWidth;
            borderExit = type == Type.SideExit || type == Type.SkyExit || type == Type.SeaExit;
        }

        public readonly Type type;
        public readonly bool borderExit;
        /// <summary>Steps from the entrance to the room exit, for exits and dens; -1 otherwise.</summary>
        public readonly int shortCutLength;
        public readonly bool submerged;
        public readonly int entranceWidth;

        /// <summary>
        /// What walking from this node to <paramref name="otherNode"/> through the room costs
        /// <paramref name="creatureType"/>, or -1 when it can't (<c>AbstractRoomNode.ConnectionCost</c>).
        /// </summary>
        public int ConnectionCost(int otherNode, CreatureTemplate creatureType) => Connectivity(otherNode, creatureType, 0);

        /// <summary>Tiles walked from this node to <paramref name="otherNode"/>, or -1 (<c>AbstractRoomNode.ConnectionLength</c>).</summary>
        public int ConnectionLength(int otherNode, CreatureTemplate creatureType) => Connectivity(otherNode, creatureType, 1);

        private int Connectivity(int otherNode, CreatureTemplate creatureType, int part)
        {
            if (creatureType.PreBakedPathingIndex == -1 || otherNode < 0)
                return -1;
            var map = room.realizedRoom.aimap.CreatureSpecificAImap(creatureType);
            return map == null ? -1 : map.GetConnectivity(index, otherNode, part);
        }

        public override string ToString() => type + " node " + index;
    }
}
