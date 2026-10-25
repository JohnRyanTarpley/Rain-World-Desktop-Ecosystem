namespace RWDE.Sim
{
    /// <summary>
    /// One direction of a shortcut, from an entrance tile to where it leads. Mirrors the game's
    /// <c>ShortcutData</c>: a pipe yields two of these, one per entrance.
    /// </summary>
    public readonly struct ShortcutData
    {
        // ShortcutData.Type. Desktop pipes are always Normal; DeadEnd is what the game returns for a
        // tile with no shortcut. The other types are kept so ported code can name them.
        public enum Type
        {
            Normal,
            RoomExit,
            CreatureHole,
            NPCTransportation,
            RegionTransportation,
            DeadEnd
        }

        public ShortcutData(Type shortCutType, IntVector2 start, IntVector2 dest, int destNode, IntVector2[] path, int pipeId)
        {
            this.shortCutType = shortCutType;
            StartTile = start;
            DestTile = dest;
            this.destNode = destNode;
            this.path = path;
            this.pipeId = pipeId;
        }

        public readonly Type shortCutType;
        public readonly int destNode;
        /// <summary>Tiles from <see cref="StartTile"/> to <see cref="DestTile"/>, both included.</summary>
        public readonly IntVector2[] path;
        /// <summary>The <see cref="Pipe.Id"/> this shortcut belongs to, or -1 for a dead end.</summary>
        public readonly int pipeId;

        public IntVector2 StartTile { get; }
        public IntVector2 DestTile { get; }

        /// <summary>Steps along the path (the game's <c>connection.distance</c>).</summary>
        public int length => path == null || path.Length == 0 ? 0 : path.Length - 1;

        public bool LeadingSomewhere => shortCutType == Type.Normal || shortCutType == Type.RoomExit;
        public bool ToNode => shortCutType == Type.CreatureHole || shortCutType == Type.RoomExit;

        public static ShortcutData DeadEnd(IntVector2 pos) =>
            new ShortcutData(Type.DeadEnd, pos, pos, -1, new IntVector2[0], -1);
    }
}
