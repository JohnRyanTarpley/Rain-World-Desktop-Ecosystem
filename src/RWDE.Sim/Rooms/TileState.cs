using System;

namespace RWDE.Sim
{
    /// <summary>
    /// What the terrain source says about one tile. Shortcut entrances and shortcut paths are not
    /// part of it: they come from <see cref="Pipe"/>s, and the room overlays them on top.
    /// </summary>
    public readonly struct TileState : IEquatable<TileState>
    {
        public TileState(Room.Tile.TerrainType terrain, bool verticalBeam = false, bool horizontalBeam = false, bool wallbehind = false)
        {
            if (terrain == Room.Tile.TerrainType.ShortcutEntrance)
                throw new ArgumentException("Shortcut entrances come from pipes, not tile states.", nameof(terrain));
            Terrain = terrain;
            VerticalBeam = verticalBeam;
            HorizontalBeam = horizontalBeam;
            Wallbehind = wallbehind;
        }

        public static TileState Air => new TileState(Room.Tile.TerrainType.Air);
        public static TileState Solid => new TileState(Room.Tile.TerrainType.Solid);

        public Room.Tile.TerrainType Terrain { get; }
        public bool VerticalBeam { get; }
        public bool HorizontalBeam { get; }
        public bool Wallbehind { get; }

        public bool Equals(TileState other) =>
            Terrain == other.Terrain && VerticalBeam == other.VerticalBeam
            && HorizontalBeam == other.HorizontalBeam && Wallbehind == other.Wallbehind;

        public override bool Equals(object obj) => obj is TileState other && Equals(other);
        public override int GetHashCode() =>
            ((int)Terrain << 3) | (VerticalBeam ? 4 : 0) | (HorizontalBeam ? 2 : 0) | (Wallbehind ? 1 : 0);

        public override string ToString() =>
            Terrain + (VerticalBeam ? " |" : "") + (HorizontalBeam ? " -" : "") + (Wallbehind ? " wall" : "");
    }
}
