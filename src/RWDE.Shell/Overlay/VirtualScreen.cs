using System;

namespace RWDE.Shell.Overlay
{
    /// <summary>
    /// The bounding rectangle of all monitors in Win32 screen pixels (y down). Its origin can be
    /// negative when a monitor sits left of or above the primary one.
    /// </summary>
    public readonly struct VirtualScreen : IEquatable<VirtualScreen>
    {
        public readonly int X;
        public readonly int Y;
        public readonly int Width;
        public readonly int Height;

        public VirtualScreen(int x, int y, int width, int height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        /// <summary>
        /// Converts a Win32 screen point to the overlay's client pixels with y up and (0, 0) at the
        /// bottom-left, which is the sprite stage's space when the overlay covers this rectangle.
        /// </summary>
        public void ScreenToClientYUp(int screenX, int screenY, out float clientX, out float clientY)
        {
            clientX = screenX - X;
            clientY = Height - (screenY - Y);
        }

        public bool Equals(VirtualScreen other) => X == other.X && Y == other.Y && Width == other.Width && Height == other.Height;

        public override bool Equals(object obj) => obj is VirtualScreen other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(X, Y, Width, Height);

        public override string ToString() => $"({X}, {Y}) {Width}x{Height}";
    }
}
