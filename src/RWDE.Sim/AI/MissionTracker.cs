using System.Collections.Generic;

namespace RWDE.Sim
{
    /// <summary>
    /// Longer-term goals a creature has taken on, ported from the game's <c>MissionTracker</c>. Its utility
    /// is that of its most important mission, which the AI acts on when that utility wins.
    /// </summary>
    /// <remarks>
    /// The game abandons a <see cref="LeaveRoom"/> mission in a single-room (arena) world. A desktop room
    /// can also have no exit nodes at all (no pipes to other monitors), which never happens in the game;
    /// there the mission is abandoned too rather than indexing a missing exit.
    /// </remarks>
    public class MissionTracker : AIModule
    {
        /// <summary>A goal with a utility and an optional time limit (<c>MissionTracker.Mission</c>).</summary>
        public abstract class Mission
        {
            protected Mission(MissionTracker mTracker, float utility, int forgetCounter)
            {
                this.mTracker = mTracker;
                baseUtility = utility;
                currentUtility = utility;
                this.forgetCounter = forgetCounter;
            }

            public MissionTracker mTracker;
            public float baseUtility;
            public float currentUtility;
            public bool deleteMeNextFrame;
            /// <summary>Ticks until the mission is given up; -1 for never.</summary>
            public int forgetCounter;

            public virtual void UpdateUtility()
            {
                if (forgetCounter > -1)
                {
                    forgetCounter--;
                    if (forgetCounter < 1)
                        Abandon();
                }
            }

            public virtual void Act()
            {
            }

            public void Abandon() => deleteMeNextFrame = true;
        }

        /// <summary>
        /// Go to another room (<c>MissionTracker.LeaveRoom</c>): picks an exit (away from threats if
        /// there are any, else the nearest), then a node beyond the next room, and heads there.
        /// </summary>
        public class LeaveRoom : Mission
        {
            public WorldCoordinate? destination;
            public int destUnreachable;

            public LeaveRoom(MissionTracker mTracker, float utility, int forgetCounter) : base(mTracker, utility, forgetCounter)
            {
            }

            public override void Act()
            {
                var AI = mTracker.AI;
                WorldCoordinate coord = AI.creature.pos;
                World world = AI.creature.world;
                var template = AI.creature.creatureTemplate;
                if (!coord.NodeDefined)
                    coord = QuickConnectivity.DefineNodeOfLocalCoordinate(coord, world, template);
                if (destination.HasValue)
                {
                    if (coord.CompareDisregardingTile(destination.Value))
                        Abandon();
                    else
                        AI.creature.abstractAI.SetDestination(destination.Value);
                    if (AI.pathFinder != null && (!AI.pathFinder.CoordinatePossibleToGetBackFrom(destination.Value) || !AI.pathFinder.CoordinateReachable(destination.Value)))
                    {
                        destUnreachable++;
                        if (destUnreachable > 1000)
                            Abandon();
                    }
                    else
                    {
                        destUnreachable = 0;
                    }
                    return;
                }

                var here = world.GetAbstractRoom(coord);
                int bestDist = int.MaxValue;
                int exit = -1;
                if (here.realizedRoom != null && here.realizedRoom.readyForAI)
                {
                    if (AI.threatTracker != null && AI.threatTracker.Utility() > 0f)
                    {
                        exit = AI.threatTracker.FindMostAttractiveExit();
                        if (exit != -1 && here.nodes[exit].type != AbstractRoomNode.Type.Exit)
                            exit = -1;
                    }
                    if (exit == -1)
                    {
                        for (int i = 0; i < here.connections.Length; i++)
                        {
                            int dist = here.realizedRoom.aimap.ExitDistanceForCreatureAndCheckNeighbours(coord.Tile,
                                here.CommonToCreatureSpecificNodeIndex(i, template), template);
                            if (dist > -1 && dist < bestDist && here.nodes[i].type == AbstractRoomNode.Type.Exit && here.connections[i] > -1)
                            {
                                bestDist = dist;
                                exit = i;
                            }
                        }
                    }
                }
                if (exit == -1)
                {
                    if (coord.NodeDefined && coord.abstractNode < here.nodes.Length
                        && here.nodes[coord.abstractNode].type == AbstractRoomNode.Type.Exit && here.connections[coord.abstractNode] > -1)
                    {
                        exit = coord.abstractNode;
                    }
                    else
                    {
                        for (int j = 0; j < here.connections.Length; j++)
                        {
                            if (here.connections[j] > -1 && ConnectionAndBackPossible(here, coord.abstractNode, j, template))
                            {
                                exit = j;
                                break;
                            }
                        }
                        if (exit == -1 && here.connections.Length != 0)
                            exit = AI.Random.Range(0, here.connections.Length);
                    }
                }
                // Desktop: a room without exits, or an exit leading nowhere, has no room to leave for.
                if (exit == -1 || here.connections[exit] < 0)
                {
                    Abandon();
                    return;
                }

                var next = world.GetAbstractRoom(here.connections[exit]);
                var entrance = new WorldCoordinate(next.index, -1, -1, next.ExitIndex(here.index));
                int onward = -1;
                float onwardCost = float.MaxValue;
                for (int k = 0; k < next.nodes.Length; k++)
                {
                    if (entrance.abstractNode == k || !ConnectionAndBackPossible(next, entrance.abstractNode, k, template))
                        continue;
                    float cost = next.nodes[k].type == AbstractRoomNode.Type.Exit ? 1f : 10f;
                    cost *= ConnectivityCost(next, entrance.abstractNode, k, template);
                    if (cost < onwardCost)
                    {
                        onwardCost = cost;
                        onward = k;
                    }
                }
                if (onward == -1)
                {
                    destination = entrance;
                }
                else if (next.nodes[onward].type == AbstractRoomNode.Type.Exit && next.connections[onward] > -1)
                {
                    var beyond = world.GetAbstractRoom(next.connections[onward]);
                    destination = new WorldCoordinate(beyond.index, -1, -1, beyond.ExitIndex(next.index));
                }
                else
                {
                    destination = new WorldCoordinate(next.index, -1, -1, onward);
                }
            }

            // AbstractRoom.ConnectionAndBackPossible
            private static bool ConnectionAndBackPossible(AbstractRoom room, int startNode, int destNode, CreatureTemplate creatureType) =>
                startNode == destNode
                || (ConnectivityCost(room, startNode, destNode, creatureType) > -1 && ConnectivityCost(room, destNode, startNode, creatureType) > -1);

            // AbstractRoom.ConnectivityCost
            private static int ConnectivityCost(AbstractRoom room, int startNode, int destNode, CreatureTemplate creatureType)
            {
                if (startNode < 0 || destNode < 0 || startNode >= room.nodes.Length || destNode >= room.nodes.Length)
                    return -1;
                return room.nodes[startNode].ConnectionCost(destNode, creatureType);
            }
        }

        public List<Mission> missions = new List<Mission>();
        private float utility;
        public Mission mostRelevantMission;

        public MissionTracker(ArtificialIntelligence AI) : base(AI)
        {
        }

        public override void Update()
        {
            utility = 0f;
            mostRelevantMission = null;
            for (int i = missions.Count - 1; i >= 0; i--)
            {
                missions[i].UpdateUtility();
                if (missions[i].deleteMeNextFrame)
                {
                    missions.RemoveAt(i);
                }
                else if (missions[i].currentUtility > utility)
                {
                    utility = missions[i].currentUtility;
                    mostRelevantMission = missions[i];
                }
            }
        }

        public void ActOnMostImportantMission() => mostRelevantMission?.Act();

        /// <summary>
        /// Takes on <paramref name="mission"/>. Unless it may coexist, an existing mission of the same type
        /// that matters more than the current utility keeps it out; otherwise those are abandoned.
        /// </summary>
        public void AddMission(Mission mission, bool canCoexistWithMissionOfSameType)
        {
            if (!canCoexistWithMissionOfSameType)
            {
                for (int i = 0; i < missions.Count; i++)
                    if (missions[i].GetType() == mission.GetType() && missions[i].currentUtility > utility)
                        return;
                for (int i = missions.Count - 1; i >= 0; i--)
                {
                    if (missions[i].GetType() == mission.GetType())
                    {
                        missions[i].Abandon();
                        missions.RemoveAt(i);
                    }
                }
            }
            missions.Add(mission);
        }

        public override float Utility() => utility;
    }
}
