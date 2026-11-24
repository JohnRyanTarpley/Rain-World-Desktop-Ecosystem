using System;
using System.Collections.Generic;

namespace RWDE.Sim
{
    /// <summary>
    /// What a creature is afraid of and where to run, ported from the game's <c>ThreatTracker</c>.
    /// Each feared creature leaves a threat point at its best-guess position; the threat of a tile falls
    /// off with distance from those points, and <see cref="FleeTo"/> samples tiles nearby for the least
    /// threatening one to run to (or an exit out of the room).
    /// </summary>
    /// <remarks>
    /// Desktop rooms only have Exit nodes, so the game's escape into the offscreen den through sky,
    /// side, sea or region-transport nodes never applies. The game measures open-air exit distances
    /// with the Batfly template; a Batfly template is built once here for the same purpose.
    /// </remarks>
    public class ThreatTracker : AIModule
    {
        /// <summary>A spot to keep away from (<c>ThreatTracker.ThreatPoint</c>).</summary>
        public class ThreatPoint
        {
            public ThreatPoint(CreatureTemplate crit, WorldCoordinate pos, float severity)
            {
                this.crit = crit;
                this.pos = pos;
                this.severity = severity;
            }

            public CreatureTemplate crit;
            public WorldCoordinate pos;
            public float severity;
        }

        /// <summary>A feared creature and its threat point (<c>ThreatTracker.ThreatCreature</c>).</summary>
        public class ThreatCreature
        {
            public ThreatCreature(ThreatTracker owner, Tracker.CreatureRepresentation creature)
            {
                this.owner = owner;
                this.creature = creature;
                severity = owner.AI.DynamicRelationship(creature).intensity;
                threatPoint = owner.AddThreatPoint(creature.representedCreature.creatureTemplate, creature.BestGuessForPosition(), severity);
            }

            private readonly ThreatTracker owner;
            public Tracker.CreatureRepresentation creature;
            private float severity;
            private readonly ThreatPoint threatPoint;

            public float CurrentThreat => creature.deleteMeNextFrame
                ? 0f
                : Mathf.InverseLerp(0.05f, 0.5f, Mathf.Pow(creature.EstimatedChanceOfFinding, 0.75f) * severity);

            public void Update()
            {
                severity = owner.AI.DynamicRelationship(creature).intensity;
                creature.forgetCounter = 0;
                var guess = creature.BestGuessForPosition();
                if (guess.room == owner.AI.creature.pos.room)
                {
                    threatPoint.severity = CurrentThreat;
                    threatPoint.pos = guess;
                    return;
                }
                // In a neighbouring room: half the threat, at the exit leading there.
                threatPoint.severity = 0f;
                int exit = owner.AI.creature.Room.ExitIndex(guess.room);
                if (exit > -1)
                {
                    threatPoint.severity = CurrentThreat * 0.5f;
                    var start = owner.AI.creature.Room.realizedRoom.ShortcutLeadingToNode(exit).StartTile;
                    threatPoint.pos = owner.AI.creature.Room.realizedRoom.GetWorldCoordinate(start);
                }
            }

            public void Destroy(bool stopTracking)
            {
                owner.AI.relationshipTracker?.ModuleHasAbandonedCreature(creature, owner);
                owner.threatPoints.Remove(threatPoint);
                owner.threatCreatures.Remove(this);
            }
        }

        public ThreatTracker(ArtificialIntelligence AI, int maxRememberedCreatures) : base(AI)
        {
            this.maxRememberedCreatures = maxRememberedCreatures;
            accessibilityConsideration = 5f;
        }

        private AImap aiMap;
        private readonly List<ThreatPoint> threatPoints = new List<ThreatPoint>();
        private readonly List<ThreatCreature> threatCreatures = new List<ThreatCreature>();
        public Tracker.CreatureRepresentation mostThreateningCreature;
        public WorldCoordinate savedFleeDest;
        public WorldCoordinate testFleeDest;
        private int antiFlickerCounter;
        private int resetCounter;
        private readonly int maxRememberedCreatures;
        public float accessibilityConsideration;
        private float currentThreat;
        private List<IntVector2> scratchPath;

        private static CreatureTemplate flyTemplate;

        // StaticWorld.GetCreatureTemplate(CreatureTemplate.Type.Fly)
        private static CreatureTemplate FlyTemplate =>
            flyTemplate ??= StaticWorld.Init(StaticWorld.Dlc.BaseOnly).GetCreatureTemplate(CreatureTemplate.Type.Fly);

        public int TotalTrackedThreats => threatPoints.Count;
        public int TotalTrackedThreatCreatures => threatPoints.Count;
        public float Panic => 1f - 1f / (currentThreat + 1f);

        public override float Utility() => Mathf.Clamp(Mathf.Lerp(-0.01f, 1.01f, currentThreat), 0f, 1f);

        public override void NewRoom(Room room)
        {
            aiMap = room.aimap;
            savedFleeDest = new WorldCoordinate(room.abstractRoom.index, -1, -1, AI.Random.Range(0, room.abstractRoom.nodes.Length));
        }

        public override void Update()
        {
            currentThreat = ThreatOfArea(AI.creature.pos, false);
            if (currentThreat <= 0f)
                resetCounter++;
            else
                resetCounter = 0;
            if (antiFlickerCounter > 0)
                antiFlickerCounter--;
            if (resetCounter < 200)
            {
                float highest = 0f;
                mostThreateningCreature = null;
                for (int i = threatCreatures.Count - 1; i >= 0; i--)
                {
                    threatCreatures[i].Update();
                    if (threatCreatures[i].creature.deleteMeNextFrame)
                    {
                        threatCreatures[i].Destroy(false);
                    }
                    else if (threatCreatures[i].CurrentThreat > highest)
                    {
                        highest = threatCreatures[i].CurrentThreat;
                        mostThreateningCreature = threatCreatures[i].creature;
                    }
                }
                if (mostThreateningCreature != null && mostThreateningCreature.BestGuessForPosition().room == AI.creature.pos.room)
                    currentThreat = Mathf.Pow(currentThreat, 1f / (1f + highest * 3f));
            }
            else if (threatCreatures.Count > 0)
            {
                // Nothing threatening for 200 ticks: forget every threat.
                for (int i = threatCreatures.Count - 1; i >= 0; i--)
                    threatCreatures[i].Destroy(true);
            }
        }

        public WorldCoordinate FleeTo(WorldCoordinate occupyTile, int reevalutaions, int maximumDistance, bool considerLeavingRoom) =>
            FleeTo(occupyTile, reevalutaions, maximumDistance, considerLeavingRoom, false);

        /// <summary>
        /// Where to run from <paramref name="occupyTile"/>: the least threatening of the tiles sampled so far
        /// within <paramref name="maximumDistance"/>, kept for a while to avoid flicker, or, when
        /// <paramref name="considerLeavingRoom"/>, the far side of a near exit that no threat is closer to.
        /// </summary>
        public WorldCoordinate FleeTo(WorldCoordinate occupyTile, int reevalutaions, int maximumDistance, bool considerLeavingRoom, bool considerGoingHome)
        {
            reevalutaions = AI.creature.world.pathfinderResourceDivider.RequestPathfinderUpdates(reevalutaions);
            if (!AI.pathFinder.CoordinateViable(occupyTile))
            {
                for (int i = 0; i < 4; i++)
                {
                    if (AI.pathFinder.CoordinateViable(occupyTile + Custom.fourDirections[i]))
                    {
                        occupyTile += Custom.fourDirections[i];
                        break;
                    }
                }
            }
            var room = AI.creature.Room.realizedRoom;
            int tried = 0;
            float bestThreat = EvaluateFlightDestThreat(occupyTile, testFleeDest, maximumDistance);
            for (int j = 0; j < reevalutaions * 5; j++)
            {
                var test = new WorldCoordinate(occupyTile.room,
                    occupyTile.x + AI.Random.Range(0, maximumDistance) * (AI.Random.Value() < 0.5f ? -1 : 1),
                    occupyTile.y + AI.Random.Range(0, maximumDistance) * (AI.Random.Value() < 0.5f ? -1 : 1), -1);
                test.x = Custom.IntClamp(test.x, 0, room.TileWidth - 1);
                test.y = Custom.IntClamp(test.y, 0, room.TileHeight - 1);
                if (testFleeDest.Tile.FloatDist(test.Tile) > 3f && aiMap.WorldCoordinateAccessibleToCreature(test, AI.creature.creatureTemplate))
                {
                    float threat = EvaluateFlightDestThreat(occupyTile, test, maximumDistance);
                    if (threat < bestThreat)
                    {
                        testFleeDest = test;
                        bestThreat = threat;
                    }
                    tried++;
                }
                if (tried >= reevalutaions)
                    break;
            }
            if (antiFlickerCounter < 1 && savedFleeDest != testFleeDest
                && bestThreat < EvaluateFlightDestThreat(occupyTile, savedFleeDest, maximumDistance) - 0.5f * Mathf.InverseLerp(1f, 0.5f, currentThreat))
            {
                savedFleeDest = testFleeDest;
                antiFlickerCounter = 20;
            }
            if (considerLeavingRoom)
            {
                var abstractRoom = AI.creature.Room;
                var template = AI.creature.creatureTemplate;
                int bestNode = -1;
                int bestDist = int.MaxValue;
                int relevant = abstractRoom.NodesRelevantToCreature(template);
                for (int k = 0; k < relevant; k++)
                {
                    int dist = room.aimap.ExitDistanceForCreatureAndCheckNeighbours(occupyTile.Tile, k, template);
                    int node = abstractRoom.CreatureSpecificToCommonNodeIndex(k, template);
                    if (AI.pathFinder != null && AI.timeInRoom < 100 && AI.pathFinder.forbiddenEntrance.abstractNode == node)
                        dist = -1;
                    if (abstractRoom.nodes[node].type == AbstractRoomNode.Type.Exit && abstractRoom.connections[node] > -1
                        && mostThreateningCreature != null && mostThreateningCreature.BestGuessForPosition().room == abstractRoom.connections[node])
                        dist = -1;
                    if (dist > 0)
                    {
                        // A threat closer to the exit (as the bat flies, give or take 10) rules it out.
                        int airDist = room.aimap.ExitDistanceForCreatureAndCheckNeighbours(occupyTile.Tile, k, FlyTemplate);
                        if (airDist > 5)
                        {
                            for (int l = 0; l < threatPoints.Count; l++)
                            {
                                if (threatPoints[l].pos.room != abstractRoom.index)
                                    continue;
                                int threatDist = room.aimap.ExitDistanceForCreatureAndCheckNeighbours(threatPoints[l].pos.Tile, k, FlyTemplate);
                                if (threatDist > 0)
                                {
                                    threatDist += 10;
                                    if (threatDist < airDist)
                                    {
                                        dist = -1;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                    if (dist > 0 && dist < bestDist
                        && ((abstractRoom.nodes[node].type == AbstractRoomNode.Type.Exit && abstractRoom.connections[node] > -1)
                            || (abstractRoom.nodes[node].borderExit && considerGoingHome)))
                    {
                        bestNode = node;
                        bestDist = dist;
                    }
                }
                if (bestNode > -1 && bestDist < maximumDistance && abstractRoom.nodes[bestNode].type == AbstractRoomNode.Type.Exit)
                {
                    // Through the exit, then as far into the next room as the creature can get and back.
                    var next = AI.creature.world.GetAbstractRoom(abstractRoom.connections[bestNode]);
                    int entrance = next.ExitIndex(abstractRoom.index);
                    int farNode = entrance;
                    int farLength = int.MinValue;
                    for (int m = 0; m < next.nodes.Length; m++)
                    {
                        if (ConnectionAndBackPossible(next, entrance, m, template) && ConnectionLength(next, entrance, m, template) > farLength)
                        {
                            farNode = m;
                            farLength = ConnectionLength(next, entrance, m, template);
                        }
                    }
                    return new WorldCoordinate(next.index, -1, -1, farNode);
                }
            }
            return savedFleeDest;
        }

        /// <summary>A random walk of up to <paramref name="length"/> tiles along the creature's connections, never revisiting a tile.</summary>
        public List<IntVector2> GenerateRandomPath(WorldCoordinate occupyTile, int length)
        {
            if (!AI.pathFinder.CoordinateViable(occupyTile))
                return null;
            var room = AI.creature.Room.realizedRoom;
            var template = AI.creature.creatureTemplate;
            var list = new List<IntVector2>();
            var at = occupyTile.Tile;
            for (int i = 0; i < length; i++)
            {
                var next = at;
                float best = 0f;
                foreach (var connection in room.aimap.getAItile(at).outgoingPaths)
                {
                    if (!template.ConnectionResistance(connection.type).Allowed
                        || !template.AccessibilityResistance(room.aimap.getAItile(connection.destinationCoord.Tile).acc).Allowed)
                        continue;
                    float roll = AI.Random.Value();
                    if (!(roll > best))
                        continue;
                    for (int k = 0; k < list.Count && roll > 0f; k++)
                        if (list[k] == connection.DestTile)
                            roll = 0f;
                    if (roll > best)
                    {
                        next = connection.DestTile;
                        best = roll;
                    }
                }
                if (!(best > 0f))
                    break;
                list.Add(at);
                at = next;
            }
            return list;
        }

        private float EvaluateFlightDestThreat(WorldCoordinate occupyTile, WorldCoordinate coord, int maximumDistance)
        {
            if (coord.room != occupyTile.room)
                return float.MaxValue;
            if (Custom.ManhattanDistance(coord, occupyTile) >= maximumDistance * 2)
                return float.MaxValue;
            if (!AI.pathFinder.CoordinateViable(coord))
                return float.MaxValue;
            var room = AI.creature.Room.realizedRoom;
            int count = RayTraceTilesList(occupyTile.x, occupyTile.y, coord.x, coord.y, ref scratchPath);
            bool straight = true;
            for (int i = 0; i < count && straight; i++)
                if (!aiMap.TileAccessibleToCreature(scratchPath[i], AI.creature.creatureTemplate))
                    straight = false;
            if (straight)
                return ThreatOfPath(scratchPath, count);
            count = QuickPath(room, AI.creature.creatureTemplate, occupyTile.Tile, coord.Tile, maximumDistance * 2, 500, true, ref scratchPath);
            return ThreatOfPath(scratchPath, count);
        }

        private float ThreatOfPath(List<IntVector2> path, int pathCount)
        {
            if (path == null || pathCount == 0)
                return float.MaxValue;
            var room = AI.creature.Room.realizedRoom;
            float threat = 0f;
            for (int i = 0; i < pathCount; i++)
            {
                threat += ThreatOfTile(room.GetWorldCoordinate(path[i]), true);
                for (int j = 0; j < threatPoints.Count; j++)
                    if (threatPoints[j].pos.Tile == path[i])
                        threat += 10f * threatPoints[j].severity;
            }
            if (pathCount < 2 && ThreatOfTile(room.GetWorldCoordinate(path[pathCount - 1]), false) < 0.5f)
                threat += 100f;
            if (ThreatOfArea(room.GetWorldCoordinate(path[pathCount - 1]), true) > ThreatOfArea(room.GetWorldCoordinate(path[0]), true))
                threat += 1000f;
            threat /= pathCount;
            return threat + ThreatOfArea(room.GetWorldCoordinate(path[pathCount - 1]), true);
        }

        /// <summary>The exit node that leads furthest from the threats, or -1 when every exit has a threat nearer to it.</summary>
        public int FindMostAttractiveExit()
        {
            if (aiMap == null)
                return -1;
            var abstractRoom = AI.creature.Room;
            var template = AI.creature.creatureTemplate;
            float best = float.MinValue;
            int result = -1;
            for (int i = 0; i < abstractRoom.nodes.Length; i++)
            {
                if (!template.mappedNodeTypes[(int)abstractRoom.nodes[i].type]
                    || (abstractRoom.nodes[i].type == AbstractRoomNode.Type.Exit && abstractRoom.connections[i] <= -1))
                    continue;
                int myDist = aiMap.ExitDistanceForCreature(AI.creature.pos.Tile, abstractRoom.CommonToCreatureSpecificNodeIndex(i, template), template);
                float score = 0f;
                for (int j = 0; j < threatPoints.Count; j++)
                {
                    var tp = threatPoints[j];
                    if (tp.pos.room != AI.creature.pos.room)
                        continue;
                    int threatDist = tp.crit == null
                        ? aiMap.ExitDistanceForCreature(tp.pos.Tile, abstractRoom.CommonToCreatureSpecificNodeIndex(i, template), template)
                        : aiMap.ExitDistanceForCreature(tp.pos.Tile, abstractRoom.CommonToCreatureSpecificNodeIndex(i, tp.crit), tp.crit);
                    if (threatDist < myDist)
                    {
                        score = -1f;
                        break;
                    }
                    score += threatDist - myDist;
                }
                score /= ThreatOfArea(aiMap.room.LocalCoordinateOfNode(i), true);
                if (abstractRoom.nodes[i].type != AbstractRoomNode.Type.Exit)
                    score = float.MinValue;
                if (score > best)
                {
                    best = score;
                    result = i;
                }
            }
            return best >= 0f ? result : -1;
        }

        public ThreatPoint AddThreatPoint(CreatureTemplate crit, WorldCoordinate pos, float severity)
        {
            var tp = new ThreatPoint(crit, pos, severity);
            threatPoints.Add(tp);
            return tp;
        }

        public void RemoveThreatPoint(ThreatPoint tp)
        {
            for (int i = threatPoints.Count - 1; i >= 0; i--)
                if (threatPoints[i] == tp)
                    threatPoints.RemoveAt(i);
        }

        public void AddThreatCreature(Tracker.CreatureRepresentation creature)
        {
            foreach (var tc in threatCreatures)
                if (tc.creature == creature)
                    return;
            threatCreatures.Add(new ThreatCreature(this, creature));
            resetCounter = 0;
            if (threatCreatures.Count <= maxRememberedCreatures)
                return;
            float lowest = float.MaxValue;
            ThreatCreature drop = null;
            foreach (var tc in threatCreatures)
            {
                if (tc.CurrentThreat < lowest)
                {
                    lowest = tc.CurrentThreat;
                    drop = tc;
                }
            }
            drop?.Destroy(true);
        }

        public void RemoveThreatCreature(AbstractCreature crit)
        {
            for (int i = threatCreatures.Count - 1; i >= 0; i--)
            {
                if (threatCreatures[i].creature.representedCreature == crit)
                {
                    threatCreatures[i].Destroy(false);
                    break;
                }
            }
        }

        public ThreatCreature GetThreatCreature(AbstractCreature crit)
        {
            for (int i = threatCreatures.Count - 1; i >= 0; i--)
                if (threatCreatures[i].creature.representedCreature == crit)
                    return threatCreatures[i];
            return null;
        }

        /// <summary>The mean threat of a tile and its eight neighbours.</summary>
        public float ThreatOfArea(WorldCoordinate coord, bool accountThreatCreatureAccessibility)
        {
            float sum = 0f;
            for (int i = 0; i < 9; i++)
                sum += ThreatOfTile(coord + Custom.eightDirectionsAndZero[i], accountThreatCreatureAccessibility);
            return sum / 9f;
        }

        /// <summary>
        /// How threatening a tile is: each threat point adds its severity^1.5 * 10 over its distance^1.25,
        /// weighed by how easily the threatening creature gets there and by how hidden the tile is.
        /// </summary>
        public float ThreatOfTile(WorldCoordinate coord, bool accountThreatCreatureAccessibility)
        {
            if (coord.room != AI.creature.pos.room || aiMap == null)
                return 0f;
            float threat = 0f;
            for (int i = 0; i < threatPoints.Count; i++)
            {
                var tp = threatPoints[i];
                if (tp.pos.room != AI.creature.pos.room)
                    continue;
                float dist = Mathf.Sqrt(Mathf.Pow(coord.Tile.x - tp.pos.Tile.x, 2f) + Mathf.Pow(coord.Tile.y - tp.pos.Tile.y, 2f));
                float far = Mathf.Clamp(Mathf.InverseLerp(2f, 10f, dist), 0f, 1f);
                dist = Mathf.Pow(dist, 1.25f);
                dist = Mathf.Pow(tp.severity, 1.5f) * 10f / Mathf.Max(1f, dist);
                if (tp.crit != null && aiMap.AccessibilityForCreature(coord.Tile, tp.crit) > 0f)
                    dist *= Mathf.Pow(aiMap.AccessibilityForCreature(coord.Tile, tp.crit), accessibilityConsideration * far);
                dist *= Mathf.Lerp(1f, (float)aiMap.getAItile(coord.Tile).visibility / (aiMap.width * aiMap.height), Mathf.InverseLerp(15f, 25f, dist));
                threat += dist;
            }
            return threat;
        }

        // ---- Helpers the game keeps elsewhere ----

        // AbstractRoom.ConnectionAndBackPossible
        private static bool ConnectionAndBackPossible(AbstractRoom room, int startNode, int destNode, CreatureTemplate crit) =>
            startNode != destNode && ConnectivityCost(room, startNode, destNode, crit) > -1 && ConnectivityCost(room, destNode, startNode, crit) > -1;

        // AbstractRoom.ConnectivityCost
        private static int ConnectivityCost(AbstractRoom room, int startNode, int destNode, CreatureTemplate crit) =>
            startNode < 0 || destNode < 0 || startNode >= room.nodes.Length || destNode >= room.nodes.Length ? -1 : room.nodes[startNode].ConnectionCost(destNode, crit);

        // AbstractRoom.ConnectionLength
        private static int ConnectionLength(AbstractRoom room, int startNode, int destNode, CreatureTemplate crit) =>
            startNode < 0 || destNode < 0 || startNode >= room.nodes.Length || destNode >= room.nodes.Length ? -1 : room.nodes[startNode].ConnectionLength(destNode, crit);

        // Room.RayTraceTilesList: the tiles on the line between two tiles, written into path; returns how many.
        private static int RayTraceTilesList(int x0, int y0, int x1, int y1, ref List<IntVector2> path)
        {
            int dx = Math.Abs(x1 - x0);
            int dy = Math.Abs(y1 - y0);
            int x = x0;
            int y = y0;
            int n = 1 + dx + dy;
            int xInc = x1 > x0 ? 1 : -1;
            int yInc = y1 > y0 ? 1 : -1;
            int error = dx - dy;
            dx *= 2;
            dy *= 2;
            if (path == null)
                path = new List<IntVector2>();
            int count = 0;
            for (; n > 0; n--)
            {
                if (path.Count <= count)
                    path.Add(new IntVector2(x, y));
                else
                    path[count] = new IntVector2(x, y);
                count++;
                if (error > 0)
                {
                    x += xInc;
                    error -= dy;
                }
                else
                {
                    y += yInc;
                    error += dx;
                }
            }
            return count;
        }

        // QuickConnectivity.QuickPath: a short search from the goal back toward the start over incoming
        // connections, then the cheapest walk forward; writes the path and returns its length, 0 if none.
        // (The game checks a per-frame iteration quota there behind a flag that is never set.)
        private static int QuickPath(Room room, CreatureTemplate creatureType, IntVector2 start, IntVector2 goal,
            int maxDistanceFromStart, int maxGenerations, bool inOpenMedium, ref List<IntVector2> path)
        {
            if (start == goal)
            {
                if (path == null)
                    path = new List<IntVector2>();
                if (path.Count < 1)
                    path.Add(start);
                else
                    path[0] = start;
                return 1;
            }
            if (start.FloatDist(goal) > maxDistanceFromStart || !room.aimap.TileAccessibleToCreature(start, creatureType)
                || !room.aimap.TileAccessibleToCreature(goal, creatureType))
                return 0;
            start.x = Custom.IntClamp(start.x, 0, room.TileWidth - 1);
            start.y = Custom.IntClamp(start.y, 0, room.TileHeight - 1);
            goal.x = Custom.IntClamp(goal.x, 0, room.TileWidth - 1);
            goal.y = Custom.IntClamp(goal.y, 0, room.TileHeight - 1);
            var matrix = new Dictionary<int, PathCost>();
            var checkNext = new List<IntVector2> { goal };
            var startDists = new List<float>();
            if (inOpenMedium)
                startDists.Add(goal.FloatDist(start));
            matrix.Add(goal.x * room.TileHeight + goal.y, new PathCost(0f, PathCost.Legality.Allowed));
            IntVector2? found = null;
            for (int i = 0; i < maxGenerations && !found.HasValue; i++)
            {
                if (checkNext.Count < 1)
                    return 0;
                int index = 0;
                if (inOpenMedium)
                {
                    float closest = float.MaxValue;
                    for (int j = 0; j < startDists.Count; j++)
                    {
                        if (startDists[j] < closest)
                        {
                            index = j;
                            closest = startDists[j];
                        }
                    }
                }
                var tile = checkNext[index];
                int key = tile.x * room.TileHeight + tile.y;
                if (!matrix.TryGetValue(key, out var cost))
                {
                    cost = new PathCost(-1f, PathCost.Legality.Unallowed);
                    matrix.Add(key, cost);
                }
                checkNext.RemoveAt(index);
                if (inOpenMedium)
                    startDists.RemoveAt(index);
                var aiTile = room.aimap.map[tile.x, tile.y];
                for (int k = 0; k < aiTile.incomingPaths.Count; k++)
                {
                    var connection = aiTile.incomingPaths[k];
                    var step = creatureType.ConnectionResistance(connection.type) + room.aimap.TileCostForCreature(connection.startCoord, creatureType);
                    if (!step.Allowed)
                        continue;
                    int key2 = connection.startCoord.x * room.TileHeight + connection.startCoord.y;
                    if (!matrix.TryGetValue(key2, out var known))
                    {
                        known = new PathCost(-1f, PathCost.Legality.Unallowed);
                        matrix.Add(key2, known);
                    }
                    if (!(known.resistance < 0f))
                        continue;
                    var startTile = connection.StartTile;
                    float d = startTile.FloatDist(start);
                    if (d <= maxDistanceFromStart)
                    {
                        matrix[key2] = cost + step;
                        checkNext.Add(startTile);
                        if (inOpenMedium)
                            startDists.Add(d);
                        if (startTile == start)
                        {
                            found = start;
                            break;
                        }
                    }
                }
            }
            if (!found.HasValue)
                return 0;
            if (path == null)
                path = new List<IntVector2>();
            int count = 0;
            for (int l = 0; l < maxGenerations; l++)
            {
                var bestCost = new PathCost(0f, PathCost.Legality.Unallowed);
                var bestTile = found.Value;
                var aiTile = room.aimap.map[found.Value.x, found.Value.y];
                for (int m = 0; m < aiTile.outgoingPaths.Count; m++)
                {
                    var connection = aiTile.outgoingPaths[m];
                    int key3 = connection.destinationCoord.x * room.TileHeight + connection.destinationCoord.y;
                    var c = matrix.TryGetValue(key3, out var v) ? v : new PathCost(-1f, PathCost.Legality.Unallowed);
                    if (c < bestCost)
                    {
                        bestCost = c;
                        bestTile = connection.DestTile;
                    }
                }
                if (!bestCost.Allowed)
                    return 0;
                if (path.Count <= count)
                    path.Add(found.Value);
                else
                    path[count] = found.Value;
                count++;
                found = bestTile;
                if (bestTile == goal)
                    return count;
            }
            return 0;
        }
    }
}
