using RWDE.Shell.Native;

namespace RWDE.Shell.Overlay
{
    /// <summary>
    /// The window style bits for the overlay in each <see cref="OverlayMode"/>. Pure bit arithmetic,
    /// so it is unit-tested without a window.
    /// </summary>
    public static class OverlayStyle
    {
        // Every mode: no redirection bitmap (DirectComposition supplies the pixels), a tool window (no
        // taskbar button, not in Alt+Tab) and never activated, so it never takes focus from the user.
        const long CommonEx = User32.WS_EX_NOREDIRECTIONBITMAP | User32.WS_EX_TOOLWINDOW | User32.WS_EX_NOACTIVATE;

        /// <summary>
        /// A child of the wallpaper host in desktop mode, a borderless popup in topmost mode. Neither has
        /// a caption, frame or system menu.
        /// </summary>
        public static long Style(OverlayMode mode) =>
            (mode == OverlayMode.Desktop ? User32.WS_CHILD : User32.WS_POPUP) | User32.WS_VISIBLE | User32.WS_CLIPSIBLINGS;

        /// <summary>
        /// In desktop mode the icon layer sits in front of the overlay and takes every click, so the
        /// overlay needs nothing more. In topmost mode it is layered, so <c>WS_EX_TRANSPARENT</c> can
        /// pass clicks through. Being topmost is set with <c>SetWindowPos(HWND_TOPMOST)</c>, not here.
        /// </summary>
        public static long ExStyle(OverlayMode mode, bool clickThrough) =>
            mode == OverlayMode.Desktop
                ? CommonEx
                : WithClickThrough(CommonEx | User32.WS_EX_LAYERED, clickThrough);

        /// <summary>Adds or removes <c>WS_EX_TRANSPARENT</c>, which makes mouse input fall through to the desktop.</summary>
        public static long WithClickThrough(long exStyle, bool clickThrough) =>
            clickThrough ? exStyle | User32.WS_EX_TRANSPARENT : exStyle & ~User32.WS_EX_TRANSPARENT;

        public static bool IsClickThrough(long exStyle) => (exStyle & User32.WS_EX_TRANSPARENT) != 0;
    }
}
