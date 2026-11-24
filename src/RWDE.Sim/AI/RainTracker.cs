namespace RWDE.Sim
{
    /// <summary>
    /// How urgently the creature wants to get away from the coming rain, ported from the game's
    /// <c>RainTracker</c>. Night creatures and ones that ignore the cycle never mind it; otherwise the
    /// utility rises as the time until rain falls from 4000 to 800 ticks (the story-session curve, which
    /// the desktop's campaigns follow).
    /// </summary>
    /// <remarks>
    /// The time comes from <see cref="World.timeUntilRain"/>, which stays at its maximum until the rain
    /// cycle is ported, so the utility is 0 for now. Not ported: the pre-cycle rain (needs the rain
    /// cycle's pre-timer and room danger types), scavengers in locust swarms, Daddy corruption grabs and
    /// Rain Deer carrying players.
    /// </remarks>
    public class RainTracker : AIModule
    {
        public RainTracker(ArtificialIntelligence AI) : base(AI)
        {
        }

        public override float Utility()
        {
            if (AI.creature != null && (AI.creature.nightCreature || AI.creature.ignoreCycle))
                return 0f;
            return Mathf.InverseLerp(1f, 0f, Custom.SCurve(Mathf.InverseLerp(800f, 4000f, AI.creature.world.timeUntilRain), 0.1f));
        }
    }
}
