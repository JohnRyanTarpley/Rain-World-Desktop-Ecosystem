namespace RWDE.Sim
{
    // PLACEHOLDER: replaced by the port of the game's Tracker.
    public class Tracker : AIModule
    {
        public Tracker(ArtificialIntelligence AI) : base(AI) { }
        public NoiseTracker noiseTracker;
        public CreatureRepresentation RepresentationForCreature(AbstractCreature crit, bool addIfMissing) => null;

        public abstract class CreatureRepresentation
        {
            protected CreatureRepresentation(Tracker parent, AbstractCreature representedCreature, float priority)
            {
                this.parent = parent;
                this.representedCreature = representedCreature;
            }
            public Tracker parent;
            public AbstractCreature representedCreature;
            public RelationshipTracker.DynamicRelationship dynamicRelationship;
        }

        public class SimpleCreatureRepresentation : CreatureRepresentation
        {
            public SimpleCreatureRepresentation(Tracker parent, AbstractCreature representedCreature, float priority, bool forgetWhenNotVisible)
                : base(parent, representedCreature, priority) { }
        }
    }
}
