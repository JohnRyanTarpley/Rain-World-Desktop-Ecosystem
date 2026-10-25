using System;
using System.Collections.Generic;

namespace RWDE.Sim
{
    /// <summary>
    /// A creature's pathfinding, ported from the game's <c>PathFinder</c>. It keeps a cell per tile of the
    /// room the creature is in and a cell per node of every room, and searches backwards from the
    /// destination a few cells per tick, so a path improves over several ticks; the creature reads it one
    /// step at a time (<see cref="StandardPather.FollowPath"/>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// As in the game, the search runs over the room's <see cref="AImap"/> connections plus the
    /// connections between tiles and nodes: a shortcut entrance joins its room-exit node, an exit node
    /// joins the node it leads to in the neighbouring room (a pipe between monitor rooms), and a room's
    /// nodes join each other at their pre-processed connection costs. Pipes inside a room are AI-map
    /// shortcut connections. Before searching, an accessibility pass marks which cells the creature can
    /// reach from where it is and get back from.
    /// </para>
    /// <para>
    /// The game's terrain never changes. Desktop terrain does, so when the room's terrain or any room's
    /// nodes change, <see cref="Update"/> redoes the room as on entering it (<see cref="Reset"/>), which
    /// remaps accessibility and then searches for the current destination again.
    /// </para>
    /// <para>
    /// Not ported: sharing and merging accessibility mappers between creatures with identical pathing (a
    /// speed-up; each pathfinder maps on its own), the debug visualisers, and the side, sky, sea and region
    /// highways and room border exits, which need node types the desktop doesn't have yet.
    /// </para>
    /// </remarks>
    public abstract class PathFinder : AIModule
    {
        /// <summary>The search state of one tile or node (<c>PathFinder.PathingCell</c>).</summary>
        public sealed class PathingCell
        {
            public PathingCell(WorldCoordinate worldCoordinate, Random random)
            {
                this.worldCoordinate = worldCoordinate;
                generation = -1;
                heuristicValue = new PathCost(123456f * (float)random.NextDouble(), PathCost.Legality.Allowed);
                costToGoal = new PathCost(123456f * (float)random.NextDouble(), PathCost.Legality.Allowed);
            }

            /// <summary>The search (destination) that last set <see cref="costToGoal"/>.</summary>
            public int generation;
            public PathCost heuristicValue;
            public PathCost costToGoal;
            public bool inCheckNextList;
            /// <summary>The creature can get here from where the accessibility pass started.</summary>
            public bool reachable;
            /// <summary>The creature can get back from here to where the accessibility pass started.</summary>
            public bool possibleToGetBackFrom;
            public readonly WorldCoordinate worldCoordinate;
        }

        /// <summary>
        /// Floods out from a start cell to mark what is reachable (modes 1-2), then floods back to mark what
        /// can get back (modes 3-4), one cell per call (<c>PathFinder.AccessibilityMapper</c>, one client).
        /// </summary>
        protected sealed class AccessibilityMapper
        {
            private readonly PathFinder parent;
            private readonly List<WorldCoordinate> accessibilityCheckNext = new List<WorldCoordinate>(500);
            private readonly IntVector2[] alreadyCheckedTiles;
            private int mapAccessibilityMode;
            public readonly bool taintedByForbiddenNode;
            public readonly Room mappingRoom;

            public AccessibilityMapper(PathFinder initiator, WorldCoordinate accessibilityCenter, IntVector2[] alreadyCheckedTiles, bool taintedByForbiddenNode)
            {
                parent = initiator;
                this.taintedByForbiddenNode = taintedByForbiddenNode;
                mappingRoom = initiator.realizedRoom;
                initiator.accessibilityCenter = accessibilityCenter;
                this.alreadyCheckedTiles = alreadyCheckedTiles;
                mapAccessibilityMode = 1;
                UpdateAccessibilityMapping();
            }

            public void UpdateAccessibilityMapping()
            {
                switch (mapAccessibilityMode)
                {
                    case 1:
                        foreach (var roomCells in parent.WorldCells)
                            foreach (var cell in roomCells)
                            {
                                cell.reachable = false;
                                cell.possibleToGetBackFrom = false;
                            }
                        if (mappingRoom != null)
                            foreach (var cell in parent.CurrentRoomCells)
                            {
                                cell.reachable = false;
                                cell.possibleToGetBackFrom = false;
                            }
                        accessibilityCheckNext.Clear();
                        accessibilityCheckNext.Add(parent.accessibilityCenter);
                        if (alreadyCheckedTiles != null)
                        {
                            // A quick path already known: its tiles count as mapped, cheapest nearest its end.
                            for (int n = 0; n < alreadyCheckedTiles.Length; n++)
                            {
                                var coord = new WorldCoordinate(mappingRoom.abstractRoom.index, alreadyCheckedTiles[n].x, alreadyCheckedTiles[n].y, -1);
                                accessibilityCheckNext.Add(coord);
                                var cell = parent.PathingCellAtWorldCoordinate(coord);
                                cell.reachable = true;
                                cell.possibleToGetBackFrom = true;
                                cell.costToGoal = new PathCost(alreadyCheckedTiles.Length - n, PathCost.Legality.Allowed);
                                cell.generation = parent.pathGeneration;
                            }
                        }
                        mapAccessibilityMode = 2;
                        break;

                    case 2:
                    {
                        if (accessibilityCheckNext.Count < 1)
                        {
                            mapAccessibilityMode = 3;
                            break;
                        }
                        var coord = accessibilityCheckNext[0];
                        accessibilityCheckNext.RemoveAt(0);
                        for (int i = 0; ; i++)
                        {
                            var connection = parent.ConnectionAtCoordinate(true, coord, i);
                            if (connection == default || mappingRoom.aimap == null)
                                break;
                            if (!parent.PathingCellAtWorldCoordinate(connection.destinationCoord).reachable
                                && mappingRoom.aimap.IsConnectionAllowedForCreature(connection, parent.creatureType)
                                && (connection.startCoord.room == connection.destinationCoord.room
                                    || parent.creatureType.ConnectionResistance(MovementConnection.MovementType.BetweenRooms).Allowed)
                                && parent.CoordinateCost(connection.destinationCoord).Allowed)
                            {
                                accessibilityCheckNext.Add(connection.destinationCoord);
                                parent.PathingCellAtWorldCoordinate(connection.destinationCoord).reachable = true;
                            }
                        }
                        break;
                    }

                    case 3:
                        accessibilityCheckNext.Clear();
                        accessibilityCheckNext.Add(parent.accessibilityCenter);
                        if (alreadyCheckedTiles != null)
                            foreach (var tile in alreadyCheckedTiles)
                            {
                                var coord = new WorldCoordinate(mappingRoom.abstractRoom.index, tile.x, tile.y, -1);
                                accessibilityCheckNext.Add(coord);
                                parent.PathingCellAtWorldCoordinate(coord).reachable = true;
                                parent.PathingCellAtWorldCoordinate(coord).possibleToGetBackFrom = true;
                            }
                        mapAccessibilityMode = 4;
                        break;

                    case 4:
                    {
                        if (accessibilityCheckNext.Count < 1)
                        {
                            mapAccessibilityMode = 5;
                            break;
                        }
                        var coord = accessibilityCheckNext[0];
                        accessibilityCheckNext.RemoveAt(0);
                        for (int i = 0; ; i++)
                        {
                            var connection = parent.ConnectionAtCoordinate(false, coord, i);
                            if (connection == default || mappingRoom.aimap == null)
                                break;
                            if (!parent.PathingCellAtWorldCoordinate(connection.startCoord).possibleToGetBackFrom
                                && mappingRoom.aimap.IsConnectionAllowedForCreature(connection, parent.creatureType)
                                && (connection.startCoord.room == connection.destinationCoord.room
                                    || parent.creatureType.ConnectionResistance(MovementConnection.MovementType.BetweenRooms).Allowed)
                                && parent.CoordinateCost(connection.startCoord).Allowed)
                            {
                                accessibilityCheckNext.Add(connection.startCoord);
                                parent.PathingCellAtWorldCoordinate(connection.startCoord).possibleToGetBackFrom = true;
                            }
                        }
                        break;
                    }

                    case 5:
                        parent.AccessibilityMappingDone();
                        parent.accessibilityMapper = null;
                        if (taintedByForbiddenNode)
                        {
                            // Mapped with the entrance node forbidden: map again without it.
                            parent.forbiddenNode = null;
                            parent.InitiAccessibilityMapping(parent.creature.pos, alreadyCheckedTiles);
                        }
                        break;
                }
            }
        }

        protected WorldCoordinate destination;
        protected WorldCoordinate? nextDestination;
        protected WorldCoordinate currentlyFollowingDestination;
        /// <summary>Goes up with each new destination; cells set by the current search carry it.</summary>
        public int pathGeneration;
        /// <summary>The generation of the path the creature last followed.</summary>
        public int creatureFollowingGeneration;
        public int goalFoundGeneration;
        protected List<PathingCell> checkNextList;
        /// <summary>The smallest rectangle holding every tile of the room the creature can be on.</summary>
        public IntRect coveredArea;
        protected int acceptablePathAge;
        protected PathCost minimumPossiblePathCost;
        public PathingCell fallbackPathingCell;
        public int timeUntilPathCertain = -1;
        protected int timeInGeneration;
        protected int lastFollowingGeneration;
        protected bool reAssignDestinationOnceAccessibilityMappingIsDone;
        public WorldCoordinate? forbiddenNode;
        protected PathingCell[][] WorldCells;
        protected AItile[][] WorldAITiles;
        protected PathingCell[,] CurrentRoomCells;
        protected Room realizedRoom;
        protected int room;
        protected World world;
        public readonly AbstractCreature creature;
        protected WorldCoordinate accessibilityCenter;
        protected AccessibilityMapper accessibilityMapper;
        private readonly PathfinderResourceDivider pathfinderResourceDivider;
        public int stepsPerFrame;
        public int accessibilityStepsPerFrame;
        public WorldCoordinate? nonShortcutRoomEntrancePos;
        public bool walkPastPointOfNoReturn;
        public WorldCoordinate forbiddenEntrance = new WorldCoordinate(-1, -1, -1, -1);
        public int forbiddenEntranceCounter;

        // The terrain and node layout the cells were last built for.
        private int seenTerrainVersion;
        private int seenNodeGraphVersion;

        public bool lookingForImpossiblePath { get; protected set; }
        public bool DoneMappingAccessibility => accessibilityMapper == null;
        public WorldCoordinate creaturePos => creature.pos;
        public CreatureTemplate creatureType => creature.creatureTemplate;
        public WorldCoordinate GetDestination => nextDestination ?? destination;
        public WorldCoordinate GetEffectualDestination => currentlyFollowingDestination;
        public bool DestInRoom => InThisRealizedRoom(GetDestination);

        protected PathFinder(ArtificialIntelligence AI, World world, AbstractCreature creature) : base(AI)
        {
            this.world = world;
            this.creature = creature;
            pathfinderResourceDivider = world.pathfinderResourceDivider;
            destination = creature.abstractAI.destination;
            if (!destination.TileDefined && creature.pos.NodeDefined)
                destination = creature.pos;
            nextDestination = destination;
            currentlyFollowingDestination = destination;
            acceptablePathAge = 5;
            creatureFollowingGeneration = -2;
            stepsPerFrame = 10;
            accessibilityStepsPerFrame = 10;
            checkNextList = new List<PathingCell>(100);
            fallbackPathingCell = new PathingCell(new WorldCoordinate(-1, -1, -1, -1), world.random);
            SetUpWorld();
            pathGeneration = 1;
        }

        // A cell and an AI tile per node of every room, with the node-to-node connections (PathFinder.SetUpWorld).
        private void SetUpWorld()
        {
            seenNodeGraphVersion = world.NodeGraphVersion;
            WorldCells = new PathingCell[world.NumberOfRooms][];
            WorldAITiles = new AItile[world.NumberOfRooms][];
            for (int i = 0; i < world.NumberOfRooms; i++)
            {
                int nodes = world.GetAbstractRoom(i + world.firstRoomIndex).nodes.Length;
                WorldCells[i] = new PathingCell[nodes];
                WorldAITiles[i] = new AItile[nodes];
                for (int j = 0; j < nodes; j++)
                {
                    WorldCells[i][j] = new PathingCell(new WorldCoordinate(i + world.firstRoomIndex, -1, -1, j), world.random);
                    WorldAITiles[i][j] = new AItile(AItile.Accessibility.OffScreen, 0);
                }
            }
            for (int k = 0; k < world.NumberOfRooms; k++)
                for (int l = 0; l < world.GetAbstractRoom(k + world.firstRoomIndex).nodes.Length; l++)
                    ConnectAITile(k + world.firstRoomIndex, l);
        }

        public override void Update()
        {
            if (realizedRoom != null && (realizedRoom.TerrainVersion != seenTerrainVersion || world.NodeGraphVersion != seenNodeGraphVersion))
                TerrainChanged();

            timeInGeneration++;
            if (lastFollowingGeneration != creatureFollowingGeneration)
            {
                timeInGeneration = 0;
                lastFollowingGeneration = creatureFollowingGeneration;
            }
            PathingCellAtWorldCoordinate(destination).costToGoal.resistance = 0f;
            bool mapping = false;
            if (accessibilityMapper != null)
            {
                mapping = true;
                int n = pathfinderResourceDivider.RequesAccesibilityUpdates(accessibilityStepsPerFrame);
                while (n > 0 && accessibilityMapper != null)
                {
                    accessibilityMapper.UpdateAccessibilityMapping();
                    n--;
                }
            }
            else if (forbiddenEntranceCounter > 0)
            {
                forbiddenEntranceCounter--;
            }
            for (int steps = mapping ? 1 : pathfinderResourceDivider.RequestPathfinderUpdates(stepsPerFrame); steps > 0; steps--)
            {
                if (checkNextList.Count > 0)
                {
                    var cell = checkNextList[0];
                    checkNextList.RemoveAt(0);
                    cell.inCheckNextList = false;
                    CheckNeighbours(cell);
                    if (minimumPossiblePathCost < cell.costToGoal)
                        minimumPossiblePathCost = cell.costToGoal;
                    if (creatureFollowingGeneration == pathGeneration)
                    {
                        if (timeInGeneration > timeUntilPathCertain)
                            AbortCurrentGenerationPathFinding();
                        currentlyFollowingDestination = destination;
                    }
                    else if (cell.worldCoordinate.CompareDisregardingNode(creature.pos))
                    {
                        currentlyFollowingDestination = destination;
                    }
                }
                else if (nextDestination.HasValue)
                {
                    AssignNewDestination(nextDestination.Value);
                    nextDestination = null;
                }
                else if (creatureFollowingGeneration < pathGeneration - 1 && !reAssignDestinationOnceAccessibilityMappingIsDone)
                {
                    AssignNewDestination(destination);
                }
            }
        }

        /// <summary>
        /// Desktop addition: the room's terrain or the world's node graph (any room's nodes, connections or
        /// terrain) changed under the pathfinder. Rebuilds the node cells if needed and redoes the room as on
        /// entering it, which repaths to the destination.
        /// </summary>
        protected virtual void TerrainChanged()
        {
            if (world.NodeGraphVersion != seenNodeGraphVersion)
            {
                SetUpWorld();
                // A node that went with its pipe: head for the tile, or let the node be worked out again.
                if (destination.room != room && destination.NodeDefined
                    && destination.abstractNode >= world.GetAbstractRoom(destination).nodes.Length)
                    destination.abstractNode = -1;
                if (nextDestination.HasValue && nextDestination.Value.room != room && nextDestination.Value.NodeDefined
                    && nextDestination.Value.abstractNode >= world.GetAbstractRoom(nextDestination.Value).nodes.Length)
                    nextDestination = null;
            }
            Reset(realizedRoom);
        }

        // The search: from each cell, price every connection leading into it (PathFinder.CheckNeighbours).
        protected void CheckNeighbours(PathingCell checkNow)
        {
            for (int i = 0; ; i++)
            {
                var connection = ConnectionAtCoordinate(false, checkNow.worldCoordinate, i);
                if (connection == default)
                    break;
                var cell = PathingCellAtWorldCoordinate(connection.startCoord);
                if (!cell.worldCoordinate.TileDefined && !cell.worldCoordinate.NodeDefined)
                    continue;
                var cost = CheckConnectionCost(cell, checkNow, connection, false);
                if ((!cost.Allowed || !cell.reachable) && (!lookingForImpossiblePath || !cost.Considerable))
                    continue;
                var costToGoal = checkNow.costToGoal + cost;
                var heuristic = HeuristicForCell(cell, costToGoal);
                if (cell.generation == pathGeneration)
                {
                    if (cell.inCheckNextList)
                    {
                        if (cell.heuristicValue > heuristic)
                        {
                            checkNextList.Remove(cell);
                            cell.heuristicValue = heuristic;
                            AddToCheckNextList(cell);
                        }
                        if (cell.costToGoal > costToGoal)
                            cell.costToGoal = costToGoal;
                    }
                }
                else if (cell.generation < pathGeneration)
                {
                    cell.costToGoal = costToGoal;
                    cell.heuristicValue = heuristic;
                    cell.inCheckNextList = true;
                    cell.generation = pathGeneration;
                    AddToCheckNextList(cell);
                }
            }
        }

        /// <summary>Call as the creature leaves the room: a destination elsewhere needs a node to aim for.</summary>
        protected void LeavingRoom()
        {
            if (nextDestination.HasValue && !nextDestination.Value.NodeDefined)
                destination = QuickConnectivity.DefineNodeOfLocalCoordinate(nextDestination.Value, world, creatureType);
            else if (!destination.NodeDefined)
                destination = QuickConnectivity.DefineNodeOfLocalCoordinate(destination, world, creatureType);
            nextDestination = null;
        }

        public override void NewRoom(Room newRealizedRoom)
        {
            if (realizedRoom != newRealizedRoom)
                Reset(newRealizedRoom);
        }

        /// <summary>
        /// Starts over in <paramref name="newRealizedRoom"/>: new tile cells over the area the creature can
        /// use, then accessibility mapping from where it entered or stands (<c>PathFinder.Reset</c>).
        /// </summary>
        public void Reset(Room newRealizedRoom)
        {
            currentlyFollowingDestination = creature.pos;
            realizedRoom = newRealizedRoom;
            room = realizedRoom.abstractRoom.index;
            seenTerrainVersion = realizedRoom.TerrainVersion;
            if (world != newRealizedRoom.world)
                world = newRealizedRoom.world;
            checkNextList.Clear();

            int left = realizedRoom.TileWidth, top = 0, right = 0, bottom = realizedRoom.TileHeight;
            bool any = false;
            for (int i = 0; i < realizedRoom.TileWidth; i++)
                for (int j = 0; j < realizedRoom.TileHeight; j++)
                    if (realizedRoom.aimap.TileAccessibleToCreature(i, j, creatureType))
                    {
                        any = true;
                        if (i < left) left = i;
                        if (j < bottom) bottom = j;
                        if (i > right) right = i;
                        if (j > top) top = j;
                    }
            if (!any)
            {
                left = 0;
                bottom = 0;
                right = 1;
                top = 1;
            }
            coveredArea = new IntRect(left, bottom, right, top);
            CurrentRoomCells = new PathingCell[right - left + 1, top - bottom + 1];
            for (int k = 0; k <= right - left; k++)
                for (int l = 0; l <= top - bottom; l++)
                    CurrentRoomCells[k, l] = new PathingCell(new WorldCoordinate(room, k + left, l + bottom, -1), world.random);

            accessibilityMapper = null;
            bool initiated = false;
            if (nonShortcutRoomEntrancePos.HasValue)
                initiated = InitiatePath(nonShortcutRoomEntrancePos.Value.abstractNode);
            if (!initiated && !creature.pos.TileDefined && creature.pos.NodeDefined)
                initiated = InitiatePath(creature.pos.abstractNode);
            if (!initiated)
                InitiAccessibilityMapping(creature.pos, null);
            reAssignDestinationOnceAccessibilityMappingIsDone = true;
        }

        public void ForceNextDestination()
        {
            if (!destination.Equals(nextDestination) && nextDestination.HasValue)
                AssignNewDestination(nextDestination.Value);
        }

        /// <summary>
        /// The creature is coming in through node <paramref name="theDoorICameInThrough"/>: forbid going
        /// straight back out, quick-path from the entrance toward the destination (or the exit it's likely
        /// to use) and map accessibility from there (<c>PathFinder.InitiatePath</c>).
        /// </summary>
        protected bool InitiatePath(int theDoorICameInThrough)
        {
            if (theDoorICameInThrough < 0 || theDoorICameInThrough >= realizedRoom.abstractRoom.nodes.Length)
                return false;
            forbiddenEntrance = new WorldCoordinate(realizedRoom.abstractRoom.index, -1, -1, theDoorICameInThrough);
            forbiddenEntranceCounter = 40;
            forbiddenNode = new WorldCoordinate(realizedRoom.abstractRoom.index, -1, -1, theDoorICameInThrough);
            var entranceCoordinate = realizedRoom.LocalCoordinateOfNode(theDoorICameInThrough);
            WorldCoordinate goal;
            if (InThisRealizedRoom(destination))
            {
                goal = destination;
            }
            else
            {
                int exit = DestinationExit(room, theDoorICameInThrough);
                if (exit < 0)
                    exit = theDoorICameInThrough;
                goal = realizedRoom.LocalCoordinateOfNode(exit);
            }
            var quickPathFinder = new QuickPathFinder(entranceCoordinate.Tile, goal.Tile, realizedRoom.aimap, creatureType);
            while (quickPathFinder.status == 0)
                quickPathFinder.Update();
            var quickPath = quickPathFinder.ReturnPath();
            reAssignDestinationOnceAccessibilityMappingIsDone = true;
            InitiAccessibilityMapping(entranceCoordinate, quickPath?.tiles);
            return true;
        }

        public void InitiAccessibilityMapping(WorldCoordinate entranceCoordinate, IntVector2[] alreadyAccessible)
        {
            accessibilityMapper = new AccessibilityMapper(this, entranceCoordinate, alreadyAccessible, forbiddenNode.HasValue);
        }

        // Accessibility is known: search for the destination, and make sure the creature's node is one it can use.
        protected void AccessibilityMappingDone()
        {
            AssignNewDestination(destination);
            bool stranded = false;
            var nodes = realizedRoom.abstractRoom.nodes;
            if (creature.pos.abstractNode < 0 || creature.pos.abstractNode >= nodes.Length
                || !creatureType.mappedNodeTypes[(int)nodes[creature.pos.abstractNode].type])
            {
                var usable = new List<int>();
                for (int i = 0; i < nodes.Length; i++)
                    if (creatureType.mappedNodeTypes[(int)nodes[i].type]
                        && CoordinateReachableAndGetbackable(new WorldCoordinate(creature.pos.room, -1, -1, i)))
                        usable.Add(i);
                if (usable.Count > 0)
                    creature.pos.abstractNode = usable[world.random.Next(0, usable.Count)];
            }
            if (!CoordinateReachableAndGetbackable(new WorldCoordinate(creature.pos.room, -1, -1, creature.pos.abstractNode)))
            {
                stranded = true;
                for (int j = 0; j < creature.Room.nodes.Length; j++)
                    if (CoordinateReachable(new WorldCoordinate(creature.pos.room, -1, -1, j)))
                    {
                        creature.pos.abstractNode = j;
                        stranded = false;
                        break;
                    }
                if (stranded && realizedRoom.aimap != null)
                    for (int k = 0; k < creature.Room.nodes.Length; k++)
                        if (realizedRoom.aimap.ExitReachableFromTile(creature.pos.Tile, k, creatureType)
                            || realizedRoom.aimap.ExitReachableFromTile(accessibilityCenter.Tile, k, creatureType))
                        {
                            creature.pos.abstractNode = k;
                            stranded = false;
                            break;
                        }
            }
            AI?.NewArea(stranded);
        }

        public void RestartPathFinding() => AssignNewDestination(destination);

        /// <summary>
        /// Of the nodes reachable from <paramref name="theDoorICameInThrough"/>, the one the latest search
        /// prices cheapest; failing that, one on the abstract path (<c>PathFinder.DestinationExit</c>).
        /// </summary>
        public int DestinationExit(int evaluateRoom, int theDoorICameInThrough)
        {
            int bestGeneration = -1;
            var bestCost = new PathCost(0f, PathCost.Legality.Unallowed);
            int exit = -1;
            var abstractRoom = world.GetAbstractRoom(evaluateRoom);
            for (int i = 0; i < abstractRoom.nodes.Length; i++)
            {
                if (i == theDoorICameInThrough || abstractRoom.nodes[theDoorICameInThrough].ConnectionCost(i, creatureType) <= -1)
                    continue;
                var cell = PathingCellAtWorldCoordinate(new WorldCoordinate(evaluateRoom, -1, -1, i));
                if (!cell.reachable)
                    continue;
                if (cell.generation > bestGeneration || (cell.generation == bestGeneration && cell.costToGoal < bestCost))
                {
                    bestGeneration = cell.generation;
                    bestCost = cell.costToGoal;
                    exit = i;
                }
            }
            if (exit == -1)
                foreach (var coord in creature.abstractAI.path)
                    if (coord.room == room)
                        exit = coord.abstractNode;
            if (exit == -1)
            {
                for (int j = 0; j < abstractRoom.nodes.Length; j++)
                {
                    if ((j == creature.abstractAI.destination.abstractNode && creature.abstractAI.destination.room == evaluateRoom)
                        || (j < abstractRoom.connections.Length && abstractRoom.connections[j] > -1
                            && creature.abstractAI.path.Contains(new WorldCoordinate(abstractRoom.connections[j], -1, -1,
                                world.GetAbstractRoom(abstractRoom.connections[j]).ExitIndex(evaluateRoom)))))
                    {
                        exit = j;
                        break;
                    }
                }
            }
            return exit;
        }

        protected virtual void DestinationHasChanged(WorldCoordinate oldDestination, WorldCoordinate newDestination)
        {
        }

        /// <summary>
        /// Asks for a new destination (<c>PathFinder.SetDestination</c>). In this room it is clamped to the
        /// covered area and moved to a reachable neighbour if possible; in another room it gets a node. A
        /// destination quickly connected to the current one waits as the next destination, so the creature
        /// keeps following the old path until the new search is done.
        /// </summary>
        public void SetDestination(WorldCoordinate newDestination)
        {
            if (destination.Equals(newDestination) || destination.Equals(nextDestination))
                return;
            if (InThisRealizedRoom(newDestination))
            {
                newDestination.x = Custom.IntClamp(newDestination.x, coveredArea.left, coveredArea.right);
                newDestination.y = Custom.IntClamp(newDestination.y, coveredArea.bottom, coveredArea.top);
                newDestination = FindReachableNeighbourIfPossible(newDestination);
            }
            if (newDestination.room != room && !newDestination.NodeDefined)
            {
                // Desktop rooms are always ready for AI, so the game's keep-the-old-node branch never applies.
                newDestination = QuickConnectivity.DefineNodeOfLocalCoordinate(newDestination, world, creatureType);
            }
            if (destination.Equals(newDestination) || destination.Equals(nextDestination))
                return;
            DestinationHasChanged(destination, newDestination);
            if (InThisRealizedRoom(newDestination))
            {
                if (CoordinateCost(newDestination).Considerable)
                {
                    if (PathingCellAtWorldCoordinate(newDestination).reachable && !PathingCellAtWorldCoordinate(destination).reachable)
                        AssignNewDestination(newDestination);
                    else if (QuickConnectivity.Check(realizedRoom, creatureType, destination.Tile, newDestination.Tile, 100) > -1)
                        nextDestination = newDestination;
                    else
                        AssignNewDestination(newDestination);
                }
            }
            else if (newDestination.NodeDefined)
            {
                AssignNewDestination(newDestination);
            }
        }

        protected WorldCoordinate FindReachableNeighbourIfPossible(WorldCoordinate coord)
        {
            for (int i = 0; i < 9; i++)
            {
                var neighbour = new WorldCoordinate(coord.room, coord.x + Custom.eightDirectionsAndZero[i].x,
                    coord.y + Custom.eightDirectionsAndZero[i].y, coord.abstractNode);
                if (PathingCellAtWorldCoordinate(neighbour).reachable && Custom.InsideRect(neighbour.Tile, coveredArea))
                    return neighbour;
            }
            return coord;
        }

        // Starts a new search generation from the destination.
        protected void AssignNewDestination(WorldCoordinate dest)
        {
            destination = dest;
            pathGeneration++;
            forbiddenNode = null;
            checkNextList.Clear();
            if (destination.room != room && !destination.NodeDefined)
                destination = QuickConnectivity.DefineNodeOfLocalCoordinate(destination, world, creatureType);
            minimumPossiblePathCost = CoordinateCost(destination);
            lookingForImpossiblePath = !PathingCellAtWorldCoordinate(destination).reachable;
            var cell = PathingCellAtWorldCoordinate(destination);
            AddToCheckNextList(cell);
            cell.heuristicValue = new PathCost(0f, minimumPossiblePathCost.legality);
            cell.costToGoal = new PathCost(0f, minimumPossiblePathCost.legality);
        }

        // Keeps the list sorted by heuristic, a new cell going before equal ones.
        protected void AddToCheckNextList(PathingCell cell)
        {
            if (cell == fallbackPathingCell)
                return;
            for (int i = 0; i < checkNextList.Count; i++)
                if (cell.heuristicValue <= checkNextList[i].heuristicValue)
                {
                    checkNextList.Insert(i, cell);
                    return;
                }
            checkNextList.Add(cell);
        }

        protected void AbortCurrentGenerationPathFinding() => checkNextList.Clear();

        protected bool InThisRealizedRoom(WorldCoordinate coord) => coord.room == room && coord.TileDefined;

        /// <summary>The cell for a tile of this room or a node of any room; a shared dummy cell for anything else.</summary>
        public PathingCell PathingCellAtWorldCoordinate(WorldCoordinate coord)
        {
            if (InThisRealizedRoom(coord))
            {
                if (Custom.InsideRect(coord.Tile, coveredArea))
                    return CurrentRoomCells[coord.x - coveredArea.left, coord.y - coveredArea.bottom];
            }
            else if (coord.NodeDefined && coord.room >= world.firstRoomIndex && coord.room < world.firstRoomIndex + world.NumberOfRooms
                     && coord.abstractNode < WorldCells[coord.room - world.firstRoomIndex].Length)
            {
                return WorldCells[coord.room - world.firstRoomIndex][coord.abstractNode];
            }
            return fallbackPathingCell;
        }

        protected AItile AITileAtWorldCoordinate(WorldCoordinate coord)
        {
            if (InThisRealizedRoom(coord) && realizedRoom.aimap != null)
                return realizedRoom.aimap.getAItile(coord.x, coord.y);
            if (coord.NodeDefined && coord.room >= world.firstRoomIndex && coord.room < world.firstRoomIndex + world.NumberOfRooms
                && coord.abstractNode < WorldAITiles[coord.room - world.firstRoomIndex].Length)
                return WorldAITiles[coord.room - world.firstRoomIndex][coord.abstractNode];
            return new AItile(AItile.Accessibility.Solid, 0);
        }

        /// <summary>
        /// The <paramref name="index"/>-th connection out of (or into) <paramref name="coord"/>, or the default
        /// connection past the last (<c>PathFinder.ConnectionAtCoordinate</c>). A tile lists its AI-map
        /// connections then its link to a node; a node of this room lists its link to its shortcut entrance
        /// and then the pipe to the next room.
        /// </summary>
        public MovementConnection ConnectionAtCoordinate(bool outGoing, WorldCoordinate coord, int index)
        {
            var tile = AITileAtWorldCoordinate(coord);
            var list = outGoing ? tile.outgoingPaths : tile.incomingPaths;
            if (coord.TileDefined)
            {
                if (index < list.Count)
                    return list[index].InRoom(room);
                if (index == list.Count)
                    return FromRealizedToWorldConnection(outGoing, coord);
            }
            else if (coord.NodeDefined)
            {
                if (coord.room == room)
                    return ConnectionsOfAbstractNodeInRealizedRoom(outGoing, coord, index);
                if (index < list.Count)
                    return list[index];
            }
            return default;
        }

        // Desktop nodes are all exits; the game's region transportation and border exit branches have no
        // desktop nodes to apply to yet.
        protected MovementConnection ConnectionsOfAbstractNodeInRealizedRoom(bool outGoing, WorldCoordinate coord, int index)
        {
            if (coord.abstractNode < realizedRoom.exitAndDenIndex.Length)
            {
                if (index == 0)
                    return FromWorldToRealizedRoomShortCutConnection(outGoing, coord);
                if (realizedRoom.abstractRoom.nodes[coord.abstractNode].type == AbstractRoomNode.Type.Exit && index == 1)
                {
                    int other = realizedRoom.abstractRoom.connections[coord.abstractNode];
                    if (other == -1)
                        return default;
                    var far = new WorldCoordinate(other, -1, -1, world.GetAbstractRoom(other).ExitIndex(room));
                    return outGoing
                        ? new MovementConnection(MovementConnection.MovementType.ShortCut, coord, far, 1)
                        : new MovementConnection(MovementConnection.MovementType.ShortCut, far, coord, 1);
                }
            }
            return default;
        }

        // A shortcut entrance leads to the node its shortcut ends in.
        protected MovementConnection FromRealizedToWorldConnection(bool outGoing, WorldCoordinate coord)
        {
            if (!InThisRealizedRoom(coord))
                return default;
            if (realizedRoom.GetTile(coord.Tile).Terrain == Room.Tile.TerrainType.ShortcutEntrance)
            {
                var shortcut = realizedRoom.shortcutData(coord.Tile);
                if (shortcut.shortCutType == ShortcutData.Type.RoomExit || shortcut.shortCutType == ShortcutData.Type.CreatureHole
                    || shortcut.shortCutType == ShortcutData.Type.RegionTransportation)
                {
                    int node = Array.IndexOf(realizedRoom.exitAndDenIndex, shortcut.DestTile);
                    var nodeCoord = new WorldCoordinate(coord.room, -1, -1, node);
                    return outGoing
                        ? new MovementConnection(MovementConnection.MovementType.ShortCut, coord, nodeCoord, 1)
                        : new MovementConnection(MovementConnection.MovementType.ShortCut, nodeCoord, coord, 1);
                }
            }
            return default;
        }

        protected MovementConnection FromWorldToRealizedRoomShortCutConnection(bool outGoing, WorldCoordinate coord)
        {
            var entrance = realizedRoom.ShortcutLeadingToNode(coord.abstractNode).StartTile;
            var tile = new WorldCoordinate(room, entrance.x, entrance.y, -1);
            return outGoing
                ? new MovementConnection(MovementConnection.MovementType.ShortCut, coord, tile, 1)
                : new MovementConnection(MovementConnection.MovementType.ShortCut, tile, coord, 1);
        }

        /// <summary>The creature got somewhere the last mapping didn't cover: map again from there.</summary>
        protected void OutOfElement(WorldCoordinate ps)
        {
            if (realizedRoom != null && realizedRoom.aimap != null && accessibilityMapper == null
                && realizedRoom.aimap.WorldCoordinateAccessibleToCreature(ps, creatureType)
                && (!PathingCellAtWorldCoordinate(ps).reachable || !PathingCellAtWorldCoordinate(ps).possibleToGetBackFrom))
                InitiAccessibilityMapping(ps, null);
        }

        protected void OutOfElement() => OutOfElement(creaturePos);

        public bool CoordinateReachable(WorldCoordinate coord) => PathingCellAtWorldCoordinate(coord).reachable;

        public bool CoordinatePossibleToGetBackFrom(WorldCoordinate coord) => PathingCellAtWorldCoordinate(coord).possibleToGetBackFrom;

        public bool CoordinateReachableAndGetbackable(WorldCoordinate coord)
        {
            var cell = PathingCellAtWorldCoordinate(coord);
            return cell.reachable && cell.possibleToGetBackFrom;
        }

        public bool CoordinateViable(WorldCoordinate coord) =>
            walkPastPointOfNoReturn ? CoordinateReachable(coord) : CoordinateReachableAndGetbackable(coord);

        public bool CoordinateAtCurrentPathingGeneration(WorldCoordinate coord) => PathingCellAtWorldCoordinate(coord).generation == pathGeneration;

        // A node's connections: the pipe to the next room, and walking to each other node of its room.
        protected void ConnectAITile(int room, int node)
        {
            var tile = WorldAITiles[room - world.firstRoomIndex][node];
            var abstractRoom = world.GetAbstractRoom(room);
            var start = new WorldCoordinate(room, -1, -1, node);
            if (abstractRoom.nodes[node].type == AbstractRoomNode.Type.Exit && node < abstractRoom.connections.Length)
            {
                int other = abstractRoom.connections[node];
                if (other > -1)
                    tile.outgoingPaths.Add(new MovementConnection(MovementConnection.MovementType.ShortCut, start,
                        new WorldCoordinate(other, -1, -1, world.GetAbstractRoom(other).ExitIndex(room)),
                        world.TotalShortCutLengthBetweenTwoConnectedRooms(room, other)));
            }
            for (int i = 0; i < abstractRoom.nodes.Length; i++)
            {
                if (node == i) continue;
                int cost = abstractRoom.nodes[node].ConnectionCost(i, creatureType);
                tile.outgoingPaths.Add(cost > -1
                    ? new MovementConnection(MovementConnection.MovementType.OffScreenMovement, start, new WorldCoordinate(room, -1, -1, i), cost)
                    : new MovementConnection(MovementConnection.MovementType.OffScreenUnallowed, start, new WorldCoordinate(room, -1, -1, i), 1));
            }
            foreach (var outgoing in tile.outgoingPaths)
                AITileAtWorldCoordinate(outgoing.destinationCoord).incomingPaths.Add(outgoing);
        }

        /// <summary>
        /// The node path the latest search gives from the creature's node to <paramref name="searchDestination"/>,
        /// destination first, for handing to the abstract AI; null if there is none (<c>PathFinder.CreatePathForAbstractreature</c>).
        /// </summary>
        public List<WorldCoordinate> CreatePathForAbstractreature(WorldCoordinate searchDestination)
        {
            if (realizedRoom == null || !creatureType.canAutoAbstractPath)
                return null;
            var coord = creature.pos;
            coord.Tile = new IntVector2(-1, -1);
            var list = new List<WorldCoordinate> { coord };
            for (int i = 0; i < 100; i++)
            {
                var best = default(MovementConnection);
                var bestCost = new PathCost(0f, PathCost.Legality.Unallowed);
                int bestGeneration = -acceptablePathAge;
                var legality = PathCost.Legality.Unallowed;
                var start = PathingCellAtWorldCoordinate(coord);
                foreach (var outgoing in AITileAtWorldCoordinate(coord).outgoingPaths)
                {
                    var cell = PathingCellAtWorldCoordinate(outgoing.destinationCoord);
                    var step = CheckConnectionCost(start, cell, outgoing, true);
                    var total = cell.costToGoal + step;
                    if (step.legality < legality
                        || (step.legality == legality && (cell.generation > bestGeneration || (cell.generation == bestGeneration && total < bestCost))))
                    {
                        best = outgoing;
                        legality = step.legality;
                        bestGeneration = cell.generation;
                        bestCost = total;
                    }
                    if (step.Allowed && outgoing.destinationCoord.room == searchDestination.room
                        && outgoing.destinationCoord.abstractNode == searchDestination.abstractNode)
                    {
                        list.Add(outgoing.destinationCoord);
                        list.Reverse();
                        return list;
                    }
                }
                if (best == default)
                    return null;
                coord = best.destinationCoord;
                if (list.Contains(coord) || !bestCost.Allowed)
                    break;
                list.Add(coord);
            }
            return null;
        }

        /// <summary>
        /// Just after entering: the step that best moves away from the entrance node, preferring tiles
        /// already mapped as reachable (<c>PathFinder.AwayFromForbiddenEntrance</c>).
        /// </summary>
        public MovementConnection AwayFromForbiddenEntrance(WorldCoordinate origin)
        {
            // Desktop: the entrance node may have gone with its pipe.
            if (forbiddenEntrance.abstractNode < 0 || forbiddenEntrance.abstractNode >= realizedRoom.abstractRoom.nodes.Length)
                return default;
            if (!creatureType.mappedNodeTypes[(int)realizedRoom.abstractRoom.nodes[forbiddenEntrance.abstractNode].type])
                return default;
            int node = realizedRoom.abstractRoom.CommonToCreatureSpecificNodeIndex(forbiddenEntrance.abstractNode, creatureType);
            if (node < 0 || realizedRoom.aimap == null)
                return default;
            float here = realizedRoom.aimap.ExitDistanceForCreature(origin.Tile, node, creatureType);
            float best = float.MinValue;
            var result = default(MovementConnection);
            for (int i = 0; ; i++)
            {
                var connection = ConnectionAtCoordinate(true, origin, i);
                if (connection == default)
                    break;
                if (connection.type == MovementConnection.MovementType.ShortCut || !creatureType.ConnectionResistance(connection.type).Allowed
                    || !realizedRoom.aimap.TileAccessibleToCreature(connection.DestTile, creatureType))
                    continue;
                float score = realizedRoom.aimap.ExitDistanceForCreature(connection.DestTile, node, creatureType);
                if (score > 0f && score > here)
                    score = (float)world.random.NextDouble() * 100f + (realizedRoom.aimap.getAItile(connection.DestTile).narrowSpace ? 0f : 100f);
                if (PathingCellAtWorldCoordinate(connection.destinationCoord).reachable)
                    score += 1000f;
                if (score > best)
                {
                    best = score;
                    result = connection;
                }
            }
            return result;
        }

        public int ForbiddenEntranceDist(WorldCoordinate origin)
        {
            // Desktop: the entrance node may have gone with its pipe.
            if (forbiddenEntrance.abstractNode < 0 || forbiddenEntrance.abstractNode >= realizedRoom.abstractRoom.nodes.Length)
                return int.MaxValue;
            if (!creatureType.mappedNodeTypes[(int)realizedRoom.abstractRoom.nodes[forbiddenEntrance.abstractNode].type])
                return int.MaxValue;
            int node = realizedRoom.abstractRoom.CommonToCreatureSpecificNodeIndex(forbiddenEntrance.abstractNode, creatureType);
            if (node < 0 || realizedRoom.aimap == null)
                return int.MaxValue;
            return realizedRoom.aimap.ExitDistanceForCreature(origin.Tile, node, creatureType);
        }

        /// <summary>
        /// A step toward the room node the latest search likes best, by exit distance rather than by the
        /// tile search (<c>PathFinder.PathWithExits</c>).
        /// </summary>
        public MovementConnection PathWithExits(WorldCoordinate origin, bool avoidForbiddenEntrance)
        {
            if (realizedRoom.aimap == null)
                return default;
            int bestNode = -1;
            var bestCost = new PathCost(100f, PathCost.Legality.Unallowed);
            int bestGeneration = -1;
            var nodes = realizedRoom.abstractRoom.nodes;
            for (int i = 0; i < nodes.Length; i++)
            {
                int distance = realizedRoom.aimap.ExitDistanceForCreature(origin.Tile,
                    realizedRoom.abstractRoom.CommonToCreatureSpecificNodeIndex(i, creatureType), creatureType);
                if (!creatureType.mappedNodeTypes[(int)nodes[i].type])
                    continue;
                var cell = PathingCellAtWorldCoordinate(new WorldCoordinate(realizedRoom.abstractRoom.index, -1, -1, i));
                var cost = new PathCost(cell.costToGoal.resistance * distance, cell.costToGoal.legality);
                if (avoidForbiddenEntrance && forbiddenEntrance.room == origin.room && forbiddenEntrance.abstractNode == i)
                    cost += new PathCost(0f, PathCost.Legality.Unallowed);
                // As in the game, a newer generation wins without its cost being kept.
                if (cell.generation > bestGeneration)
                {
                    bestNode = i;
                    bestGeneration = cell.generation;
                }
                else if (cell.generation == bestGeneration && cost < bestCost)
                {
                    bestNode = i;
                    bestCost = cost;
                }
            }
            if (bestNode < 0)
                return default;
            int specific = realizedRoom.abstractRoom.CommonToCreatureSpecificNodeIndex(bestNode, creatureType);
            if (specific < 0)
                return default;
            int closest = int.MaxValue;
            var result = default(MovementConnection);
            for (int j = 0; ; j++)
            {
                var connection = ConnectionAtCoordinate(true, origin, j);
                if (connection == default)
                    break;
                int d = realizedRoom.aimap.ExitDistanceForCreature(connection.DestTile, specific, creatureType);
                if (d > 0 && d < closest)
                {
                    closest = d;
                    result = connection;
                }
            }
            if (result != default)
                creatureFollowingGeneration = bestGeneration;
            return result;
        }

        /// <summary>
        /// What taking <paramref name="connection"/> costs: illegal unless the AI map allows it, else the
        /// template's resistance times distance plus the goal tile's cost, plus the template's aversion
        /// to changing rooms or using shortcuts, adjusted by the AI's travel preference (<c>PathFinder.CheckConnectionCost</c>).
        /// </summary>
        protected virtual PathCost CheckConnectionCost(PathingCell start, PathingCell goal, MovementConnection connection, bool followingPath)
        {
            var cost = new PathCost(100f * connection.distance, PathCost.Legality.IllegalConnection);
            if (realizedRoom.aimap != null && !realizedRoom.aimap.IsConnectionAllowedForCreature(connection, creatureType))
            {
                cost += new PathCost(0f, PathCost.Legality.IllegalConnection);
            }
            else
            {
                var resistance = creatureType.ConnectionResistance(connection.type);
                if (resistance.Considerable)
                {
                    var goalCost = CoordinateCost(goal.worldCoordinate);
                    if (goalCost.Considerable && CoordinateCost(start.worldCoordinate).Considerable)
                    {
                        resistance.resistance *= connection.distance;
                        cost = resistance + goalCost + new PathCost(0f, CoordinateCost(start.worldCoordinate).legality);
                    }
                }
            }
            if (start.worldCoordinate.room != goal.worldCoordinate.room)
                cost += creatureType.ConnectionResistance(MovementConnection.MovementType.BetweenRooms);
            else if (connection.type == MovementConnection.MovementType.NPCTransportation)
                cost += creatureType.NPCTravelAversion;
            else if (connection.type == MovementConnection.MovementType.ShortCut)
                cost += creatureType.shortcutAversion;
            if (cost.Considerable && InThisRealizedRoom(connection.destinationCoord) && InThisRealizedRoom(connection.startCoord))
                cost = AI.TravelPreference(connection, cost);
            return cost;
        }

        protected virtual PathCost HeuristicForCell(PathingCell cell, PathCost costToGoal) => costToGoal;

        /// <summary>What being at <paramref name="coord"/> costs the creature; the forbidden node is unallowed (<c>PathFinder.CoordinateCost</c>).</summary>
        public PathCost CoordinateCost(WorldCoordinate coord)
        {
            if (forbiddenNode.HasValue && coord.room == forbiddenNode.Value.room && coord.abstractNode == forbiddenNode.Value.abstractNode)
                return new PathCost(0f, PathCost.Legality.Unallowed);
            if (InThisRealizedRoom(coord) && realizedRoom.aimap != null)
                return realizedRoom.aimap.TileCostForCreature(coord, creatureType);
            return creatureType.AccessibilityResistance(AITileAtWorldCoordinate(coord).acc);
        }

        /// <summary>Whether every tile on the line from A to B is reachable and get-back-able.</summary>
        public bool RayTraceInAccessibleTiles(IntVector2 A, IntVector2 B)
        {
            int dx = Math.Abs(B.x - A.x);
            int dy = Math.Abs(B.y - A.y);
            int x = A.x;
            int y = A.y;
            int n = 1 + dx + dy;
            int xInc = B.x > A.x ? 1 : -1;
            int yInc = B.y > A.y ? 1 : -1;
            int error = dx - dy;
            dx *= 2;
            dy *= 2;
            for (; n > 0; n--)
            {
                if (!CoordinateReachableAndGetbackable(new WorldCoordinate(room, x, y, -1)))
                    return false;
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
            return true;
        }
    }
}
