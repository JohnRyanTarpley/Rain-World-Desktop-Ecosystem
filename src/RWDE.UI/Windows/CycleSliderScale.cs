using System;
using System.Collections.Generic;
using System.Globalization;
using RWDE.UI.Configuration;

namespace RWDE.UI.Windows
{
    /// <summary>
    /// The cycle-length slider's scale. It is piecewise linear so short cycles get most of the track:
    /// 0–60% of the track covers 1 min to 1 h, and 60–100% covers 1 h to 23 h 59 min. Values snap to
    /// 10 s below an hour (so the game's 6:40 and 13:20 are reachable) and to whole minutes above.
    /// </summary>
    public static class CycleSliderScale
    {
        /// <summary>Where 1 h sits on the track.</summary>
        public const double HourMark = 0.6;

        public const int Hour = 3600;
        public const int Min = Settings.MinCycleSeconds;
        public const int Max = Settings.MaxCycleSeconds;

        /// <summary>The snapped cycle length, in seconds, at <paramref name="fraction"/> (0–1) of the track.</summary>
        public static int ToSeconds(double fraction)
        {
            if (double.IsNaN(fraction)) fraction = 0;
            fraction = Math.Clamp(fraction, 0, 1);
            double seconds = fraction <= HourMark
                ? Min + fraction / HourMark * (Hour - Min)
                : Hour + (fraction - HourMark) / (1 - HourMark) * (Max - Hour);
            return Snap(seconds);
        }

        /// <summary>Where <paramref name="seconds"/> sits on the track, 0–1.</summary>
        public static double ToFraction(int seconds)
        {
            seconds = Math.Clamp(seconds, Min, Max);
            return seconds <= Hour
                ? (double)(seconds - Min) / (Hour - Min) * HourMark
                : HourMark + (double)(seconds - Hour) / (Max - Hour) * (1 - HourMark);
        }

        /// <summary>Rounds to the slider's step (10 s below an hour, 1 min above) and clamps to its range.</summary>
        public static int Snap(double seconds)
        {
            int step = seconds < Hour ? 10 : 60;
            var snapped = (int)Math.Round(seconds / step, MidpointRounding.AwayFromZero) * step;
            return Math.Clamp(snapped, Min, Max);
        }

        /// <summary>
        /// One keyboard step from <paramref name="seconds"/> in <paramref name="direction"/> (+1 or -1):
        /// 10 s / 1 min, or with <paramref name="large"/> 1 min / 15 min, below / above an hour.
        /// </summary>
        public static int Step(int seconds, int direction, bool large)
        {
            direction = Math.Sign(direction);
            if (direction == 0) return Math.Clamp(seconds, Min, Max);
            bool below = direction > 0 ? seconds < Hour : seconds <= Hour;
            int step = below ? (large ? 60 : 10) : (large ? 900 : 60);
            int next = seconds + direction * step;
            // Don't jump past the hour mark with a large step.
            if (direction > 0 && seconds < Hour && next > Hour) next = Hour;
            if (direction < 0 && seconds > Hour && next < Hour) next = Hour;
            return Snap(next);
        }

        /// <summary>A readable time such as "6 min 40 s", "1 h" or "23 h 59 min".</summary>
        public static string Format(int seconds)
        {
            if (seconds < 0) seconds = 0;
            int h = seconds / 3600, m = seconds / 60 % 60, s = seconds % 60;
            var parts = new List<string>(3);
            if (h > 0) parts.Add(h.ToString(CultureInfo.InvariantCulture) + " h");
            if (m > 0) parts.Add(m.ToString(CultureInfo.InvariantCulture) + " min");
            if (s > 0 || parts.Count == 0) parts.Add(s.ToString(CultureInfo.InvariantCulture) + " s");
            return string.Join(" ", parts);
        }
    }
}
