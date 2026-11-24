namespace RWDE.Sim
{
    // PLACEHOLDER: replaced by the port of the game's DenFinder.
    public class DenFinder : AIModule
    {
        public DenFinder(ArtificialIntelligence AI, AbstractCreature creature) : base(AI) { }
        public WorldCoordinate? GetDenPosition() => null;
        public void ResetMapping(bool strandedFromExits) { }
    }
}
