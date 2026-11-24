namespace RWDE.Sim
{
    /// <summary>
    /// What a creature carries between being realized and abstract, ported from the game's
    /// <c>CreatureState</c>: alive or dead, and the meat left on it.
    /// </summary>
    /// <remarks>
    /// Not ported: save strings (the desktop doesn't save creatures) and <c>socialMemory</c>, which holds
    /// how a creature feels about individual players; no one plays on the desktop.
    /// </remarks>
    public abstract class CreatureState
    {
        protected CreatureState(AbstractCreature creature)
        {
            this.creature = creature;
            alive = true;
            meatLeft = creature.creatureTemplate.meatPoints;
        }

        protected readonly AbstractCreature creature;
        public int meatLeft;

        public bool alive { get; private set; }
        public bool dead => !alive;

        public void Die()
        {
            alive = false;
        }

        /// <summary>A new cycle began (<c>CreatureState.CycleTick</c>).</summary>
        public virtual void CycleTick()
        {
        }
    }

    /// <summary>A state with health, which damage lowers (<c>HealthState</c>). Below 0 the creature may die.</summary>
    public class HealthState : CreatureState
    {
        public HealthState(AbstractCreature creature) : base(creature)
        {
            health = 1f;
        }

        public float health;

        public float ClampedHealth => Mathf.Clamp(health, 0f, 1f);

        public override void CycleTick()
        {
            health = Mathf.Min(1f, health + 0.25f);
            base.CycleTick();
        }
    }
}
