using System;

namespace RWDE.Sim
{
    /// <summary>
    /// A room as the world sees it, ported from the parts of the game's <c>AbstractRoom</c> that
    /// pathfinding uses: its index, its nodes and which room each exit node leads to. Every desktop room
    /// (one per monitor) is always realized, so <see cref="realizedRoom"/> is never null.
    /// </summary>
    /// <remarks>
    /// The game reads a room's nodes from its level files and its <see cref="connections"/> from the
    /// world file. On the desktop both follow the pipes: each room-exit pipe makes an Exit node, and the
    /// <see cref="World"/> joins exit nodes whose pipes share a <see cref="Pipe.RoomLink"/>.
    /// </remarks>
    public sealed class AbstractRoom
    {
        internal AbstractRoom(Room realizedRoom)
        {
            this.realizedRoom = realizedRoom;
            RefreshNodes();
        }

        public Room realizedRoom { get; }

        /// <summary>The world this room is in, or null while it stands alone.</summary>
        public World world { get; internal set; }

        /// <summary>The room's index in its world; 0 while it stands alone.</summary>
        public int index { get; internal set; }

        public AbstractRoomNode[] nodes { get; private set; } = new AbstractRoomNode[0];

        /// <summary>For each exit node, the index of the room it leads to, or -1 when it leads nowhere.</summary>
        public int[] connections { get; internal set; } = new int[0];

        /// <summary>The exit node that leads to <paramref name="targetRoom"/>, or -1 (<c>AbstractRoom.ExitIndex</c>).</summary>
        public int ExitIndex(int targetRoom) => Array.IndexOf(connections, targetRoom);

        // Called by the room after diffs add or remove pipes.
        internal void ExitsChanged()
        {
            RefreshNodes();
            if (world != null) world.ConnectRooms();
            else connections = Unconnected(nodes.Length);
        }

        // AIdataPreprocessor.ConnectivityMapper.ReturnNodes, for a room whose nodes are all exits.
        private void RefreshNodes()
        {
            var room = realizedRoom;
            var fresh = new AbstractRoomNode[room.exitAndDenIndex.Length];
            for (int i = 0; i < fresh.Length; i++)
            {
                var shortcut = room.ShortcutLeadingToNode(i);
                fresh[i] = new AbstractRoomNode(this, i, AbstractRoomNode.Type.Exit, shortcut.length,
                    room.GetTile(shortcut.StartTile).AnyWater, 1);
            }
            nodes = fresh;
        }

        internal static int[] Unconnected(int n)
        {
            var c = new int[n];
            for (int i = 0; i < n; i++) c[i] = -1;
            return c;
        }

        // ---- Creature-specific node indices (AbstractRoom.NodesRelevantToCreature and friends) ----

        /// <summary>How many of the room's nodes are of a type <paramref name="crit"/> maps.</summary>
        public int NodesRelevantToCreature(CreatureTemplate crit)
        {
            int n = 0;
            for (int i = 0; i < nodes.Length; i++)
                if (crit.mappedNodeTypes[(int)nodes[i].type])
                    n++;
            return n;
        }

        /// <summary>The room node index of <paramref name="crit"/>'s <paramref name="specific"/>-th relevant node, or -1.</summary>
        public int CreatureSpecificToCommonNodeIndex(int specific, CreatureTemplate crit)
        {
            int n = 0;
            for (int i = 0; i < nodes.Length; i++)
            {
                if (!crit.mappedNodeTypes[(int)nodes[i].type]) continue;
                if (specific == n) return i;
                n++;
            }
            return -1;
        }

        /// <summary>Which of <paramref name="crit"/>'s relevant nodes room node <paramref name="common"/> is, or -1.</summary>
        public int CommonToCreatureSpecificNodeIndex(int common, CreatureTemplate crit)
        {
            int n = 0;
            for (int i = 0; i < nodes.Length; i++)
            {
                if (!crit.mappedNodeTypes[(int)nodes[i].type]) continue;
                if (i == common) return n;
                n++;
            }
            return -1;
        }

        public override string ToString() => "abstract room " + index;
    }
}
