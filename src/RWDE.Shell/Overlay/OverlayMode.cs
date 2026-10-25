namespace RWDE.Shell.Overlay
{
    /// <summary>Where the creatures are drawn (see "How it runs" in docs/VISION.md).</summary>
    public enum OverlayMode
    {
        /// <summary>On the desktop layer, behind every app window and the icons, like an animated wallpaper. The default.</summary>
        Desktop,

        /// <summary>On top of every app window.</summary>
        Topmost,
    }

    public static class OverlayModes
    {
        public static OverlayMode Toggle(this OverlayMode mode) =>
            mode == OverlayMode.Desktop ? OverlayMode.Topmost : OverlayMode.Desktop;
    }
}
