namespace RWDE.App
{
    /// <summary>
    /// Decides whether a frame needs drawing, so the app draws nothing while nothing on screen changes.
    /// A frame is drawn when something moves (every frame then, for smooth interpolation between
    /// ticks) or when the picture is stale: the first frame, after the window was placed again, or
    /// after the terrain changed.
    /// </summary>
    public sealed class RenderGate
    {
        bool stale = true;

        /// <summary>Whether a frame is owed even if nothing moves.</summary>
        public bool Stale => stale;

        /// <summary>The picture on screen is out of date and must be drawn again.</summary>
        public void Invalidate() => stale = true;

        public bool ShouldRender(bool animating) => stale || animating;

        /// <summary>Call after drawing a frame.</summary>
        public void Rendered() => stale = false;
    }
}
