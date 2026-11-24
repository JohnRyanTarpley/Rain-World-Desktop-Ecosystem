using System.Collections.Generic;

namespace RWDE.Sim
{
    /// <summary>
    /// Implemented by an AI that keeps a <see cref="RelationshipTracker"/> (<c>IUseARelationshipTracker</c>):
    /// it picks the module that follows each kind of relationship and updates relationships each tick.
    /// </summary>
    public interface IUseARelationshipTracker
    {
        AIModule ModuleToTrackRelationship(CreatureTemplate.Relationship relationship);

        CreatureTemplate.Relationship UpdateDynamicRelationship(RelationshipTracker.DynamicRelationship dRelation);

        RelationshipTracker.TrackedCreatureState CreateTrackedCreatureState(RelationshipTracker.DynamicRelationship rel);
    }

    /// <summary>
    /// Keeps a live relationship to every tracked creature and hands each to the module that deals with
    /// that kind (prey, threats, rivals), ported from the game's <c>RelationshipTracker</c>. The AI must
    /// implement <see cref="IUseARelationshipTracker"/>.
    /// </summary>
    /// <remarks>Not ported: the debug relationship visualizer.</remarks>
    public class RelationshipTracker : AIModule
    {
        /// <summary>How the creature regards one tracked creature right now (<c>RelationshipTracker.DynamicRelationship</c>).</summary>
        public class DynamicRelationship
        {
            public DynamicRelationship(RelationshipTracker rt, Tracker.CreatureRepresentation trackerRep, CreatureTemplate.Relationship initialRelationship)
            {
                this.rt = rt;
                this.trackerRep = trackerRep;
                currentRelationship = initialRelationship;
                rt.SortCreatureIntoModule(this, initialRelationship);
                state = ((IUseARelationshipTracker)rt.AI).CreateTrackedCreatureState(this);
                trackerRep.dynamicRelationship = this;
                trackedByModuleWeigth = 1f;
            }

            public RelationshipTracker rt;
            public Tracker.CreatureRepresentation trackerRep;
            public CreatureTemplate.Relationship currentRelationship;
            public AIModule trackedByModule;
            public float trackedByModuleWeigth;
            public TrackedCreatureState state;

            public void Update()
            {
                var newRelationship = ((IUseARelationshipTracker)rt.AI).UpdateDynamicRelationship(this);
                if (newRelationship.type != currentRelationship.type)
                    rt.SortCreatureIntoModule(this, newRelationship);
                trackerRep.priority = newRelationship.intensity * trackedByModuleWeigth;
                currentRelationship = newRelationship;
            }
        }

        /// <summary>Extra per-creature memory an AI may keep (<c>RelationshipTracker.TrackedCreatureState</c>).</summary>
        public class TrackedCreatureState
        {
            public bool alive;
        }

        public RelationshipTracker(ArtificialIntelligence AI, Tracker tracker) : base(AI)
        {
            this.tracker = tracker;
        }

        public Tracker tracker;
        public List<DynamicRelationship> relationships = new List<DynamicRelationship>();
        private bool ignoreModuleAbandon;

        public override void Update()
        {
            for (int i = relationships.Count - 1; i >= 0; i--)
            {
                if (relationships[i].trackerRep.deleteMeNextFrame)
                    relationships.RemoveAt(i);
                else
                    relationships[i].Update();
            }
        }

        public void EstablishDynamicRelationship(Tracker.CreatureRepresentation crit)
        {
            var initial = AI.StaticRelationship(crit.representedCreature);
            if (initial.type == CreatureTemplate.Relationship.Type.DoesntTrack)
                return;
            for (int i = 0; i < relationships.Count; i++)
                if (relationships[i].trackerRep == crit)
                    return;
            relationships.Add(new DynamicRelationship(this, crit, initial));
        }

        private void ForgetCreatureAndStopTracking(AbstractCreature creature)
        {
            for (int i = 0; i < relationships.Count; i++)
            {
                if (relationships[i].trackerRep.representedCreature == creature)
                {
                    relationships[i].trackerRep.Destroy();
                    relationships.RemoveAt(i);
                }
            }
        }

        /// <summary>A module stopped following <paramref name="crit"/>; the creature is forgotten.</summary>
        public void ModuleHasAbandonedCreature(Tracker.CreatureRepresentation crit, AIModule module)
        {
            if (ignoreModuleAbandon)
                return;
            for (int i = 0; i < relationships.Count; i++)
            {
                if (relationships[i].trackerRep == crit)
                {
                    ForgetCreatureAndStopTracking(crit.representedCreature);
                    break;
                }
            }
        }

        /// <summary>Moves a relationship to the module the AI picks for its new type (<c>SortCreatureIntoModule</c>).</summary>
        public void SortCreatureIntoModule(DynamicRelationship relCrit, CreatureTemplate.Relationship newRelationship)
        {
            var module = ((IUseARelationshipTracker)AI).ModuleToTrackRelationship(newRelationship);
            if (relCrit.trackedByModule == module)
                return;
            if (relCrit.trackedByModule != null)
            {
                ignoreModuleAbandon = true;
                switch (relCrit.trackedByModule)
                {
                    case PreyTracker prey:
                        prey.ForgetPrey(relCrit.trackerRep.representedCreature);
                        break;
                    case ThreatTracker threats:
                        threats.RemoveThreatCreature(relCrit.trackerRep.representedCreature);
                        break;
                    case AgressionTracker aggression:
                        aggression.ForgetCreature(relCrit.trackerRep.representedCreature);
                        break;
                }
                ignoreModuleAbandon = false;
            }
            relCrit.trackedByModule = module;
            if (newRelationship.type == CreatureTemplate.Relationship.Type.DoesntTrack)
            {
                ForgetCreatureAndStopTracking(relCrit.trackerRep.representedCreature);
                return;
            }
            switch (relCrit.trackedByModule)
            {
                case PreyTracker prey:
                    prey.AddPrey(relCrit.trackerRep);
                    break;
                case ThreatTracker threats:
                    threats.AddThreatCreature(relCrit.trackerRep);
                    break;
                case AgressionTracker aggression:
                    aggression.AddCreature(relCrit.trackerRep, 0.1f, newRelationship.intensity);
                    break;
            }
        }
    }
}
