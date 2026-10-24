namespace RWDE.Sim.Physics
{
    /// <summary>Tile terrain, in the order of the game's <c>Room.Tile.TerrainType</c>.</summary>
    public enum TerrainType
    {
        Air,
        Solid,
        Slope,
        /// <summary>A one-way platform: solid from above unless the chunk goes through floors.</summary>
        Floor,
        ShortcutEntrance,
    }

    /// <summary>The game's <c>Room.SlopeDirection</c>.</summary>
    public enum SlopeDirection
    {
        UpLeft,
        UpRight,
        DownLeft,
        DownRight,
        Broken,
    }

    /// <summary>
    /// What body-chunk physics needs from the room it is in: gravity, size and the tile grid.
    /// The room model implements this; physics never sees Win32 or rendering.
    /// </summary>
    public interface IPhysicsRoom
    {
        /// <summary><c>Room.gravity</c>; 1 in an ordinary room.</summary>
        float gravity { get; }

        /// <summary><c>Room.PixelWidth</c>: tile width * 20.</summary>
        float PixelWidth { get; }

        /// <summary><c>Room.PixelHeight</c>: tile height * 20.</summary>
        float PixelHeight { get; }

        /// <summary>
        /// <c>Room.GetTile(x, y).Terrain</c> for any tile, including outside the room, where the game
        /// returns the nearest edge tile (with a shortcut entrance reading as solid).
        /// </summary>
        TerrainType GetTerrain(int x, int y);
    }

    /// <summary>The 20 px tile-grid helpers from the game's <c>Room</c> that physics uses.</summary>
    public static class RoomGeometry
    {
        public const float TileSize = 20f;

        // Room.StaticGetTilePosition: truncates toward zero after the +20 offset, as the game does.
        public static IntVector2 GetTilePosition(Vector2 pos) =>
            new IntVector2((int)((pos.x + 20f) / 20f) - 1, (int)((pos.y + 20f) / 20f) - 1);

        // Room.MiddleOfTile
        public static Vector2 MiddleOfTile(int x, int y) => new Vector2(10f + x * 20f, 10f + y * 20f);
        public static Vector2 MiddleOfTile(IntVector2 pos) => MiddleOfTile(pos.x, pos.y);
        public static Vector2 MiddleOfTile(Vector2 pos) => MiddleOfTile(GetTilePosition(pos));

        public static TerrainType GetTerrain(this IPhysicsRoom room, IntVector2 pos) => room.GetTerrain(pos.x, pos.y);
        public static TerrainType GetTerrain(this IPhysicsRoom room, Vector2 pos) => room.GetTerrain(GetTilePosition(pos));

        public static SlopeDirection IdentifySlope(this IPhysicsRoom room, Vector2 pos) => room.IdentifySlope(GetTilePosition(pos));
        public static SlopeDirection IdentifySlope(this IPhysicsRoom room, int x, int y) => room.IdentifySlope(new IntVector2(x, y));

        // Room.IdentifySlope: a slope tile's direction comes from which neighbours are solid.
        public static SlopeDirection IdentifySlope(this IPhysicsRoom room, IntVector2 pos)
        {
            if (room.GetTerrain(pos.x, pos.y) == TerrainType.Slope)
            {
                if (room.GetTerrain(pos.x - 1, pos.y) == TerrainType.Solid)
                {
                    if (room.GetTerrain(pos.x, pos.y - 1) == TerrainType.Solid) return SlopeDirection.UpRight;
                    if (room.GetTerrain(pos.x, pos.y + 1) == TerrainType.Solid) return SlopeDirection.DownRight;
                }
                else if (room.GetTerrain(pos.x + 1, pos.y) == TerrainType.Solid)
                {
                    if (room.GetTerrain(pos.x, pos.y - 1) == TerrainType.Solid) return SlopeDirection.UpLeft;
                    if (room.GetTerrain(pos.x, pos.y + 1) == TerrainType.Solid) return SlopeDirection.DownLeft;
                }
            }
            return SlopeDirection.Broken;
        }
    }
}
