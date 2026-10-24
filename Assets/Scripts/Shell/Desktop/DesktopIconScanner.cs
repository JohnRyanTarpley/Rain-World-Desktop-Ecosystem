using System;
using System.Collections.Generic;

namespace RWDE.Shell.Desktop
{
    /// <summary>
    /// Reads the rectangles of the desktop icons, in virtual-screen pixels (see <see cref="ScreenRect"/>).
    /// </summary>
    /// <remarks>
    /// The calling process must be per-monitor DPI aware (Unity players are). Explorer reports
    /// item rectangles in physical pixels, but a DPI-unaware caller gets a scaled client origin,
    /// and the two would not line up.
    /// </remarks>
    public sealed class DesktopIconScanner
    {
        private readonly IDesktopShell shell;
        private readonly IconRectPart part;
        private readonly List<ScreenRect> buffer = new List<ScreenRect>();

        public DesktopIconScanner(IconRectPart part = IconRectPart.Bounds) : this(new Win32DesktopShell(), part) { }

        internal DesktopIconScanner(IDesktopShell shell, IconRectPart part)
        {
            this.shell = shell ?? throw new ArgumentNullException(nameof(shell));
            this.part = part;
        }

        public IconRectPart Part => part;

        /// <summary>
        /// One rectangle per desktop icon. Empty when there is no desktop, when desktop icons are
        /// hidden, or when explorer could not be read.
        /// </summary>
        public List<ScreenRect> Scan()
        {
            var icons = new List<ScreenRect>();

            IntPtr listView = DesktopListViewLocator.Find(shell);
            if (listView == IntPtr.Zero || !shell.IsVisible(listView)) return icons;
            if (!shell.TryGetClientOrigin(listView, out int originX, out int originY)) return icons;

            buffer.Clear();
            if (!shell.TryReadItemRects(listView, part, buffer)) return icons;

            foreach (ScreenRect client in buffer)
            {
                if (!client.IsEmpty) icons.Add(client.Offset(originX, originY));
            }
            return icons;
        }
    }
}
