using System;
using System.Collections.Generic;

namespace RWDE.Sim
{
    /// <summary>
    /// The rooms of the desktop and how they connect, ported from the parts of the game's <c>World</c>
    /// that pathfinding uses. Each monitor is a room; pipes between monitors join their exit nodes.
    /// </summary>
    /// <remarks>
    /// The game's world file names which room each exit leads to. On the desktop a pipe between two
    /// monitor rooms is reported as one room-exit <see cref="Pipe"/> in each room, both with the same
    /// <see cref="Pipe.RoomLink"/>, and the world pairs them up whenever pipes change. As in the game,
    /// <see cref="AbstractRoom.ExitIndex"/> finds one exit per neighbouring room, so a second pipe
    /// between the same two rooms is entered from the far side through the first one's exit.
    /// </remarks>
    public sealed partial class World
    {
        private readonly AbstractRoom[] abstractRooms;

        /// <param name="seed">Seeds <see cref="random"/>, the world's one source of randomness.</param>
        /// <param name="rooms">The rooms, which get indices 0, 1, ... in this order.</param>
        public World(int seed, params Room[] rooms)
        {
            if (rooms == null || rooms.Length == 0)
                throw new ArgumentException("A world needs at least one room.", nameof(rooms));
            random = new Random(seed);
            abstractRooms = new AbstractRoom[rooms.Length];
            for (int i = 0; i < rooms.Length; i++)
            {
                var abstractRoom = rooms[i].abstractRoom;
                if (abstractRoom.world != null)
                    throw new ArgumentException("Room " + i + " is already in a world.", nameof(rooms));
                abstractRoom.world = this;
                abstractRoom.index = firstRoomIndex + i;
                abstractRooms[i] = abstractRoom;
            }
            ConnectRooms();
        }

        /// <summary>The world's seeded randomness, standing in for the game's <c>UnityEngine.Random</c>.</summary>
        public readonly Random random;

        /// <summary>Shares the pathfinders' work per tick (<c>RainWorldGame.pathfinderResourceDivider</c>).</summary>
        public readonly PathfinderResourceDivider pathfinderResourceDivider = new PathfinderResourceDivider();

        public int firstRoomIndex => 0;
        public int NumberOfRooms => abstractRooms.Length;

        /// <summary>
        /// Goes up whenever anything the node graph depends on changes: any room's nodes or connections, or
        /// its terrain (which sets what walking between its nodes costs). Pathfinders then rebuild their node cells.
        /// </summary>
        public int NodeGraphVersion { get; private set; }

        // Called by a room in this world after diffs changed its terrain but not its pipes.
        internal void NodeCostsChanged() => NodeGraphVersion++;

        public AbstractRoom GetAbstractRoom(int room) =>
            room >= firstRoomIndex && room < firstRoomIndex + abstractRooms.Length ? abstractRooms[room - firstRoomIndex] : null;

        public AbstractRoom GetAbstractRoom(WorldCoordinate coord) => GetAbstractRoom(coord.room);

        public AbstractRoomNode GetNode(WorldCoordinate coord)
        {
            var room = GetAbstractRoom(coord.room);
            return room != null && coord.abstractNode >= 0 && coord.abstractNode < room.nodes.Length ? room.nodes[coord.abstractNode] : null;
        }

        /// <summary>
        /// Call once per tick, before any pathfinder updates (the game does this in <c>RainWorldGame.Update</c>).
        /// Also moves the offscreen population (see <c>World.Population.cs</c>).
        /// </summary>
        public void Update()
        {
            pathfinderResourceDivider.Update();
            UpdatePopulation();
        }

        /// <summary>Both rooms' exit shortcut lengths added up (<c>World.TotalShortCutLengthBetweenTwoConnectedRooms</c>).</summary>
        public int TotalShortCutLengthBetweenTwoConnectedRooms(int room1, int room2)
        {
            var a = GetAbstractRoom(room1);
            var b = GetAbstractRoom(room2);
            if (a != null && b != null && a.ExitIndex(b.index) != -1 && b.ExitIndex(a.index) != -1)
                return a.nodes[a.ExitIndex(b.index)].shortCutLength + b.nodes[b.ExitIndex(a.index)].shortCutLength;
            return -1;
        }

        // Points each exit node at the room holding the other half of its pipe.
        internal void ConnectRooms()
        {
            var linkRooms = new Dictionary<int, List<int>>();
            foreach (var room in abstractRooms)
                for (int node = 0; node < room.nodes.Length; node++)
                {
                    int link = room.realizedRoom.RoomLinkOfNode(node);
                    if (!linkRooms.TryGetValue(link, out var list)) linkRooms[link] = list = new List<int>();
                    list.Add(room.index);
                }

            foreach (var room in abstractRooms)
            {
                var connections = AbstractRoom.Unconnected(room.nodes.Length);
                for (int node = 0; node < connections.Length; node++)
                    foreach (int other in linkRooms[room.realizedRoom.RoomLinkOfNode(node)])
                        if (other != room.index)
                        {
                            connections[node] = other;
                            break;
                        }
                room.connections = connections;
            }
            NodeGraphVersion++;
        }
    }
}
