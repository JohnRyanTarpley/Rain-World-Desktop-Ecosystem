using System.Collections.Generic;

namespace RWDE.Sim
{
    /// <summary>
    /// A creature as the world sees it, ported from the game's <c>AbstractCreature</c>. So far only what
    /// the pathfinder reads: its template, world, position and abstract AI. The abstract world adds the
    /// rest (realizing, entity ids, states).
    /// </summary>
    public class AbstractCreature
    {
        public AbstractCreature(World world, CreatureTemplate creatureTemplate, WorldCoordinate pos)
        {
            this.world = world;
            this.creatureTemplate = creatureTemplate;
            this.pos = pos;
            abstractAI = new AbstractCreatureAI(world, this);
        }

        public World world;
        public readonly CreatureTemplate creatureTemplate;
        /// <summary>Where the creature is: its room and tile, and the node it last entered through or is heading for.</summary>
        public WorldCoordinate pos;
        public AbstractCreatureAI abstractAI;

        public AbstractRoom Room => world.GetAbstractRoom(pos);

        public override string ToString() => creatureTemplate.name + " at " + pos;
    }

    /// <summary>
    /// The offscreen side of a creature's AI, ported from the game's <c>AbstractCreatureAI</c>. So far
    /// only its destination, its node path and the link to the realized AI.
    /// </summary>
    public class AbstractCreatureAI
    {
        public AbstractCreatureAI(World world, AbstractCreature parent)
        {
            this.world = world;
            this.parent = parent;
            destination = parent.pos;
        }

        public World world;
        public readonly AbstractCreature parent;
        public WorldCoordinate destination;
        /// <summary>The nodes the creature plans to pass through offscreen.</summary>
        public readonly List<WorldCoordinate> path = new List<WorldCoordinate>();
        /// <summary>The realized AI, while the creature is in a room.</summary>
        public ArtificialIntelligence RealAI;
    }
}
