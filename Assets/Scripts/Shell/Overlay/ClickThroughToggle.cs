namespace RWDE.Shell.Overlay
{
    /// <summary>Reads and writes a window's extended style. The real one wraps an HWND.</summary>
    public interface IExStyleAccess
    {
        long GetExStyle();
        void SetExStyle(long exStyle);
    }

    /// <summary>
    /// Turns <c>WS_EX_TRANSPARENT</c> on and off as asked each frame, touching the window only when
    /// the wanted state differs from the last one written. SetWindowLong every frame would be wasted
    /// work for the window manager.
    /// </summary>
    public sealed class ClickThroughToggle
    {
        readonly IExStyleAccess window;
        bool? current;

        public ClickThroughToggle(IExStyleAccess window)
        {
            this.window = window;
        }

        /// <summary>The last state written, or null before the first <see cref="Set"/>.</summary>
        public bool? Current => current;

        /// <summary>Makes the window click-through or not. Returns true when the style was written.</summary>
        public bool Set(bool clickThrough)
        {
            if (current == clickThrough) return false;
            window.SetExStyle(OverlayStyle.WithClickThrough(window.GetExStyle(), clickThrough));
            current = clickThrough;
            return true;
        }
    }
}
