namespace RWDE.Sim
{
    /// <summary>
    /// The lizards' pathfinder, ported from the game's <c>LizardPather</c>: a heuristic that pulls the
    /// search toward the lizard (Yellow Lizards search without one), and <see cref="FollowPath"/>, which
    /// also counts turning around on the floor as unwanted, so lizards keep walking the way they face.
    /// </summary>
    public class LizardPather : PathFinder
    {
        public LizardPather(ArtificialIntelligence AI, World world, AbstractCreature creature) : base(AI, world, creature)
        {
        }

        // LizardPather.HeuristicForCell. The Salamander and Eel Lizard water case needs water, which the desktop lacks.
        protected override PathCost HeuristicForCell(PathingCell cell, PathCost costToGoal)
        {
            if (creature.creatureTemplate.type == CreatureTemplate.Type.YellowLizard)
                return costToGoal;
            if (!InThisRealizedRoom(cell.worldCoordinate))
                return costToGoal;
            if (lookingForImpossiblePath && !cell.reachable)
                return new PathCost(costToGoal.resistance * 5.5f, costToGoal.legality);
            return new PathCost(costToGoal.resistance * 0.5f + Mathf.Abs(cell.worldCoordinate.x - creaturePos.x)
                + Mathf.Abs(cell.worldCoordinate.y - creaturePos.y), costToGoal.legality);
        }

        /// <summary>
        /// The connection to take from <paramref name="originPos"/> (<c>LizardPather.FollowPath</c>): the most
        /// legal one, then the one priced by the newest search, then the cheapest. With
        /// <paramref name="bodyDirection"/> (an index into <see cref="Custom.fourDirections"/>), a floor step
        /// against the way the body faces is unwanted, unless the lizard is about to fall anyway.
        /// </summary>
        public MovementConnection FollowPath(WorldCoordinate originPos, int? bodyDirection, bool actuallyFollowingThisPath)
        {
            bool fallRisk = !realizedRoom.aimap.getAItile(originPos.Tile).narrowSpace
                            && realizedRoom.aimap.getAItile(originPos.Tile).fallRiskTile.y < originPos.y - 5;
            if (forbiddenEntranceCounter > 0)
            {
                if (actuallyFollowingThisPath && ForbiddenEntranceDist(originPos) > 6)
                    forbiddenEntranceCounter = 0;
                return AwayFromForbiddenEntrance(originPos);
            }
            var cell = PathingCellAtWorldCoordinate(originPos);
            if (cell == null)
                return default;
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
                if (!fallRisk && bodyDirection.HasValue && connection.type == MovementConnection.MovementType.Standard
                    && realizedRoom.aimap.getAItile(connection.startCoord.Tile).acc == AItile.Accessibility.Floor)
                {
                    var dir = IntVector2.ClampAtOne(new IntVector2(connection.destinationCoord.x - connection.startCoord.x,
                        connection.destinationCoord.y - connection.startCoord.y));
                    if ((dir.x != 0 || realizedRoom.aimap.getAItile(connection.startCoord.Tile).narrowSpace)
                        && Custom.IntVectorsOpposite(dir, Custom.fourDirections[bodyDirection.Value]))
                        step += new PathCost(0f, PathCost.Legality.Unwanted);
                }
                if (!next.possibleToGetBackFrom && !walkPastPointOfNoReturn)
                    step.legality = PathCost.Legality.Unallowed;
                var total = next.costToGoal + step;
                if (step.legality < legality)
                {
                    best = connection;
                    legality = step.legality;
                    bestGeneration = next.generation;
                    bestCost = total;
                }
                else if (step.legality == legality)
                {
                    if (next.generation > bestGeneration || (next.generation == bestGeneration && total <= bestCost))
                    {
                        best = connection;
                        legality = step.legality;
                        bestGeneration = next.generation;
                        bestCost = total;
                    }
                }
            }
            if (actuallyFollowingThisPath)
            {
                if (best != default && !best.destinationCoord.TileDefined)
                    LeavingRoom();
                creatureFollowingGeneration = bestGeneration;
            }
            return best;
        }
    }
}
