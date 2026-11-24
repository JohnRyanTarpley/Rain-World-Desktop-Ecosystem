using System;
using System.Collections.Generic;
using DamageType = RWDE.Sim.CreatureTemplate.DamageType;
using Move = RWDE.Sim.MovementConnection.MovementType;
using Rel = RWDE.Sim.CreatureTemplate.Relationship.Type;

namespace RWDE.Sim
{
    /// <summary>
    /// Every lizard breed, ported from the game's <c>Lizard</c>: a three-chunk body (head, torso, hips)
    /// that walks the room's AI map by following its <see cref="LizardPather"/> one connection at a time,
    /// with the game's movement animations for reaches, turns and drops, its lunge, bite, prey carrying
    /// and shaking. Breeds differ only through their <see cref="LizardBreedParams"/> and templates.
    /// </summary>
    /// <remarks>
    /// The desktop runs lizards as the game runs them in rooms its camera isn't viewing: there is no
    /// <c>LizardGraphics</c>, so <see cref="LegsGripping"/> is the game's no-graphics 2 and
    /// <see cref="NoGripCounter"/> 0, and nothing graphics-only (limbs, head, white flicker) is touched.
    /// Not ported here: the tongue, spitting, Cyan jumps and Yellow pack signals (the breed-specials item),
    /// voice and sounds, swimming (no water), burrowing, safari control, and the Watcher-only rot,
    /// blizzard, basilisk and indigo modules. Spear sticking and rocks turning the body arrive with weapons.
    /// </remarks>
    public class Lizard : AirBreatherCreature
    {
        // Lizard.Animation, in the game's order: EnterAnimation compares their indices.
        public enum Animation
        {
            Standard,
            HearSound,
            PreyReSpotted,
            PreySpotted,
            FightingStance,
            ThreatSpotted,
            ThreatReSpotted,
            ShootTongue,
            Spit,
            PrepareToJump,
            Jumping,
            PrepareToLounge,
            Lounge,
            ShakePrey
        }

        /// <summary>
        /// A scripted move for a connection the lizard can't just walk (<c>Lizard.MovementAnimation</c>):
        /// chunks are pulled toward set points, step by step, until they arrive or stray too far.
        /// </summary>
        public class MovementAnimation
        {
            private struct BodyPartDestination
            {
                public int bodyPart;
                public Vector2 destination;
                public float finishedDist;
                public float maxDistance;

                public BodyPartDestination(BodyChunk bodyChunk, Vector2 destination, float finishedDist)
                {
                    bodyPart = bodyChunk.index;
                    this.destination = destination;
                    this.finishedDist = finishedDist;
                    maxDistance = Vector2.Distance(bodyChunk.pos, destination) + 10f;
                }
            }

            private readonly Lizard parent;
            public readonly MovementConnection connection;
            private readonly int step;
            private readonly int totalSteps;
            private readonly BodyPartDestination[] destinations = new BodyPartDestination[0];
            private bool finished;
            private readonly float friction;
            private readonly float speed;

            public MovementAnimation(Lizard parent, MovementConnection connection, int step, float friction, float speed)
            {
                this.parent = parent;
                this.connection = connection;
                this.step = step;
                this.friction = friction;
                this.speed = speed;
                var room = parent.room;
                var c = parent.bodyChunks;
                Vector2 start = room.MiddleOfTile(connection.StartTile);
                Vector2 dest = room.MiddleOfTile(connection.DestTile);
                switch (connection.type)
                {
                    case Move.Standard:
                        totalSteps = 1;
                        destinations = new[] { new BodyPartDestination(c[0], dest, 5f), new BodyPartDestination(c[2], start, 5f) };
                        break;
                    case Move.ReachOverGap:
                    case Move.ReachUp:
                    case Move.ReachDown:
                        totalSteps = 2;
                        if (step == 1)
                        {
                            destinations = new[] { new BodyPartDestination(c[0], (start + dest) * 0.5f, 10f) };
                            return;
                        }
                        destinations = new[]
                        {
                            new BodyPartDestination(c[0], dest, 5f),
                            new BodyPartDestination(c[1], (start + dest) * 0.5f, 100f),
                            new BodyPartDestination(c[2], start, 12f)
                        };
                        break;
                    case Move.SemiDiagonalReach:
                        totalSteps = 1;
                        destinations = new[] { new BodyPartDestination(c[0], dest, 5f), new BodyPartDestination(c[2], start, 15f) };
                        break;
                    case Move.DoubleReachUp:
                        totalSteps = 1;
                        destinations = new[] { new BodyPartDestination(c[0], dest, 5f), new BodyPartDestination(c[2], Vector2.Lerp(dest, start, 0.5f), 15f) };
                        break;
                    case Move.LizardTurn:
                        totalSteps = 2;
                        if (step == 1)
                            destinations = new[]
                            {
                                new BodyPartDestination(c[0], start, 8f),
                                new BodyPartDestination(c[1], room.MiddleOfTile(connection.StartTile + new IntVector2(0, 1)), 9f),
                                new BodyPartDestination(c[2], room.MiddleOfTile(connection.DestTile + new IntVector2(0, 1)), 10f)
                            };
                        else
                            destinations = new[]
                            {
                                new BodyPartDestination(c[0], dest, 5f),
                                new BodyPartDestination(c[1], start, 5f),
                                new BodyPartDestination(c[2], room.MiddleOfTile(connection.StartTile + new IntVector2(0, 1)), 8f)
                            };
                        break;
                    case Move.DropToClimb:
                        totalSteps = 1;
                        destinations = new[] { new BodyPartDestination(c[0], dest + new Vector2(0f, 5f), 5f), new BodyPartDestination(c[1], start, 12f) };
                        break;
                }
            }

            public void Update(float speedFac)
            {
                finished = true;
                bool strayed = false;
                var random = parent.Random;
                foreach (var d in destinations)
                {
                    var chunk = parent.bodyChunks[d.bodyPart];
                    if (finished && !Custom.DistLess(chunk.pos, d.destination, d.finishedDist))
                        finished = false;
                    chunk.vel *= friction;
                    float push = speed * (d.bodyPart == 0 ? parent.BodyForce : 1f) * Mathf.Pow(parent.AI.runSpeed, 0.2f);
                    if (Custom.DistLess(chunk.pos, d.destination, push))
                        push = Vector2.Distance(chunk.pos, d.destination);
                    push *= parent.BodyDesperation * parent.lizardParams.maxMusclePower * 2f * random.Value() + 1f;
                    chunk.vel += Custom.DirVec(chunk.pos, d.destination) * (push * speedFac);
                    chunk.vel += Custom.DegToVec(random.Value() * 360f) * (parent.BodyDesperation * parent.lizardParams.maxMusclePower * 2f * random.Value() * speed);
                    if (!Custom.DistLess(chunk.pos, d.destination, d.maxDistance))
                    {
                        finished = true;
                        strayed = true;
                        break;
                    }
                }
                if (!finished)
                    return;
                if (step < totalSteps && !strayed)
                    parent.movementAnimation = new MovementAnimation(parent, connection, step + 1, friction, speed);
                else
                    parent.MovementAnimationEnded(connection, !strayed);
            }

            public override string ToString() => "movAn. " + connection.type;
        }

        public Lizard(AbstractCreature abstractCreature, World world) : base(abstractCreature, world)
        {
            lizardParams = (LizardBreedParams)abstractCreature.creatureTemplate.breedParameters;
            float rad = 8f * lizardParams.bodySizeFac * lizardParams.bodyRadFac;
            bodyChunks = new BodyChunk[3];
            for (int i = 0; i < 3; i++)
                bodyChunks[i] = new BodyChunk(this, i, new Vector2(200f, 500f), rad, lizardParams.bodyMass / 3f);
            float length = 17f * lizardParams.bodyLengthFac * ((lizardParams.bodySizeFac + 1f) / 2f);
            bodyChunkConnections = new[]
            {
                new BodyChunkConnection(bodyChunks[0], bodyChunks[1], length, BodyChunkConnection.Type.Normal, 0.95f, 0.5f),
                new BodyChunkConnection(bodyChunks[1], bodyChunks[2], length, BodyChunkConnection.Type.Normal, 0.95f, 0.5f),
                new BodyChunkConnection(bodyChunks[0], bodyChunks[2], length * (1f + lizardParams.bodyStiffnes), BodyChunkConnection.Type.Push,
                    1f - Mathf.Lerp(0.9f, 0.5f, lizardParams.bodyStiffnes), 0.5f)
            };
            animation = Animation.Standard;
            airFriction = 0.999f;
            gravity = 0.9f;
            bounce = 0.1f;
            surfaceFriction = 0.5f;
            collisionLayer = 1;
            waterFriction = 0.92f;
            buoyancy = 1.06f;
            GoThroughFloors = true;
            // A {Mean:x} spawn flag sets how evil the lizard is toward players (spawnDataEvil).
            foreach (var flag in abstractCreature.unrecognizedFlags)
            {
                var parts = flag.Split(':');
                if (parts.Length > 1 && parts[0] == "Mean" && float.TryParse(parts[1], System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out float mean))
                    spawnDataEvil = mean;
            }
            effectColor = EffectColor(abstractCreature);
            upcomingConnections = new List<MovementConnection>();
        }

        // The effect colour picked in the Lizard constructor, from randomness seeded with the creature's ID
        // (the game seeds UnityEngine.Random with ID.RandomSeed; here a System.Random with the ID number).
        private Color EffectColor(AbstractCreature abstractCreature)
        {
            var random = new System.Random(abstractCreature.ID.number);
            switch (Template.type)
            {
                case CreatureTemplate.Type.PinkLizard:
                    return Custom.HSL2RGB(Custom.WrappedRandomVariation(random, 0.87f, 0.1f, 0.6f), 1f, Custom.ClampedRandomVariation(random, 0.5f, 0.15f, 0.1f));
                case CreatureTemplate.Type.GreenLizard:
                    return Custom.HSL2RGB(Custom.WrappedRandomVariation(random, 0.32f, 0.1f, 0.6f), 1f, Custom.ClampedRandomVariation(random, 0.5f, 0.15f, 0.1f));
                case CreatureTemplate.Type.BlueLizard:
                    return Custom.HSL2RGB(Custom.WrappedRandomVariation(random, 0.57f, 0.08f, 0.6f), 1f, Custom.ClampedRandomVariation(random, 0.5f, 0.15f, 0.1f));
                case CreatureTemplate.Type.YellowLizard:
                    return Custom.HSL2RGB(Custom.WrappedRandomVariation(random, 0.1f, 0.05f, 0.6f), 1f, Custom.ClampedRandomVariation(random, 0.5f, 0.15f, 0.1f));
                case CreatureTemplate.Type.RedLizard:
                    return Custom.HSL2RGB(Custom.WrappedRandomVariation(random, 0.0025f, 0.02f, 0.6f), 1f, Custom.ClampedRandomVariation(random, 0.5f, 0.15f, 0.1f));
                case CreatureTemplate.Type.SpitLizard:
                    return Custom.HSL2RGB(Custom.WrappedRandomVariation(random, 0.1f, 0.03f, 0.2f), 0.55f, Custom.ClampedRandomVariation(random, 0.55f, 0.36f, 0.2f));
                default:
                    return lizardParams.standardColor;
            }
        }

        public readonly LizardBreedParams lizardParams;
        public LizardAI AI;
        /// <summary>The breed colour graphics tint the lizard with (<c>effectColor</c>).</summary>
        public Color effectColor { get; }
        /// <summary>How hostile a {Mean} spawn flag makes it toward players.</summary>
        public float spawnDataEvil;

        private int biteDelay;
        public int grabbedAttackCounter;
        private int turnedByRockDirection;
        private int turnedByRockCounter;
        private float jawOpen;
        public float lastJawOpen;
        public float jawForcedShut;
        public Vector2 loungeDir;
        /// <summary>Emotion bubble timer and strength, which graphics show (<c>bubble</c>, <c>bubbleIntensity</c>).</summary>
        public int bubble;
        public float bubbleIntensity;
        public int loungeDelay;
        public int postLoungeStun;
        public int timeInAnimation;
        public int timeToRemainInAnimation;
        public Animation animation;
        public float swim;

        public List<MovementConnection> upcomingConnections;
        public MovementConnection followingConnection;
        public MovementConnection lastFollowingConnection;
        public MovementConnection commitedToDropConnection;
        public MovementAnimation movementAnimation;
        public bool applyGravity;
        public int inAllowedTerrainCounter;
        public Vector2? gripPoint;
        public int timeSpentTryingThisMove;
        public float desperationSmoother;
        /// <summary>Where the legs reach for, which graphics read (<c>limbsAimFor</c>).</summary>
        public Vector2 limbsAimFor;
        public float straightenOutNeeded;
        private bool narrowUpcoming;
        private bool climbUpcoming;
        private bool shortcutUpcoming;
        private float bodyWiggle;
        private int bwc;

        internal new System.Random Random => base.Random;

        public LizardState LizardState => (LizardState)abstractCreature.state;

        public float JawOpen
        {
            get
            {
                if (grasps[0] != null)
                    jawOpen = 0f;
                if (jawForcedShut > 0f)
                    jawOpen = Mathf.Min(Mathf.Pow(1f - jawForcedShut, 4f), jawOpen);
                return jawOpen;
            }
            set => jawOpen = grasps[0] == null ? Mathf.Clamp(value, 0f, 1f) : 0f;
        }

        public bool JawReadyForBite => JawOpen > 0.9f && biteDelay == 0;

        public bool IsWallClimber => lizardParams.WallClimber;

        /// <summary>Which way the body points, as an index into <see cref="Custom.fourDirections"/> (<c>BodyDirection</c>).</summary>
        public int BodyDirection => (Mathf.RoundToInt((-Custom.AimFromOneVectorToAnother(bodyChunks[1].pos, bodyChunks[0].pos) + 180f) / 90f) + 1) % 4;

        private float BodyForce => Mathf.Clamp(desperationSmoother * 0.025f, 1f, lizardParams.maxMusclePower);

        private float BodyDesperation => Mathf.InverseLerp(120f, 400f, desperationSmoother);

        /// <summary>Legs holding on: <c>LizardGraphics.legsGrabbing</c>, or 2 without graphics as here.</summary>
        public int LegsGripping => 2;

        /// <summary>Ticks without grip: <c>LizardGraphics.noGripCounter</c>, or 0 without graphics as here.</summary>
        public int NoGripCounter => 0;

        private float BodyWiggleFac => Mathf.Clamp((bodyWiggleCounter - (float)lizardParams.wiggleDelay) / (50f + lizardParams.wiggleDelay), 0f, 1f);

        public int bodyWiggleCounter
        {
            get => bwc;
            set => bwc = Custom.IntClamp(value, 0, 100);
        }

        /// <summary>Switches to <paramref name="anim"/> unless a later (more important) one is running (<c>EnterAnimation</c>).</summary>
        public void EnterAnimation(Animation anim, bool forceAnimationChange)
        {
            if ((!forceAnimationChange && anim < animation) || animation == anim)
                return;
            timeInAnimation = 0;
            animation = anim;
            var focus = AI.focusCreature?.representedCreature?.realizedCreature;
            switch (animation)
            {
                case Animation.Standard:
                    timeToRemainInAnimation = -1;
                    break;
                case Animation.HearSound:
                    timeToRemainInAnimation = 40;
                    bubble = 40;
                    bubbleIntensity = 0.1f;
                    break;
                case Animation.PreySpotted:
                    bubble = 30;
                    bubbleIntensity = 1f;
                    timeToRemainInAnimation = 30;
                    break;
                case Animation.PreyReSpotted:
                    bubble = 10;
                    bubbleIntensity = 0.4f;
                    timeToRemainInAnimation = 10;
                    break;
                case Animation.ThreatSpotted:
                    bubbleIntensity = 1f;
                    timeToRemainInAnimation = Random.Range(10, 50);
                    break;
                case Animation.ThreatReSpotted:
                    bubbleIntensity = 1f;
                    timeToRemainInAnimation = 10;
                    break;
                case Animation.ShootTongue:
                    timeToRemainInAnimation = lizardParams.tongueWarmUp;
                    break;
                case Animation.Spit:
                    timeToRemainInAnimation = 200;
                    break;
                case Animation.PrepareToJump:
                    bubble = 3;
                    bubbleIntensity = 0.1f;
                    break;
                case Animation.Jumping:
                    timeToRemainInAnimation = -1;
                    GoThroughFloors = true;
                    break;
                case Animation.PrepareToLounge:
                    bubble = 3;
                    bubbleIntensity = 0.1f;
                    timeToRemainInAnimation = lizardParams.preLoungeCrouch + 5;
                    if (focus != null)
                        loungeDir = Custom.DirVec(mainBodyChunk.pos, focus.mainBodyChunk.pos);
                    break;
                case Animation.Lounge:
                    loungeDelay = lizardParams.loungeDelay * (Random.Value() < lizardParams.riskOfDoubleLoungeDelay ? 2 : 1) + lizardParams.loungeMaximumFrames;
                    if (focus != null)
                        loungeDir = Vector2.Slerp(loungeDir, Custom.DirVec(mainBodyChunk.pos, focus.mainBodyChunk.pos), lizardParams.findLoungeDirection);
                    if (Vector2.Dot(Custom.DirVec(bodyChunks[1].pos, bodyChunks[0].pos), loungeDir) < 0f)
                    {
                        if (lizardParams.canExitLounge || lizardParams.canExitLoungeWarmUp)
                            EnterAnimation(Animation.Standard, true);
                        else
                            loungeDir = Custom.DirVec(bodyChunks[1].pos, bodyChunks[0].pos);
                    }
                    if (loungeDir.y > 0f)
                        loungeDir.y += lizardParams.loungeJumpyness;
                    loungeDir.y *= 0.5f;
                    timeToRemainInAnimation = lizardParams.loungeMaximumFrames + 5;
                    break;
                case Animation.ShakePrey:
                    bubbleIntensity = 0f;
                    timeToRemainInAnimation = Random.Range(lizardParams.shakePrey, lizardParams.shakePrey * 2);
                    timeToRemainInAnimation = (int)(timeToRemainInAnimation * AI.DynamicRelationship(((Creature)grasps[0].grabbed).abstractCreature).intensity);
                    break;
                case Animation.FightingStance:
                    bubble = 10;
                    bubbleIntensity = 1f;
                    timeToRemainInAnimation = Random.Range(20, 50);
                    break;
            }
        }

        public override void Update(bool eu)
        {
            if (abstractCreature.InDen)
                return;
            lastJawOpen = JawOpen;
            if (JawOpen > 0.9f)
            {
                if (biteDelay > 0)
                    biteDelay--;
            }
            else if (biteDelay < lizardParams.biteDelay)
            {
                biteDelay++;
            }
            jawForcedShut = Mathf.Max(0f, jawForcedShut - 1f / 60f);
            if (!dead && Random.Value() * 0.7f > LizardState.health && Random.Value() < 0.125f)
                Stun(Random.Range(1, Random.Range(1, 27 - Custom.IntClamp((int)(20f * LizardState.health), 0, 10))));
            if (!dead && Random.Value() * 0.5f > LizardState.health && Random.Value() < 0.05f)
                JawOpen = Random.Value();
            bodyChunkConnections[2].active = BodyStiff();
            if (turnedByRockCounter > 0)
            {
                turnedByRockCounter--;
                WeightedPush(0, 2, new Vector2(turnedByRockDirection, 0f), 6f);
                if (Mathf.Abs(bodyChunks[0].pos.y - bodyChunks[2].pos.y) < 5f)
                    WeightedPush(0, 2, new Vector2(0f, 1f), 2f);
            }
            if (bubble > 0)
                bubble--;
            loungeDelay--;

            if (Consious)
                Act();
            else
                applyGravity = true;

            // Held by something: struggle and, now and then, bite it to get free.
            if (!Consious && !dead && stun < 35 && grabbedBy.Count > 0 && grabbedAttackCounter < 22)
            {
                grabbedAttackCounter++;
                jawForcedShut = Mathf.Max(0f, jawForcedShut - 1f / 24f);
                JawOpen = JawOpen + 0.1f + 0.1f * LizardState.health;
                for (int j = 0; j < bodyChunks.Length; j++)
                    bodyChunks[j].vel += Custom.RNV(Random) * (Random.Value() * 6f * LizardState.health);
                var grabber = grabbedBy[0].grabber;
                if (grabbedAttackCounter == 22 && JawReadyForBite
                    && ((Random.Value() < lizardParams.getFreeBiteChance * Custom.LerpMap(grabber.TotalMass, TotalMass, TotalMass * 3f, 1f, 0.1f) * LizardState.health
                         && grabber.Template.type != CreatureTemplate.Type.RedLizard
                         && (grabber.Template.type != CreatureTemplate.Type.Vulture || Random.Value() < 0.5f)
                         && grabber.Template.type != CreatureTemplate.Type.KingVulture)
                        || (Template.type == CreatureTemplate.Type.RedLizard && Random.Value() < LizardState.health)))
                    DamageAttackClosestChunk(grabber);
            }
            else if (grabbedBy.Count == 0)
            {
                grabbedAttackCounter = 0;
            }
            for (int k = 0; k < 2; k++)
                bodyChunkConnections[k].type = movementAnimation != null && movementAnimation.connection.type == Move.Standard
                    ? BodyChunkConnection.Type.Pull
                    : BodyChunkConnection.Type.Normal;

            if (animation == Animation.Lounge || postLoungeStun > 0)
            {
                airFriction = 0.999f;
                gravity = 0.9f;
                bounce = 0.1f;
                surfaceFriction = animation == Animation.Lounge ? 0.5f : 0.4f;
                GoThroughFloors = loungeDir.y < 0f;
                postLoungeStun--;
            }
            else if (applyGravity)
            {
                airFriction = 0.999f;
                gravity = 0.9f;
                bounce = 0.1f;
                surfaceFriction = 0.3f;
                GoThroughFloors = swim > 0f;
            }
            else
            {
                // Holding on to terrain: no gravity, heavy damping, as the game's lizards cling.
                airFriction = 0.8f;
                gravity = 0f;
                bounce = 0.1f;
                surfaceFriction = 0.5f;
                GoThroughFloors = true;
            }
            if (grasps[0] != null)
                CarryObject(eu);
            if (Consious && AI.pathFinder.forbiddenEntranceCounter > 0)
            {
                foreach (var chunk in bodyChunks)
                    if (room.GetTile(chunk.pos).Terrain == Room.Tile.TerrainType.ShortcutEntrance)
                        chunk.vel += room.ShorcutEntranceHoleDirection(room.GetTilePosition(chunk.pos)).ToVector2() * 4f;
            }
            if (lizardParams.pullDownFac != 1f && Submersion == 0f && !room.aimap.getAItile(abstractCreature.pos).narrowSpace
                && (room.GetTile(abstractCreature.pos.Tile + new IntVector2(0, -1)).Solid || room.GetTile(abstractCreature.pos.Tile + new IntVector2(0, -2)).Solid))
            {
                for (int m = 1; m < bodyChunks.Length; m++)
                    bodyChunks[m].vel.y -= lizardParams.pullDownFac - 1f;
            }
            base.Update(eu);
        }

        /// <summary>Opens the jaws toward, then bites, <paramref name="creature"/> if it is in reach (<c>AttemptBite</c>).</summary>
        public void AttemptBite(Creature creature)
        {
            if (grasps[0] != null || !Consious)
                return;
            if (!JawReadyForBite)
            {
                JawOpen += 0.05f;
                return;
            }
            bool bit = false;
            if (Random.Value() < lizardParams.biteChance && creature != null)
            {
                Vector2 mouth = mainBodyChunk.pos + Custom.DirVec(bodyChunks[1].pos, mainBodyChunk.pos) * lizardParams.biteInFront;
                foreach (var chunk in creature.bodyChunks)
                {
                    if (Custom.DistLess(mouth, chunk.pos, (StaticWorld.Remix ? Mathf.Max(8f, chunk.rad) : chunk.rad) + lizardParams.biteRadBonus))
                    {
                        bit = true;
                        Bite(chunk);
                        break;
                    }
                }
            }
            else if (creature == null)
            {
                Bite(null);
            }
            if (LegsGripping <= 0)
                return;
            if (bit)
            {
                for (int j = 0; j < 3; j++)
                    bodyChunks[j].vel += Custom.DegToVec(Random.Value() * 360f) * 7f;
            }
            else if (creature != null)
            {
                Vector2 dir = Custom.DirVec(mainBodyChunk.pos, creature.mainBodyChunk.pos);
                mainBodyChunk.vel += dir * (3f * lizardParams.biteHomingSpeed);
                bodyChunks[1].vel -= dir * lizardParams.biteHomingSpeed;
                bodyChunks[2].vel -= dir * lizardParams.biteHomingSpeed;
            }
        }

        // Lizard.Bite: grab what was bitten (prey, or anything light enough), hurt it, or snap shut on air.
        private void Bite(BodyChunk chunk)
        {
            if ((grasps[0] != null && grabbedBy.Count == 0) || (chunk != null && chunk.owner is Creature c && c.newToRoomInvinsibility > 0))
                return;
            JawOpen = 0f;
            lastJawOpen = 0f;
            if (chunk == null)
                return;
            chunk.vel += mainBodyChunk.vel * Mathf.Lerp(mainBodyChunk.mass, 1.1f, 0.5f) / Mathf.Max(1f, chunk.mass);
            bool grabbed = false;
            var owner = chunk.owner;
            if ((owner is Creature eaten && AI.DynamicRelationship(eaten.abstractCreature).type == Rel.Eats)
                || (Random.Value() < 0.5f && owner.TotalMass < TotalMass * 1.2f)
                || (!(owner is Creature) && owner.TotalMass < TotalMass * 1.2f))
                grabbed = Grab(owner, 0, chunk.index, Grasp.Shareability.CanOnlyShareWithNonExclusive, lizardParams.biteDominance * Random.Value(), true, true);
            if (grabbed)
            {
                if (owner is Creature victim)
                {
                    if (Template.type == CreatureTemplate.Type.RedLizard)
                        victim.LoseAllGrasps();
                    AI.BitCreature(chunk);
                    if (Random.Value() < lizardParams.biteDamageChance)
                        victim.Violence(mainBodyChunk, Custom.DirVec(mainBodyChunk.pos, chunk.pos) * 0.1f, chunk, DamageType.Bite,
                            lizardParams.biteDamage * Mathf.Lerp(0.8f, 1.2f, Random.Value()), 0f);
                }
                return;
            }
            if (Random.Value() < 0.5f && owner is Creature target && AI.DynamicRelationship(target.abstractCreature).type == Rel.Attacks)
            {
                DamageAttack(chunk, 0.5f);
                return;
            }
            for (int i = owner.grabbedBy.Count - 1; i >= 0; i--)
                if (owner.grabbedBy[i].grabber is Lizard)
                    owner.grabbedBy[i].Release();
        }

        private void DamageAttackClosestChunk(Creature target)
        {
            BodyChunk closest = null;
            float dst = float.MaxValue;
            foreach (var chunk in target.bodyChunks)
            {
                if (Custom.DistLess(mainBodyChunk.pos, chunk.pos, dst))
                {
                    closest = chunk;
                    dst = Vector2.Distance(mainBodyChunk.pos, chunk.pos);
                }
            }
            if (closest != null)
                DamageAttack(closest, 1f);
        }

        // Lizard.DamageAttack: a hard bite that hurts and knocks back instead of holding on.
        private void DamageAttack(BodyChunk chunk, float dmgFac)
        {
            if (chunk == null || !(chunk.owner is Creature target))
                return;
            if (AI.DynamicRelationship(target.abstractCreature).type == Rel.AgressiveRival)
                dmgFac = 0f;
            target.Violence(mainBodyChunk, Custom.DirVec(mainBodyChunk.pos, chunk.pos) * 11f, chunk, DamageType.Bite,
                lizardParams.biteDamage * Random.Value() * dmgFac, 30f);
            JawOpen = 0f;
            lastJawOpen = 0f;
            Vector2 dir = Custom.DirVec(mainBodyChunk.pos, chunk.pos);
            mainBodyChunk.vel += dir * 8f;
            bodyChunks[1].vel -= dir * 6f;
            bodyChunks[2].vel -= dir * 6f;
            biteDelay = lizardParams.biteDelay;
            stun = Math.Max(stun, (int)(16f * dmgFac));
            LoseAllGrasps();
        }

        /// <summary>The jaws snap shut on nothing (<c>JawsSnapShut</c>).</summary>
        public void JawsSnapShut(Vector2 pos)
        {
            JawOpen = 0f;
            lastJawOpen = 0f;
            Vector2 dir = Custom.DirVec(mainBodyChunk.pos, pos);
            mainBodyChunk.vel += dir * 8f;
            bodyChunks[1].vel -= dir * 6f;
            bodyChunks[2].vel -= dir * 6f;
            biteDelay = lizardParams.biteDelay;
            jawForcedShut = 1f;
        }

        // Lizard.CarryObject: hold the bitten object in front of the head; drop it if it pulls away too hard.
        private void CarryObject(bool eu)
        {
            var grasp = grasps[0];
            if (Random.Value() < 0.025f && (!(grasp.grabbed is Creature held) || AI.DynamicRelationship(held.abstractCreature).type != Rel.Eats))
            {
                LoseAllGrasps();
                return;
            }
            Vector2 holdPos = mainBodyChunk.pos + Custom.DirVec(bodyChunks[1].pos, mainBodyChunk.pos) * (25f * lizardParams.headSize);
            var grabbed = grasp.grabbed;
            var heldChunk = grabbed.bodyChunks[grasp.chunkGrabbed];
            Vector2 relVel = heldChunk.vel - mainBodyChunk.vel;
            heldChunk.vel = mainBodyChunk.vel;
            if (!enteringShortCut.HasValue && (relVel.magnitude * heldChunk.mass > 30f || !Custom.DistLess(holdPos, heldChunk.pos, 70f + heldChunk.rad)))
                LoseAllGrasps();
            else
                heldChunk.MoveFromOutsideMyUpdate(eu, holdPos);
            if (grasps[0] != null)
                for (int i = 0; i < 2; i++)
                    grasps[0].grabbed.PushOutOf(bodyChunks[i].pos, bodyChunks[i].rad, grasps[0].chunkGrabbed);
        }

        private bool BodyStiff() => !Consious || movementAnimation == null;

        public override void Collide(PhysicalObject otherObject, int myChunk, int otherChunk)
        {
            if (otherObject is Creature other)
            {
                AI.tracker.SeeCreature(other.abstractCreature);
                var rel = AI.DynamicRelationship(other.abstractCreature);
                if (rel.type == Rel.AgressiveRival && AI.submittedTo != other.abstractCreature)
                    AI.agressionTracker.IncrementAnger(AI.tracker.RepresentationForObject(otherObject, false), 0.05f * rel.intensity);
                if (Consious && grasps[0] == null && rel.GoForKill)
                {
                    if (animation == Animation.Lounge && myChunk == 0)
                        Bite(otherObject.bodyChunks[otherChunk]);
                    else
                        AttemptBite(other);
                }
            }
            base.Collide(otherObject, myChunk, otherChunk);
        }

        public override void Violence(BodyChunk source, Vector2? directionAndMomentum, BodyChunk hitChunk, DamageType type, float damage, float stunBonus)
        {
            float dmg = damage / Template.baseDamageResistance;
            float stn = (damage * 30f + stunBonus) / Template.baseStunResistance;
            if (Template.damageRestistances[(int)type, 0] > 0f)
                dmg /= Template.damageRestistances[(int)type, 0];
            if (Template.damageRestistances[(int)type, 1] > 0f)
                stn /= Template.damageRestistances[(int)type, 1];
            if (hitChunk != null && hitChunk.index == 0 && directionAndMomentum.HasValue)
            {
                if (HitHeadShield(directionAndMomentum.Value))
                {
                    // The armoured head turns the hit aside.
                    dmg *= 0.1f;
                    stn = (damage * 0.5f * 30f + stunBonus * (2f / 3f)) / Template.baseStunResistance;
                    directionAndMomentum = directionAndMomentum.Value / 3f;
                }
                else if (HitInMouth(directionAndMomentum.Value))
                {
                    dmg *= 1.5f;
                    stn = Mathf.Min(stn * 2f, 120f);
                    LizardState.throatHealth -= dmg;
                }
            }
            if (directionAndMomentum.HasValue && hitChunk != null)
                hitChunk.vel += directionAndMomentum.Value / hitChunk.mass;
            stn *= 1f + Mathf.InverseLerp(0.75f, 0f, LizardState.health) * Random.Value();
            Stun((int)stn);
            LizardState.health -= dmg;
        }

        // Lizard.HitHeadShield: a hit straight on the head, outside the open mouth, glances off.
        private bool HitHeadShield(Vector2 direction)
        {
            float angle = Vector2.Angle(direction, -bodyChunks[0].Rotation);
            if (HitInMouth(direction))
                return false;
            return angle < lizardParams.headShieldAngle + 20f * JawOpen;
        }

        // Lizard.HitInMouth: a hit from above into the open jaws.
        private bool HitInMouth(Vector2 direction)
        {
            if (direction.y > 0f)
                return false;
            direction = Vector2.Slerp(direction, new Vector2(0f, 1f), 0.1f);
            return Mathf.Abs(Vector2.Angle(direction, -bodyChunks[0].Rotation)) < Mathf.Lerp(-15f, 11f, JawOpen);
        }

        public override void Stun(int st)
        {
            if (LizardState.health < 0.5f)
                st = (int)(st * (1f + Mathf.InverseLerp(0.75f, 0f, LizardState.health) * 1.5f));
            base.Stun(st);
            if (Stunned)
            {
                gripPoint = null;
                movementAnimation = null;
                LoseAllGrasps();
            }
        }

        public override void SpitOutOfShortCut(IntVector2 pos, Room newRoom, bool spitOutAllSticks)
        {
            base.SpitOutOfShortCut(pos, newRoom, spitOutAllSticks);
            Vector2 dir = Custom.IntVector2ToVector2(newRoom.ShorcutEntranceHoleDirection(pos));
            for (int i = 0; i < bodyChunks.Length; i++)
            {
                bodyChunks[i].pos = newRoom.MiddleOfTile(pos) - dir * ((-1.5f + i) * 15f);
                bodyChunks[i].lastPos = newRoom.MiddleOfTile(pos);
                bodyChunks[i].vel = dir * 2f;
            }
        }

        public void MovementAnimationEnded(MovementConnection connection, bool success)
        {
            movementAnimation = null;
        }

        // ---- Movement (Lizard.Act, FollowConnection, ActAnimation, GetFrameSpeed, GripPointBehavior) ----

        private void Act()
        {
            AI.Update();
            var template = abstractCreature.creatureTemplate;
            bool headOnTerrain = false;
            bool bodyOnTerrain = false;
            for (int i = 0; i < bodyChunks.Length; i++)
            {
                if (room.aimap.TileAccessibleToCreature(bodyChunks[i].pos, template))
                {
                    headOnTerrain = headOnTerrain || i == 0;
                    bodyOnTerrain = bodyOnTerrain || i > 0;
                }
            }
            if ((headOnTerrain || bodyOnTerrain || movementAnimation != null) && inAllowedTerrainCounter < 100)
            {
                inAllowedTerrainCounter++;
            }
            else
            {
                bool nearTerrain = false;
                for (int c = 0; c < 3 && !nearTerrain; c++)
                    for (int d = 0; d < 4 && !nearTerrain; d++)
                    {
                        var tile = room.GetTilePosition(bodyChunks[c].pos) + Custom.fourDirections[d];
                        if (room.aimap.TileAccessibleToCreature(tile, template) && !room.GetTile(tile).AnyWater)
                            nearTerrain = true;
                    }
                inAllowedTerrainCounter = nearTerrain ? Math.Max(0, inAllowedTerrainCounter - 10) : 0;
            }
            applyGravity = inAllowedTerrainCounter < lizardParams.regainFootingCounter || NoGripCounter > 10 || commitedToDropConnection != default;
            swim = Mathf.Clamp(swim - 0.05f, 0f, 1f);
            AI.stuckTracker.satisfiedWithThisPosition = swim > 0.5f;
            float runSpeed = ActAnimation();

            var pather = (LizardPather)AI.pathFinder;
            WorldCoordinate here = room.GetWorldCoordinate(mainBodyChunk.pos);
            if (movementAnimation == null && here.TileDefined && !pather.CoordinateReachableAndGetbackable(here))
            {
                for (int j = 0; j < 8; j++)
                {
                    if (pather.CoordinateReachableAndGetbackable(here + Custom.eightDirectionsDiagonalsLast[j]))
                    {
                        here += Custom.eightDirectionsDiagonalsLast[j];
                        break;
                    }
                }
            }
            followingConnection = pather.FollowPath(here, BodyDirection, true);
            if (commitedToDropConnection != default)
            {
                followingConnection = commitedToDropConnection;
                commitedToDropConnection = default;
            }

            if ((followingConnection == default || Vector2.Distance(mainBodyChunk.pos, room.MiddleOfTile(followingConnection.DestTile))
                    > Vector2.Distance(bodyChunks[1].pos, room.MiddleOfTile(followingConnection.DestTile))) && timeSpentTryingThisMove > 10)
            {
                bodyChunkConnections[0].type = BodyChunkConnection.Type.Pull;
                bodyChunkConnections[1].type = BodyChunkConnection.Type.Pull;
            }
            else
            {
                bodyChunkConnections[0].type = BodyChunkConnection.Type.Normal;
                bodyChunkConnections[1].type = BodyChunkConnection.Type.Pull;
            }
            foreach (var chunk in bodyChunks)
            {
                chunk.terrainSqueeze = Custom.LerpMap(timeSpentTryingThisMove, 10f, 30f, 1f, 0.05f);
                chunk.collideWithSlopes = timeSpentTryingThisMove < 30;
            }
            if (runSpeed > 0.1f && (followingConnection == default || AI.stuckTracker.moveBacklog.IsMoveInLog(followingConnection, (int)Mathf.Lerp(1f, 20f, runSpeed))))
                timeSpentTryingThisMove = Custom.IntClamp(timeSpentTryingThisMove + 1, 0, 600);
            else
                timeSpentTryingThisMove = 0;
            desperationSmoother = Custom.LerpAndTick(desperationSmoother,
                Mathf.Max(timeSpentTryingThisMove + Mathf.Lerp(-300f, 0f, runSpeed), AI.stuckTracker.Utility() * 100f), 0.05f, 0.5f);
            if (desperationSmoother > Mathf.Lerp(40f, 10f, runSpeed))
                bodyWiggleCounter += 2;
            else
                bodyWiggleCounter--;
            AI.stuckTracker.moveBacklog.ReportNewMove(followingConnection);
            upcomingConnections.Clear();

            if (followingConnection != default)
            {
                limbsAimFor = room.MiddleOfTile(followingConnection.destinationCoord);
                if (followingConnection.type == Move.ShortCut || followingConnection.type == Move.NPCTransportation)
                {
                    enteringShortCut = followingConnection.StartTile;
                    if (followingConnection.type == Move.NPCTransportation)
                        NPCTransportationDestination = followingConnection.destinationCoord;
                }
                else if (followingConnection.type == Move.ReachUp || followingConnection.type == Move.ReachOverGap || followingConnection.type == Move.ReachDown
                         || followingConnection.type == Move.LizardTurn || followingConnection.type == Move.SemiDiagonalReach || followingConnection.type == Move.DoubleReachUp)
                {
                    movementAnimation = new MovementAnimation(this, followingConnection, 1, 0.99f, 0.75f);
                }
                // Look a few connections ahead for narrow spaces, climbs and shortcuts.
                var ahead = followingConnection;
                narrowUpcoming = false;
                climbUpcoming = false;
                shortcutUpcoming = false;
                for (int n = 0; n < 5; n++)
                {
                    var d = ahead.destinationCoord;
                    if (!narrowUpcoming && ((room.GetTile(d + new IntVector2(0, 1)).Solid && room.GetTile(d + new IntVector2(0, -1)).Solid)
                                            || (room.GetTile(d + new IntVector2(1, 0)).Solid && room.GetTile(d + new IntVector2(-1, 0)).Solid)))
                        narrowUpcoming = true;
                    if (!climbUpcoming && room.aimap.getAItile(d).acc == AItile.Accessibility.Climb && Submersion < 0.8f)
                        climbUpcoming = true;
                    upcomingConnections.Add(ahead);
                    if (!shortcutUpcoming && ahead.type == Move.ShortCut)
                        shortcutUpcoming = true;
                    ahead = pather.FollowPath(ahead.destinationCoord, null, false);
                    for (int k = 0; k < upcomingConnections.Count && ahead != default; k++)
                        if (upcomingConnections[k].destinationCoord == ahead.destinationCoord)
                            ahead = default;
                    if (ahead == default)
                        break;
                }
            }

            timeInAnimation++;
            if (timeToRemainInAnimation > -1 && timeInAnimation > timeToRemainInAnimation)
                EnterAnimation(Animation.Standard, true);
            if (bubble == 0 && Random.Value() < 0.05f && Random.Value() < AI.excitement && Random.Value() < AI.excitement)
            {
                bubbleIntensity = AI.excitement * AI.excitement * Random.Value();
                bubble = 1;
            }

            if (gripPoint.HasValue)
                GripPointBehavior();
            else if (movementAnimation != null)
                movementAnimation.Update(runSpeed * lizardParams.baseSpeed * 0.5f);
            else if (followingConnection != default)
                FollowConnection(runSpeed);

            if (followingConnection == default && movementAnimation == null)
            {
                if (!applyGravity && !room.aimap.TileAccessibleToCreature(mainBodyChunk.pos, template))
                {
                    var tile = room.GetTilePosition(mainBodyChunk.pos) + Custom.fourDirections[Random.Range(0, 4)];
                    if (room.aimap.TileAccessibleToCreature(tile, template))
                        followingConnection = new MovementConnection(Move.Standard, room.GetWorldCoordinate(mainBodyChunk.pos), room.GetWorldCoordinate(tile), 1);
                }
                else if (headOnTerrain && !bodyOnTerrain && timeSpentTryingThisMove > 20)
                {
                    for (int d = 0; d < 4; d++)
                    {
                        var tile = room.GetTilePosition(bodyChunks[1].pos) + Custom.fourDirections[d];
                        if (room.aimap.TileAccessibleToCreature(tile, template))
                        {
                            bodyChunks[1].vel += Custom.DirVec(bodyChunks[1].pos, room.MiddleOfTile(tile));
                            break;
                        }
                    }
                }
            }
            if (BodyDesperation > 0f && swim == 0f)
            {
                Vector2 shake = Custom.DegToVec(Random.Value() * 360f) * (BodyDesperation * lizardParams.maxMusclePower * 2f * Random.Value());
                bodyChunks[0].vel += shake * 0.5f;
                bodyChunks[1].vel -= shake;
                bodyChunks[2].vel += shake * 0.5f;
            }
            bodyChunkConnections[2].elasticity = 0.7f + 0.3f / BodyForce;
            if (animation == Animation.ShakePrey)
            {
                bodyWiggleCounter = Custom.IntClamp(50 - timeInAnimation, 0, 100);
                Vector2 shake = Custom.DegToVec(Random.Value() * 360f) * (12f * Random.Value());
                if (grasps[0] == null)
                {
                    shake *= 0.5f;
                }
                else
                {
                    float fit = Mathf.Abs(grasps[0].grabbed.bodyChunks[grasps[0].chunkGrabbed].mass - mainBodyChunk.mass * 0.5f);
                    fit = Mathf.InverseLerp(mainBodyChunk.mass * 0.5f, 0f, fit);
                    fit = Mathf.Pow(fit, 1.5f);
                    shake *= fit;
                    bodyWiggleCounter = Custom.IntClamp((int)(bodyWiggleCounter * Mathf.Lerp(fit, 1f, 0.5f)), 0, 100);
                    if (fit == 0f && Random.Value() < 0.025f)
                    {
                        bodyWiggleCounter = 0;
                        EnterAnimation(Animation.Standard, true);
                    }
                }
                bodyChunks[0].vel -= shake * 0.5f;
                bodyChunks[1].vel += shake;
                bodyChunks[2].vel -= shake * 0.5f;
            }
            if (BodyWiggleFac > 0f && swim < 0.5f)
            {
                bodyWiggle += Mathf.Lerp(0.05f, 0.15f, BodyWiggleFac) * Mathf.Lerp(0.5f, 0.8f, lizardParams.wiggleSpeed);
                float sin = Mathf.Sin(bodyWiggle * 2f * Mathf.PI);
                Vector2 perp = Custom.PerpendicularVector(Vector2.Slerp(Custom.DirVec(bodyChunks[1].pos, bodyChunks[0].pos), Custom.DirVec(bodyChunks[2].pos, bodyChunks[1].pos), 0.5f));
                float amount = (BodyWiggleFac + 2f) / (Mathf.Pow(1f - lizardParams.wiggleSpeed, 2f) + 2f);
                Vector2 wiggle = perp * (sin * amount);
                bodyChunks[0].vel += wiggle;
                bodyChunks[1].vel -= wiggle * 2f;
                bodyChunks[2].vel += wiggle;
            }
            if (followingConnection != default)
                lastFollowingConnection = followingConnection;
            straightenOutNeeded = Custom.LerpAndTick(straightenOutNeeded, 0f, 0.1f, 0.05f);
        }

        private void FollowConnection(float runSpeed)
        {
            float frameSpeed = GetFrameSpeed(runSpeed);
            if (timeSpentTryingThisMove < 20 && narrowUpcoming && frameSpeed > 0.2f && upcomingConnections.Count > 1
                && room.aimap.getAItile(upcomingConnections[1].DestTile).narrowSpace)
            {
                var now = IntVector2.ClampAtOne(followingConnection.DestTile - followingConnection.StartTile);
                var next = IntVector2.ClampAtOne(upcomingConnections[1].DestTile - upcomingConnections[1].StartTile);
                if (now.x != next.x && now.y != next.y)
                {
                    // A corner into a narrow space: slow down and turn.
                    bodyChunks[0].vel *= 0.75f;
                    bodyChunks[0].vel += next.ToVector2() * 2f;
                    runSpeed = Mathf.Min(runSpeed, 0.2f);
                    bodyChunks[1].vel *= 0.75f;
                    bodyChunks[2].vel *= 0.5f;
                }
            }
            var type = followingConnection.type;
            if (type == Move.Standard || type == Move.Slope || type == Move.CeilingSlope || type == Move.OpenDiagonal)
            {
                Vector2 lean = new Vector2(0f, 0f);
                if (room.aimap.getAItile(followingConnection.DestTile).acc == AItile.Accessibility.Climb)
                {
                    var dir = upcomingConnections.Count <= 0
                        ? IntVector2.ClampAtOne(followingConnection.DestTile - followingConnection.StartTile)
                        : IntVector2.ClampAtOne(upcomingConnections[Math.Min(2, upcomingConnections.Count - 1)].DestTile - followingConnection.StartTile);
                    if (followingConnection.destinationCoord.y == followingConnection.startCoord.y)
                    {
                        lean.y = dir.y * 8f;
                    }
                    else if (followingConnection.destinationCoord.x == followingConnection.startCoord.x)
                    {
                        if (room.aimap.getAItile(followingConnection.StartTile).acc != AItile.Accessibility.Climb)
                            dir.x = bodyChunks[2].pos.x < room.MiddleOfTile(mainBodyChunk.pos).x ? -1 : 1;
                        lean.x = dir.x * 8f;
                    }
                    if (IsTileSolid(0, dir.x, 0))
                    {
                        dir.x *= -1;
                        lean.x = 0f;
                    }
                    if (IsTileSolid(0, 0, dir.y))
                    {
                        dir.y *= -1;
                        lean.y = 0f;
                    }
                }
                else if (NoGripCounter == 0 && followingConnection.destinationCoord.y == followingConnection.startCoord.y && !IsTileSolid(0, 0, 1)
                         && !IsTileSolid(0, (followingConnection.destinationCoord.x < abstractCreature.pos.x ? -1 : 0) + (followingConnection.destinationCoord.x > abstractCreature.pos.x ? 1 : 0), 1))
                {
                    lean.y = lizardParams.floorLeverage;
                }
                // Keep the head centred on the tile row (or column) it moves along.
                if (followingConnection.startCoord.x != followingConnection.destinationCoord.x && followingConnection.startCoord.y == followingConnection.destinationCoord.y)
                {
                    if ((IsTileSolid(0, 0, -1) && mainBodyChunk.pos.y < room.MiddleOfTile(mainBodyChunk.pos).y) || (IsTileSolid(0, 0, 1) && mainBodyChunk.pos.y > room.MiddleOfTile(mainBodyChunk.pos).y))
                        mainBodyChunk.vel.y -= (mainBodyChunk.pos.y - room.MiddleOfTile(mainBodyChunk.pos).y) * 0.5f;
                }
                else if (followingConnection.startCoord.y != followingConnection.destinationCoord.y && followingConnection.startCoord.x == followingConnection.destinationCoord.x
                         && ((IsTileSolid(0, -1, 0) && mainBodyChunk.pos.x < room.MiddleOfTile(mainBodyChunk.pos).x) || (IsTileSolid(0, 1, 0) && mainBodyChunk.pos.x > room.MiddleOfTile(mainBodyChunk.pos).x)))
                {
                    mainBodyChunk.vel.x -= (mainBodyChunk.pos.x - room.MiddleOfTile(mainBodyChunk.pos).x) * 0.5f;
                }
                var delta = followingConnection.DestTile - followingConnection.StartTile;
                if (delta.y != 0)
                {
                    for (int i = 0; i < 3; i++)
                    {
                        if (bodyChunks[i].ContactPoint.y != 0 && !IsTileSolid(i, 0, bodyChunks[i].ContactPoint.y))
                        {
                            bodyChunks[i].vel.x -= (bodyChunks[i].pos.x - room.MiddleOfTile(bodyChunks[i].pos).x) * 0.25f;
                            lean.x *= 0f;
                        }
                    }
                }
                else if (delta.x != 0)
                {
                    for (int j = 0; j < 3; j++)
                    {
                        if (bodyChunks[j].ContactPoint.x != 0 && !IsTileSolid(j, bodyChunks[j].ContactPoint.x, 0))
                        {
                            bodyChunks[j].vel.y -= (bodyChunks[j].pos.y - room.MiddleOfTile(bodyChunks[j].pos).y) * 0.25f;
                            lean.y *= 0f;
                        }
                    }
                }
                // Aim at the furthest upcoming tile in a straight line of accessible tiles.
                Vector2 goal = room.MiddleOfTile(followingConnection.DestTile);
                if (upcomingConnections.Count > 0 && room.aimap.getAItile(upcomingConnections[upcomingConnections.Count - 1].destinationCoord).acc < AItile.Accessibility.Climb)
                {
                    for (int k = upcomingConnections.Count - 1; k >= 0; k--)
                    {
                        if ((room.GetTilePosition(mainBodyChunk.pos).FloatDist(upcomingConnections[k].DestTile) < 2f
                             && room.VisualContact(mainBodyChunk.pos, room.MiddleOfTile(upcomingConnections[k].DestTile)))
                            || AI.pathFinder.RayTraceInAccessibleTiles(room.GetTilePosition(mainBodyChunk.pos), upcomingConnections[k].DestTile))
                        {
                            goal = room.MiddleOfTile(upcomingConnections[k].DestTile);
                            break;
                        }
                    }
                }
                goal += lean;
                Vector2 behind = goal + Custom.DirVec(goal, bodyChunks[0].pos) * bodyChunkConnections[0].distance;
                bodyChunks[0].vel += Custom.DirVec(bodyChunks[0].pos, goal) * (frameSpeed * BodyForce);
                bodyChunks[1].vel += Custom.DirVec(bodyChunks[1].pos, behind)
                                     * (frameSpeed * BodyForce * Custom.LerpMap(Vector2.Dot(Custom.DirVec(bodyChunks[1].pos, goal), Custom.DirVec(bodyChunks[1].pos, behind)), -1f, 1f, 0.5f, 1f));
                if (animation == Animation.Standard && AI.runSpeed > 0.1f)
                {
                    float straighten = Mathf.InverseLerp(0f, -1f, Vector2.Dot(Custom.DirVec(bodyChunks[0].pos, goal), Custom.DirVec(bodyChunks[1].pos, bodyChunks[0].pos)));
                    if (straighten > 0.5f)
                        straightenOutNeeded = Custom.LerpAndTick(straightenOutNeeded, 1f, 0.1f, 0.1f);
                    straighten = Mathf.Lerp(straighten, 1f, Mathf.InverseLerp(20f, 40f, timeSpentTryingThisMove));
                    straighten *= Mathf.Max(Mathf.Max(narrowUpcoming || shortcutUpcoming ? 1f : 0.2f, climbUpcoming ? 0.5f : 0.2f),
                        Mathf.Max(Mathf.InverseLerp(5f, 20f, timeSpentTryingThisMove), straightenOutNeeded));
                    bodyChunks[0].vel += Custom.DirVec(bodyChunks[1].pos, goal) * ((straighten * 2f * BodyForce + BodyDesperation * 2f) * frameSpeed);
                    Vector2 back = Custom.DirVec(bodyChunks[1].pos, goal)
                                   * ((straighten * 2f * BodyForce + BodyDesperation * 2f) * 0.5f * Custom.LerpMap(timeSpentTryingThisMove, 20f, 60f, 1f, 0.5f) * frameSpeed);
                    bodyChunks[1].vel -= back;
                    bodyChunks[2].vel -= back;
                }
                return;
            }
            if (type != Move.DropToFloor && type != Move.DropToClimb)
                return;

            // A drop: walk off the edge, then catch a pole or the floor below.
            bool commit = !(Math.Abs(abstractCreature.pos.x - followingConnection.startCoord.x) > 4 && Math.Abs(abstractCreature.pos.x - followingConnection.destinationCoord.x) > 4);
            for (int k = 0; k < 3; k++)
                if (bodyChunks[k].ContactPoint.y < 0)
                    commit = false;
            if (commit)
                commitedToDropConnection = followingConnection;
            bodyChunks[0].vel.y -= 0.01f;
            bodyChunks[2].vel.y += 0.01f;
            float destX = room.MiddleOfTile(followingConnection.DestTile).x;
            if (Mathf.Abs(mainBodyChunk.pos.x - destX) < 5f)
            {
                if (Mathf.Abs(mainBodyChunk.vel.x) < 5f)
                    mainBodyChunk.vel.x *= 0.8f;
            }
            else if (mainBodyChunk.pos.x < destX)
            {
                mainBodyChunk.vel.x += 0.5f;
            }
            else
            {
                mainBodyChunk.vel.x -= 0.5f;
            }
            bool onGround = false;
            bool overStartColumn = false;
            bool atStartHeight = false;
            foreach (var chunk in bodyChunks)
            {
                if (chunk.ContactPoint.y < 0)
                    onGround = true;
                var tile = room.GetTilePosition(chunk.pos);
                if (tile.y >= followingConnection.StartTile.y)
                {
                    atStartHeight = true;
                    if (tile.x == followingConnection.StartTile.x)
                    {
                        chunk.vel += Custom.DirVec(chunk.pos, room.MiddleOfTile(followingConnection.DestTile)) * (frameSpeed * (chunk.index == 0 ? BodyForce : 1f));
                        overStartColumn = true;
                    }
                    else
                    {
                        chunk.vel += Custom.DirVec(chunk.pos, room.MiddleOfTile(followingConnection.StartTile)) * (frameSpeed * (chunk.index == 0 ? BodyForce : 1f));
                    }
                }
                if (tile.x == followingConnection.StartTile.x)
                    chunk.vel.y -= 1f;
            }
            if (overStartColumn)
                return;
            if (upcomingConnections.Count > 0)
            {
                float swing = 0.3f * (followingConnection.destinationCoord.x - upcomingConnections[upcomingConnections.Count - 1].destinationCoord.x);
                bodyChunks[0].vel.x -= swing;
                bodyChunks[2].vel.x += swing;
            }
            if (mainBodyChunk.pos.y <= mainBodyChunk.lastPos.y)
            {
                if (type == Move.DropToClimb)
                {
                    int from = room.GetTilePosition(mainBodyChunk.lastPos).y + 1;
                    int to = room.GetTilePosition(mainBodyChunk.pos).y - 1;
                    for (int y = from; y >= to; y--)
                    {
                        if (y <= followingConnection.destinationCoord.y && room.aimap.getAItile(room.GetTilePosition(mainBodyChunk.pos).x, y).acc == AItile.Accessibility.Climb)
                        {
                            gripPoint = room.MiddleOfTile(new IntVector2(room.GetTilePosition(mainBodyChunk.pos).x, y));
                            mainBodyChunk.vel.y = 0f;
                            break;
                        }
                    }
                }
                else if (followingConnection.destinationCoord.x != followingConnection.startCoord.x
                         && Math.Abs(room.GetTilePosition(mainBodyChunk.pos).x - followingConnection.destinationCoord.x) < 2)
                {
                    int from = room.GetTilePosition(mainBodyChunk.lastPos).y + 1;
                    int to = room.GetTilePosition(mainBodyChunk.pos).y - 1;
                    for (int y = from; y >= to; y--)
                    {
                        if (y <= followingConnection.destinationCoord.y && room.aimap.getAItile(followingConnection.destinationCoord.x, y).acc == AItile.Accessibility.Floor)
                        {
                            gripPoint = room.MiddleOfTile(new IntVector2(followingConnection.destinationCoord.x, y));
                            mainBodyChunk.vel.y = 0f;
                            break;
                        }
                    }
                }
            }
            if (onGround && !atStartHeight && !gripPoint.HasValue)
                followingConnection = default;
        }

        // Lizard.ActAnimation: what the current animation does to the body, and the run speed it allows.
        private float ActAnimation()
        {
            float speed = 0f;
            var focus = AI.focusCreature;
            switch (animation)
            {
                case Animation.Standard:
                    speed = AI.runSpeed;
                    break;
                case Animation.HearSound:
                    speed = 0f;
                    JawOpen = 0.2f;
                    break;
                case Animation.PreySpotted:
                    speed = 0f;
                    JawOpen = 1f;
                    break;
                case Animation.PreyReSpotted:
                    speed = AI.runSpeed * 0.3f;
                    JawOpen = Mathf.Lerp(JawOpen, 1f, Random.Value() * Random.Value());
                    break;
                case Animation.ThreatSpotted:
                    speed = 0.5f;
                    JawOpen = Random.Value();
                    if (focus != null && Template.CreatureRelationship(focus.representedCreature.creatureTemplate).type == Rel.Afraid && focus.VisualContact)
                    {
                        Vector2 dir = Custom.DirVec(mainBodyChunk.pos, focus.representedCreature.realizedCreature.DangerPos);
                        mainBodyChunk.vel += dir * (5f * LegsGripping);
                        bodyChunks[1].vel -= dir * (6f * LegsGripping);
                    }
                    break;
                case Animation.FightingStance:
                    JawOpen = 1f;
                    speed = 0f;
                    if (focus != null && !AI.UnpleasantFallRisk(room.GetTilePosition(mainBodyChunk.pos)) && focus.VisualContact)
                    {
                        Vector2 dir = Custom.DirVec(mainBodyChunk.pos, focus.representedCreature.realizedCreature.DangerPos);
                        mainBodyChunk.vel += dir * (4f * LegsGripping);
                        bodyChunks[1].vel -= dir * (2.2f * LegsGripping);
                        bodyChunks[2].vel -= dir * ((IsWallClimber ? 2.2f : 2.6f) * LegsGripping);
                    }
                    break;
                case Animation.ThreatReSpotted:
                    speed = AI.runSpeed * 0.8f;
                    JawOpen = Mathf.Lerp(JawOpen, Random.Value(), Random.Value());
                    break;
                case Animation.ShootTongue:
                case Animation.Spit:
                case Animation.PrepareToJump:
                    // Tongue, spit and jump belong to the breed specials; without them the lizard stands down.
                    EnterAnimation(Animation.Standard, true);
                    speed = AI.runSpeed;
                    break;
                case Animation.Jumping:
                    break;
                case Animation.PrepareToLounge:
                    bodyWiggleCounter = 0;
                    if (!lizardParams.canExitLoungeWarmUp || (AI.behavior == LizardAI.Behavior.Hunt && focus != null && focus.VisualContact))
                    {
                        speed = 0f;
                        if (focus != null && focus.representedCreature.realizedCreature != null)
                        {
                            if (LegsGripping > 0)
                                foreach (var chunk in bodyChunks)
                                    chunk.vel += Custom.DirVec(chunk.pos, focus.representedCreature.realizedCreature.mainBodyChunk.pos) * lizardParams.preLoungeCrouchMovement;
                            if (timeInAnimation >= lizardParams.preLoungeCrouch)
                                EnterAnimation(Animation.Lounge, false);
                        }
                        else
                        {
                            EnterAnimation(Animation.Standard, true);
                        }
                    }
                    else
                    {
                        EnterAnimation(Animation.Standard, true);
                    }
                    break;
                case Animation.Lounge:
                    if (timeInAnimation < lizardParams.loungeMaximumFrames
                        && (!lizardParams.canExitLounge || (AI.behavior == LizardAI.Behavior.Hunt && focus != null && focus.VisualContact)))
                    {
                        speed = 0f;
                        JawOpen += 0.1f;
                        if (timeInAnimation < lizardParams.loungePropulsionFrames && LegsGripping > 0)
                            for (int k = 0; k < 3; k++)
                                bodyChunks[k].vel += loungeDir * lizardParams.loungeSpeed / (k + 1);
                    }
                    else
                    {
                        // The lunge is over (or lost its target): snap shut and recover.
                        JawsSnapShut(mainBodyChunk.pos + Custom.DirVec(bodyChunks[1].pos, mainBodyChunk.pos) * 10f);
                        EnterAnimation(Animation.Standard, true);
                        postLoungeStun = 40;
                        inAllowedTerrainCounter = 0;
                    }
                    break;
                case Animation.ShakePrey:
                    speed = 0.75f;
                    if (grasps[0] == null && Random.Value() < 0.025f)
                        EnterAnimation(Animation.Standard, true);
                    break;
            }
            if (postLoungeStun > 0)
                speed *= 0.1f;
            if (Template.type == CreatureTemplate.Type.RedLizard && AI.runSpeed > 0.1f && animation != Animation.Spit)
                speed = Mathf.Lerp(speed, 1f, Mathf.Lerp(0.2f, 0.7f, AI.hunger));
            return speed;
        }

        // Lizard.GetFrameSpeed: this tick's push, from the breed's speed, grip, health and the terrain under each chunk.
        private float GetFrameSpeed(float runSpeed)
        {
            float grip = LegsGripping / 4f;
            grip = grip * (1f - lizardParams.noGripSpeed) + lizardParams.noGripSpeed;
            if (NoGripCounter < 1)
                grip = Mathf.Lerp(grip, 1f + BodyDesperation, BodyForce - 1f);
            if (grip == 0f && BodyWiggleFac > 0f)
            {
                for (int i = 0; i < 3; i++)
                {
                    if (bodyChunks[i].ContactPoint.x != 0 || bodyChunks[i].ContactPoint.y != 0)
                    {
                        grip = BodyWiggleFac * (0.5f + 0.5f * Mathf.Sin(bodyWiggle * 2f * Mathf.PI)) * 0.2f;
                        break;
                    }
                }
            }
            if (grip < 0.1f || BodyForce > 1.2f)
                bodyWiggleCounter++;
            else
                bodyWiggleCounter--;
            float speed = lizardParams.baseSpeed * grip * Mathf.Lerp(runSpeed, 1f, BodyForce - 1f);
            speed *= Mathf.Lerp(Random.Value(), 1f, (LizardState.health - 0.5f) * 2f);
            var multiplier = lizardParams.TerrainSpeed(room.aimap.getAItile(bodyChunks[0].pos).acc)
                             + lizardParams.TerrainSpeed(room.aimap.getAItile(bodyChunks[1].pos).acc)
                             + lizardParams.TerrainSpeed(room.aimap.getAItile(bodyChunks[2].pos).acc);
            multiplier /= 3f;
            speed *= multiplier.speed;
            var dir = followingConnection.DestTile - followingConnection.StartTile;
            if (dir.x != 0)
                return speed * multiplier.horizontal;
            if (dir.y > 0)
                return speed * multiplier.up;
            return speed * multiplier.down;
        }

        // Lizard.GripPointBehavior: hang on to a caught pole or ledge until footing returns.
        private void GripPointBehavior()
        {
            if (mainBodyChunk.vel.magnitude > 10f && inAllowedTerrainCounter > 5)
            {
                gripPoint = null;
                followingConnection = default;
            }
            else if (!applyGravity)
            {
                var next = ((LizardPather)AI.pathFinder).FollowPath(room.GetWorldCoordinate(mainBodyChunk.pos), 0, true);
                movementAnimation = null;
                if (followingConnection != default && followingConnection.type == Move.DropToClimb && next != default
                    && next.DestTile.y - next.StartTile.y == 0 && Math.Abs(next.DestTile.x - next.StartTile.x) == 1)
                {
                    next = new MovementConnection(Move.DropToClimb, next.startCoord, next.destinationCoord, next.distance);
                    movementAnimation = new MovementAnimation(this, next, 1, 0.99f, 0.75f);
                }
                gripPoint = null;
                followingConnection = default;
            }
            else
            {
                if (bodyChunks[1].pos.y < bodyChunks[0].pos.y)
                    bodyChunks[1].vel.y += Random.Value() * 2f;
                if (upcomingConnections.Count > 0)
                {
                    int side = IntVector2.ClampAtOne(room.GetTilePosition(mainBodyChunk.pos) - upcomingConnections[upcomingConnections.Count - 1].DestTile).x;
                    bodyChunks[1].vel.x += 3f * side;
                    bodyChunks[2].vel.x += Random.Value() * 3f * side;
                }
                else
                {
                    bodyChunks[1].vel.x += -2f + Random.Value() * 4f;
                }
            }
            mainBodyChunk.vel *= 0f;
            if (gripPoint.HasValue)
                mainBodyChunk.vel -= mainBodyChunk.pos - gripPoint.Value;
        }
    }
}
