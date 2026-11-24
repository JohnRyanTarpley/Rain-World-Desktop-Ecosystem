using System;

namespace RWDE.App
{
    /// <summary>
    /// Rain World's fixed 40 ticks per second on a real-time accumulator. Each frame,
    /// <see cref="Advance"/> says how many sim ticks are due; <see cref="Alpha"/> is how far the
    /// frame is between the last tick and the next, for interpolating (the game's <c>timeStacker</c>).
    /// </summary>
    public sealed class FixedTickClock
    {
        public const int TicksPerSecond = 40;

        /// <summary>One tick: 25 ms.</summary>
        public static readonly TimeSpan TickLength = TimeSpan.FromTicks(TimeSpan.TicksPerSecond / TicksPerSecond);

        /// <summary>
        /// Most ticks run for one frame. After a longer stall (a hang, a debugger break) the rest of the
        /// backlog is dropped, so the sim slows down instead of spiralling to catch up.
        /// </summary>
        public const int MaxTicksPerFrame = 4;

        TimeSpan? last;
        TimeSpan accumulated;

        /// <summary>0 right after a tick, approaching 1 just before the next one.</summary>
        public float Alpha => (float)(accumulated.Ticks / (double)TickLength.Ticks);

        /// <summary>How long until the next tick is due.</summary>
        public TimeSpan UntilNextTick => TickLength - accumulated;

        /// <summary>Takes the time elapsed since the last call and returns the number of ticks to run now.</summary>
        public int Advance(TimeSpan now)
        {
            if (!last.HasValue)
            {
                last = now;
                return 0;
            }
            var elapsed = now - last.Value;
            last = now;
            if (elapsed > TimeSpan.Zero) accumulated += elapsed;

            int ticks = 0;
            while (accumulated >= TickLength)
            {
                accumulated -= TickLength;
                if (++ticks == MaxTicksPerFrame)
                {
                    if (accumulated >= TickLength) accumulated = TimeSpan.Zero;
                    break;
                }
            }
            return ticks;
        }

        /// <summary>
        /// Stops the clock, e.g. while a fullscreen app hides the desktop. The next <see cref="Advance"/>
        /// starts it again without running the time that passed in between.
        /// </summary>
        public void Stop()
        {
            last = null;
        }
    }
}
