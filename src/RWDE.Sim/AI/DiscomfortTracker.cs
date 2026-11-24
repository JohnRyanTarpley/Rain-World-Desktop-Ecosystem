namespace RWDE.Sim
{
    // PLACEHOLDER: replaced by the port of the game's DiscomfortTracker.
    public class DiscomfortTracker : AIModule
    {
        public DiscomfortTracker(ArtificialIntelligence AI, Tracker tracker, float discomfortTreshold) : base(AI) { }
        public float DiscomfortOfTile(WorldCoordinate coord) => 0f;
    }
}
