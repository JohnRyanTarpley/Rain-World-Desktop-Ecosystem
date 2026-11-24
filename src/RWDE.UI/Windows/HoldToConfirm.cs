using System;

namespace RWDE.UI.Windows
{
    /// <summary>
    /// A press that only counts once it has been held for <see cref="Duration"/>. Letting go early
    /// cancels it. It fires once per press; the clock is passed in so it can be tested.
    /// </summary>
    public sealed class HoldToConfirm
    {
        TimeSpan? pressedAt;
        bool fired;

        public HoldToConfirm(TimeSpan duration)
        {
            if (duration <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(duration));
            Duration = duration;
        }

        public TimeSpan Duration { get; }

        /// <summary>How far the hold has got, 0 to 1. Back to 0 on release.</summary>
        public double Progress { get; private set; }

        /// <summary>True while pressed and not yet fired.</summary>
        public bool IsHolding => pressedAt != null && !fired;

        public void Press(TimeSpan now)
        {
            if (pressedAt != null) return;
            pressedAt = now;
            fired = false;
            Progress = 0;
        }

        /// <summary>Lets go. Before <see cref="Duration"/> has passed this cancels the hold.</summary>
        public void Release()
        {
            pressedAt = null;
            fired = false;
            Progress = 0;
        }

        /// <summary>Advances the hold. Returns true exactly once, when it completes.</summary>
        public bool Update(TimeSpan now)
        {
            if (pressedAt == null || fired) return false;
            var held = now - pressedAt.Value;
            Progress = Math.Clamp(held.TotalMilliseconds / Duration.TotalMilliseconds, 0, 1);
            if (held < Duration) return false;
            fired = true;
            Progress = 1;
            return true;
        }
    }
}
