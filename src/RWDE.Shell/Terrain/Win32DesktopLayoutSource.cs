using System.Runtime.InteropServices;
using RWDE.Shell.Desktop;
using RWDE.Shell.Native;

namespace RWDE.Shell.Terrain
{
    /// <summary>
    /// The live desktop: the virtual screen (<c>GetSystemMetrics</c>), the taskbar
    /// (<c>SHAppBarMessage(ABM_GETTASKBARPOS)</c>) and the icons (<see cref="DesktopIconScanner"/>).
    /// Like the scanner, it needs a per-monitor DPI aware process so every rectangle is in physical pixels.
    /// </summary>
    public sealed class Win32DesktopLayoutSource : IDesktopLayoutSource
    {
        private readonly DesktopIconScanner icons;

        public Win32DesktopLayoutSource() : this(new DesktopIconScanner()) { }

        public Win32DesktopLayoutSource(DesktopIconScanner icons)
        {
            this.icons = icons;
        }

        public DesktopLayout Read()
        {
            int x = User32.GetSystemMetrics(User32.SM_XVIRTUALSCREEN);
            int y = User32.GetSystemMetrics(User32.SM_YVIRTUALSCREEN);
            var screen = new ScreenRect(x, y,
                x + User32.GetSystemMetrics(User32.SM_CXVIRTUALSCREEN),
                y + User32.GetSystemMetrics(User32.SM_CYVIRTUALSCREEN));
            return new DesktopLayout(screen, ReadTaskbar(), icons.Scan());
        }

        private static ScreenRect? ReadTaskbar()
        {
            var data = new Shell32.APPBARDATA { cbSize = (uint)Marshal.SizeOf<Shell32.APPBARDATA>() };
            if (Shell32.SHAppBarMessage(Shell32.ABM_GETTASKBARPOS, ref data) == System.UIntPtr.Zero) return null;
            return new ScreenRect(data.rc.Left, data.rc.Top, data.rc.Right, data.rc.Bottom);
        }
    }
}
