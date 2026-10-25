using System;
using RWDE.Shell.Desktop;
using RWDE.Sim;

namespace RWDE.Shell.Terrain
{
    /// <summary>
    /// Converts between Win32 screen pixels (y down) and room space (y up, tile (0, 0) at the
    /// bottom-left, 20 px tiles). The room's origin is the bottom-left corner of <see cref="Screen"/>,
    /// so the floor lines up with the bottom of the screen; when the height is not a multiple of 20
    /// the top tile row reaches past the top of the screen.
    /// </summary>
    public readonly struct RoomCoordinates
    {
        private const int Tile = (int)Room.TileSize;

        public RoomCoordinates(ScreenRect screen)
        {
            if (screen.IsEmpty) throw new ArgumentException("The screen rectangle is empty.", nameof(screen));
            Screen = screen;
            Width = CeilDiv(screen.Width, Tile);
            Height = CeilDiv(screen.Height, Tile);
        }

        public ScreenRect Screen { get; }

        /// <summary>Room width in tiles.</summary>
        public int Width { get; }

        /// <summary>Room height in tiles.</summary>
        public int Height { get; }

        /// <summary>A screen pixel position in room pixels.</summary>
        public Vector2 ScreenToRoom(float screenX, float screenY) =>
            new Vector2(screenX - Screen.Left, Screen.Bottom - screenY);

        /// <summary>A room pixel position in screen pixels.</summary>
        public void RoomToScreen(Vector2 room, out float screenX, out float screenY)
        {
            screenX = room.x + Screen.Left;
            screenY = Screen.Bottom - room.y;
        }

        /// <summary>
        /// The tiles <paramref name="rect"/> covers: those whose centre lies inside it, so two icons
        /// that touch never leave a gap and a sliver of overlap never adds a tile. A rectangle too
        /// small to hold any centre still covers the tile under its own centre. The range is
        /// inclusive and clipped to the room; returns false when none of it is in the room.
        /// </summary>
        public bool TryGetTiles(ScreenRect rect, out IntVector2 min, out IntVector2 max)
        {
            min = max = new IntVector2(0, 0);
            if (rect.IsEmpty) return false;

            // Room pixels, y up, half-open: [left, right) x [bottom, top).
            int left = rect.Left - Screen.Left;
            int right = rect.Right - Screen.Left;
            int bottom = Screen.Bottom - rect.Bottom;
            int top = Screen.Bottom - rect.Top;

            // Tile i's centre is 20i + 10; it is inside [a, b) when ceil((a - 10) / 20) <= i < ceil((b - 10) / 20).
            int x0 = CeilDiv(left - Tile / 2, Tile), x1 = CeilDiv(right - Tile / 2, Tile) - 1;
            int y0 = CeilDiv(bottom - Tile / 2, Tile), y1 = CeilDiv(top - Tile / 2, Tile) - 1;
            if (x1 < x0) x0 = x1 = FloorDiv(left + right, 2 * Tile);
            if (y1 < y0) y0 = y1 = FloorDiv(bottom + top, 2 * Tile);

            x0 = Math.Max(x0, 0);
            y0 = Math.Max(y0, 0);
            x1 = Math.Min(x1, Width - 1);
            y1 = Math.Min(y1, Height - 1);
            if (x1 < x0 || y1 < y0) return false;

            min = new IntVector2(x0, y0);
            max = new IntVector2(x1, y1);
            return true;
        }

        private static int FloorDiv(int a, int b) => a >= 0 ? a / b : -((-a + b - 1) / b);

        private static int CeilDiv(int a, int b) => -FloorDiv(-a, b);
    }
}
