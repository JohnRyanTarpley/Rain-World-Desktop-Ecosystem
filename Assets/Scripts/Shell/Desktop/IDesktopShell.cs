using System;
using System.Collections.Generic;

namespace RWDE.Shell.Desktop
{
    /// <summary>
    /// The handful of window queries the icon scanner needs. <see cref="Win32DesktopShell"/> is the
    /// real one; tests substitute a fake window tree.
    /// </summary>
    internal interface IDesktopShell
    {
        /// <summary>
        /// Like <c>FindWindowEx</c>: the next window of the class after <paramref name="after"/>,
        /// among the children of <paramref name="parent"/>, or among top-level windows when it is zero.
        /// </summary>
        IntPtr FindWindow(IntPtr parent, IntPtr after, string className);

        bool IsVisible(IntPtr hwnd);

        /// <summary>The screen position of the window's client-area origin.</summary>
        bool TryGetClientOrigin(IntPtr hwnd, out int x, out int y);

        /// <summary>
        /// Appends the rectangle of every item in the list view, in its client coordinates.
        /// <paramref name="part"/> selects which part of each item is measured.
        /// </summary>
        bool TryReadItemRects(IntPtr listView, IconRectPart part, List<ScreenRect> into);
    }
}
