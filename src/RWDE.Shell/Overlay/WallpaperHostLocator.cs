using System;
using RWDE.Shell.Desktop;

namespace RWDE.Shell.Overlay
{
    /// <summary>
    /// Finds the window that sits between the wallpaper and the desktop icons, which the overlay
    /// joins as a child in desktop mode. This is the animated-wallpaper technique: the shell only
    /// creates that window after <see cref="SplitMessage"/> is sent to <c>Progman</c>.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Windows 10 and most of 11: the icons (<c>SHELLDLL_DefView</c>) move into a new top-level
    /// <c>WorkerW</c>, and the host is the next top-level <c>WorkerW</c> after it in z-order.</item>
    /// <item>Windows 11 24H2 and later: the icons stay in <c>Progman</c>, and the host is a <c>WorkerW</c>
    /// child of <c>Progman</c>. Untested on real hardware so far.</item>
    /// </list>
    /// </remarks>
    internal static class WallpaperHostLocator
    {
        /// <summary>The undocumented <c>Progman</c> message that splits the wallpaper from the icon layer.</summary>
        public const uint SplitMessage = 0x052C;

        /// <summary>The host window, or <see cref="IntPtr.Zero"/> when the desktop is not split (or there is none).</summary>
        public static IntPtr Find(IDesktopShell shell)
        {
            IntPtr progman = shell.FindWindow(IntPtr.Zero, IntPtr.Zero, DesktopListViewLocator.ProgmanClass);
            if (progman == IntPtr.Zero) return IntPtr.Zero;

            if (shell.FindWindow(progman, IntPtr.Zero, DesktopListViewLocator.DefViewClass) != IntPtr.Zero)
                return shell.FindWindow(progman, IntPtr.Zero, DesktopListViewLocator.WorkerWClass);

            IntPtr workerW = IntPtr.Zero;
            while ((workerW = shell.FindWindow(IntPtr.Zero, workerW, DesktopListViewLocator.WorkerWClass)) != IntPtr.Zero)
            {
                if (shell.FindWindow(workerW, IntPtr.Zero, DesktopListViewLocator.DefViewClass) != IntPtr.Zero)
                    return shell.FindWindow(IntPtr.Zero, workerW, DesktopListViewLocator.WorkerWClass);
            }
            return IntPtr.Zero;
        }
    }
}
