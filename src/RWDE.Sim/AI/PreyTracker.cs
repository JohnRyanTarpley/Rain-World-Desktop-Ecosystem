using System;
using System.Collections.Generic;

namespace RWDE.Sim
{
    /// <summary>
    /// The creatures a creature hunts and which one it is after, ported from the game's
    /// <c>PreyTracker</c>. Prey is ranked by relationship intensity, distance, how likely it is to be
    /// found and whether the pathfinder can reach it; frustration builds while the chosen prey can't be reached.
    /// </summary>
    /// <remarks>
    /// Not ported: mud on prey (the desktop has no mud) and the Artificer scavenger rule (no story
    /// campaigns are played). Remix (<c>ModManager.MMF</c>) is on, so prey held by another creature counts
    /// with the template's interest in other creatures' catches.
    /// </remarks>
    public class PreyTracker : AIModule
    {
        /// <summary>One hunted creature (<c>PreyTracker.TrackedPrey</c>).</summary>
        public class TrackedPrey
        {
            public TrackedPrey(PreyTracker owner, Tracker.CreatureRepresentation critRep)
            {
                this.owner = owner;
                this.critRep = critRep;
            }

            public PreyTracker owner;
            public Tracker.CreatureRepresentation critRep;
            public int unreachableCounter;
            public int atPositionButCantSeeCounter;
            public WorldCoordinate lastBestGuessPos;

            private ArtificialIntelligence AI => owner.AI;

            /// <summary>0 when the prey seems out of reach for a while or is never where it should be, else 1.</summary>
            public float Reachable
            {
                get
                {
                    var guess = critRep.BestGuessForPosition();
                    if (guess.room == AI.creature.pos.room && AI.creature.Room.realizedRoom != null && AI.creature.Room.realizedRoom.GetTile(guess.Tile).Solid)
                        return 0f;
                    float reachable = owner.giveUpOnUnreachablePrey >= 0
                        ? Mathf.InverseLerp(owner.giveUpOnUnreachablePrey, 0f, unreachableCounter) * Mathf.InverseLerp(200f, 100f, atPositionButCantSeeCounter)
                        : 1f;
                    var preyRoom = AI.creature.world.GetAbstractRoom(guess);
                    if (preyRoom != null && preyRoom.AttractionValueForCreature(AI.creature) < AI.creature.Room.AttractionValueForCreature(AI.creature))
                        reachable *= 0.5f;
                    return reachable;
                }
            }

            public void Update()
            {
                var guess = critRep.BestGuessForPosition();
                if (AI.pathFinder != null && AI.pathFinder.DoneMappingAccessibility)
                {
                    bool reachable = AI.pathFinder.CoordinateReachableAndGetbackable(guess);
                    for (int i = 0; i < 4 && !reachable; i++)
                        reachable = AI.pathFinder.CoordinateReachableAndGetbackable(guess + Custom.fourDirections[i]);
                    if (reachable)
                        unreachableCounter = 0;
                    else
                        unreachableCounter++;
                }
                // The hunter is where it thinks the prey is and sees the spot, but not the prey.
                if (lastBestGuessPos == guess && AI.creature.pos.room == guess.room && AI.creature.pos.Tile.FloatDist(guess.Tile) < 5f
                    && AI.pathFinder != null && AI.pathFinder.GetDestination.room == guess.room && AI.pathFinder.GetDestination.Tile.FloatDist(guess.Tile) < 5f
                    && AI.creature.Room.realizedRoom != null && AI.creature.Room.realizedRoom.VisualContact(AI.creature.pos, guess))
                    atPositionButCantSeeCounter += 5;
                else
                    atPositionButCantSeeCounter--;
                atPositionButCantSeeCounter = Custom.IntClamp(atPositionButCantSeeCounter, 0, 200);
                lastBestGuessPos = guess;
            }

            /// <summary>Whether the pathfinder reaches the prey's tile, a neighbour, or a tile two steps off.</summary>
            public bool PathFinderCanGetToPrey()
            {
                var wc = critRep.BestGuessForPosition();
                for (int i = 0; i < 9; i++)
                {
                    var c = wc + Custom.eightDirectionsAndZero[i];
                    if (AI.pathFinder.CoordinateReachable(c) && AI.pathFinder.CoordinatePossibleToGetBackFrom(c))
                        return true;
                }
                for (int j = 0; j < 4; j++)
                {
                    var c = wc + Custom.fourDirections[j] * 2;
                    if (AI.pathFinder.CoordinateReachable(c) && AI.pathFinder.CoordinatePossibleToGetBackFrom(c))
                        return true;
                }
                return false;
            }

            public float Attractiveness()
            {
                float attractiveness = AI.DynamicRelationship(critRep).intensity;
                // num2 in the game: how much mud on the prey stands out. No mud on the desktop.
                const float mud = 0f;
                var guess = critRep.BestGuessForPosition();
                float dist = owner.DistanceEstimation(AI.creature.pos, guess, critRep.representedCreature.creatureTemplate);
                dist = Mathf.Pow(dist, 1.5f);
                dist = Mathf.Lerp(dist, 1f, 0.5f);
                if (AI.pathFinder != null)
                {
                    if (!AI.pathFinder.CoordinateReachable(guess))
                        attractiveness /= 2f - mud * 2f;
                    if (!AI.pathFinder.CoordinatePossibleToGetBackFrom(guess))
                        attractiveness /= 2f;
                    if (!PathFinderCanGetToPrey())
                        attractiveness /= 2f - mud * 2f;
                }
                attractiveness *= Mathf.Lerp(critRep.EstimatedChanceOfFinding, 1f, mud);
                attractiveness *= Mathf.Lerp(Reachable, 1f, mud);
                var realized = critRep.representedCreature.realizedCreature;
                if (StaticWorld.Remix && realized != null && realized.grabbedBy.Count > 0)
                {
                    var template = AI.creature.creatureTemplate;
                    attractiveness *= template.TopAncestor() != realized.grabbedBy[0].grabber.abstractCreature.creatureTemplate.TopAncestor()
                        ? template.interestInOtherCreaturesCatches
                        : template.interestInOtherAncestorsCatches;
                }
                if (guess.room != AI.creature.pos.room)
                {
                    var room = AI.creature.world.GetAbstractRoom(guess);
                    float a = Mathf.InverseLerp(0f, 0.5f, room != null ? room.AttractionValueForCreature(AI.creature) : 0f);
                    attractiveness *= Mathf.Lerp(a, 1f, mud);
                }
                attractiveness /= dist;
                attractiveness *= Mathf.InverseLerp(owner.giveUpOnGhostGeneration, owner.giveUpOnGhostGeneration / 2, critRep.LowestGenerationAvailable);
                return attractiveness;
            }
        }

        public PreyTracker(ArtificialIntelligence AI, int maxRememberedCreatures, float persistanceBias, float sureToGetPreyDistance,
            float sureToLosePreyDistance, float successEstimationDistReliance) : base(AI)
        {
            this.maxRememberedCreatures = maxRememberedCreatures;
            this.persistanceBias = persistanceBias;
            this.sureToGetPreyDistance = sureToGetPreyDistance;
            this.sureToLosePreyDistance = sureToLosePreyDistance;
            frustrationSpeed = 0.0125f;
        }

        private readonly int maxRememberedCreatures;
        private readonly float persistanceBias;
        private readonly List<TrackedPrey> prey = new List<TrackedPrey>();
        private TrackedPrey currentPrey;
        private readonly float sureToGetPreyDistance;
        private readonly float sureToLosePreyDistance;
        public float frustration;
        private readonly float frustrationSpeed;
        /// <summary>Ticks out of reach before prey stops being worth chasing; -1 never gives up.</summary>
        public int giveUpOnUnreachablePrey = 400;
        public int giveUpOnGhostGeneration = 50;

        public AImap aimap => AI.creature.realizedCreature.room?.aimap;

        public int TotalTrackedPrey => prey.Count;

        public Tracker.CreatureRepresentation MostAttractivePrey => currentPrey?.critRep;

        public Tracker.CreatureRepresentation GetTrackedPrey(int index) => prey[index].critRep;

        public override float Utility()
        {
            if (currentPrey == null)
                return 0f;
            var guess = currentPrey.critRep.BestGuessForPosition();
            var abstractAI = AI.creature.abstractAI;
            if (abstractAI.WantToMigrate && guess.room != abstractAI.MigrationDestination.room && guess.room != AI.creature.pos.room)
                return 0f;
            float dist = DistanceEstimation(AI.creature.pos, guess, AI.creature.creatureTemplate);
            dist = Mathf.Lerp(1f - Mathf.InverseLerp(sureToGetPreyDistance, sureToLosePreyDistance, dist),
                Mathf.Lerp(sureToGetPreyDistance, sureToLosePreyDistance, 0.25f) / dist, 0.5f);
            return Mathf.Min(dist, currentPrey.critRep.EstimatedChanceOfFinding
                                   * Mathf.InverseLerp(giveUpOnGhostGeneration, giveUpOnGhostGeneration / 2, currentPrey.critRep.LowestGenerationAvailable))
                   * Mathf.Pow(AI.DynamicRelationship(currentPrey.critRep).intensity, 0.75f) * currentPrey.Reachable;
        }

        public void AddPrey(Tracker.CreatureRepresentation creature)
        {
            foreach (var item in prey)
                if (item.critRep == creature)
                    return;
            prey.Add(new TrackedPrey(this, creature));
            if (prey.Count > maxRememberedCreatures)
            {
                float lowest = float.MaxValue;
                TrackedPrey drop = null;
                foreach (var item in prey)
                {
                    float a = item.Attractiveness();
                    if (a < lowest)
                    {
                        lowest = a;
                        drop = item;
                    }
                }
                if (drop != null)
                {
                    if (AI.relationshipTracker != null)
                        AI.relationshipTracker.ModuleHasAbandonedCreature(drop.critRep, this);
                    else
                        drop.critRep.Destroy();
                    prey.Remove(drop);
                }
            }
            Update();
        }

        public void ForgetPrey(AbstractCreature crit)
        {
            for (int i = prey.Count - 1; i >= 0; i--)
                if (prey[i].critRep.representedCreature == crit)
                    prey.RemoveAt(i);
        }

        public void ForgetAllPrey()
        {
            prey.Clear();
            currentPrey = null;
        }

        public override void Update()
        {
            float best = float.MinValue;
            TrackedPrey chosen = null;
            for (int i = prey.Count - 1; i >= 0; i--)
            {
                prey[i].Update();
                float a = prey[i].Attractiveness();
                prey[i].critRep.forgetCounter = 0;
                if (prey[i] == currentPrey)
                    a *= persistanceBias;
                if (prey[i].critRep.deleteMeNextFrame)
                {
                    prey.RemoveAt(i);
                }
                else if (a > best)
                {
                    best = a;
                    chosen = prey[i];
                }
            }
            currentPrey = chosen;
            if (currentPrey != null && AI.pathFinder != null && AI.creature.pos.room == currentPrey.critRep.BestGuessForPosition().room && !currentPrey.PathFinderCanGetToPrey())
                frustration = Mathf.Clamp(frustration + frustrationSpeed, 0f, 1f);
            else
                frustration = Mathf.Clamp(frustration - frustrationSpeed * 4f, 0f, 1f);
        }

        /// <summary>
        /// Tiles between two coordinates for <paramref name="crit"/>: straight-line within a room, else the
        /// walk to the exit toward the other room, or 50 (<c>PreyTracker.DistanceEstimation</c>).
        /// </summary>
        /// <remarks>
        /// The game caps the walk at 50 for creatures that use sky highways between rooms with sky access;
        /// desktop rooms have no sky exit nodes, so that never applies.
        /// </remarks>
        public float DistanceEstimation(WorldCoordinate from, WorldCoordinate to, CreatureTemplate crit)
        {
            if (from.room != to.room)
            {
                var fromRoom = AI.creature.world.GetAbstractRoom(from);
                if (fromRoom != null && fromRoom.realizedRoom != null && fromRoom.realizedRoom.readyForAI && fromRoom.ExitIndex(to.room) > -1)
                {
                    int specific = fromRoom.CommonToCreatureSpecificNodeIndex(fromRoom.ExitIndex(to.room), crit);
                    int dist = specific < 0 || crit.PreBakedPathingIndex == -1
                        ? -1
                        : fromRoom.realizedRoom.aimap.ExitDistanceForCreatureAndCheckNeighbours(from.Tile, specific, crit);
                    if (dist > -1)
                        return dist;
                }
                return 50f;
            }
            return Vector2.Distance(IntVector2.ToVector2(from.Tile), IntVector2.ToVector2(to.Tile));
        }
    }
}
