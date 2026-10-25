using System;

namespace RWDE.Shell.Lifecycle
{
    /// <summary>
    /// Quits when Escape has been held down without a break for <see cref="HoldTime"/> (8 seconds,
    /// see docs/VISION.md). Pure timing: the caller feeds it the key state and a clock each frame.
    /// </summary>
    public sealed class EscapeHoldQuit
    {
        public static readonly TimeSpan DefaultHoldTime = TimeSpan.FromSeconds(8);

        TimeSpan? downSince;

        public EscapeHoldQuit() : this(DefaultHoldTime) { }

        public EscapeHoldQuit(TimeSpan holdTime)
        {
            HoldTime = holdTime;
        }

        public TimeSpan HoldTime { get; }

        /// <summary>How long Escape has been held so far, or zero when it is up.</summary>
        public TimeSpan Held { get; private set; }

        /// <summary>Returns true once Escape has been held for <see cref="HoldTime"/>.</summary>
        public bool Update(bool escapeDown, TimeSpan now)
        {
            if (!escapeDown)
            {
                downSince = null;
                Held = TimeSpan.Zero;
                return false;
            }
            downSince ??= now;
            Held = now - downSince.Value;
            return Held >= HoldTime;
        }
    }
}
