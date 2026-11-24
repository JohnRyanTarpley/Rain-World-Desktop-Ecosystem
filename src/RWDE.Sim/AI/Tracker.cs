using System.Collections.Generic;

namespace RWDE.Sim
{
    // PLACEHOLDER: replaced by the port of the game's Tracker. Fork B's members are stubs only.
    public class Tracker : AIModule
    {
        public Tracker(ArtificialIntelligence AI) : base(AI) { }
        public NoiseTracker noiseTracker;
        public int maxTrackedCreatures = 10;
        public readonly List<CreatureRepresentation> creatures = new List<CreatureRepresentation>();
        public int CreaturesCount => creatures.Count;
        public CreatureRepresentation GetRep(int index) => creatures[index];
        public CreatureRepresentation RepresentationForCreature(AbstractCreature crit, bool addIfMissing) => null;
        public CreatureRepresentation RepresentationForObject(PhysicalObject obj, bool AddIfMissing) => null;

        public abstract class CreatureRepresentation
        {
            protected CreatureRepresentation(Tracker parent, AbstractCreature representedCreature, float priority)
            {
                this.parent = parent;
                this.representedCreature = representedCreature;
            }
            public Tracker parent;
            public AbstractCreature representedCreature;
            public RelationshipTracker.DynamicRelationship dynamicRelationship;
            public bool deleteMeNextFrame;
            public bool VisualContact;
            public int TicksSinceSeen;
            public WorldCoordinate lastSeenCoord;
            public virtual WorldCoordinate BestGuessForPosition() => lastSeenCoord;
            public void HeardThisCreature() { }
        }

        public class SimpleCreatureRepresentation : CreatureRepresentation
        {
            public SimpleCreatureRepresentation(Tracker parent, AbstractCreature representedCreature, float priority, bool forgetWhenNotVisible)
                : base(parent, representedCreature, priority) { }
        }

        public class ElaborateCreatureRepresentation : CreatureRepresentation
        {
            public ElaborateCreatureRepresentation(Tracker parent, AbstractCreature representedCreature, float priority, int numberOfGhosts)
                : base(parent, representedCreature, priority) { }
            public List<Ghost> ghosts = new List<Ghost>();
            public bool bestGhostDirty;
        }

        public class Ghost
        {
            public WorldCoordinate coord;
            public WorldCoordinate lastCoord;
            public Vector2 vel;
            public int generation;
            public void Move(WorldCoordinate newCoord, Vector2 newPos) { }
            public Ghost Clone(WorldCoordinate newCoord, int generation) => this;
        }
    }
}
