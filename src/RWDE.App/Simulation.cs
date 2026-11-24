using System;
using RWDE.Graphics;
using RWDE.Sim;

namespace RWDE.App
{
    /// <summary>
    /// The desktop room, its world and the camera that draws it, advanced one 40 Hz tick at a time.
    /// Terrain diffs are applied at the start of a tick only, never in the middle of one.
    /// </summary>
    public sealed class Simulation
    {
        static readonly PhysicalObject[] NoObjects = new PhysicalObject[0];

        readonly ITerrainSource terrain;

        /// <param name="spawnData">The Outskirts' world file, or null for a world without spawning.</param>
        /// <param name="stage">The stage the camera draws on, or null to run without drawing.</param>
        public Simulation(ITerrainSource terrain, StaticWorld staticWorld, RegionSpawnData spawnData,
            string campaignSetting, int seed, FStage stage = null, GraphicsModuleRegistry graphics = null)
        {
            this.terrain = terrain ?? throw new ArgumentNullException(nameof(terrain));
            Room = new Room(terrain);
            World = spawnData != null && staticWorld != null
                ? World.Create(staticWorld, spawnData, campaignSetting, seed, null, Room)
                : new World(seed, Room);
            if (stage != null) Camera = new RoomCamera(stage, Room, graphics ?? new GraphicsModuleRegistry());
        }

        public Room Room { get; }
        public World World { get; }
        public RoomCamera Camera { get; }

        /// <summary>Ticks run so far.</summary>
        public long Ticks { get; private set; }

        /// <summary>
        /// Something on screen moves from tick to tick, so every frame must be drawn: a creature is in the
        /// room, or a drawable is still on the camera.
        /// </summary>
        public bool Animating
        {
            get
            {
                if (Camera != null && Camera.SpriteLeasers.Count > 0) return true;
                foreach (var _ in World.CreaturesInRooms) return true;
                return false;
            }
        }

        /// <summary>One sim tick. Returns true when terrain diffs changed the room, so the picture is stale.</summary>
        public bool Tick()
        {
            var changes = Room.ApplyTerrainDiffs(terrain);
            World.Update();
            // Realized bodies come with the species ports; until then the room holds no objects.
            Camera?.UpdateGraphicsModules(NoObjects);
            Ticks++;
            return changes.Any;
        }

        /// <summary>Positions every sprite at <paramref name="alpha"/> between the last tick and the next.</summary>
        public void DrawUpdate(float alpha)
        {
            Camera?.DrawUpdate(alpha);
        }
    }
}
