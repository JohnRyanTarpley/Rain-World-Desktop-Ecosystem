using System;
using RWDE.Shell.Native;

namespace RWDE.Shell.Lifecycle
{
    /// <summary>
    /// Whether the app should pause because the user can't see the desktop: a fullscreen app or game
    /// is in the foreground, a presentation is running, or the session is locked. Read from
    /// <c>SHQueryUserNotificationState</c>, at most once per <see cref="CheckInterval"/>.
    /// </summary>
    public sealed class FullscreenPause
    {
        public static readonly TimeSpan DefaultCheckInterval = TimeSpan.FromMilliseconds(500);

        readonly Func<int?> queryState;
        TimeSpan? nextCheck;

        /// <param name="queryState">Returns a <c>QUNS_*</c> value, or null when the query failed.</param>
        public FullscreenPause(Func<int?> queryState) : this(queryState, DefaultCheckInterval) { }

        public FullscreenPause(Func<int?> queryState, TimeSpan checkInterval)
        {
            this.queryState = queryState ?? throw new ArgumentNullException(nameof(queryState));
            CheckInterval = checkInterval;
        }

        /// <summary>Reads the live notification state of the current user.</summary>
        public static FullscreenPause ForCurrentUser() => new FullscreenPause(QueryCurrentUser);

        public TimeSpan CheckInterval { get; }

        /// <summary>The answer of the last check.</summary>
        public bool Paused { get; private set; }

        /// <summary>Re-checks when <see cref="CheckInterval"/> has passed and returns <see cref="Paused"/>.</summary>
        public bool Update(TimeSpan now)
        {
            if (nextCheck.HasValue && now < nextCheck.Value) return Paused;
            nextCheck = now + CheckInterval;
            var state = queryState();
            // A failed query keeps the app running: better visible than stuck paused.
            Paused = state.HasValue && ShouldPause(state.Value);
            return Paused;
        }

        /// <summary>
        /// True for the states where the desktop is hidden: a fullscreen app (<c>QUNS_BUSY</c>), a
        /// fullscreen Direct3D game, presentation mode, and no user present (locked, switched away or
        /// screen saver). Quiet time and Store apps don't hide the desktop, so they don't pause.
        /// </summary>
        public static bool ShouldPause(int quns)
        {
            switch (quns)
            {
                case Shell32.QUNS_NOT_PRESENT:
                case Shell32.QUNS_BUSY:
                case Shell32.QUNS_RUNNING_D3D_FULL_SCREEN:
                case Shell32.QUNS_PRESENTATION_MODE:
                    return true;
                default:
                    return false;
            }
        }

        static int? QueryCurrentUser() =>
            Shell32.SHQueryUserNotificationState(out int state) == 0 ? state : (int?)null;
    }
}
