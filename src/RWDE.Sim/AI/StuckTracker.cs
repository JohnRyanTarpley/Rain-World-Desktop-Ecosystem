using System;
using System.Collections.Generic;

namespace RWDE.Sim
{
    /// <summary>
    /// Notices when a creature keeps circling the same tiles without reaching its goal, ported from the
    /// game's <c>StuckTracker</c>. Its utility rises with the stuck counter; creatures use it to try harder.
    /// Optional sub-modules add other signs of being stuck.
    /// </summary>
    public class StuckTracker : AIModule
    {
        /// <summary>A part of the stuck tracker (<c>StuckTracker.StuckTrackerModule</c>).</summary>
        public abstract class StuckTrackerModule
        {
            protected StuckTrackerModule(StuckTracker parent)
            {
                this.parent = parent;
            }

            public StuckTracker parent;
            public ArtificialIntelligence AI => parent.AI;
            public virtual bool AddToStuckCounter => false;

            public virtual void Update()
            {
            }
        }

        /// <summary>Close to the destination but unable to see it (<c>CloseToGoalButNotSeeingItTracker</c>).</summary>
        public class CloseToGoalButNotSeeingItTracker : StuckTrackerModule
        {
            public float tileDistance;
            public int counter;
            public int counterMin = 20;
            public int counterMax = 200;

            public CloseToGoalButNotSeeingItTracker(StuckTracker parent, float tileDistance) : base(parent)
            {
                this.tileDistance = tileDistance;
            }

            public float Stuck => Mathf.InverseLerp(counterMin, counterMax, counter);
            public override bool AddToStuckCounter => counter > counterMin;

            public override void Update()
            {
                var dest = AI.pathFinder.GetDestination;
                var realized = AI.creature.realizedCreature;
                if (dest.room == AI.creature.pos.room && realized != null && realized.room != null
                    && dest.Tile.FloatDist(AI.creature.pos.Tile) < tileDistance
                    && !AI.VisualContact(realized.room.MiddleOfTile(dest.Tile), 0f))
                    counter++;
                else
                    counter--;
                counter = Custom.IntClamp(counter, 0, counterMax);
            }
        }

        /// <summary>While stuck, looks for a reachable spot about 10 tiles away to head for (<c>GetUnstuckPosCalculator</c>).</summary>
        public class GetUnstuckPosCalculator : StuckTrackerModule
        {
            public WorldCoordinate unstuckGoalPosition;

            public GetUnstuckPosCalculator(StuckTracker parent) : base(parent)
            {
                unstuckGoalPosition = AI.creature.pos;
            }

            public override void Update()
            {
                if (parent.stuckCounter <= 0)
                    return;
                var realized = AI.creature.realizedCreature;
                var test = realized.room.GetWorldCoordinate(realized.mainBodyChunk.pos + Custom.RNV(AI.Random) * (AI.Random.Value() * 15f * 20f));
                if (UnstuckGoalScore(test) < UnstuckGoalScore(unstuckGoalPosition))
                    unstuckGoalPosition = test;
            }

            private float UnstuckGoalScore(WorldCoordinate testPos)
            {
                if (!AI.pathFinder.CoordinateReachableAndGetbackable(testPos))
                    return float.MaxValue;
                float score = Mathf.Abs(10f - testPos.Tile.FloatDist(AI.creature.pos.Tile));
                if (testPos.Tile.FloatDist(AI.creature.pos.Tile) < 20f && AI.creature.realizedCreature.room.VisualContact(testPos.Tile, AI.creature.pos.Tile))
                    score -= 1000f;
                return score;
            }
        }

        /// <summary>While stuck, notes a shortcut entrance within reach of the body (<c>StuckCloseToShortcutModule</c>).</summary>
        public class StuckCloseToShortcutModule : StuckTrackerModule
        {
            private readonly bool activeWhenStuck = true;
            public IntVector2? foundShortCut;

            public StuckCloseToShortcutModule(StuckTracker parent) : base(parent)
            {
            }

            public override void Update()
            {
                foundShortCut = null;
                var room = AI.creature.Room?.realizedRoom;
                if ((activeWhenStuck && parent.stuckCounter < 1) || room == null || AI.creature.realizedCreature.enteringShortCut.HasValue)
                    return;
                for (int n = 0; !foundShortCut.HasValue && n < room.shortcuts.Length; n++)
                {
                    int ends = room.shortcuts[n].shortCutType == ShortcutData.Type.Normal ? 2 : 1;
                    for (int i = 0; !foundShortCut.HasValue && i < ends; i++)
                    {
                        IntVector2 entrance = i == 0 ? room.shortcuts[n].StartTile : room.shortcuts[n].DestTile;
                        if (room.GetTile(entrance).Terrain != Room.Tile.TerrainType.ShortcutEntrance)
                            continue;
                        var chunks = AI.creature.realizedCreature.bodyChunks;
                        for (int j = 0; j < chunks.Length; j++)
                        {
                            if (Custom.ManhattanDistance(entrance, room.GetTilePosition(chunks[j].pos)) < 3)
                            {
                                foundShortCut = entrance;
                                break;
                            }
                        }
                    }
                }
            }
        }

        /// <summary>The creature's most recent distinct moves, newest first (<c>StuckTracker.MoveBacklog</c>).</summary>
        public class MoveBacklog : StuckTrackerModule
        {
            public int trackedMoves = 10;
            public List<MovementConnection> log = new List<MovementConnection>();

            public MoveBacklog(StuckTracker parent) : base(parent)
            {
            }

            public void Reset() => log.Clear();

            /// <summary>Logs a move unless it is empty or the same as the last one.</summary>
            public void ReportNewMove(MovementConnection connection)
            {
                if (connection == default(MovementConnection) || (log.Count != 0 && log[0] == connection))
                    return;
                log.Insert(0, connection);
                if (log.Count > trackedMoves)
                    log.RemoveAt(log.Count - 1);
            }

            /// <summary>Whether <paramref name="testConnection"/> is among the last <paramref name="howManyStepsBack"/> logged moves.</summary>
            public bool IsMoveInLog(MovementConnection testConnection, int howManyStepsBack)
            {
                if (testConnection == default(MovementConnection) || log.Count == 0)
                    return false;
                for (int i = 0; i < Math.Min(howManyStepsBack, log.Count); i++)
                    if (log[i] == testConnection)
                        return true;
                return false;
            }
        }

        public List<IntVector2> pastPositions;
        public int stuckCounter;
        public int notFollowingPathToCurrentGoalCounter;
        public int notFollowingCurrentGoalMax = 200;
        public int goalSatisfactionDistance = 3;
        public int totalTrackedLastPositions = 80;
        public int checkPastPositionsFrom = 40;
        public int pastPosStuckDistance = 4;
        public int pastStuckPositionsCloseToIncrementStuckCounter = 30;
        public int minStuckCounter = 100;
        public int maxStuckCounter = 200;
        public bool trackPastPositions;
        public bool trackNotFollowingCurrentGoal;
        public bool calculateGetUnstuckPosition;
        /// <summary>Set by the creature when it is where it wants to be, so standing still doesn't count as stuck.</summary>
        public bool satisfiedWithThisPosition;
        private readonly List<StuckTrackerModule> subModules = new List<StuckTrackerModule>();
        public CloseToGoalButNotSeeingItTracker closeToGoalButNotSeeingItTracker;
        public GetUnstuckPosCalculator getUnstuckPosCalculator;
        public StuckCloseToShortcutModule stuckCloseToShortcutModule;
        public MoveBacklog moveBacklog;

        public StuckTracker(ArtificialIntelligence AI, bool trackPastPositions, bool trackNotFollowingCurrentGoal) : base(AI)
        {
            this.trackPastPositions = trackPastPositions;
            this.trackNotFollowingCurrentGoal = trackNotFollowingCurrentGoal;
            if (trackPastPositions)
                pastPositions = new List<IntVector2>();
        }

        public override void NewRoom(Room room)
        {
            base.NewRoom(room);
            moveBacklog?.Reset();
        }

        public override void Update()
        {
            for (int i = 0; i < subModules.Count; i++)
                subModules[i].Update();
            if (trackNotFollowingCurrentGoal)
            {
                if (notFollowingPathToCurrentGoalCounter < notFollowingCurrentGoalMax && AI.pathFinder.GetEffectualDestination != AI.pathFinder.GetDestination)
                    notFollowingPathToCurrentGoalCounter++;
                else if (notFollowingPathToCurrentGoalCounter > 0)
                    notFollowingPathToCurrentGoalCounter--;
            }
            bool stuck = false;
            if (trackPastPositions)
            {
                int close = 0;
                if (!satisfiedWithThisPosition
                    && (Custom.ManhattanDistance(AI.creature.pos, AI.pathFinder.GetEffectualDestination) > goalSatisfactionDistance
                        || notFollowingPathToCurrentGoalCounter > notFollowingCurrentGoalMax / 2))
                {
                    pastPositions.Insert(0, AI.creature.pos.Tile);
                    if (pastPositions.Count > totalTrackedLastPositions)
                        pastPositions.RemoveAt(pastPositions.Count - 1);
                    for (int j = checkPastPositionsFrom; j < pastPositions.Count; j++)
                        if (Custom.DistLess(AI.creature.pos.Tile, pastPositions[j], pastPosStuckDistance))
                            close++;
                }
                if (close > pastStuckPositionsCloseToIncrementStuckCounter)
                    stuck = true;
            }
            for (int k = 0; !stuck && k < subModules.Count; k++)
                stuck = subModules[k].AddToStuckCounter;
            if (stuck)
                stuckCounter++;
            else
                stuckCounter -= 2;
            stuckCounter = Custom.IntClamp(stuckCounter, 0, maxStuckCounter);
        }

        public void Reset() => stuckCounter = 0;

        public override float Utility() => Mathf.InverseLerp(minStuckCounter, maxStuckCounter, stuckCounter);

        public void AddSubModule(StuckTrackerModule newModule)
        {
            subModules.Add(newModule);
            switch (newModule)
            {
                case CloseToGoalButNotSeeingItTracker m: closeToGoalButNotSeeingItTracker = m; break;
                case GetUnstuckPosCalculator m: getUnstuckPosCalculator = m; break;
                case StuckCloseToShortcutModule m: stuckCloseToShortcutModule = m; break;
                case MoveBacklog m: moveBacklog = m; break;
            }
        }
    }
}
