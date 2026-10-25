using System.Collections.Generic;

namespace RWDE.Sim
{
    /// <summary>
    /// The pathfinder most creatures use, ported from the game's <c>StandardPather</c>: an A*-style
    /// heuristic toward the creature, and <see cref="FollowPath"/>, which picks the creature's next step.
    /// </summary>
    public class StandardPather : PathFinder
    {
        public float heuristicCostFac = 40f;
        public float heuristicDestFac = 1f;
        /// <summary>The connections recently followed, newest first.</summary>
        public List<MovementConnection> pastConnections = new List<MovementConnection>();
        private int s;
        public int numnerOfTimesConnectionHasToHaveBeenFollowedToBeOffLimits;

        public StandardPather(ArtificialIntelligence AI, World world, AbstractCreature creature) : base(AI, world, creature)
        {
            savedPastConnections = 20;
            numnerOfTimesConnectionHasToHaveBeenFollowedToBeOffLimits = 3;
        }

        /// <summary>How many past connections to remember; lowering it forgets the oldest (the game's quirky trim kept).</summary>
        public int savedPastConnections
        {
            get => s;
            set
            {
                s = value;
                if (s < pastConnections.Count)
                    for (int i = pastConnections.Count - 1; i > s; i--)
                        pastConnections.RemoveAt(i);
            }
        }

        protected override PathCost HeuristicForCell(PathingCell cell, PathCost costToGoal)
        {
            if (!InThisRealizedRoom(cell.worldCoordinate))
                return costToGoal;
            if (lookingForImpossiblePath && !cell.reachable)
                return new PathCost(costToGoal.resistance * heuristicCostFac
                    + (Mathf.Abs(cell.worldCoordinate.x - creaturePos.x) + Mathf.Abs(cell.worldCoordinate.y - creaturePos.y)) * 1.5f
                    + (Mathf.Abs(cell.worldCoordinate.x - destination.x) + Mathf.Abs(cell.worldCoordinate.y - destination.y)) * 0.75f,
                    costToGoal.legality);
            return new PathCost(costToGoal.resistance * heuristicCostFac + creaturePos.Tile.FloatDist(cell.worldCoordinate.Tile) * heuristicDestFac,
                costToGoal.legality);
        }

        /// <summary>
        /// The connection to take from <paramref name="originPos"/>: of its outgoing connections, the most
        /// legal one, then the one priced by the newest search, then the cheapest to the goal. A connection
        /// followed 3 times among the last 20 counts as unwanted, which breaks loops. Returns the default
        /// connection when there is no allowed step. Pass <paramref name="actuallyFollowingThisPath"/> when
        /// the creature will take the step, so the pather can record it.
        /// </summary>
        public MovementConnection FollowPath(WorldCoordinate originPos, bool actuallyFollowingThisPath)
        {
            if (originPos.TileDefined)
            {
                originPos.x = Custom.IntClamp(originPos.x, 0, realizedRoom.TileWidth - 1);
                originPos.y = Custom.IntClamp(originPos.y, 0, realizedRoom.TileHeight - 1);
            }
            if (forbiddenEntranceCounter > 0)
            {
                if (actuallyFollowingThisPath && ForbiddenEntranceDist(originPos) > 6)
                    forbiddenEntranceCounter = 0;
                return AwayFromForbiddenEntrance(originPos);
            }
            if (!CoordinateCost(originPos).Allowed)
                return default;

            var cell = PathingCellAtWorldCoordinate(originPos);
            if (!cell.reachable || !cell.possibleToGetBackFrom)
                OutOfElement();
            var best = default(MovementConnection);
            var bestCost = new PathCost(0f, PathCost.Legality.Unallowed);
            int bestGeneration = -acceptablePathAge;
            var legality = PathCost.Legality.Unallowed;
            for (int i = 0; ; i++)
            {
                var connection = ConnectionAtCoordinate(true, originPos, i);
                if (connection == default)
                    break;
                if (connection.destinationCoord.TileDefined && !Custom.InsideRect(connection.DestTile, coveredArea))
                    continue;
                var next = PathingCellAtWorldCoordinate(connection.destinationCoord);
                var step = CheckConnectionCost(cell, next, connection, true);
                if (!next.possibleToGetBackFrom && !walkPastPointOfNoReturn)
                    step.legality = PathCost.Legality.Unallowed;
                var total = next.costToGoal + step;
                if (connection.destinationCoord.Tile == destination.Tile)
                    total.resistance = 0f;
                else if (savedPastConnections > 0 && ConnectionAlreadyFollowedSeveralTimes(connection))
                    step += new PathCost(0f, PathCost.Legality.Unwanted);
                if (step.legality < legality
                    || (step.legality == legality && (next.generation > bestGeneration || (next.generation == bestGeneration && total <= bestCost))))
                {
                    best = connection;
                    legality = step.legality;
                    bestGeneration = next.generation;
                    bestCost = total;
                }
            }
            if (legality > PathCost.Legality.Unwanted)
                return default;

            if (actuallyFollowingThisPath)
            {
                if (best != default && !best.destinationCoord.TileDefined)
                    LeavingRoom();
                creatureFollowingGeneration = bestGeneration;
            }
            if (savedPastConnections > 0)
            {
                if (actuallyFollowingThisPath && (pastConnections.Count == 0 || best != pastConnections[0]))
                    pastConnections.Insert(0, best);
                if (pastConnections.Count > savedPastConnections)
                    pastConnections.RemoveAt(savedPastConnections);
            }
            return best;
        }

        private bool ConnectionAlreadyFollowedSeveralTimes(MovementConnection connection)
        {
            int n = 0;
            for (int i = 0; i < pastConnections.Count; i++)
                if (pastConnections[i] == connection && ++n >= numnerOfTimesConnectionHasToHaveBeenFollowedToBeOffLimits)
                    return true;
            return false;
        }
    }
}
