using System.Collections.Generic;

namespace RWDE.Sim
{
    /// <summary>
    /// Base of the AI modules that search the world's nodes for a kind of place, ported from the game's
    /// <c>NodeFinder</c>. A search starts (<see cref="Initiate"/>) while the status is Working and reports
    /// through <see cref="Finished"/>.
    /// </summary>
    /// <remarks>
    /// The game runs the search over several ticks with an <c>AbstractSpaceNodeFinder</c>. That flood fill
    /// isn't ported: subclasses search directly in <see cref="Initiate"/> and call <see cref="Finished"/>.
    /// <see cref="ResetMapping"/> skips giving the creature's position a node in a room without nodes,
    /// which desktop rooms without pipes are and game rooms never are.
    /// </remarks>
    public abstract class NodeFinder : AIModule
    {
        // NodeFinder.Status
        public enum Status
        {
            Working,
            Found,
            NoAccessible
        }

        protected NodeFinder(ArtificialIntelligence AI, AbstractCreature creature) : base(AI)
        {
            this.creature = creature;
        }

        protected AbstractCreature creature;
        public Status status = Status.Working;

        public override void Update()
        {
            if (status == Status.Working)
                Initiate();
        }

        /// <summary>Starts over (<c>NodeFinder.ResetMapping</c>); a creature stranded from every exit gives up.</summary>
        public virtual void ResetMapping(bool strandedAllFromExits)
        {
            // A desktop room may have no nodes at all (no pipes), where there is no node to define.
            if (!creature.pos.NodeDefined && creature.Room != null && creature.Room.nodes.Length > 0)
                creature.pos = QuickConnectivity.DefineNodeOfLocalCoordinate(creature.pos, creature.world, creature.creatureTemplate);
            status = strandedAllFromExits ? Status.NoAccessible : Status.Working;
        }

        public virtual void NewWorld() => ResetMapping(false);

        public virtual void ResetMappingIfNecessary(bool strandedAllFromExits)
        {
        }

        /// <summary>The search ended; <paramref name="path"/> leads to the place found (first entry), or is null.</summary>
        public virtual void Finished(List<WorldCoordinate> path)
        {
        }

        public virtual void Initiate()
        {
        }
    }
}
