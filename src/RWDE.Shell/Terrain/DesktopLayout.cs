using System;
using System.Collections.Generic;
using RWDE.Shell.Desktop;

namespace RWDE.Shell.Terrain
{
    /// <summary>
    /// What the desktop looks like at one moment, in Win32 virtual-screen pixels (see
    /// <see cref="ScreenRect"/>): the area that becomes the room, the taskbar and the icons.
    /// Nothing here depends on the overlay's display mode.
    /// </summary>
    public sealed class DesktopLayout
    {
        public DesktopLayout(ScreenRect screen, ScreenRect? taskbar, IReadOnlyList<ScreenRect> icons)
        {
            if (screen.IsEmpty) throw new ArgumentException("The screen rectangle is empty.", nameof(screen));
            Screen = screen;
            Taskbar = taskbar.HasValue && !taskbar.Value.IsEmpty ? taskbar : null;
            Icons = icons ?? new ScreenRect[0];
        }

        /// <summary>The area the room covers (the whole virtual screen, until monitors become separate rooms).</summary>
        public ScreenRect Screen { get; }

        /// <summary>The taskbar rectangle (<c>ABM_GETTASKBARPOS</c>), or null when there is none.</summary>
        public ScreenRect? Taskbar { get; }

        /// <summary>One rectangle per desktop icon.</summary>
        public IReadOnlyList<ScreenRect> Icons { get; }
    }
}
