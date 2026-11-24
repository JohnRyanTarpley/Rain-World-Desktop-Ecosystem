using System.Collections.Generic;

namespace RWDE.Sim
{
    /// <summary>
    /// Finds the den a creature retreats to (from the rain, when hurt, or carrying prey), ported from the
    /// game's <c>DenFinder</c>. The den lives in <see cref="AbstractCreatureAI.denPosition"/>.
    /// </summary>
    /// <remarks>
    /// Desktop rooms have no den nodes: every den is offscreen, and the offscreen world is not a room
    /// with nodes. So the search finds nothing and <see cref="GetDenPosition"/> stays null, as in the game
    /// when no den is reachable. The game's check for the creature's original spawn den
    /// (<c>CheckOriginalDenReachability</c>) needs that den to be a node in the world, so it never applies.
    /// The game's multi-tick <c>AbstractSpaceNodeFinder</c> flood fill isn't ported; <see cref="Initiate"/>
    /// takes the first den node the pathfinder can reach, which is the same as the flood fill's answer
    /// while there are none.
    /// </remarks>
    public class DenFinder : NodeFinder
    {
        public DenFinder(ArtificialIntelligence AI, AbstractCreature creature) : base(AI, creature)
        {
            if (denPosition.HasValue)
                status = Status.Found;
            else
                ResetMapping(false);
        }

        private WorldCoordinate? denPosition
        {
            get => creature.abstractAI.denPosition;
            set => creature.abstractAI.denPosition = value;
        }

        public WorldCoordinate? GetDenPosition() => denPosition;

        public override void ResetMapping(bool strandedAllFromExits)
        {
            base.ResetMapping(strandedAllFromExits);
            if (strandedAllFromExits)
                denPosition = null;
        }

        public override void Initiate()
        {
            List<WorldCoordinate> path = null;
            var world = creature.world;
            for (int r = world.firstRoomIndex; r < world.firstRoomIndex + world.NumberOfRooms && path == null; r++)
            {
                var room = world.GetAbstractRoom(r);
                for (int n = 0; n < room.nodes.Length; n++)
                {
                    if (room.nodes[n].type != AbstractRoomNode.Type.Den || !creature.creatureTemplate.mappedNodeTypes[(int)AbstractRoomNode.Type.Den])
                        continue;
                    var den = new WorldCoordinate(room.index, -1, -1, n);
                    if (AI.pathFinder == null || AI.pathFinder.CoordinateReachable(den))
                    {
                        path = new List<WorldCoordinate> { den };
                        break;
                    }
                }
            }
            Finished(path);
        }

        public override void Finished(List<WorldCoordinate> path)
        {
            if (path != null)
            {
                denPosition = path[0].WashTileData();
                status = Status.Found;
            }
            else
            {
                denPosition = null;
                status = Status.NoAccessible;
            }
        }

        public override void NewWorld()
        {
            denPosition = null;
            base.NewWorld();
        }
    }
}
