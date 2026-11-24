using System.Collections.Generic;

namespace RWDE.Sim
{
    /// <summary>
    /// Anger toward rivals, ported from the game's <c>AgressionTracker</c>. Each rival's anger drifts
    /// toward a base level that is higher when the rival is close, or close to the creature's prey; the
    /// angriest one is the aggression target.
    /// </summary>
    public class AgressionTracker : AIModule
    {
        /// <summary>Anger toward one creature (<c>AgressionTracker.AngerRelationship</c>).</summary>
        public class AngerRelationship
        {
            public AngerRelationship(AgressionTracker tTracker, Tracker.CreatureRepresentation crit, float initialAnger, float baseAnger)
            {
                this.tTracker = tTracker;
                this.crit = crit;
                anger = initialAnger;
                this.baseAnger = baseAnger;
            }

            public AgressionTracker tTracker;
            public Tracker.CreatureRepresentation crit;
            public float anger;
            public float baseAnger;

            public void Update()
            {
                var ai = tTracker.AI;
                float target = baseAnger;
                target *= Mathf.InverseLerp(10f + baseAnger * 70f, 5f, Custom.ManhattanDistance(ai.creature.pos, crit.BestGuessForPosition()));
                if (ai.preyTracker != null && ai.preyTracker.MostAttractivePrey != null)
                {
                    float nearPrey = Mathf.InverseLerp(10f, 3f, Custom.ManhattanDistance(crit.BestGuessForPosition(), ai.preyTracker.MostAttractivePrey.BestGuessForPosition()));
                    target = Mathf.Lerp(target, nearPrey, 0.5f);
                }
                var room = ai.creature.world.GetAbstractRoom(crit.BestGuessForPosition().room);
                target *= Mathf.InverseLerp(0.3f, 0.6f, room != null ? room.AttractionValueForCreature(ai.creature) : 0f);
                if (anger < target)
                    anger = Mathf.Clamp(anger + tTracker.angerSpeedUp, 0f, target);
                else if (anger > target)
                    anger = Mathf.Clamp(anger - tTracker.angerSpeedDown, target, 1f);
                crit.forgetCounter = 0;
            }
        }

        public AgressionTracker(ArtificialIntelligence AI, float angerSpeedUp, float angerSpeedDown) : base(AI)
        {
            this.angerSpeedUp = angerSpeedUp;
            this.angerSpeedDown = angerSpeedDown;
        }

        private readonly List<AngerRelationship> creatures = new List<AngerRelationship>();
        public float highestAnger;
        public AngerRelationship highestAgressionTarget;
        public float angerSpeedUp;
        public float angerSpeedDown;

        public int TotalTrackedCreatures => creatures.Count;

        public override void Update()
        {
            highestAnger = 0f;
            highestAgressionTarget = null;
            for (int i = creatures.Count - 1; i >= 0; i--)
            {
                creatures[i].Update();
                if (creatures[i].crit.deleteMeNextFrame)
                {
                    creatures.RemoveAt(i);
                }
                else if (creatures[i].anger > highestAnger)
                {
                    highestAnger = creatures[i].anger;
                    highestAgressionTarget = creatures[i];
                }
            }
        }

        public override float Utility() => Mathf.InverseLerp(0.35f, 1f, highestAnger);

        public Tracker.CreatureRepresentation AgressionTarget() => highestAgressionTarget?.crit;

        public void AddCreature(Tracker.CreatureRepresentation newCrit, float initialAnger, float baseAnger)
        {
            for (int i = 0; i < creatures.Count; i++)
                if (creatures[i].crit == newCrit)
                    return;
            creatures.Add(new AngerRelationship(this, newCrit, initialAnger, baseAnger));
        }

        public void ForgetCreature(AbstractCreature removeCrit)
        {
            for (int i = 0; i < creatures.Count; i++)
            {
                if (creatures[i].crit.representedCreature == removeCrit)
                {
                    AI.relationshipTracker?.ModuleHasAbandonedCreature(creatures[i].crit, this);
                    creatures.RemoveAt(i);
                    break;
                }
            }
        }

        public void IncrementAnger(Tracker.CreatureRepresentation crit, float angerAdd)
        {
            for (int i = 0; i < creatures.Count; i++)
            {
                if (creatures[i].crit == crit)
                {
                    creatures[i].anger = Mathf.Clamp(creatures[i].anger + angerAdd, 0f, 1f);
                    break;
                }
            }
        }

        /// <summary>Sets the anger, and the base anger unless <paramref name="newBaseAnger"/> is -1.</summary>
        public void SetAnger(Tracker.CreatureRepresentation crit, float newAnger, float newBaseAnger)
        {
            for (int i = 0; i < creatures.Count; i++)
            {
                if (creatures[i].crit == crit)
                {
                    creatures[i].anger = Mathf.Clamp(newAnger, 0f, 1f);
                    if (newBaseAnger != -1f)
                        creatures[i].baseAnger = Mathf.Clamp(newBaseAnger, 0f, 1f);
                    break;
                }
            }
        }
    }
}
