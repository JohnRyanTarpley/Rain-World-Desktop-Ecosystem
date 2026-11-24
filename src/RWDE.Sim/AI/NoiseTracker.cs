namespace RWDE.Sim
{
    // PLACEHOLDER: replaced by the port of the game's NoiseTracker.
    public class NoiseTracker : AIModule
    {
        public NoiseTracker(ArtificialIntelligence AI, Tracker tracker) : base(AI) { }
        public void HeardNoise(InGameNoise noise) { }
    }

    public struct InGameNoise { }
}
