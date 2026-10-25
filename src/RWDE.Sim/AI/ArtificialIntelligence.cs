using System.Collections.Generic;

namespace RWDE.Sim
{
    /// <summary>
    /// Base of every realized creature AI, ported from the game's <c>ArtificialIntelligence</c>. So far
    /// the module list and the hooks the <see cref="PathFinder"/> calls; the trackers and the rest arrive
    /// with the creatures that need them.
    /// </summary>
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
        public int timeInRoom;
        public int lastRoom = -1;

        public PathFinder pathFinder { get; private set; }

        public void AddModule(AIModule module)
        {
            modules.Add(module);
            if (module is PathFinder p)
                pathFinder = p;
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

        /// <summary>
        /// Lets the AI adjust what a step costs. The game adds the discomfort tracker's dislike of the
        /// destination tile here; until that tracker exists, the cost passes through unchanged.
        /// </summary>
        public virtual PathCost TravelPreference(MovementConnection coord, PathCost cost) => cost;

        /// <summary>Called when the pathfinder has mapped a new area (the game also resets its den finders here).</summary>
        public virtual void NewArea(bool strandedFromExits)
        {
            stranded = strandedFromExits;
        }
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
