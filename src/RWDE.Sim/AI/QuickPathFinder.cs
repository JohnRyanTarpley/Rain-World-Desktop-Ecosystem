using System;
using System.Collections.Generic;

namespace RWDE.Sim
{
    /// <summary>
    /// A one-off A* search for a tile path inside one room, ported from the game's <c>QuickPathFinder</c>.
    /// It searches back from the goal through incoming connections, then walks the found path forward.
    /// Call <see cref="Update"/> until <see cref="status"/> is not 0 (1 found, -1 no path).
    /// </summary>
    public sealed class QuickPathFinder
    {
        private sealed class PathNode
        {
            public PathNode(int x, int y, PathNode oneStepCloserToGoal, PathCost stepsToGoal, PathCost heuresticValue)
            {
                this.oneStepCloserToGoal = oneStepCloserToGoal;
                this.x = x;
                this.y = y;
                this.stepsToGoal = stepsToGoal;
                this.heuresticValue = heuresticValue;
            }

            public readonly PathNode oneStepCloserToGoal;
            public PathNode nextInHeap;
            public readonly int x;
            public readonly int y;
            public readonly PathCost stepsToGoal;
            public readonly PathCost heuresticValue;
            public IntVector2 Pos => new IntVector2(x, y);
        }

        // QuickPathFinder.MinHeap: a sorted linked list, kept for its tie order.
        private sealed class MinHeap
        {
            private PathNode head;

            public MinHeap(PathNode hd)
            {
                head = hd;
            }

            public bool Empty() => head == null;

            public void Add(PathNode addNode)
            {
                if (head == null)
                {
                    head = addNode;
                    return;
                }
                if (head.nextInHeap == null && addNode.heuresticValue > head.heuresticValue)
                {
                    head.nextInHeap = addNode;
                    return;
                }
                if (head.heuresticValue > addNode.heuresticValue)
                {
                    addNode.nextInHeap = head;
                    head = addNode;
                    return;
                }
                var node = head;
                while (node.nextInHeap != null && node.nextInHeap.heuresticValue < addNode.heuresticValue)
                    node = node.nextInHeap;
                addNode.nextInHeap = node.nextInHeap;
                node.nextInHeap = addNode;
            }

            public void FindAndReplace(PathNode addNode)
            {
                PathNode previous = null;
                var node = head;
                while (node.x != addNode.x || node.y != addNode.y)
                {
                    previous = node;
                    node = node.nextInHeap;
                }
                if (previous != null && addNode.heuresticValue < node.heuresticValue)
                {
                    previous.nextInHeap = node.nextInHeap;
                    Add(addNode);
                }
            }

            public PathNode ExtractFirst()
            {
                var result = head;
                head = head.nextInHeap;
                return result;
            }
        }

        private readonly AImap map;
        private readonly IntVector2 start;
        private readonly IntVector2 goal;
        private readonly CreatureTemplate creatureType;
        private bool startFound;
        private PathNode walker;
        private List<IntVector2> path;
        private PathCost pathCost;
        private readonly int[] checkedNodes;
        private readonly MinHeap nodeHeap;

        /// <summary>0 while searching, 1 when a path is found, -1 when there is none.</summary>
        public int status;

        public QuickPathFinder(IntVector2 start, IntVector2 goal, AImap map, CreatureTemplate creatureType)
        {
            this.start = new IntVector2(Custom.IntClamp(start.x, 0, map.width - 1), Custom.IntClamp(start.y, 0, map.height - 1));
            this.goal = new IntVector2(Custom.IntClamp(goal.x, 0, map.width - 1), Custom.IntClamp(goal.y, 0, map.height - 1));
            this.map = map;
            this.creatureType = creatureType;
            pathCost = new PathCost(0f, PathCost.Legality.Allowed);
            if (this.start == this.goal)
            {
                status = 1;
                path = new List<IntVector2> { this.start };
            }
            else
            {
                checkedNodes = new int[map.width * map.height];
                nodeHeap = new MinHeap(new PathNode(this.goal.x, this.goal.y, null, new PathCost(0f, PathCost.Legality.Allowed), new PathCost(0f, PathCost.Legality.Allowed)));
                MarkNodeAsChecked(this.goal);
                startFound = false;
                status = 0;
            }
        }

        public void Update()
        {
            if (status != 0)
                return;
            if (!startFound)
            {
                if (nodeHeap.Empty())
                {
                    status = -1;
                    return;
                }
                var node = nodeHeap.ExtractFirst();
                MarkNodeAsClosed(node.Pos);
                var incoming = map.map[node.x, node.y].incomingPaths;
                for (int i = 0; i < incoming.Count; i++)
                {
                    var connection = incoming[i];
                    var s = connection.StartTile;
                    if (s.x < 0 || s.x >= map.width || s.y < 0 || s.y >= map.height
                        || !map.IsConnectionAllowedForCreature(connection, creatureType) || IsNodeClosed(s))
                        continue;
                    if (s.x == start.x && s.y == start.y)
                    {
                        startFound = true;
                        path = new List<IntVector2>();
                        walker = new PathNode(s.x, s.y, node, new PathCost(node.stepsToGoal.resistance + 1f, node.stepsToGoal.legality), new PathCost(0f, PathCost.Legality.Allowed));
                        continue;
                    }
                    var stepsToGoal = node.stepsToGoal;
                    stepsToGoal += creatureType.ConnectionResistance(connection.type) * connection.distance;
                    stepsToGoal += map.TileCostForCreature(s, creatureType);
                    var added = new PathNode(s.x, s.y, node, stepsToGoal, AddHeuresticForTile(s, stepsToGoal));
                    if (IsNodePreviouslyChecked(s))
                        nodeHeap.FindAndReplace(added);
                    else
                        nodeHeap.Add(added);
                    MarkNodeAsChecked(s);
                }
                return;
            }

            path.Add(walker.Pos);
            pathCost += map.TileCostForCreature(walker.x, walker.y, creatureType);
            var step = new PathCost(0f, PathCost.Legality.Unallowed);
            var outgoing = map.map[walker.x, walker.y].outgoingPaths;
            for (int j = 0; j < outgoing.Count; j++)
            {
                var resistance = creatureType.ConnectionResistance(outgoing[j].type);
                if (outgoing[j].DestTile.x == walker.oneStepCloserToGoal.x && outgoing[j].DestTile.y == walker.oneStepCloserToGoal.y
                    && resistance.Allowed && resistance < step)
                    step = resistance;
            }
            pathCost += step;
            walker = walker.oneStepCloserToGoal;
            if (walker.x == goal.x && walker.y == goal.y)
            {
                status = 1;
                path.Add(walker.Pos);
            }
        }

        private PathCost AddHeuresticForTile(IntVector2 tl, PathCost distanceToGoal) =>
            new PathCost(distanceToGoal.resistance + Math.Abs(tl.x - start.x) + Math.Abs(tl.y - start.y), distanceToGoal.legality);

        private bool IsNodePreviouslyChecked(IntVector2 tl)
        {
            if (tl.x > -1 && tl.y > -1 && tl.x + tl.y * map.width < checkedNodes.Length)
                return checkedNodes[tl.x + tl.y * map.width] > 0;
            return true;
        }

        private bool IsNodeClosed(IntVector2 tl)
        {
            if (tl.x > -1 && tl.y > -1 && tl.x + tl.y * map.width < checkedNodes.Length)
                return checkedNodes[tl.x + tl.y * map.width] == 2;
            return true;
        }

        private void MarkNodeAsChecked(IntVector2 tl)
        {
            if (tl.x > -1 && tl.y > -1 && tl.x + tl.y * map.width < checkedNodes.Length && checkedNodes[tl.x + tl.y * map.width] == 0)
                checkedNodes[tl.x + tl.y * map.width] = 1;
        }

        private void MarkNodeAsClosed(IntVector2 tl)
        {
            if (tl.x > -1 && tl.y > -1 && tl.x + tl.y * map.width < checkedNodes.Length)
                checkedNodes[tl.x + tl.y * map.width] = 2;
        }

        /// <summary>The path, start to goal, once <see cref="status"/> is 1; otherwise null.</summary>
        public QuickPath ReturnPath() => status == 1 ? new QuickPath(creatureType, pathCost, path.ToArray()) : null;
    }

    /// <summary>A tile path found by <see cref="QuickPathFinder"/>. Mirrors the game's <c>QuickPath</c>.</summary>
    public sealed class QuickPath
    {
        public QuickPath(CreatureTemplate calculatedFor, PathCost cost, IntVector2[] tiles)
        {
            this.calculatedFor = calculatedFor;
            this.cost = cost;
            this.tiles = tiles;
        }

        public readonly CreatureTemplate calculatedFor;
        public readonly PathCost cost;
        public readonly IntVector2[] tiles;
        public int Length => tiles.Length;
    }
}
