using System;
using Rel = RWDE.Sim.CreatureTemplate.Relationship.Type;

namespace RWDE.Sim
{
    /// <summary>
    /// Every lizard's AI, ported from the game's <c>LizardAI</c>. Each tick a <see cref="UtilityComparer"/>
    /// weighs the modules (threats, prey, rain, injury, rivals, missions, noises) and the winner sets the
    /// <see cref="Behavior"/>: fleeing, hunting with lunges and bites, fighting rivals, idling in a liked
    /// spot, going home, and so on. The behaviour sets the pathfinder's destination and the run speed
    /// the <see cref="Lizard"/> body moves at.
    /// </summary>
    /// <remarks>
    /// Not ported, as the desktop has no player: the friend tracker (taming), likes of players and social
    /// events. Without a friend the game's friend tracker has no utility and keeps rain-fear on, which is
    /// what happens here. The lurk tracker (White Lizard, Salamander, Eel Lizard), Yellow pack signals
    /// (<c>YellowAI</c>) and the spit tracker (Red and Caramel) arrive with the breed specials. A realized
    /// lizard doesn't yet roll <c>AbstractCreatureAI.AbstractBehavior</c> to migrate on its own; leaving
    /// rooms through their exits comes with the rain cycle and the abstract world.
    /// </remarks>
    public class LizardAI : ArtificialIntelligence, IUseARelationshipTracker, IAINoiseReaction
    {
        // LizardAI.Behavior
        public enum Behavior
        {
            Idle,
            Hunt,
            Flee,
            Travelling,
            EscapeRain,
            ReturnPrey,
            Injured,
            Fighting,
            Frustrated,
            ActingOutMission,
            Lurk,
            InvestigateSound,
            GoToSpitPos,
            FollowFriend
        }

        // LizardAI.LizardCommunication
        public enum LizardCommunication
        {
            Agression,
            Submission
        }

        /// <summary>How badly hurt the lizard is, which sends it home (<c>LizardAI.LizardInjuryTracker</c>).</summary>
        private class LizardInjuryTracker : AIModule
        {
            private readonly Lizard lizard;

            public LizardInjuryTracker(ArtificialIntelligence AI, Lizard lizard) : base(AI)
            {
                this.lizard = lizard;
            }

            public override float Utility() =>
                Custom.SCurve(Mathf.InverseLerp(0.2f, 0.9f, 1f - Mathf.Clamp(lizard.LizardState.health, 0f, 1f)), 0.01f);
        }

        public LizardAI(AbstractCreature creature, World world) : base(creature)
        {
            lizard = (Lizard)creature.realizedCreature;
            lizard.AI = this;
            AddModule(new LizardPather(this, world, creature));
            if (creature.creatureTemplate.type == CreatureTemplate.Type.RedLizard || creature.creatureTemplate.type == CreatureTemplate.Type.SpitLizard)
                pathFinder.stepsPerFrame = 30;
            AddModule(new Tracker(this, 10, 10, -1, 0.35f, 5, 5, 10));
            AddModule(new NoiseTracker(this, tracker));
            AddModule(new DenFinder(this, creature));
            AddModule(new RainTracker(this));
            AddModule(new PreyTracker(this, 5, 2f, 3f, 70f, 0.5f));
            if (lizard.Template.type == CreatureTemplate.Type.RedLizard)
                preyTracker.giveUpOnUnreachablePrey = 1800;
            AddModule(new ThreatTracker(this, 3));
            AddModule(new AgressionTracker(this, 0.001f, 0.001f));
            AddModule(new UtilityComparer(this));
            AddModule(new MissionTracker(this));
            AddModule(new RelationshipTracker(this, tracker));
            AddModule(new StuckTracker(this, true, true));
            AddModule(new DiscomfortTracker(this, tracker, 0f));
            stuckTracker.totalTrackedLastPositions = 40;
            stuckTracker.checkPastPositionsFrom = 30;
            stuckTracker.pastPosStuckDistance = 5;
            stuckTracker.pastStuckPositionsCloseToIncrementStuckCounter = 10;
            stuckTracker.AddSubModule(new StuckTracker.MoveBacklog(stuckTracker));

            FloatTweener.FloatTween smoother = new FloatTweener.FloatTweenUpAndDown(
                new FloatTweener.FloatTweenBasic(FloatTweener.TweenType.Lerp, 0.5f),
                new FloatTweener.FloatTweenBasic(FloatTweener.TweenType.Tick, 0.0025f));
            utilityComparer.AddComparedModule(threatTracker, smoother, 1f, 1f);
            utilityComparer.AddComparedModule(preyTracker, new FloatTweener.FloatTweenBasic(FloatTweener.TweenType.Lerp, 0.15f), 0.6f, 1f);
            utilityComparer.AddComparedModule(rainTracker, null, 0.9f, 1f);
            utilityComparer.AddComparedModule(new LizardInjuryTracker(this, lizard), null,
                creature.creatureTemplate.type == CreatureTemplate.Type.RedLizard ? 0.4f : 0.9f, 1f);
            utilityComparer.AddComparedModule(agressionTracker, null, 0.5f, 1.2f);
            utilityComparer.AddComparedModule(missionTracker, null, 0.8f, 1f);
            utilityComparer.AddComparedModule(noiseTracker, null, 0.2f, 1.2f);
            behavior = Behavior.Idle;
            panic = 0;
            excitement = 0.5f;
        }

        private readonly Lizard lizard;

        public float fear;
        public float hunger;
        public float rainFear;
        public float excitement;
        public float runSpeed;
        public int panic;
        public int lastDistressLength;
        public float currentUtility;
        public int idleCounter;
        public int idleSpotWinStreak;
        private WorldCoordinate forbiddenIdleSpot = new WorldCoordinate(-1, -1, -1, -1);
        public int idleRestlessness;
        public int unableToFindComfortablePosition;
        /// <summary>The creature the lizard is attending to (prey, threat or rival).</summary>
        public Tracker.CreatureRepresentation focusCreature;
        public Tracker.CreatureRepresentation casualAggressionTarget;
        /// <summary>The lizard this one gave in to in a dominance display.</summary>
        public AbstractCreature submittedTo;
        public int attemptBiteFrames;
        public Behavior behavior;

        public float CombinedFear => Mathf.Max(fear, Mathf.Pow(rainFear, 3f));

        // LizardAI.TravelUtility: how much the lizard wants to go on to the room it is migrating to.
        private float TravelUtility
        {
            get
            {
                if (!creature.abstractAI.WantToMigrate)
                    return 0f;
                if (!pathFinder.CoordinateReachable(creature.abstractAI.MigrationDestination))
                    return 0f;
                float here = lizard.room.abstractRoom.AttractionValueForCreature(creature);
                float there = lizard.room.world.GetAbstractRoom(creature.abstractAI.MigrationDestination).AttractionValueForCreature(creature);
                if (here > there)
                    return 0f;
                float utility = Mathf.Lerp(0.99f, 0.1f, Mathf.Pow(here, 0.25f));
                if (here == there)
                    return utility * 0.5f;
                return utility;
            }
        }

        private Behavior DetermineBehavior()
        {
            var result = Behavior.Idle;
            var module = utilityComparer.HighestUtilityModule();
            currentUtility = utilityComparer.HighestUtility();
            switch (module)
            {
                case ThreatTracker _: result = Behavior.Flee; break;
                case RainTracker _: result = Behavior.EscapeRain; break;
                case PreyTracker _: result = Behavior.Hunt; break;
                case LizardInjuryTracker _: result = Behavior.Injured; break;
                case AgressionTracker _: result = Behavior.Fighting; break;
                case MissionTracker _: result = Behavior.ActingOutMission; break;
                case NoiseTracker _: result = Behavior.InvestigateSound; break;
            }
            if (currentUtility < 0.05f)
            {
                currentUtility = 0.05f;
                result = Behavior.Idle;
            }
            if (result != Behavior.Flee && lizard.grasps[0] != null && lizard.grasps[0].grabbed is Creature held
                && DynamicRelationship(held.abstractCreature).type == Rel.Eats)
            {
                result = Behavior.ReturnPrey;
                currentUtility = 1f;
            }
            var prey = preyTracker.MostAttractivePrey;
            if (result == Behavior.Hunt && prey != null && preyTracker.frustration > 0.75f && lizard.Template.type != CreatureTemplate.Type.RedLizard
                && ((prey.EstimatedChanceOfFinding > 0.4f && Custom.DistLess(lizard.mainBodyChunk.pos, lizard.room.MiddleOfTile(prey.BestGuessForPosition()), 350f))
                    || creature.pos.room != prey.BestGuessForPosition().room))
                result = Behavior.Frustrated;
            if (stranded && (result == Behavior.ReturnPrey || result == Behavior.EscapeRain))
                result = Behavior.Frustrated;
            if (result != Behavior.ReturnPrey && result != Behavior.Injured && result != Behavior.EscapeRain && result != Behavior.Flee
                && result != Behavior.ActingOutMission && currentUtility < TravelUtility)
            {
                currentUtility = TravelUtility;
                result = Behavior.Travelling;
            }
            return result;
        }

        public override void NewRoom(Room room)
        {
            attemptBiteFrames = 0;
            base.NewRoom(room);
        }

        public override void Update()
        {
            if (panic > 0)
                panic--;
            var den = denFinder.GetDenPosition();
            pathFinder.walkPastPointOfNoReturn = stranded || !den.HasValue || !pathFinder.CoordinatePossibleToGetBackFrom(den.Value);
            fear = utilityComparer.GetSmoothedNonWeightedUtility(threatTracker);
            hunger = utilityComparer.GetSmoothedNonWeightedUtility(preyTracker);
            rainFear = utilityComparer.GetSmoothedNonWeightedUtility(rainTracker);
            excitement = Mathf.Lerp(excitement, Mathf.Max(hunger, CombinedFear), 0.1f);
            if (fear > 0.8f)
                lastDistressLength = Custom.IntClamp(lastDistressLength + 1, 0, 500);
            else if (fear < 0.2f)
                lastDistressLength = 0;
            ((FloatTweener.FloatTweenBasic)((FloatTweener.FloatTweenUpAndDown)utilityComparer.GetUtilityTracker(threatTracker).smoother).down).speed =
                1f / ((lastDistressLength + 20) * 3f);
            // The desktop plays the game's story-mode weighting.
            utilityComparer.GetUtilityTracker(agressionTracker).weight = 0.25f * lizard.LizardState.health;
            utilityComparer.GetUtilityTracker(rainTracker).weight = 0.9f;
            behavior = DetermineBehavior();
            lizard.JawOpen -= 0.01f;

            switch (behavior)
            {
                case Behavior.Flee:
                {
                    var destination = threatTracker.FleeTo(creature.pos, 5, 20, fear > 1f / 3f);
                    creature.abstractAI.SetDestination(destination);
                    if (threatTracker.ThreatOfTile(creature.pos, true) > 1f)
                    {
                        if (panic < 40)
                            panic = 40;
                        else
                            panic += 2;
                        if (Random.Value() < 0.05f && threatTracker.mostThreateningCreature != null
                            && threatTracker.mostThreateningCreature.BestGuessForPosition().Tile.FloatDist(creature.pos.Tile) < 15f)
                            lizard.EnterAnimation(Lizard.Animation.ThreatSpotted, false);
                    }
                    runSpeed = Mathf.Lerp(runSpeed, Mathf.Pow(CombinedFear, 0.1f), 0.5f);
                    focusCreature = threatTracker.mostThreateningCreature;
                    break;
                }
                case Behavior.Travelling:
                    runSpeed = Mathf.Lerp(runSpeed, 0.5f + 0.5f * TravelUtility, 0.01f);
                    creature.abstractAI.SetDestination(creature.abstractAI.MigrationDestination);
                    break;
                case Behavior.Hunt:
                    if (preyTracker.MostAttractivePrey != null)
                        AggressiveBehavior(preyTracker.MostAttractivePrey, 1f);
                    runSpeed = Mathf.Lerp(runSpeed, Mathf.Max(hunger, 0.3f), 0.1f);
                    break;
                case Behavior.Fighting:
                    focusCreature = agressionTracker.AgressionTarget();
                    if (focusCreature != null)
                    {
                        var rival = focusCreature.representedCreature.realizedCreature;
                        if (focusCreature.VisualContact && Custom.DistLess(lizard.mainBodyChunk.pos, rival.mainBodyChunk.pos, 160f)
                            && (!(rival is Lizard) || Vector2.Distance(lizard.mainBodyChunk.pos, rival.mainBodyChunk.pos) < Vector2.Distance(lizard.mainBodyChunk.pos, rival.bodyChunks[2].pos)))
                        {
                            if (Random.Value() < 0.05f)
                                lizard.EnterAnimation(Lizard.Animation.FightingStance, false);
                            runSpeed = Mathf.Lerp(runSpeed, 0.2f, 0.15f);
                        }
                        else
                        {
                            runSpeed = Mathf.Lerp(runSpeed, 0.8f, 0.15f);
                            if (Random.Value() < 1f / 180f)
                                lizard.EnterAnimation(Lizard.Animation.FightingStance, false);
                        }
                        if (rival is Lizard rivalLizard && Random.Value() < 0.05f && Random.Value() < DynamicRelationship(focusCreature).intensity)
                            SendCommunication(rivalLizard, LizardCommunication.Agression);
                        AggressiveBehavior(focusCreature, 0.025f);
                    }
                    break;
                case Behavior.Idle:
                    IdleBehavior();
                    break;
                case Behavior.EscapeRain:
                    if (den.HasValue)
                        creature.abstractAI.SetDestination(den.Value);
                    runSpeed = Mathf.Lerp(runSpeed, 1f, 0.1f);
                    break;
                case Behavior.ReturnPrey:
                    if (den.HasValue)
                        creature.abstractAI.SetDestination(den.Value);
                    runSpeed = Mathf.Lerp(runSpeed, Mathf.Pow(Mathf.Max(CombinedFear, 0.5f), 0.5f), 0.1f);
                    break;
                case Behavior.Injured:
                    if (den.HasValue)
                        creature.abstractAI.SetDestination(den.Value);
                    runSpeed = Mathf.Lerp(runSpeed, 1f, 0.01f);
                    break;
                case Behavior.Frustrated:
                    focusCreature = preyTracker.MostAttractivePrey;
                    runSpeed = Mathf.Lerp(runSpeed, 1f, 0.5f);
                    if (Random.Value() < 0.1f || (Custom.ManhattanDistance(creature.pos.Tile, creature.abstractAI.destination.Tile) < 10 && creature.pos.room == creature.abstractAI.destination.room))
                    {
                        var spot = new WorldCoordinate(creature.Room.index, Random.Range(0, lizard.room.TileWidth), Random.Range(0, lizard.room.TileHeight), -1);
                        if (pathFinder.CoordinateReachable(spot) && pathFinder.CoordinatePossibleToGetBackFrom(spot))
                        {
                            creature.abstractAI.SetDestination(spot);
                            idleRestlessness = Random.Range(30, 100);
                        }
                    }
                    if (Random.Value() < 0.0125f)
                        lizard.EnterAnimation(Lizard.Animation.PreyReSpotted, false);
                    if (Random.Value() < 0.0125f)
                        lizard.bodyWiggleCounter = Math.Max(lizard.bodyWiggleCounter, (int)(Random.Value() * 100f));
                    break;
                case Behavior.ActingOutMission:
                    missionTracker.ActOnMostImportantMission();
                    runSpeed = Mathf.Lerp(runSpeed, 0.6f, 0.05f);
                    break;
                case Behavior.InvestigateSound:
                    creature.abstractAI.SetDestination(noiseTracker.ExaminePos);
                    runSpeed = Mathf.Lerp(runSpeed, 0.6f, 0.2f);
                    break;
            }
            if (behavior != Behavior.Idle && creature.creatureTemplate.type == CreatureTemplate.Type.RedLizard)
                runSpeed = Mathf.Pow(runSpeed, 0.7f);
            runSpeed = Mathf.Max(runSpeed, stuckTracker.Utility());
            noiseTracker.hearingSkill = Custom.LerpMap(runSpeed, 0f, 0.6f, 1.3f, 0.6f);

            // Snap at anything bite-worthy in reach, prey or not.
            if (lizard.grasps[0] == null && tracker.CreaturesCount > 0)
            {
                var rep = tracker.GetRep(Random.Range(0, tracker.CreaturesCount));
                if ((DoIWantToBiteThisCreature(rep) || (Random.Value() > lizard.LizardState.health && Random.Value() < 0.05f)) && rep.VisualContact
                    && (casualAggressionTarget == null || casualAggressionTarget.representedCreature.realizedCreature == null
                        || Vector2.Distance(lizard.mainBodyChunk.pos, rep.representedCreature.realizedCreature.mainBodyChunk.pos)
                        < Vector2.Distance(lizard.mainBodyChunk.pos, casualAggressionTarget.representedCreature.realizedCreature.mainBodyChunk.pos)))
                    casualAggressionTarget = rep;
            }
            if (casualAggressionTarget != null && Random.Value() < 0.5f && casualAggressionTarget.VisualContact
                && Custom.DistLess(casualAggressionTarget.representedCreature.realizedCreature.mainBodyChunk.pos, lizard.mainBodyChunk.pos, lizard.lizardParams.attemptBiteRadius))
                lizard.AttemptBite(casualAggressionTarget.representedCreature.realizedCreature);
            base.Update();
        }

        // The Idle branch of LizardAI.Update: pick a comfortable, well-hidden spot and rest there for a while.
        private void IdleBehavior()
        {
            bool moving = true;
            int lastIdleCounter = idleCounter;
            idleCounter--;
            if (creature.pos.room == pathFinder.GetDestination.room)
            {
                if (!pathFinder.GetDestination.TileDefined)
                {
                    creature.abstractAI.SetDestination(creature.pos);
                    idleCounter = 0;
                }
                else if (Custom.ManhattanDistance(creature.pos.Tile, pathFinder.GetDestination.Tile) < 5)
                {
                    idleCounter -= lizard.lizardParams.idleCounterSubtractWhenCloseToIdlePos;
                    idleRestlessness--;
                    moving = idleCounter < 1 || idleRestlessness > 0;
                    if (!moving && !ComfortableIdlePosition())
                    {
                        unableToFindComfortablePosition++;
                        moving = true;
                    }
                    else if (lizard.swim < 0.5f && (lizard.bodyChunks[0].vel.magnitude > 2f || lizard.bodyChunks[1].vel.magnitude > 2f || lizard.bodyChunks[2].vel.magnitude > 2f))
                    {
                        unableToFindComfortablePosition++;
                    }
                    else
                    {
                        unableToFindComfortablePosition = 0;
                    }
                    if (unableToFindComfortablePosition > 40)
                    {
                        idleCounter = 0;
                        unableToFindComfortablePosition = 0;
                    }
                    else if (lizard.IsTileSolid(0, 0, -1) && lizard.IsTileSolid(1, 0, -1) && lizard.IsTileSolid(2, 0, -1))
                    {
                        for (int i = 0; i < 3; i++)
                            lizard.bodyChunks[i].vel.y -= 0.1f;
                    }
                    if (idleRestlessness < 40 && Random.Value() < 0.0016666667f)
                        idleRestlessness = Random.Range(1, 40);
                }
            }
            if (IdleSpotScore(creature.abstractAI.destination) == float.MaxValue)
                idleCounter = 0;
            if (idleCounter < 1)
            {
                if (lastIdleCounter > 0)
                    forbiddenIdleSpot = pathFinder.GetDestination;
                var spot = new WorldCoordinate(creature.Room.index, Random.Range(0, lizard.room.TileWidth), Random.Range(0, lizard.room.TileHeight), -1);
                if (IdleSpotScore(spot) < IdleSpotScore(creature.abstractAI.destination))
                {
                    creature.abstractAI.SetDestination(spot);
                    idleSpotWinStreak = 0;
                }
                else
                {
                    idleSpotWinStreak++;
                    if (idleSpotWinStreak > 50)
                    {
                        idleCounter = Random.Range(1000, 4000);
                        idleSpotWinStreak = 0;
                    }
                }
            }
            if (moving)
            {
                if (!forbiddenIdleSpot.CompareDisregardingNode(creature.abstractAI.destination))
                    runSpeed = Mathf.Lerp(runSpeed, 0.25f, 0.5f);
            }
            else
            {
                runSpeed = Mathf.Lerp(runSpeed, 0f, 0.5f);
            }
            focusCreature = null;
        }

        public override bool WantToStayInDenUntilEndOfCycle() => behavior == Behavior.EscapeRain || behavior == Behavior.Injured;

        /// <summary>Lizards see best straight ahead: the score drops toward the edge of their view (<c>LizardAI.VisualScore</c>).</summary>
        public override float VisualScore(Vector2 lookAtPoint, float bonus) =>
            base.VisualScore(lookAtPoint, bonus) - Mathf.InverseLerp(lizard.lizardParams.perfectVisionAngle, lizard.lizardParams.periferalVisionAngle,
                Vector2.Dot((lizard.bodyChunks[0].pos - lizard.bodyChunks[1].pos).normalized, (lookAtPoint - lizard.bodyChunks[1].pos).normalized));

        // LizardAI.AggressiveBehavior: head for the target, open the jaws as it nears, lunge, and bite.
        private void AggressiveBehavior(Tracker.CreatureRepresentation target, float tongueChance)
        {
            casualAggressionTarget = null;
            creature.abstractAI.SetDestination(target.BestGuessForPosition());
            switch (lizard.animation)
            {
                case Lizard.Animation.PrepareToLounge:
                    lizard.JawOpen = Mathf.Lerp(lizard.JawOpen, 0.2f, 0.2f);
                    break;
                case Lizard.Animation.Lounge:
                    lizard.JawOpen += 0.2f;
                    break;
                default:
                    if (target.VisualContact)
                        lizard.JawOpen = Mathf.InverseLerp(400f, 90f + lizard.lizardParams.biteInFront,
                            Vector2.Distance(lizard.mainBodyChunk.pos, target.representedCreature.realizedCreature.mainBodyChunk.pos));
                    else
                        lizard.JawOpen = Mathf.Lerp(lizard.JawOpen, Mathf.InverseLerp(0.6f, 0.9f, hunger * hunger) * 0.7f, 0.2f);
                    break;
            }
            if (lizard.Template.type == CreatureTemplate.Type.RedLizard && target.VisualContact)
                lizard.JawOpen = Mathf.Clamp(lizard.JawOpen + 0.1f, 0f, 1f);
            if (lizard.Template.type == CreatureTemplate.Type.SpitLizard && target.VisualContact)
                lizard.JawOpen = Mathf.Clamp(lizard.JawOpen + 0.01f, 0f, 0.5f);
            focusCreature = target;
            if (!focusCreature.VisualContact)
                return;
            var prey = focusCreature.representedCreature.realizedCreature;
            if (lizard.loungeDelay < 1 && Random.Value() < lizard.lizardParams.loungeTendensy && !UnpleasantFallRisk(creature.pos.Tile) && !FallRisk(creature.pos.Tile)
                && !UnpleasantFallRisk(focusCreature.representedCreature.pos.Tile)
                && Custom.DistLess(lizard.mainBodyChunk.pos, prey.mainBodyChunk.pos, lizard.lizardParams.loungeDistance)
                && !lizard.room.aimap.getAItile(focusCreature.representedCreature.pos.Tile).narrowSpace && !lizard.room.aimap.getAItile(creature.pos).narrowSpace
                && (creature.creatureTemplate.type != CreatureTemplate.Type.GreenLizard || lizard.room.GetTile(lizard.mainBodyChunk.pos + new Vector2(0f, -20f)).Solid)
                && lizard.LegsGripping > 2)
                lizard.EnterAnimation(Lizard.Animation.PrepareToLounge, false);
            if (focusCreature.age > 120 && lizard.JawReadyForBite)
            {
                attemptBiteFrames++;
                foreach (var chunk in prey.bodyChunks)
                {
                    if (Custom.DistLess(chunk.pos, lizard.mainBodyChunk.pos, lizard.lizardParams.attemptBiteRadius))
                    {
                        lizard.AttemptBite(prey);
                        break;
                    }
                }
            }
            else
            {
                if (attemptBiteFrames > 5)
                    lizard.JawsSnapShut(prey.mainBodyChunk.pos);
                attemptBiteFrames = 0;
            }
        }

        /// <summary>Whether falling from <paramref name="tile"/> would be bad: a long or unrecoverable drop, or off the room (<c>UnpleasantFallRisk</c>).</summary>
        public bool UnpleasantFallRisk(IntVector2 tile)
        {
            var fall = lizard.room.aimap.getAItile(tile).fallRiskTile;
            if (lizard.room.GetTile(fall).AnyWater)
                return true;
            if (fall.y < 0)
                return true;
            if (fall.y < tile.y - 20)
                return true;
            if (fall.y < tile.y - 10 && !pathFinder.CoordinatePossibleToGetBackFrom(lizard.room.GetWorldCoordinate(fall)))
                return true;
            return false;
        }

        private bool FallRisk(IntVector2 tile) => lizard.room.aimap.getAItile(tile).fallRiskTile.y < creature.pos.y - 5;

        /// <summary>The lizard got its jaws on a creature: shake it, if footing allows (<c>BitCreature</c>).</summary>
        public void BitCreature(BodyChunk chunk)
        {
            if (!UnpleasantFallRisk(creature.pos.Tile))
                lizard.EnterAnimation(Lizard.Animation.ShakePrey, true);
        }

        public override void CreatureSpotted(bool firstSpot, Tracker.CreatureRepresentation otherCreature)
        {
            var relationship = DynamicRelationship(otherCreature);
            if (relationship.GoForKill && relationship.intensity > (firstSpot ? 0.2f : 0.45f)
                && (lizard.grasps[0] == null || !(lizard.grasps[0].grabbed is Creature held) || held.abstractCreature != otherCreature.representedCreature))
                lizard.EnterAnimation(firstSpot ? Lizard.Animation.PreySpotted : Lizard.Animation.PreyReSpotted, false);
        }

        public override Tracker.CreatureRepresentation CreateTrackerRepresentationForCreature(AbstractCreature otherCreature)
        {
            if (otherCreature.creatureTemplate.smallCreature)
                return new Tracker.SimpleCreatureRepresentation(tracker, otherCreature, 0f, false);
            return new Tracker.ElaborateCreatureRepresentation(tracker, otherCreature, 1f, 5);
        }

        AIModule IUseARelationshipTracker.ModuleToTrackRelationship(CreatureTemplate.Relationship relationship)
        {
            switch (relationship.type)
            {
                case Rel.Afraid: return threatTracker;
                case Rel.Eats:
                case Rel.Attacks: return preyTracker;
                case Rel.AgressiveRival: return agressionTracker;
                default: return null;
            }
        }

        // Only slugcats get a LizardTrackState, and the desktop has none.
        RelationshipTracker.TrackedCreatureState IUseARelationshipTracker.CreateTrackedCreatureState(RelationshipTracker.DynamicRelationship rel) => null;

        // With no player and no friend, the game's LizardAI.UpdateDynamicRelationship keeps the template's relationship.
        CreatureTemplate.Relationship IUseARelationshipTracker.UpdateDynamicRelationship(RelationshipTracker.DynamicRelationship dRelation) =>
            StaticRelationship(dRelation.trackerRep.representedCreature).Duplicate();

        // LizardAI.IdleSpotScore: lower is better; breeds prefer floors, heights or tunnels.
        private float IdleSpotScore(WorldCoordinate coord)
        {
            if (coord.room != creature.pos.room || !coord.TileDefined)
                return 50f;
            var aimap = lizard.room.aimap;
            if (!aimap.WorldCoordinateAccessibleToCreature(coord, creature.creatureTemplate) || !pathFinder.CoordinateReachableAndGetbackable(coord)
                || coord.CompareDisregardingNode(forbiddenIdleSpot))
                return float.MaxValue;
            var tile = aimap.getAItile(coord);
            float score = 10f;
            score /= Mathf.Lerp(tile.visibility, 1f, 0.9995f);
            if (tile.narrowSpace)
                score += 10f;
            if (lizard.room.GetTile(coord).AnyWater)
                score += 100f;
            switch (creature.creatureTemplate.type)
            {
                case CreatureTemplate.Type.GreenLizard:
                    if (tile.acc == AItile.Accessibility.Floor)
                        score *= 0.5f;
                    if (tile.narrowSpace)
                        score += 20f;
                    break;
                case CreatureTemplate.Type.BlueLizard:
                    score += Mathf.Clamp(10f - tile.floorAltitude, 0f, 10f);
                    score += Mathf.Clamp(3f - aimap.getTerrainProximity(coord), 0f, 3f);
                    if (tile.acc == AItile.Accessibility.Climb || tile.acc == AItile.Accessibility.Wall)
                        score *= 0.5f;
                    break;
                case CreatureTemplate.Type.PinkLizard:
                    score += tile.floorAltitude * 2f;
                    if (tile.narrowSpace)
                        score -= 15f;
                    break;
            }
            if (threatTracker != null && threatTracker.Utility() > 0f)
                score += Mathf.Clamp(threatTracker.ThreatOfArea(coord, true), 0f, 100f);
            if (discomfortTracker != null)
                score -= 1000f * discomfortTracker.DiscomfortOfTile(coord);
            return score;
        }

        private bool ComfortableIdlePosition()
        {
            var room = lizard.room;
            if (room.GetTile(lizard.bodyChunks[0].pos).AnyWater)
                return false;
            if (room.GetTile(lizard.bodyChunks[0].pos + Custom.DirVec(lizard.bodyChunks[1].pos, lizard.bodyChunks[0].pos) * 30f).Terrain == Room.Tile.TerrainType.Solid)
                return false;
            if (room.GetTilePosition(lizard.bodyChunks[0].pos).y == room.GetTilePosition(lizard.bodyChunks[2].pos).y)
                return true;
            return creature.creatureTemplate.type == CreatureTemplate.Type.BlueLizard
                   && room.GetTilePosition(lizard.bodyChunks[0].pos).x == room.GetTilePosition(lizard.bodyChunks[2].pos).x;
        }

        public override PathCost TravelPreference(MovementConnection connection, PathCost cost)
        {
            cost = base.TravelPreference(connection, cost);
            if (threatTracker != null && threatTracker.Utility() > 0f)
            {
                float threat = threatTracker.ThreatOfTile(connection.destinationCoord, true);
                cost += new PathCost(Mathf.Pow(threat, 3f) * 2f * Mathf.Pow(fear, 0.5f), PathCost.Legality.Allowed);
            }
            if (fear > 0.2f && (connection.type == MovementConnection.MovementType.DropToClimb || connection.type == MovementConnection.MovementType.DropToFloor)
                && threatTracker.ThreatOfArea(connection.destinationCoord, true) > threatTracker.ThreatOfArea(connection.startCoord, true))
                cost += new PathCost(100f, PathCost.Legality.Allowed);
            var tile = lizard.room.aimap.getAItile(connection.destinationCoord);
            switch (creature.creatureTemplate.type)
            {
                case CreatureTemplate.Type.GreenLizard:
                    if (tile.narrowSpace)
                        cost += new PathCost(10f, PathCost.Legality.Allowed);
                    break;
                case CreatureTemplate.Type.BlueLizard:
                    cost += new PathCost(Mathf.Clamp(30 - tile.floorAltitude * 3, 0f, 10f), PathCost.Legality.Allowed);
                    break;
                case CreatureTemplate.Type.PinkLizard:
                    cost += new PathCost(tile.floorAltitude * 2f, PathCost.Legality.Allowed);
                    break;
            }
            return cost;
        }

        public bool DoIWantToBiteThisCreature(Tracker.CreatureRepresentation otherCrit)
        {
            if (Random.Value() < 0.1f && Random.Value() > lizard.LizardState.health)
                return true;
            var current = otherCrit.dynamicRelationship.currentRelationship;
            if (current.GoForKill)
                return true;
            return Random.Value() < 0.5f && current.type == Rel.AgressiveRival;
        }

        // LizardAI.SendCommunication: a dominance (or submission) display at another lizard.
        private void SendCommunication(Lizard otherLizard, LizardCommunication signal)
        {
            if (otherLizard == null || otherLizard.dead || otherLizard.room != lizard.room)
                return;
            focusCreature = tracker.RepresentationForObject(otherLizard, false);
            otherLizard.AI.RecieveCommunication(lizard, signal);
        }

        /// <summary>Another lizard's display: rare chance to submit and leave the room, else grow angrier (<c>RecieveCommunication</c>).</summary>
        public void RecieveCommunication(Lizard otherLizard, LizardCommunication signal)
        {
            if (!VisualContact(otherLizard.mainBodyChunk))
                return;
            focusCreature = tracker.RepresentationForObject(otherLizard, false);
            if (signal == LizardCommunication.Agression)
            {
                if (otherLizard.AI.submittedTo == creature)
                    return;
                if (submittedTo != otherLizard.abstractCreature)
                    agressionTracker.IncrementAnger(tracker.RepresentationForObject(otherLizard, false), 0.1f);
                if (Random.Value() < 1f / 24f)
                {
                    SendCommunication(otherLizard, LizardCommunication.Submission);
                    agressionTracker.SetAnger(tracker.RepresentationForObject(otherLizard, false), 0f, 0f);
                    // The sender can be in sight without being tracked yet (the tracker notices one random
                    // creature a tick). The game then throws a NullReferenceException on this line, which
                    // Unity logs before skipping the rest; stop at the same point instead of crashing the sim.
                    var submitRep = tracker.RepresentationForObject(otherLizard, false);
                    if (submitRep == null)
                        return;
                    submittedTo = submitRep.representedCreature;
                    missionTracker.AddMission(new MissionTracker.LeaveRoom(missionTracker, 0.4f, 1000), false);
                }
            }
            else
            {
                agressionTracker.SetAnger(tracker.RepresentationForObject(otherLizard, false), 0f, -1f);
            }
        }

        /// <summary>Reacts to a noise the noise tracker placed (<c>LizardAI.ReactToNoise</c>).</summary>
        public void ReactToNoise(NoiseTracker.TheorizedSource source, InGameNoise noise)
        {
            if (source.creatureRep != null && source.creatureRep == preyTracker.MostAttractivePrey)
            {
                lizard.EnterAnimation(Lizard.Animation.PreyReSpotted, false);
                return;
            }
            if ((behavior == Behavior.Idle || behavior == Behavior.InvestigateSound || behavior == Behavior.Lurk || behavior == Behavior.Travelling)
                && source == source.noiseTracker.soundToExamine)
            {
                lizard.EnterAnimation(Lizard.Animation.HearSound, false);
                return;
            }
            lizard.bubble = Math.Max(lizard.bubble, 5);
        }
    }
}
