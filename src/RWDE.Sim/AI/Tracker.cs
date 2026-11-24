using System;
using System.Collections.Generic;

namespace RWDE.Sim
{
    /// <summary>
    /// What a creature knows about the others around it, ported from the game's <c>Tracker</c>. Each tick
    /// it looks at one random creature in its room and starts tracking it on sight; each tracked creature
    /// is a <see cref="CreatureRepresentation"/> that remembers where it was last seen and, for an
    /// <see cref="ElaborateCreatureRepresentation"/>, spreads "ghosts" along the paths it may have taken.
    /// </summary>
    /// <remarks>
    /// Not ported: the debug visualizer, ripple layers, and the player's hidden state (no one plays on
    /// the desktop). The game asks the abstract object sticks whether the tracker's creature holds the
    /// noticed one; the desktop has no abstract sticks, so the realized grasps answer instead.
    /// </remarks>
    public class Tracker : AIModule
    {
        /// <summary>One tracked creature (<c>Tracker.CreatureRepresentation</c>).</summary>
        public abstract class CreatureRepresentation
        {
            protected CreatureRepresentation(Tracker parent, AbstractCreature representedCreature, float priority)
            {
                this.parent = parent;
                this.representedCreature = representedCreature;
                this.priority = priority;
                lastSeenCoord = representedCreature.pos.WashNode();
                visualContact = true;
                goneToOtherRoomSense = true;
            }

            public Tracker parent;
            public AbstractCreature representedCreature;
            public int forgetCounter;
            public int age;
            public RelationshipTracker.DynamicRelationship dynamicRelationship;
            private bool visualContact;
            private bool goneToOtherRoomSense;
            private int ticksSinceSeen;
            public bool deleteMeNextFrame;
            public float priority;
            public WorldCoordinate lastSeenCoord;

            /// <summary>Whether the creature is in sight right now; never when it isn't in the tracker's room.</summary>
            public bool VisualContact
            {
                get
                {
                    var realized = representedCreature.realizedCreature;
                    if (realized == null || parent.AI.creature.Room == null || realized.room != parent.AI.creature.Room.realizedRoom)
                        visualContact = false;
                    return visualContact;
                }
            }

            /// <summary>Ticks since last seen, not counting the first <c>seeAroundCorners</c> ticks out of sight.</summary>
            public int TicksSinceSeen => ticksSinceSeen < parent.seeAroundCorners ? 0 : ticksSinceSeen - parent.seeAroundCorners;

            public virtual int LowestGenerationAvailable => 1;

            /// <summary>How likely the creature is to be found at the best guess, falling with time and ghost generations.</summary>
            public float EstimatedChanceOfFinding
            {
                get
                {
                    float t = (LowestGenerationAvailable * 10f + TicksSinceSeen) / 4f;
                    if (visualContact)
                        return 1f;
                    if (t < 45f)
                        return Mathf.Clamp(1f / -(1f + Mathf.Pow((float)Math.E, -(t / 12f - 5f))) + 1.007f, 0f, 1f);
                    return 1f / (t - 7f) * 30f;
                }
            }

            public void HeardThisCreature()
            {
                if (ticksSinceSeen > parent.seeAroundCorners + 5)
                    ticksSinceSeen = (ticksSinceSeen - parent.seeAroundCorners) / 2;
            }

            public void MakeVisible()
            {
                ticksSinceSeen = 0;
                visualContact = true;
            }

            public virtual void Update()
            {
                age++;
                bool wasVisible = visualContact;
                ticksSinceSeen++;
                if (ticksSinceSeen > parent.seeAroundCorners)
                {
                    visualContact = false;
                    if (!representedCreature.InDen && representedCreature.pos.room == parent.AI.creature.pos.room && representedCreature.realizedCreature != null)
                    {
                        foreach (var chunk in representedCreature.realizedCreature.bodyChunks)
                        {
                            if (parent.AI.VisualContact(chunk))
                            {
                                ticksSinceSeen = 0;
                                visualContact = true;
                                break;
                            }
                        }
                    }
                }
                if (visualContact)
                {
                    if (representedCreature.realizedCreature == null || representedCreature.pos.room != parent.AI.creature.pos.room)
                    {
                        visualContact = false;
                        return;
                    }
                    lastSeenCoord = representedCreature.pos.WashNode();
                    if (!wasVisible)
                        parent.AI.CreatureSpotted(false, this);
                    forgetCounter = 0;
                    return;
                }
                forgetCounter++;
                if (goneToOtherRoomSense && representedCreature.pos.room != lastSeenCoord.room
                    && representedCreature.realizedCreature != null && representedCreature.realizedCreature.room != null)
                {
                    SenseThatCreatureHasLeftRoom();
                    goneToOtherRoomSense = false;
                }
                if (parent.framesToRememberCreatures > -1 && forgetCounter > parent.framesToRememberCreatures)
                    Destroy();
            }

            /// <summary>Where the creature probably is: here, where it was last seen.</summary>
            public virtual WorldCoordinate BestGuessForPosition() => lastSeenCoord;

            protected virtual void SenseThatCreatureHasLeftRoom()
            {
            }

            public void SetVisualContact(bool val)
            {
                visualContact = val;
            }

            public void Destroy()
            {
                deleteMeNextFrame = true;
            }

            public override string ToString() => GetType().Name + " of " + representedCreature;
        }

        /// <summary>
        /// A creature remembered only at its last seen tile (<c>Tracker.SimpleCreatureRepresentation</c>),
        /// optionally forgotten as soon as it is out of sight.
        /// </summary>
        public class SimpleCreatureRepresentation : CreatureRepresentation
        {
            public SimpleCreatureRepresentation(Tracker parent, AbstractCreature representedCreature, float priority, bool forgetWhenNotVisible)
                : base(parent, representedCreature, priority)
            {
                this.forgetWhenNotVisible = forgetWhenNotVisible;
            }

            public bool forgetWhenNotVisible;

            public override void Update()
            {
                base.Update();
                if (forgetWhenNotVisible && !VisualContact)
                    Destroy();
            }
        }

        /// <summary>
        /// A creature whose possible whereabouts are followed by ghosts once it is out of sight
        /// (<c>Tracker.ElaborateCreatureRepresentation</c>). The best guess is the most plausible ghost.
        /// </summary>
        public class ElaborateCreatureRepresentation : CreatureRepresentation
        {
            public ElaborateCreatureRepresentation(Tracker parent, AbstractCreature representedCreature, float priority, int maxGhosts)
                : base(parent, representedCreature, priority)
            {
                this.maxGhosts = maxGhosts;
                var body = representedCreature.realizedCreature.mainBodyChunk;
                ghosts = new List<Ghost> { new Ghost(this, body.pos, body.vel) };
                bestGhost = ghosts[0];
                bestGhostDirty = true;
            }

            public List<Ghost> ghosts;
            public Ghost bestGhost;
            public bool bestGhostDirty;
            private readonly int maxGhosts;
            private int lowestGenerationAvailable;
            private bool lastVisualContact;

            public int MaxGhosts => Custom.IntClamp((int)(maxGhosts * Mathf.Pow(EstimatedChanceOfFinding, 0.25f)), 2, maxGhosts);

            public override int LowestGenerationAvailable => lowestGenerationAvailable;

            public override void Update()
            {
                base.Update();
                if (VisualContact)
                {
                    if (ghosts.Count > 1)
                    {
                        ghosts.RemoveRange(1, ghosts.Count - 1);
                        bestGhost = ghosts[0];
                        bestGhostDirty = false;
                    }
                    ghosts[0].Reset();
                    lastSeenCoord = ghosts[0].coord;
                }
                else
                {
                    for (int i = ghosts.Count - 1; i >= 0; i--)
                    {
                        ghosts[i].Update();
                        if (ghosts[i].DeleteMeNextFrame && ghosts.Count > 1)
                        {
                            ghosts.RemoveAt(i);
                            bestGhostDirty = true;
                        }
                    }
                    if (lastVisualContact)
                        LostVisualContact();
                }
                lastVisualContact = VisualContact;
            }

            public override WorldCoordinate BestGuessForPosition()
            {
                if (bestGhostDirty)
                    FindBestGhost();
                if (bestGhost == null)
                    return new WorldCoordinate(-1, -1, -1, -1);
                return bestGhost.coord.room == parent.AI.creature.pos.room ? bestGhost.coord.WashNode() : bestGhost.coord;
            }

            // ElaborateCreatureRepresentation.FindBestGhost: the most reachable ghost, then the youngest and closest.
            private void FindBestGhost()
            {
                float best = -1f;
                int bestReach = 0;
                bestGhostDirty = false;
                lowestGenerationAvailable = -1;
                var ai = parent.AI;
                foreach (var ghost in ghosts)
                {
                    if (ghost == null || ai.creature == null || ai.creature.world == null)
                        continue;
                    float score = ghost.generation * 20
                                  + BetweenRoomsDistance(ai.creature.world, ai.creature.pos, ghost.coord) * 0.5f
                                  + BetweenRoomsDistance(ai.creature.world, lastSeenCoord, ghost.coord);
                    int reach = 0;
                    if (ai.pathFinder != null)
                        reach = (ai.pathFinder.CoordinateReachable(ghost.coord) ? 1 : 0) + (ai.pathFinder.CoordinatePossibleToGetBackFrom(ghost.coord) ? 1 : 0);
                    var room = ai.creature.world.GetAbstractRoom(ghost.coord);
                    if (room != null && room.realizedRoom != null && room.realizedRoom.GetTile(ghost.coord.Tile).Solid)
                        score -= 1000f;
                    if (reach > bestReach)
                    {
                        bestReach = reach;
                        best = score;
                        bestGhost = ghost;
                    }
                    else if (reach == bestReach && (best < 0f || score <= best))
                    {
                        best = score;
                        bestGhost = ghost;
                    }
                    if (ghost.generation < lowestGenerationAvailable || lowestGenerationAvailable < 0)
                        lowestGenerationAvailable = ghost.generation;
                }
            }

            // ElaborateCreatureRepresentation.LostVisualContact: one ghost stays where the creature was last
            // seen and a clone keeps moving; which one stops depends on whether the creature was standing still.
            private void LostVisualContact()
            {
                var realizedRoom = representedCreature.Room?.realizedRoom;
                if (realizedRoom == null || !realizedRoom.readyForAI)
                {
                    bestGhostDirty = true;
                    return;
                }
                var template = representedCreature.creatureTemplate;
                bool stoppedAtLastSeen = realizedRoom.aimap.TileAccessibleToCreature(bestGhost.coord.Tile, template);
                if (!stoppedAtLastSeen)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        if (realizedRoom.aimap.TileAccessibleToCreature(bestGhost.coord.Tile + Custom.fourDirections[i], template))
                        {
                            stoppedAtLastSeen = true;
                            break;
                        }
                    }
                }
                stoppedAtLastSeen = stoppedAtLastSeen && representedCreature.realizedCreature != null
                    && Vector2.Distance(representedCreature.realizedCreature.mainBodyChunk.lastLastPos, representedCreature.realizedCreature.mainBodyChunk.lastPos)
                    < Mathf.Lerp(template.offScreenSpeed, 2f, 0.5f);
                var ghost = ghosts[0];
                var clone = ghost.Clone(ghost.coord, 500);
                if (stoppedAtLastSeen)
                {
                    ghost.stopped = true;
                    ghost.coord.Tile = realizedRoom.aimap.TryForAccessibleNeighbor(ghost.coord.Tile, parent.AI.creature.creatureTemplate);
                    ghost.pos = realizedRoom.MiddleOfTile(ghost.coord.Tile);
                    clone.stopped = false;
                }
                else
                {
                    ghost.stopped = false;
                    clone.stopped = true;
                    clone.coord.Tile = realizedRoom.aimap.TryForAccessibleNeighbor(clone.coord.Tile, parent.AI.creature.creatureTemplate);
                    // The game places the stopped clone at the first ghost's tile.
                    clone.pos = realizedRoom.MiddleOfTile(ghost.coord.Tile);
                }
                bestGhostDirty = true;
            }

            protected override void SenseThatCreatureHasLeftRoom()
            {
                bool anyInItsRoom = false;
                foreach (var ghost in ghosts)
                {
                    if (ghost.coord.room == representedCreature.pos.room)
                    {
                        anyInItsRoom = true;
                        ghost.generation = 0;
                    }
                    else
                    {
                        ghost.generation += 100;
                    }
                }
                var realizedRoom = representedCreature.Room?.realizedRoom;
                if (!anyInItsRoom && realizedRoom != null)
                {
                    var coord = realizedRoom.LocalCoordinateOfNode(representedCreature.pos.abstractNode);
                    var ghost = new Ghost(this, realizedRoom.MiddleOfTile(coord.Tile), realizedRoom.ShorcutEntranceHoleDirection(coord.Tile).ToVector2());
                    ghost.forbiddenRoomExit = ghost.coord;
                    ghosts.Add(ghost);
                }
                bestGhostDirty = true;
            }
        }

        /// <summary>
        /// A guess at where an unseen creature went (<c>Tracker.Ghost</c>): it walks the creature's own
        /// movement connections, away from where the tracker can see, splitting at forks.
        /// </summary>
        public class Ghost
        {
            public Ghost(ElaborateCreatureRepresentation parent, Vector2 pos, Vector2 vel)
            {
                this.parent = parent;
                this.pos = pos;
                this.vel = vel;
                coord = parent.representedCreature.pos.WashNode();
                lastCoord = coord;
                moveBuffer = parent.parent.AI.Random.Value();
                generation = 0;
                stopped = parent.parent.ghostSpeed == 0f;
                pushForward = parent.parent.ghostPush;
                allowedToMoveInVisibleAreas = false;
                forbiddenRoomExit = new WorldCoordinate(-1, -1, -1, -1);
            }

            public ElaborateCreatureRepresentation parent;
            public Vector2 pos;
            public Vector2 vel;
            public WorldCoordinate coord;
            public WorldCoordinate lastCoord;
            public WorldCoordinate forbiddenRoomExit;
            public float moveBuffer;
            public int pushForward;
            public int generation;
            public int connectionsFollowed;
            public bool DeleteMeNextFrame;
            public bool stopped;
            public bool allowedToMoveInVisibleAreas;

            private ArtificialIntelligence AI => parent.parent.AI;

            public void Move(WorldCoordinate newCoord, Vector2 newPos)
            {
                coord = newCoord;
                lastCoord = newCoord;
                pos = newPos;
            }

            public void Update()
            {
                var realizedRoom = parent.representedCreature.world.GetAbstractRoom(coord.room)?.realizedRoom;
                if (stopped || realizedRoom == null || !realizedRoom.readyForAI)
                    return;
                var template = parent.representedCreature.creatureTemplate;
                if (!realizedRoom.aimap.TileAccessibleToCreature(pos, template))
                {
                    // Off its terrain: the ghost falls (or drifts, for flyers) until it lands.
                    pos += vel;
                    if (!template.canFly)
                        vel.y -= 0.9f;
                    coord.Tile = realizedRoom.GetTilePosition(pos);
                    lastCoord = coord;
                    if (vel.x != 0f && !NeighbourLegal(new IntVector2(vel.x < 0f ? -1 : 1, 0)))
                    {
                        pos.x = realizedRoom.MiddleOfTile(pos).x;
                        vel.x = 0f;
                    }
                    if (vel.y != 0f && !NeighbourLegal(new IntVector2(0, vel.y < 0f ? -1 : 1)))
                    {
                        pos.y = realizedRoom.MiddleOfTile(pos).y;
                        vel.y = 0f;
                    }
                    return;
                }
                moveBuffer += template.offScreenSpeed * parent.parent.ghostSpeed;
                if (!(moveBuffer > 1f))
                    return;
                moveBuffer = 0f;
                var aiTile = realizedRoom.aimap.getAItile(pos);
                if (realizedRoom.GetTile(pos).Terrain == Room.Tile.TerrainType.ShortcutEntrance && !forbiddenRoomExit.Equals(coord)
                    && realizedRoom.shortcutData(pos).shortCutType == ShortcutData.Type.RoomExit)
                {
                    // Into a pipe to another room: a clone continues there.
                    int destNode = realizedRoom.shortcutData(pos).destNode;
                    var connections = realizedRoom.abstractRoom.connections;
                    var otherRoom = destNode >= 0 && destNode < connections.Length ? realizedRoom.world.GetAbstractRoom(connections[destNode]) : null;
                    if (otherRoom != null)
                    {
                        var dest = new WorldCoordinate(otherRoom.index, -1, -1, otherRoom.ExitIndex(coord.room));
                        if (otherRoom.realizedRoom != null && dest.abstractNode > -1)
                            dest.Tile = otherRoom.realizedRoom.ShortcutLeadingToNode(dest.abstractNode).StartTile;
                        Clone(dest, 0);
                        parent.ghosts[parent.ghosts.Count - 1].forbiddenRoomExit = parent.ghosts[parent.ghosts.Count - 1].coord;
                    }
                    DeleteMeNextFrame = true;
                    return;
                }
                float best = -1f;
                var bestConnection = default(MovementConnection);
                float second = -1f;
                var secondConnection = default(MovementConnection);
                foreach (var connection in aiTile.outgoingPaths)
                {
                    if (!ConnectionLegal(connection))
                        continue;
                    float score = Vector2.Distance(IntVector2.ToVector2(connection.DestTile - connection.StartTile), vel * 100f);
                    if (!AI.creature.creatureTemplate.ConnectionResistance(connection.type).Allowed)
                        score *= 2f;
                    if (AI.pathFinder != null)
                    {
                        if (!AI.pathFinder.CoordinateReachable(connection.destinationCoord))
                            score *= 2f;
                        if (!AI.pathFinder.CoordinatePossibleToGetBackFrom(connection.destinationCoord))
                            score *= 2f;
                    }
                    if (connection.type == MovementConnection.MovementType.DropToClimb || connection.type == MovementConnection.MovementType.DropToFloor)
                        score *= 2f;
                    if (score < best || best < 0f)
                    {
                        best = score;
                        bestConnection = connection;
                    }
                    else if (score < second || second < 0f)
                    {
                        second = score;
                        secondConnection = connection;
                    }
                }
                if (bestConnection != default)
                {
                    if (secondConnection != default)
                    {
                        if (parent.ghosts.Count < parent.MaxGhosts)
                            Clone(secondConnection.destinationCoord, 1);
                        else
                            stopped = true;
                    }
                    vel = (vel * 4f + Custom.DirVec(IntVector2.ToVector2(bestConnection.StartTile), IntVector2.ToVector2(bestConnection.DestTile))) / 5f;
                    lastCoord = coord;
                    coord.Tile = bestConnection.DestTile;
                    pos = realizedRoom.MiddleOfTile(coord.Tile);
                    connectionsFollowed++;
                    // Two ghosts on one tile: the older generation wins.
                    foreach (var ghost in parent.ghosts)
                    {
                        if (ghost != this && ghost.coord.room == coord.room && ghost.coord.x == coord.x && ghost.coord.y == coord.y)
                        {
                            if (ghost.generation <= generation && !ghost.DeleteMeNextFrame)
                                DeleteMeNextFrame = true;
                            else if (!DeleteMeNextFrame)
                                ghost.DeleteMeNextFrame = true;
                        }
                    }
                    return;
                }
                stopped = true;
            }

            public bool ConnectionLegal(MovementConnection connection)
            {
                var template = parent.representedCreature.creatureTemplate;
                var destRoom = parent.representedCreature.world.GetAbstractRoom(connection.destinationCoord.room)?.realizedRoom;
                if (connection.DestTile != lastCoord.Tile && template.ConnectionResistance(connection.type).Allowed
                    && (destRoom == null || destRoom.aimap.TileAccessibleToCreature(connection.DestTile, template))
                    && IntVector2.ClampAtOne(coord.Tile - connection.DestTile) != IntVector2.ClampAtOne(coord.Tile - lastCoord.Tile))
                    return allowedToMoveInVisibleAreas || !AI.VisualContact(connection.destinationCoord, 0f);
                return false;
            }

            /// <summary>Back onto the creature, which is in sight again.</summary>
            public void Reset()
            {
                var realized = parent.representedCreature.realizedCreature;
                pos = realized.mainBodyChunk.pos;
                coord = parent.representedCreature.pos.WashNode();
                lastCoord = coord;
                generation = 0;
                stopped = false;
                vel = realized.bodyChunks[0].vel;
                for (int i = 1; i < realized.bodyChunks.Length; i++)
                    vel += realized.bodyChunks[i].vel;
                vel /= realized.bodyChunks.Length;
            }

            /// <summary>The tracker looked where the ghost is and saw nothing: move it on, or drop it.</summary>
            public void Push()
            {
                if (pushForward < 1 && parent.ghosts.Count > 1)
                {
                    DeleteMeNextFrame = true;
                }
                else
                {
                    pushForward--;
                    generation++;
                    lastCoord = coord;
                    allowedToMoveInVisibleAreas = true;
                    for (int i = 0; i < parent.parent.ghostPushSpeed; i++)
                    {
                        if (AI.Random.Value() < 0.5f)
                            vel = Custom.DirVec(AI.creature.realizedCreature.mainBodyChunk.pos, pos) * 10f;
                        else
                            vel = Custom.DegToVec(AI.Random.Value() * 360f) * 10f;
                        moveBuffer = 1f;
                        stopped = false;
                        Update();
                        if (AI.creature.pos.room != coord.room)
                            break;
                    }
                    allowedToMoveInVisibleAreas = false;
                }
                parent.bestGhostDirty = true;
            }

            public Ghost Clone(WorldCoordinate dest, int generationAdd)
            {
                var ghost = new Ghost(parent, pos, vel) { coord = dest };
                var destRoom = AI.creature.world.GetAbstractRoom(dest.room)?.realizedRoom;
                if (destRoom != null)
                    ghost.pos = destRoom.MiddleOfTile(dest.Tile);
                ghost.lastCoord = coord;
                ghost.vel = Custom.DirVec(IntVector2.ToVector2(coord.Tile), IntVector2.ToVector2(dest.Tile));
                ghost.generation = generation + generationAdd;
                ghost.pushForward = parent.parent.ghostPush - generation;
                parent.bestGhostDirty = true;
                parent.ghosts.Add(ghost);
                return ghost;
            }

            public bool NeighbourLegal(IntVector2 nb)
            {
                var realizedRoom = parent.representedCreature.world.GetAbstractRoom(coord.room)?.realizedRoom;
                if (realizedRoom == null)
                    return false;
                var template = parent.representedCreature.creatureTemplate;
                if (realizedRoom.aimap.TileAccessibleToCreature(pos, template))
                    return realizedRoom.aimap.TileAccessibleToCreature(realizedRoom.GetTilePosition(pos) + nb, template);
                return realizedRoom.GetTile(realizedRoom.GetTilePosition(pos) + nb).Terrain != Room.Tile.TerrainType.Solid;
            }
        }

        private readonly List<CreatureRepresentation> creatures = new List<CreatureRepresentation>();
        private readonly Dictionary<AbstractCreature, int> trackedCreatures;
        private readonly int seeAroundCorners;
        public int maxTrackedCreatures;
        private readonly float ghostSpeed;
        protected int ghostPush;
        protected int ghostPushSpeed;
        protected int ghostDismissalRange;
        public bool useTrackedCreatures;
        /// <summary>Ticks out of sight before a creature is forgotten; -1 never forgets.</summary>
        public int framesToRememberCreatures;
        /// <summary>Set by the creature's <see cref="NoiseTracker"/>, as in the game.</summary>
        public NoiseTracker noiseTracker;

        public Tracker(ArtificialIntelligence AI, int seeAroundCorners, int maxTrackedCreatures, int framesToRememberCreatures,
            float ghostSpeed, int ghostPush, int ghostPushSpeed, int ghostDismissalRange, bool useTrackedCreatures = false)
            : base(AI)
        {
            this.seeAroundCorners = seeAroundCorners;
            this.maxTrackedCreatures = maxTrackedCreatures;
            this.framesToRememberCreatures = framesToRememberCreatures;
            this.ghostSpeed = ghostSpeed;
            this.ghostPush = ghostPush;
            this.ghostPushSpeed = ghostPushSpeed;
            this.ghostDismissalRange = ghostDismissalRange;
            if (useTrackedCreatures)
            {
                this.useTrackedCreatures = true;
                trackedCreatures = new Dictionary<AbstractCreature, int>();
            }
        }

        public int CreaturesCount => creatures.Count;

        public CreatureRepresentation GetRep(int index) => creatures[index];

        public override void Update()
        {
            if (creatures.Count > 0)
            {
                // One random tracked creature a tick: push its ghosts, or drop it when its spot is in plain sight.
                int index = AI.Random.Range(0, creatures.Count);
                var rep = creatures[index];
                if (rep is ElaborateCreatureRepresentation elaborate && elaborate.ghosts.Count > 0)
                {
                    var ghost = elaborate.ghosts[AI.Random.Range(0, elaborate.ghosts.Count)];
                    if (!ghost.parent.VisualContact && !ghost.DeleteMeNextFrame && AI.creature.pos.room == ghost.coord.room && AI.VisualContact(ghost.pos, 0f)
                        && (Custom.ManhattanDistance(AI.creature.pos.Tile, ghost.coord.Tile) < 2
                            || (Vector2.Distance(IntVector2.ToVector2(AI.creature.pos.Tile), IntVector2.ToVector2(ghost.coord.Tile)) < ghostDismissalRange
                                && AI.VisualContact(ghost.coord, 0f))))
                        ghost.Push();
                }
                else if (!rep.VisualContact && AI.creature.Room.realizedRoom != null)
                {
                    var guess = rep.BestGuessForPosition();
                    if (AI.VisualContact(AI.creature.Room.realizedRoom.MiddleOfTile(guess.Tile), 0f)
                        && (Custom.ManhattanDistance(AI.creature.pos.Tile, guess.Tile) < 2
                            || (Vector2.Distance(IntVector2.ToVector2(AI.creature.pos.Tile), IntVector2.ToVector2(guess.Tile)) < ghostDismissalRange
                                && AI.VisualContact(guess, 0f))))
                        rep.Destroy();
                }
                if (rep.VisualContact && rep.representedCreature.state.dead && AI.TrackerToDiscardDeadCreature(rep.representedCreature))
                    rep.Destroy();
            }

            var roomCreatures = AI.creature.Room.creatures;
            if (roomCreatures.Count <= 0)
                return;
            var look = roomCreatures[AI.Random.Range(0, roomCreatures.Count)];
            bool newOne = look != AI.creature;
            if (!useTrackedCreatures)
            {
                for (int i = creatures.Count - 1; i >= 0; i--)
                {
                    creatures[i].Update();
                    if (creatures[i].representedCreature == look)
                        newOne = false;
                    if (creatures[i].deleteMeNextFrame)
                        creatures.RemoveAt(i);
                }
            }
            else
            {
                for (int i = creatures.Count - 1; i >= 0; i--)
                {
                    var rep = creatures[i];
                    if (trackedCreatures.Count != 0 && trackedCreatures.ContainsKey(rep.representedCreature))
                    {
                        if (!rep.VisualContact)
                        {
                            trackedCreatures[rep.representedCreature]--;
                            if (trackedCreatures[rep.representedCreature] == 0)
                            {
                                trackedCreatures.Remove(rep.representedCreature);
                                break;
                            }
                        }
                        else if (trackedCreatures[rep.representedCreature] < 35)
                        {
                            trackedCreatures[rep.representedCreature] += 2;
                        }
                        rep.Update();
                    }
                    else
                    {
                        rep.SetVisualContact(false);
                    }
                    if (rep.representedCreature == look)
                        newOne = false;
                    if (rep.deleteMeNextFrame)
                        creatures.RemoveAt(i);
                }
            }
            if (!newOne || look.realizedCreature == null || look.realizedCreature.inShortcut)
                return;
            foreach (var chunk in look.realizedCreature.bodyChunks)
            {
                if (AI.VisualContact(chunk))
                {
                    CreatureNoticed(look);
                    break;
                }
            }
        }

        public CreatureRepresentation RepresentationForObject(PhysicalObject obj, bool AddIfMissing)
        {
            if (!(obj is Creature creature))
                return null;
            foreach (var rep in creatures)
                if (rep.representedCreature.realizedCreature == obj)
                    return rep;
            return AddIfMissing ? CreatureNoticed(creature.abstractCreature) : null;
        }

        public void AddTrackedCreature(AbstractCreature aCreature)
        {
            if (useTrackedCreatures)
                trackedCreatures[aCreature] = 80;
        }

        public CreatureRepresentation RepresentationForCreature(AbstractCreature checkCrit, bool addIfMissing)
        {
            foreach (var rep in creatures)
                if (rep.representedCreature == checkCrit)
                    return rep;
            return addIfMissing && checkCrit != null ? CreatureNoticed(checkCrit) : null;
        }

        // Tracker.CreatureNoticed: starts tracking a creature, dropping the least interesting one when full.
        private CreatureRepresentation CreatureNoticed(AbstractCreature crit)
        {
            if (crit.realizedCreature == null)
                return null;
            if (crit.state.dead && AI.TrackerToDiscardDeadCreature(crit))
                return null;
            if (AI.StaticRelationship(crit).type == CreatureTemplate.Relationship.Type.DoesntTrack)
                return null;
            bool heldTogether = AI.creature.creatureTemplate.grasps > 0 && HeldTogether(AI.creature.realizedCreature, crit.realizedCreature);
            var rep = AI.CreateTrackerRepresentationForCreature(crit);
            if (rep == null)
                return null;
            AI.CreatureSpotted(!heldTogether, rep);
            AI.relationshipTracker?.EstablishDynamicRelationship(rep);
            creatures.Add(rep);
            if (creatures.Count > maxTrackedCreatures)
            {
                float lowest = float.MaxValue;
                CreatureRepresentation drop = null;
                foreach (var other in creatures)
                {
                    float intensity = other.dynamicRelationship != null
                        ? other.dynamicRelationship.currentRelationship.intensity
                        : AI.creature.creatureTemplate.CreatureRelationship(other.representedCreature.creatureTemplate).intensity;
                    float score = intensity * 100000f
                                  + (other.VisualContact ? 2f : 1f) / (1f + Vector2.Distance(IntVector2.ToVector2(other.BestGuessForPosition().Tile), IntVector2.ToVector2(AI.creature.pos.Tile)));
                    score /= Mathf.Lerp(other.forgetCounter, 100f, 0.7f);
                    if (score < lowest)
                    {
                        lowest = score;
                        drop = other;
                    }
                }
                if (drop == rep)
                    rep = null;
                drop.Destroy();
            }
            return rep;
        }

        // Stands in for the game's check of the abstract creature sticks between the two creatures.
        private static bool HeldTogether(Creature a, Creature b)
        {
            if (a == null || b == null)
                return false;
            return Holds(a, b) || Holds(b, a);
        }

        private static bool Holds(Creature holder, Creature held)
        {
            if (holder.grasps == null)
                return false;
            foreach (var grasp in holder.grasps)
                if (grasp != null && grasp.grabbed == held)
                    return true;
            return false;
        }

        /// <summary>Sees <paramref name="crit"/> now, starting to track it if needed (<c>Tracker.SeeCreature</c>).</summary>
        public void SeeCreature(AbstractCreature crit)
        {
            bool known = false;
            for (int i = 0; i < creatures.Count; i++)
            {
                if (creatures[i].representedCreature == crit)
                {
                    creatures[i].MakeVisible();
                    known = true;
                }
            }
            if (!known)
                CreatureNoticed(crit);
        }

        public void ForgetCreature(AbstractCreature crit)
        {
            for (int i = 0; i < creatures.Count; i++)
            {
                if (creatures[i].representedCreature == crit)
                {
                    creatures[i].Destroy();
                    break;
                }
            }
        }

        /// <summary>
        /// Distance in tiles between two coordinates, through the exit between their rooms when they
        /// differ (<c>Custom.BetweenRoomsDistance</c>).
        /// </summary>
        internal static float BetweenRoomsDistance(World world, WorldCoordinate a, WorldCoordinate b)
        {
            if (a.room == b.room)
                return a.Tile.FloatDist(b.Tile);
            var roomA = world.GetAbstractRoom(a);
            var roomB = world.GetAbstractRoom(b);
            if (roomA == null || roomB == null || roomA.ExitIndex(b.room) < 0 || roomB.ExitIndex(a.room) < 0)
                return float.MaxValue;
            // Desktop rooms are always realized with their shortcuts ready.
            float distA = a.Tile.FloatDist(roomA.realizedRoom.LocalCoordinateOfNode(roomA.ExitIndex(b.room)).Tile);
            float distB = b.Tile.FloatDist(roomB.realizedRoom.LocalCoordinateOfNode(roomB.ExitIndex(a.room)).Tile);
            return distA + distB;
        }
    }
}
