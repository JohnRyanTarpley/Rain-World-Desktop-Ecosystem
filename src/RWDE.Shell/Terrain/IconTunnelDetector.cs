using System;
using System.Collections.Generic;
using RWDE.Shell.Desktop;
using RWDE.Sim;

namespace RWDE.Shell.Terrain
{
    /// <summary>
    /// Two icon columns side by side, in Win32 virtual-screen pixels: the line where they meet and
    /// the vertical span of the rows that pair up. The rasteriser turns it into a tunnel, the
    /// game's one-tile crawl passage, running down that line between two solid walls.
    /// </summary>
    public readonly struct IconTunnel : IEquatable<IconTunnel>
    {
        public IconTunnel(int seamX, int top, int bottom, int rows)
        {
            SeamX = seamX;
            Top = top;
            Bottom = bottom;
            Rows = rows;
        }

        /// <summary>The x where the left column ends and the right one begins.</summary>
        public int SeamX { get; }

        /// <summary>Top of the highest paired row.</summary>
        public int Top { get; }

        /// <summary>Bottom of the lowest paired row, exclusive.</summary>
        public int Bottom { get; }

        /// <summary>How many rows of two icons the tunnel runs through.</summary>
        public int Rows { get; }

        public bool Equals(IconTunnel other) =>
            SeamX == other.SeamX && Top == other.Top && Bottom == other.Bottom && Rows == other.Rows;

        public override bool Equals(object obj) => obj is IconTunnel other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(SeamX, Top, Bottom, Rows);
        public override string ToString() => "x " + SeamX + ", y " + Top + ".." + Bottom + " (" + Rows + " rows)";
    }

    /// <summary>
    /// Finds icon tunnels: places where exactly two icon columns stand side by side for at least
    /// <see cref="MinRows"/> rows. Explorer reports icon rectangles a few pixels off the grid, so
    /// edges count as aligned within <see cref="Jitter"/> and as touching across a gap narrower
    /// than a tile. A row with a third icon beside the pair is a solid block, not part of a tunnel,
    /// and a single column stays a solid pillar.
    /// </summary>
    public static class IconTunnelDetector
    {
        /// <summary>The fewest rows of two icons that make a tunnel.</summary>
        public const int MinRows = 2;

        /// <summary>How far, in pixels, edges that line up on the icon grid may disagree.</summary>
        public const int Jitter = 10;

        /// <summary>The widest gap, in pixels, between two icons that still count as touching.</summary>
        private const int MaxGap = (int)Room.TileSize;

        public static List<IconTunnel> Detect(IReadOnlyList<ScreenRect> icons)
        {
            if (icons == null) throw new ArgumentNullException(nameof(icons));
            var tunnels = new List<IconTunnel>();

            // Each row of exactly two icons side by side, as (left, right).
            var pairs = new List<(ScreenRect left, ScreenRect right)>();
            foreach (var a in icons)
            {
                if (a.IsEmpty) continue;
                if (Neighbours(icons, a, toRight: true, out var right) != 1 || Neighbours(icons, a, toRight: false, out _) != 0)
                    continue;
                if (Neighbours(icons, right, toRight: true, out _) != 0 || Neighbours(icons, right, toRight: false, out _) != 1)
                    continue;
                pairs.Add((a, right));
            }

            // Chain pairs whose icons stack under each other into runs, top down. Each pair must touch
            // the one above it and line up with the run's first pair, so jitter can't add up row by row
            // into a diagonal staircase.
            pairs.Sort((p, q) => p.left.Top.CompareTo(q.left.Top));
            var used = new bool[pairs.Count];
            for (int i = 0; i < pairs.Count; i++)
            {
                if (used[i]) continue;
                used[i] = true;
                var run = new List<(ScreenRect left, ScreenRect right)> { pairs[i] };
                var first = pairs[i];
                for (int j = i + 1; j < pairs.Count; j++)
                {
                    var last = run[run.Count - 1];
                    if (!used[j] && Stacked(last.left, pairs[j].left) && Stacked(last.right, pairs[j].right)
                        && Aligned(first.left, pairs[j].left) && Aligned(first.right, pairs[j].right))
                    {
                        used[j] = true;
                        run.Add(pairs[j]);
                    }
                }
                if (run.Count < MinRows) continue;

                // The seam is the mean of each row's midpoint, rounded once and down, so the same icons
                // give the same seam wherever they sit, negative virtual-screen coordinates included.
                long edgeSum = 0;
                int top = int.MaxValue, bottom = int.MinValue;
                foreach (var (left, right) in run)
                {
                    edgeSum += left.Right + right.Left;
                    top = Math.Min(top, Math.Min(left.Top, right.Top));
                    bottom = Math.Max(bottom, Math.Max(left.Bottom, right.Bottom));
                }
                int seamX = (int)Math.Floor(edgeSum / (2.0 * run.Count));
                tunnels.Add(new IconTunnel(seamX, top, bottom, run.Count));
            }

            tunnels.Sort((p, q) => p.SeamX != q.SeamX ? p.SeamX.CompareTo(q.SeamX) : p.Top.CompareTo(q.Top));
            return tunnels;
        }

        /// <summary>True when <paramref name="b"/> sits directly right of <paramref name="a"/> in the same row.</summary>
        private static bool SideBySide(ScreenRect a, ScreenRect b) =>
            Touching(a.Right, b.Left) && Math.Abs(a.Top - b.Top) <= Jitter && Math.Abs(a.Bottom - b.Bottom) <= Jitter;

        /// <summary>True when <paramref name="below"/> sits directly under <paramref name="above"/> in the same column.</summary>
        private static bool Stacked(ScreenRect above, ScreenRect below) =>
            Touching(above.Bottom, below.Top) && Aligned(above, below);

        /// <summary>True when the two icons' left and right edges agree within the jitter.</summary>
        private static bool Aligned(ScreenRect a, ScreenRect b) =>
            Math.Abs(a.Left - b.Left) <= Jitter && Math.Abs(a.Right - b.Right) <= Jitter;

        /// <summary>An edge meets the next one when they overlap by no more than the jitter and leave less than a tile between them.</summary>
        private static bool Touching(int end, int start) => start - end >= -Jitter && start - end < MaxGap;

        /// <summary>Counts the icons directly right (or left) of <paramref name="icon"/>, returning the last one found.</summary>
        private static int Neighbours(IReadOnlyList<ScreenRect> icons, ScreenRect icon, bool toRight, out ScreenRect found)
        {
            found = default;
            int count = 0;
            foreach (var other in icons)
                if (!other.IsEmpty && (toRight ? SideBySide(icon, other) : SideBySide(other, icon)))
                {
                    found = other;
                    count++;
                }
            return count;
        }
    }
}
