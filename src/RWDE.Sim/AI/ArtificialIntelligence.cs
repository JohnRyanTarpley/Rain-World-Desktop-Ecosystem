using System.Collections.Generic;

namespace RWDE.Sim
{
    /// <summary>
    /// Base of every realized creature AI, ported from the game's <c>ArtificialIntelligence</c>: the
    /// module list (each module type gets its own property), sight, relationships and the hooks the
    /// <see cref="PathFinder"/> and the trackers call.
    /// </summary>
    /// <remarks>
    /// Not ported: the item, obstacle, injury and friend trackers and secondary node finders (no species
    /// ported so far uses them on the desktop; friends need a player), ripple pathing, the Expedition
    /// "hunted" unlock, and vision obscurers. The desktop has no water, so the water terms of
    /// <see cref="VisualScore"/> never apply.
    /// </remarks>
    public abstract class ArtificialIntelligence
    {
        protected ArtificialIntelligence(AbstractCreature creature)
        {
            this.creature = creature;
            creature.abstractAI.RealAI = this;
        }

        public readonly AbstractCreature creature;
        public List<AIModule> modules = new List<AIModule>();
        /// <summary>Set when the creature can't reach any of its room's nodes.</summary>
        public bool stranded;
        public bool seeThroughWalls;
        public int timeInRoom;
        public int lastRoom = -1;

        public PathFinder pathFinder { get; private set; }
        public Tracker tracker { get; private set; }
        public NoiseTracker noiseTracker => tracker?.noiseTracker;
        public ThreatTracker threatTracker { get; private set; }
        public PreyTracker preyTracker { get; private set; }
        public RainTracker rainTracker { get; private set; }
        public DenFinder denFinder { get; private set; }
        public AgressionTracker agressionTracker { get; private set; }
        public UtilityComparer utilityComparer { get; private set; }
        public MissionTracker missionTracker { get; private set; }
        public RelationshipTracker relationshipTracker { get; private set; }
        public DiscomfortTracker discomfortTracker { get; private set; }
        public StuckTracker stuckTracker { get; private set; }

        /// <summary>The world's seeded randomness, standing in for <c>UnityEngine.Random</c>.</summary>
        public System.Random Random => creature.world.random;

        public void AddModule(AIModule module)
        {
            modules.Add(module);
            switch (module)
            {
                case PathFinder m: pathFinder = m; break;
                case Tracker m: tracker = m; break;
                case ThreatTracker m: threatTracker = m; break;
                case PreyTracker m: preyTracker = m; break;
                case RainTracker m: rainTracker = m; break;
                case DenFinder m: denFinder = m; break;
                case UtilityComparer m: utilityComparer = m; break;
                case AgressionTracker m: agressionTracker = m; break;
                case MissionTracker m: missionTracker = m; break;
                case RelationshipTracker m: relationshipTracker = m; break;
                case DiscomfortTracker m: discomfortTracker = m; break;
                case StuckTracker m: stuckTracker = m; break;
            }
        }

        // ---- Sight (ArtificialIntelligence.VisualContact / VisualScore) ----

        public bool VisualContact(WorldCoordinate lookAtCoord, float bonus) =>
            lookAtCoord.room == creature.pos.room && lookAtCoord.TileDefined
            && VisualContact(creature.realizedCreature.room.MiddleOfTile(lookAtCoord.Tile), bonus);

        /// <summary>Whether the creature sees <paramref name="lookAtPoint"/>: in range, scored above 0, and no terrain in between.</summary>
        public bool VisualContact(Vector2 lookAtPoint, float bonus)
        {
            if (creature.realizedCreature.Blinded)
                return false;
            if (VisualScore(lookAtPoint, bonus) <= 0f)
                return false;
            return creature.Room.realizedRoom.VisualContact(creature.realizedCreature.VisionPoint, lookAtPoint);
        }

        public bool VisualContact(BodyChunk chunk) =>
            VisualContact(chunk.pos, chunk.VisibilityBonus(creature.creatureTemplate.movementBasedVision));

        /// <summary>How well the creature sees a point, 1 up close falling to 0 at its visual radius (<c>VisualScore</c>).</summary>
        public virtual float VisualScore(Vector2 lookAtPoint, float bonus)
        {
            var realized = creature.realizedCreature;
            if (realized == null || realized.room == null)
                return 0f;
            float radius = creature.creatureTemplate.visualRadius * (1f + bonus);
            if (!Custom.DistLess(realized.VisionPoint, lookAtPoint, radius))
                return 0f;
            float score = Mathf.InverseLerp(radius, 0f, Vector2.Distance(realized.VisionPoint, lookAtPoint));
            if (realized.room.aimap.getAItile(lookAtPoint).narrowSpace)
                score -= 0.5f;
            return score;
        }

        public virtual void CreatureSpotted(bool firstSpot, Tracker.CreatureRepresentation otherCreature)
        {
        }

        public void SetDestination(WorldCoordinate destination) => pathFinder?.SetDestination(destination);

        public virtual void Update()
        {
            timeInRoom++;
            for (int i = 0; i < modules.Count; i++)
                modules[i].Update();
        }

        public virtual void NewRoom(Room room)
        {
            if (lastRoom == room.abstractRoom.index)
                return;
            lastRoom = room.abstractRoom.index;
            timeInRoom = 0;
            for (int i = 0; i < modules.Count; i++)
                modules[i].NewRoom(room);
            if (creature.abstractAI.destination.room == room.abstractRoom.index && !creature.abstractAI.destination.TileDefined)
                SetDestination(new WorldCoordinate(room.abstractRoom.index, creature.world.random.Next(0, room.TileWidth),
                    creature.world.random.Next(0, room.TileHeight), -1));
        }

        /// <summary>Lets the AI adjust what a step costs: tiles the discomfort tracker dislikes cost more.</summary>
        public virtual PathCost TravelPreference(MovementConnection coord, PathCost cost)
        {
            if (discomfortTracker != null && coord.destinationCoord.TileDefined)
            {
                float discomfort = discomfortTracker.DiscomfortOfTile(coord.destinationCoord);
                if (discomfort > 0.75f)
                    cost += new PathCost(discomfort * 10f, PathCost.Legality.Unwanted);
                else
                    cost.resistance += discomfort * 10f;
            }
            return cost;
        }

        /// <summary>Called when the pathfinder has mapped a new area; den finders start over if the den is out of reach.</summary>
        public virtual void NewArea(bool strandedFromExits)
        {
            stranded = strandedFromExits;
            if (denFinder != null && (pathFinder == null || !DenPosition().HasValue || !pathFinder.CoordinateReachable(DenPosition().Value)))
                denFinder.ResetMapping(strandedFromExits);
        }

        public WorldCoordinate? DenPosition() => denFinder != null ? denFinder.GetDenPosition() : creature.abstractAI.denPosition;

        public virtual bool WantToStayInDenUntilEndOfCycle() => false;

        public virtual Tracker.CreatureRepresentation CreateTrackerRepresentationForCreature(AbstractCreature otherCreature) =>
            new Tracker.SimpleCreatureRepresentation(tracker, otherCreature, 0f, true);

        // ---- Relationships ----

        public CreatureTemplate.Relationship DynamicRelationship(AbstractCreature absCrit) => DynamicRelationship(null, absCrit);

        public CreatureTemplate.Relationship DynamicRelationship(Tracker.CreatureRepresentation rep) => DynamicRelationship(rep, null);

        /// <summary>How the creature currently regards another: the relationship tracker's view if it tracks it, else the template's.</summary>
        public CreatureTemplate.Relationship DynamicRelationship(Tracker.CreatureRepresentation rep, AbstractCreature absCrit)
        {
            if (rep == null)
                rep = tracker.RepresentationForCreature(absCrit, false);
            if (rep == null)
                return StaticRelationship(absCrit);
            if (rep.dynamicRelationship != null)
                return rep.dynamicRelationship.currentRelationship;
            return StaticRelationship(rep.representedCreature);
        }

        public CreatureTemplate.Relationship StaticRelationship(AbstractCreature otherCreature) =>
            creature.creatureTemplate.CreatureRelationship(otherCreature.creatureTemplate);

        public virtual bool TrackerToDiscardDeadCreature(AbstractCreature crit) =>
            DynamicRelationship(crit).type != CreatureTemplate.Relationship.Type.Eats;

        public virtual void HeardNoise(InGameNoise noise) => noiseTracker?.HeardNoise(noise);

        public virtual float CurrentPlayerAggression(AbstractCreature player) => 1f;
    }

    /// <summary>A part of a creature's AI, updated by it each tick. Mirrors the game's <c>AIModule</c>.</summary>
    public class AIModule
    {
        public AIModule(ArtificialIntelligence AI)
        {
            this.AI = AI;
        }

        public ArtificialIntelligence AI;

        public virtual void Update()
        {
        }

        public virtual void NewRoom(Room room)
        {
        }

        public virtual float Utility() => 0f;
    }
}
