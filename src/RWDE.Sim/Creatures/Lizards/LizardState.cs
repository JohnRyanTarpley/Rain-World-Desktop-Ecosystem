namespace RWDE.Sim
{
    /// <summary>
    /// A lizard's state, ported from the game's <c>LizardState</c>: health plus throat and limb health.
    /// The Watcher rot types aren't in the Outskirts, so <see cref="rotType"/> stays <see cref="RotType.None"/>.
    /// </summary>
    public sealed class LizardState : HealthState
    {
        // LizardState.RotType
        public enum RotType
        {
            None,
            Slight,
            Opossum,
            Full
        }

        public LizardState(AbstractCreature creature) : base(creature)
        {
            throatHealth = 1f;
            limbHealth = creature.creatureTemplate.type == CreatureTemplate.Type.SpitLizard
                ? new[] { 1f, 1f, 1f, 1f, 1f, 1f }
                : new[] { 1f, 1f, 1f, 1f };
        }

        public float throatHealth;
        public readonly float[] limbHealth;
        public RotType rotType = RotType.None;

        public override void CycleTick()
        {
            throatHealth = Mathf.Min(throatHealth + 0.25f, 1f);
            for (int i = 0; i < limbHealth.Length; i++)
                limbHealth[i] = Mathf.Min(limbHealth[i] + 0.5f, 1f);
            base.CycleTick();
        }
    }
}
