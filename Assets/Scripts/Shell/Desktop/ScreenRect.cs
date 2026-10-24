using System;

namespace RWDE.Shell.Desktop
{
    /// <summary>
    /// An integer rectangle in Win32 virtual-screen pixels: origin at the primary monitor's
    /// top-left, y pointing down, right/bottom exclusive. Monitors left of or above the primary
    /// have negative coordinates.
    /// </summary>
    public readonly struct ScreenRect : IEquatable<ScreenRect>
    {
        public readonly int Left;
        public readonly int Top;
        public readonly int Right;
        public readonly int Bottom;

        public ScreenRect(int left, int top, int right, int bottom)
        {
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
        }

        public int Width => Right - Left;
        public int Height => Bottom - Top;
        public bool IsEmpty => Right <= Left || Bottom <= Top;

        public ScreenRect Offset(int dx, int dy) => new ScreenRect(Left + dx, Top + dy, Right + dx, Bottom + dy);

        public bool Equals(ScreenRect other) =>
            Left == other.Left && Top == other.Top && Right == other.Right && Bottom == other.Bottom;

        public override bool Equals(object obj) => obj is ScreenRect other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Left;
                hash = hash * 397 ^ Top;
                hash = hash * 397 ^ Right;
                hash = hash * 397 ^ Bottom;
                return hash;
            }
        }

        public static bool operator ==(ScreenRect a, ScreenRect b) => a.Equals(b);
        public static bool operator !=(ScreenRect a, ScreenRect b) => !a.Equals(b);

        public override string ToString() => $"({Left},{Top})-({Right},{Bottom})";
    }
}
