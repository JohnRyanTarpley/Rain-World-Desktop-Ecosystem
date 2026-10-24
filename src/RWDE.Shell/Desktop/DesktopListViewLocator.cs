using System;

namespace RWDE.Shell.Desktop
{
    /// <summary>
    /// Finds the desktop's icon list view (<c>SysListView32</c> inside <c>SHELLDLL_DefView</c>).
    /// </summary>
    /// <remarks>
    /// Where <c>SHELLDLL_DefView</c> lives depends on the shell's state:
    /// <list type="bullet">
    /// <item><c>Progman</c>: the default, and still the case on Windows 11 24H2.</item>
    /// <item>A top-level <c>WorkerW</c>: after the wallpaper slideshow or the animated-wallpaper
    /// trick (message 0x052C to <c>Progman</c>) has split the desktop.</item>
    /// <item>A <c>WorkerW</c> child of <c>Progman</c>: newer Windows 11 builds nest it there.</item>
    /// </list>
    /// All three are tried in that order.
    /// </remarks>
    internal static class DesktopListViewLocator
    {
        public const string ProgmanClass = "Progman";
        public const string WorkerWClass = "WorkerW";
        public const string DefViewClass = "SHELLDLL_DefView";
        public const string ListViewClass = "SysListView32";

        /// <summary>The list view handle, or <see cref="IntPtr.Zero"/> when there is no desktop (e.g. explorer is not running).</summary>
        public static IntPtr Find(IDesktopShell shell)
        {
            IntPtr progman = shell.FindWindow(IntPtr.Zero, IntPtr.Zero, ProgmanClass);
            IntPtr found = ListViewUnder(shell, progman);
            if (found != IntPtr.Zero) return found;

            found = ListViewUnderAnyWorkerW(shell, IntPtr.Zero);
            if (found != IntPtr.Zero) return found;

            return progman == IntPtr.Zero ? IntPtr.Zero : ListViewUnderAnyWorkerW(shell, progman);
        }

        private static IntPtr ListViewUnderAnyWorkerW(IDesktopShell shell, IntPtr parent)
        {
            IntPtr workerW = IntPtr.Zero;
            while ((workerW = shell.FindWindow(parent, workerW, WorkerWClass)) != IntPtr.Zero)
            {
                IntPtr found = ListViewUnder(shell, workerW);
                if (found != IntPtr.Zero) return found;
            }
            return IntPtr.Zero;
        }

        private static IntPtr ListViewUnder(IDesktopShell shell, IntPtr host)
        {
            if (host == IntPtr.Zero) return IntPtr.Zero;
            IntPtr defView = shell.FindWindow(host, IntPtr.Zero, DefViewClass);
            return defView == IntPtr.Zero ? IntPtr.Zero : shell.FindWindow(defView, IntPtr.Zero, ListViewClass);
        }
    }
}
