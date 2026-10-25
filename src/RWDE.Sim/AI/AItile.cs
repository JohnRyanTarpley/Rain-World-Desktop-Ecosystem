using System.Collections.Generic;

namespace RWDE.Sim
{
    /// <summary>One tile of a room's <see cref="AImap"/>, ported from the game's <c>AItile</c>.</summary>
    public sealed class AItile
    {
        /// <summary>
        /// How a creature can be on a tile, from easiest to hardest, in the game's order. Templates
        /// price each one, and the mapper compares them numerically (a higher value is "harder").
        /// </summary>
        public enum Accessibility
        {
            OffScreen,
            Floor,
            CurvedFloor,
            Corridor,
            Climb,
            Wall,
            Ceiling,
            Air,
            Solid,
            Sand
        }

        public Accessibility acc;
        public readonly List<MovementConnection> incomingPaths = new List<MovementConnection>();
        public readonly List<MovementConnection> outgoingPaths = new List<MovementConnection>();
        public bool walkable;
        /// <summary>Solid on both sides along some axis (left-right, up-down or a diagonal).</summary>
        public bool narrowSpace;
        /// <summary>Tiles down to the nearest floor below, or 100000 when there is none.</summary>
        public int floorAltitude = 100000;
        /// <summary><see cref="floorAltitude"/> eased toward lower neighbours up to 4 tiles to the side.</summary>
        public int smoothedFloorAltitude = 100000;
        private int waterInt;
        public int visibility;
        /// <summary>Where a creature falling from here lands; (-1, -1) when it falls out of the room.</summary>
        public IntVector2 fallRiskTile;

        public AItile(Accessibility a, int waterInt)
        {
            acc = a;
            walkable = acc != Accessibility.Air && acc != Accessibility.Solid;
            this.waterInt = waterInt;
        }

        public bool DeepWater => waterInt == 1;
        public bool WaterSurface => waterInt == 2;
        public bool AnyWater => waterInt != 0;

        public void SetWater(int waterInt) => this.waterInt = waterInt;

        public override string ToString() => acc + (walkable ? " walkable" : "");
    }
}
