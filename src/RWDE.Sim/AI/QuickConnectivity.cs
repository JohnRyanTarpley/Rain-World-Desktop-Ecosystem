using System;
using System.Collections.Generic;

namespace RWDE.Sim
{
    /// <summary>
    /// Cheap reachability checks inside one room, ported from the parts of the game's
    /// <c>QuickConnectivity</c> the pathfinder uses.
    /// </summary>
    public static class QuickConnectivity
    {
        // Scratch lists, shared as in the game; the sim runs on one thread.
        private static readonly HashSet<int> alreadyChecked = new HashSet<int>();
        private static readonly List<IntVector2> checkNext = new List<IntVector2>();

        /// <summary>
        /// Searches back from <paramref name="goal"/> for <paramref name="start"/> for at most
        /// <paramref name="maxGenerations"/> tiles, using only connections and tiles no worse than the start
        /// tile's legality. Returns the generation it was found in, or -1.
        /// </summary>
        public static int Check(Room room, CreatureTemplate creatureType, IntVector2 start, IntVector2 goal, int maxGenerations)
        {
            if (start == goal)
                return 0;
            start.x = Custom.IntClamp(start.x, 0, room.TileWidth - 1);
            start.y = Custom.IntClamp(start.y, 0, room.TileHeight - 1);
            goal.x = Custom.IntClamp(goal.x, 0, room.TileWidth - 1);
            goal.y = Custom.IntClamp(goal.y, 0, room.TileHeight - 1);
            var legality = room.aimap.TileCostForCreature(start, creatureType).legality;
            alreadyChecked.Clear();
            checkNext.Clear();
            checkNext.Add(goal);
            for (int i = 0; i < maxGenerations; i++)
            {
                if (checkNext.Count < 1)
                    return -1;
                var tile = checkNext[0];
                checkNext.RemoveAt(0);
                var incoming = room.aimap.map[tile.x, tile.y].incomingPaths;
                for (int j = 0; j < incoming.Count; j++)
                {
                    var connection = incoming[j];
                    var s = connection.StartTile;
                    int key = s.x * room.TileHeight + s.y;
                    if (alreadyChecked.Contains(key) || creatureType.ConnectionResistance(connection.type).legality > legality
                        || room.aimap.TileCostForCreature(s, creatureType).legality > legality)
                        continue;
                    if (s == start)
                        return i;
                    alreadyChecked.Add(key);
                    // The game files the new tile by the distance of the tile it was reached from.
                    int dist = Manhattan(tile, start);
                    if (checkNext.Count < 1 || Manhattan(checkNext[checkNext.Count - 1], start) < dist)
                    {
                        checkNext.Add(s);
                        continue;
                    }
                    int k = 0;
                    while (k < checkNext.Count && Manhattan(checkNext[k], start) < dist)
                        k++;
                    checkNext.Insert(k, s);
                }
            }
            return -1;
        }

        private static int Manhattan(IntVector2 a, IntVector2 b) => Math.Abs(a.x - b.x) + Math.Abs(a.y - b.y);

        /// <summary>
        /// Gives a tile coordinate the node a creature should head for to get there: the nearest node
        /// by exit distance when one is reachable, else the node with the best quick connectivity both
        /// ways (<c>QuickConnectivity.DefineNodeOfLocalCoordinate</c>).
        /// </summary>
        public static WorldCoordinate DefineNodeOfLocalCoordinate(WorldCoordinate coord, World world, CreatureTemplate creatureType)
        {
            if (coord.NodeDefined)
                return coord;
            var abstractRoom = world.GetAbstractRoom(coord);
            int specific = world.random.Next(0, abstractRoom.NodesRelevantToCreature(creatureType));
            specific = abstractRoom.CreatureSpecificToCommonNodeIndex(specific, creatureType);
            if (specific < 0)
                specific = world.random.Next(0, abstractRoom.nodes.Length);
            var realizedRoom = abstractRoom.realizedRoom;
            if (realizedRoom == null || !realizedRoom.readyForAI)
                return new WorldCoordinate(coord.room, coord.x, coord.y, specific);

            if (TileOrNeighboursReachableFromAnyExit(coord, world, creatureType))
            {
                var pos = coord.Tile;
                for (int i = 0; i < 5; i++)
                    if (realizedRoom.aimap.AnyExitReachableFromTile(coord.Tile + Custom.fourDirectionsAndZero[i], creatureType))
                    {
                        pos = coord.Tile + Custom.fourDirectionsAndZero[i];
                        break;
                    }
                int best = int.MaxValue;
                int abstractNode = specific;
                for (int j = 0; j < realizedRoom.abstractRoom.NodesRelevantToCreature(creatureType); j++)
                {
                    int d = realizedRoom.aimap.ExitDistanceForCreature(pos, j, creatureType);
                    if (d > 0 && d < best)
                    {
                        best = d;
                        abstractNode = realizedRoom.abstractRoom.CreatureSpecificToCommonNodeIndex(j, creatureType);
                    }
                }
                return new WorldCoordinate(coord.room, coord.x, coord.y, abstractNode);
            }

            float bestDist = realizedRoom.TileWidth + realizedRoom.TileHeight;
            int bestScore = 400;
            int closest = specific;
            for (int k = 0; k < realizedRoom.abstractRoom.nodes.Length; k++)
            {
                var nodePos = realizedRoom.LocalCoordinateOfNode(k);
                float dist = Vector2.Distance(IntVector2.ToVector2(coord.Tile), IntVector2.ToVector2(nodePos.Tile));
                int there = Check(realizedRoom, creatureType, nodePos.Tile, coord.Tile, bestScore / 2);
                if (there == -1) there = 200;
                int back = Check(realizedRoom, creatureType, coord.Tile, nodePos.Tile, bestScore / 2);
                if (back == -1) back = 200;
                int score = there + back;
                if (score < bestScore || (score == bestScore && dist < bestDist))
                {
                    bestDist = dist;
                    bestScore = score;
                    closest = k;
                }
            }
            return new WorldCoordinate(coord.room, coord.x, coord.y, closest);
        }

        private static bool TileOrNeighboursReachableFromAnyExit(WorldCoordinate coord, World world, CreatureTemplate creatureType)
        {
            var realizedRoom = world.GetAbstractRoom(coord).realizedRoom;
            if (realizedRoom == null || !realizedRoom.readyForAI)
                return false;
            for (int i = 0; i < 5; i++)
                if (realizedRoom.aimap.AnyExitReachableFromTile(coord.Tile + Custom.fourDirectionsAndZero[i], creatureType))
                    return true;
            return false;
        }
    }
}
