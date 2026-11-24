namespace RWDE.Sim
{
    // PLACEHOLDER: replaced by the port of the game's ThreatTracker. Fork B's members are stubs only.
    public class ThreatTracker : AIModule
    {
        public ThreatTracker(ArtificialIntelligence AI, int maxRememberedThreats) : base(AI) { }
        public float ThreatOfTile(WorldCoordinate coord, bool accountThreatCreatureAccessibility) => 0f;
        public int FindMostAttractiveExit() => -1;
    }
}
