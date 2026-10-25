using System.Collections.Generic;

namespace RWDE.Sim
{
    /// <summary>
    /// Works out a <see cref="CreatureSpecificAImap"/>, ported from the creature part of the game's
    /// <c>AIdataPreprocessor</c>: a Dijkstra map back from each node's entrance (recording each tile's
    /// step count to the node and, where it passes another node's entrance, the node connectivity), then
    /// the accessibility map.
    /// </summary>
    /// <remarks>
    /// The game runs this offline, a step per frame, for every pre-baked creature at once, and bakes the
    /// result. The desktop runs it to completion for one creature when <see cref="AImap.CreatureSpecificAImap"/>
    /// first asks. The visibility mapper is not ported (<see cref="AItile.visibility"/> stays 0); room
    /// border exits, bat hives and garbage holes have no desktop nodes yet, so only exits are mapped.
    /// </remarks>
    internal static class AIdataPreprocessor
    {
        public static CreatureSpecificAImap Build(AImap aiMap, CreatureTemplate crit)
        {
            var map = new CreatureSpecificAImap(aiMap, crit);
            // The game's NextCreature skips creatures with no relevant nodes, leaving their maps blank.
            if (map.numberOfNodes == 0)
                return map;

            var shortCutsInNodeOrder = ShortcutsInNodeOrder(aiMap.room);
            for (int n = 0; n < map.numberOfNodes; n++)
                new NodeMapper(aiMap, crit, n, shortCutsInNodeOrder, map).Run();
            new AccessibilityDijkstraMapper(aiMap, crit, map).Run();
            return map;
        }

        // ConnectivityMapper's constructor: the shortcuts leading to nodes, in node order.
        private static List<ShortcutData> ShortcutsInNodeOrder(Room room)
        {
            var list = new List<ShortcutData>();
            foreach (var sc in room.shortcuts)
                if (sc.shortCutType == ShortcutData.Type.RoomExit || sc.shortCutType == ShortcutData.Type.CreatureHole
                    || sc.shortCutType == ShortcutData.Type.RegionTransportation)
                    list.Add(sc);
            // OrderBy is stable, so List.Sort (unstable) won't do.
            var ordered = new List<ShortcutData>(list.Count);
            foreach (var sc in System.Linq.Enumerable.OrderBy(list, s => s.destNode))
                ordered.Add(sc);
            return ordered;
        }

        private sealed class Cell
        {
            public Cell(IntVector2 pos, int generation)
            {
                this.pos = pos;
                this.generation = generation;
            }

            public readonly IntVector2 pos;
            public readonly int generation;
        }

        // AIdataPreprocessor.NodeMapper (with ConnectivityMapper.TileDiscovered).
        private sealed class NodeMapper
        {
            private readonly AImap aiMap;
            private readonly CreatureTemplate crit;
            private readonly int currentNodeForCreature;
            private readonly int currentNodeCommon;
            private readonly List<ShortcutData> shortCutsInNodeOrder;
            private readonly CreatureSpecificAImap map;
            private readonly Cell[,] cellGrid;
            private readonly MinHeap<PathCost, (IntVector2 parent, IntVector2 tile)> checkNext =
                new MinHeap<PathCost, (IntVector2, IntVector2)>();

            public NodeMapper(AImap aiMap, CreatureTemplate crit, int currentNodeForCreature, List<ShortcutData> shortCutsInNodeOrder, CreatureSpecificAImap map)
            {
                this.aiMap = aiMap;
                this.crit = crit;
                this.currentNodeForCreature = currentNodeForCreature;
                this.shortCutsInNodeOrder = shortCutsInNodeOrder;
                this.map = map;
                currentNodeCommon = aiMap.room.abstractRoom.CreatureSpecificToCommonNodeIndex(currentNodeForCreature, crit);
                cellGrid = new Cell[aiMap.width, aiMap.height];
                // Desktop nodes are all exits, so a node's tiles are its shortcut's entrance.
                var entrance = aiMap.room.ShortcutLeadingToNode(currentNodeCommon).StartTile;
                if (aiMap.TileAccessibleToCreature(entrance, crit))
                    AddCell(entrance, new PathCost(0f, PathCost.Legality.Allowed), 1);
            }

            public void Run()
            {
                while (checkNext.Count > 0)
                {
                    var next = checkNext.TakeMin();
                    var (parent, pos) = next.Value;
                    if (cellGrid[pos.x, pos.y] != null) continue;
                    var parentCell = cellGrid[parent.x, parent.y];
                    AddCell(pos, next.Key, parentCell == null ? 1 : parentCell.generation + 1);
                }
                // NodeDone: WriteDijkstraMap.
                for (int x = 0; x < aiMap.width; x++)
                    for (int y = 0; y < aiMap.height; y++)
                        if (cellGrid[x, y] != null)
                            map.SetDistanceToExit(x, y, currentNodeForCreature, cellGrid[x, y].generation);
            }

            private void AddCell(IntVector2 pos, PathCost cost, int generation)
            {
                cellGrid[pos.x, pos.y] = new Cell(pos, generation);
                foreach (var incoming in aiMap.getAItile(pos).incomingPaths)
                    if (aiMap.IsConnectionAllowedForCreature(incoming, crit) && cellGrid[incoming.StartTile.x, incoming.StartTile.y] == null)
                        checkNext.Add(cost + aiMap.ConnectionCostForCreature(incoming, crit), (pos, incoming.StartTile));
                TileDiscovered(pos, cost, generation);
            }

            // ConnectivityMapper.TileDiscovered: walking from node i's entrance to this node costs `cost`.
            private void TileDiscovered(IntVector2 tile, PathCost cost, int generation)
            {
                int b = currentNodeCommon;
                for (int i = 0; i < shortCutsInNodeOrder.Count; i++)
                {
                    var sc = shortCutsInNodeOrder[i];
                    if (sc.StartTile != tile) continue;
                    if (i != b && cost.Allowed
                        && ((crit.mappedNodeTypes[(int)AbstractRoomNode.Type.Exit] && sc.shortCutType == ShortcutData.Type.RoomExit)
                            || (crit.mappedNodeTypes[(int)AbstractRoomNode.Type.Den] && sc.shortCutType == ShortcutData.Type.CreatureHole)
                            || (crit.mappedNodeTypes[(int)AbstractRoomNode.Type.RegionTransportation] && sc.shortCutType == ShortcutData.Type.RegionTransportation)))
                        map.SetConnectivity(i, b, (int)cost.resistance, generation);
                    break;
                }
            }
        }

        // AIdataPreprocessor.AccessibilityDijkstraMapper.
        private sealed class AccessibilityDijkstraMapper
        {
            private readonly AImap aiMap;
            private readonly CreatureTemplate crit;
            private readonly CreatureSpecificAImap map;
            private readonly Cell[,] cellGrid;
            private readonly MinHeap<PathCost, (IntVector2 parent, IntVector2 tile)> checkNext =
                new MinHeap<PathCost, (IntVector2, IntVector2)>();
            private int gen;

            public AccessibilityDijkstraMapper(AImap aiMap, CreatureTemplate crit, CreatureSpecificAImap map)
            {
                this.aiMap = aiMap;
                this.crit = crit;
                this.map = map;
                cellGrid = new Cell[aiMap.width, aiMap.height];
                var middle = new IntVector2(aiMap.room.TileWidth / 2, aiMap.room.TileHeight / 2);
                float num = new IntVector2(0, 0).FloatDist(middle);
                for (int x = 0; x < aiMap.room.TileWidth; x++)
                    for (int y = 0; y < aiMap.room.TileHeight; y++)
                        if (AnyExitReachableFromTile(x, y))
                            AddCell(new IntVector2(x, y), new PathCost(new IntVector2(x, y).FloatDist(middle) / num, PathCost.Legality.Allowed), 0);
            }

            // AImap.AnyExitReachableFromTile, against the map being built.
            private bool AnyExitReachableFromTile(int x, int y)
            {
                for (int i = 0; i < map.numberOfNodes; i++)
                    if (map.GetDistanceToExit(x, y, i) > 0)
                        return true;
                return false;
            }

            public void Run()
            {
                while (checkNext.Count > 0)
                {
                    var next = checkNext.TakeMin();
                    var tile = next.Value.tile;
                    if (cellGrid[tile.x, tile.y] != null) continue;
                    gen++;
                    AddCell(tile, next.Key, gen);
                }
                // WriteAccessibilityMap. When nothing past the seed tiles is reached, gen stays 0 and the
                // game writes NaN; so does this.
                for (int x = 0; x < aiMap.width; x++)
                    for (int y = 0; y < aiMap.height; y++)
                        if (cellGrid[x, y] != null)
                            map.SetAccessibility(x, y, 1f - (float)cellGrid[x, y].generation / gen);
            }

            // Unlike the node mapper, this one follows every outgoing connection, allowed or not.
            private void AddCell(IntVector2 pos, PathCost cost, int generation)
            {
                cellGrid[pos.x, pos.y] = new Cell(pos, generation);
                foreach (var outgoing in aiMap.getAItile(pos).outgoingPaths)
                    if (cellGrid[outgoing.DestTile.x, outgoing.DestTile.y] == null)
                        checkNext.Add(cost + aiMap.ConnectionCostForCreature(outgoing, crit), (pos, outgoing.DestTile));
            }
        }
    }
}
