using System;
using System.Collections.Generic;

namespace RWDE.Sim
{
    /// <summary>
    /// A way between a desktop room and the offscreen world: a run of open tiles along the room's
    /// left or right edge (a screen-edge exit, standing in for the game's room exits and side exits)
    /// or along its top edge (the sky, the game's sky exits). Exits are read off the room's current
    /// tiles, so terrain changes move them.
    /// </summary>
    public sealed class RoomExit
    {
        public enum Edge
        {
            Left,
            Right,
            Top
        }

        private readonly IntVector2[] tiles;

        private RoomExit(Room room, Edge edge, IntVector2[] tiles)
        {
            Room = room;
            Side = edge;
            this.tiles = tiles;
        }

        public Room Room { get; }
        public Edge Side { get; }

        /// <summary>The open edge tiles, in order along the edge.</summary>
        public IReadOnlyList<IntVector2> Tiles => tiles;

        /// <summary>The game node type this exit stands in for: <see cref="AbstractRoomNode.Type.SkyExit"/> at the top, otherwise <see cref="AbstractRoomNode.Type.Exit"/>.</summary>
        public AbstractRoomNode.Type NodeType => Side == Edge.Top ? AbstractRoomNode.Type.SkyExit : AbstractRoomNode.Type.Exit;

        /// <summary>The direction pointing into the room.</summary>
        public IntVector2 Inward
        {
            get
            {
                switch (Side)
                {
                    case Edge.Left: return new IntVector2(1, 0);
                    case Edge.Right: return new IntVector2(-1, 0);
                    default: return new IntVector2(0, -1);
                }
            }
        }

        /// <summary>
        /// Whether creatures of <paramref name="template"/> travel through this kind of exit: the sky
        /// for templates that map sky exits, the screen edges for templates that map room exits or
        /// side exits (<c>CreatureTemplate.mappedNodeTypes</c>).
        /// </summary>
        public bool UsableBy(CreatureTemplate template)
        {
            if (Side == Edge.Top) return template.mappedNodeTypes[(int)AbstractRoomNode.Type.SkyExit];
            return template.mappedNodeTypes[(int)AbstractRoomNode.Type.Exit]
                   || template.mappedNodeTypes[(int)AbstractRoomNode.Type.SideExit];
        }

        /// <summary>The exit's tiles that <paramref name="template"/> may be on, per the room's AI map.</summary>
        public List<IntVector2> EntryTiles(CreatureTemplate template)
        {
            var result = new List<IntVector2>();
            if (!UsableBy(template)) return result;
            foreach (var t in tiles)
                if (Room.aimap.TileAccessibleToCreature(t, template))
                    result.Add(t);
            return result;
        }

        public bool Contains(IntVector2 tile) => Array.IndexOf(tiles, tile) >= 0;

        public override string ToString() => Side + " exit " + tiles[0] + ".." + tiles[tiles.Length - 1];

        /// <summary>Every exit of <paramref name="room"/>: left edge bottom to top, right edge bottom to top, then the top edge left to right.</summary>
        public static List<RoomExit> FindExits(Room room)
        {
            var exits = new List<RoomExit>();
            AddRuns(room, exits, Edge.Left, room.Height, i => new IntVector2(0, i));
            AddRuns(room, exits, Edge.Right, room.Height, i => new IntVector2(room.Width - 1, i));
            AddRuns(room, exits, Edge.Top, room.Width, i => new IntVector2(i, room.Height - 1));
            return exits;
        }

        private static void AddRuns(Room room, List<RoomExit> into, Edge edge, int count, Func<int, IntVector2> at)
        {
            var run = new List<IntVector2>();
            for (int i = 0; i <= count; i++)
            {
                if (i < count && !room.GetTile(at(i)).Solid)
                {
                    run.Add(at(i));
                    continue;
                }
                if (run.Count > 0) into.Add(new RoomExit(room, edge, run.ToArray()));
                run.Clear();
            }
        }
    }
}
