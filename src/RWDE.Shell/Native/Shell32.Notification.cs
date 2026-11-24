using System.Runtime.InteropServices;

namespace RWDE.Shell.Native
{
    /// <summary>shell32.dll: whether the user can be shown anything right now (fullscreen apps, lock screen).</summary>
    internal static partial class Shell32
    {
        // QUERY_USER_NOTIFICATION_STATE (shellapi.h).
        public const int QUNS_NOT_PRESENT = 1;
        public const int QUNS_BUSY = 2;
        public const int QUNS_RUNNING_D3D_FULL_SCREEN = 3;
        public const int QUNS_PRESENTATION_MODE = 4;
        public const int QUNS_ACCEPTS_NOTIFICATIONS = 5;
        public const int QUNS_QUIET_TIME = 6;
        public const int QUNS_APP = 7;

        /// <summary>Returns an HRESULT; zero (S_OK) on success.</summary>
        [DllImport("shell32.dll")]
        public static extern int SHQueryUserNotificationState(out int pquns);
    }
}
