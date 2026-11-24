namespace RWDE.Sim
{
    // PLACEHOLDER: replaced by the port of the game's RelationshipTracker.
    public class RelationshipTracker : AIModule
    {
        public RelationshipTracker(ArtificialIntelligence AI, Tracker tracker) : base(AI) { }
        public class DynamicRelationship
        {
            public CreatureTemplate.Relationship currentRelationship;
        }
    }
}
