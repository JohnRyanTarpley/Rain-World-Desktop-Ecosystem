using RWDE.Shell.Native;

namespace RWDE.Shell.Overlay
{
    /// <summary>
    /// The window style bits for the desktop overlay. Pure bit arithmetic, so it is unit-tested
    /// without a window.
    /// </summary>
    public static class OverlayStyle
    {
        const long FrameBits = User32.WS_CAPTION | User32.WS_THICKFRAME | User32.WS_SYSMENU
            | User32.WS_MINIMIZEBOX | User32.WS_MAXIMIZEBOX;

        /// <summary>A borderless popup: no caption, no resize frame, no system menu.</summary>
        public static long Borderless(long style) => (style & ~FrameBits) | User32.WS_POPUP | User32.WS_VISIBLE;

        /// <summary>
        /// Layered (so DWM composites the framebuffer alpha), a tool window (no taskbar button, not
        /// in Alt+Tab) and topmost. Click-through is left as it was; <see cref="WithClickThrough"/> owns it.
        /// </summary>
        public static long OverlayEx(long exStyle) =>
            (exStyle & ~User32.WS_EX_APPWINDOW) | User32.WS_EX_LAYERED | User32.WS_EX_TOOLWINDOW | User32.WS_EX_TOPMOST;

        /// <summary>Adds or removes <c>WS_EX_TRANSPARENT</c>, which makes mouse input fall through to the desktop.</summary>
        public static long WithClickThrough(long exStyle, bool clickThrough) =>
            clickThrough ? exStyle | User32.WS_EX_TRANSPARENT : exStyle & ~User32.WS_EX_TRANSPARENT;

        public static bool IsClickThrough(long exStyle) => (exStyle & User32.WS_EX_TRANSPARENT) != 0;
    }
}
