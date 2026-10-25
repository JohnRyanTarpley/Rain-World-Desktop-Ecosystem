using System;
using System.Runtime.InteropServices;
using RWDE.Shell.Desktop;
using RWDE.Shell.Native;

namespace RWDE.Shell.Overlay
{
    /// <summary>
    /// The window the creatures are drawn into. It covers the whole virtual screen, has no frame,
    /// taskbar button or Alt+Tab entry, never takes focus, and shows whatever the renderer composes
    /// into it (DirectComposition, so undrawn pixels stay transparent).
    /// </summary>
    /// <remarks>
    /// In <see cref="OverlayMode.Desktop"/> it is a child of the wallpaper host (see
    /// <see cref="WallpaperHostLocator"/>), so app windows and the icons cover it like wallpaper. In
    /// <see cref="OverlayMode.Topmost"/> it is a layered, click-through, topmost popup. The window
    /// handle stays the same across mode changes, but the renderer must re-attach after each one
    /// (<see cref="Placed"/>).
    /// </remarks>
    public sealed class OverlayWindow : IDisposable
    {
        const string ClassName = "RWDE.Overlay";
        const int ModeHotkeyId = 1;

        // Kept in a static field: the native window class holds this delegate for the process lifetime.
        static User32.WndProc wndProc;
        static bool classRegistered;

        readonly IntPtr hwnd;
        IntPtr host;
        bool clickThrough = true;
        bool hotkeyRegistered;

        OverlayWindow(IntPtr hwnd)
        {
            this.hwnd = hwnd;
            Bounds = ReadVirtualScreen();
        }

        public IntPtr Handle => hwnd;

        /// <summary>The mode the overlay is in. It can differ from <see cref="RequestedMode"/> after a fallback.</summary>
        public OverlayMode Mode { get; private set; } = OverlayMode.Topmost;

        /// <summary>The mode last passed to <see cref="SetMode"/>.</summary>
        public OverlayMode RequestedMode { get; private set; } = OverlayMode.Topmost;

        /// <summary>The rectangle the overlay covers, in Win32 screen pixels.</summary>
        public VirtualScreen Bounds { get; private set; }

        /// <summary>Why the last switch to desktop mode fell back to topmost, or null.</summary>
        public string DesktopModeProblem { get; private set; }

        /// <summary>
        /// Raised after the window changes parent, position or size: on every mode change, when the
        /// wallpaper host is replaced (explorer restarted), and when displays change.
        /// </summary>
        public event Action Placed;

        /// <summary>Raised when the mode-toggle hotkey (Ctrl+Alt+Shift+R) is pressed anywhere.</summary>
        public event Action ModeHotkeyPressed;

        /// <summary>Creates the window, hidden; call <see cref="SetMode"/> to place and show it.</summary>
        public static OverlayWindow Create()
        {
            var instance = Kernel32.GetModuleHandle(null);
            if (!classRegistered)
            {
                wndProc = WindowProc;
                var wc = new User32.WNDCLASSEX
                {
                    cbSize = (uint)Marshal.SizeOf<User32.WNDCLASSEX>(),
                    lpfnWndProc = Marshal.GetFunctionPointerForDelegate(wndProc),
                    hInstance = instance,
                    lpszClassName = ClassName,
                };
                if (User32.RegisterClassEx(ref wc) == 0)
                    throw new InvalidOperationException("RegisterClassEx failed, error " + Marshal.GetLastWin32Error());
                classRegistered = true;
            }

            var hwnd = User32.CreateWindowEx(
                (uint)OverlayStyle.ExStyle(OverlayMode.Topmost, clickThrough: true), ClassName, "Rain World Desktop Ecosystem",
                unchecked((uint)(User32.WS_POPUP | User32.WS_CLIPSIBLINGS)), 0, 0, 1, 1,
                IntPtr.Zero, IntPtr.Zero, instance, IntPtr.Zero);
            if (hwnd == IntPtr.Zero)
                throw new InvalidOperationException("CreateWindowEx failed, error " + Marshal.GetLastWin32Error());
            return new OverlayWindow(hwnd);
        }

        /// <summary>
        /// Moves the overlay to <paramref name="mode"/>. Desktop mode falls back to topmost when the
        /// wallpaper host can't be found; <see cref="DesktopModeProblem"/> then says why.
        /// </summary>
        public void SetMode(OverlayMode mode)
        {
            RequestedMode = mode;
            Bounds = ReadVirtualScreen();
            if (mode == OverlayMode.Desktop)
            {
                host = FindOrCreateWallpaperHost();
                if (host == IntPtr.Zero)
                {
                    DesktopModeProblem = "The desktop's wallpaper layer was not found, so creatures are drawn on top of windows instead.";
                    mode = OverlayMode.Topmost;
                }
                else
                {
                    DesktopModeProblem = null;
                }
            }

            Mode = mode;
            if (mode == OverlayMode.Desktop)
            {
                User32.SetWindowLong(hwnd, User32.GWL_STYLE, OverlayStyle.Style(mode));
                User32.SetWindowLong(hwnd, User32.GWL_EXSTYLE, OverlayStyle.ExStyle(mode, clickThrough));
                User32.SetParent(hwnd, host);
                // The host's client area spans the virtual screen; place the overlay over all of it.
                var origin = new User32.POINT { X = Bounds.X, Y = Bounds.Y };
                User32.ScreenToClient(host, ref origin);
                User32.SetWindowPos(hwnd, User32.HWND_TOP, origin.X, origin.Y, Bounds.Width, Bounds.Height,
                    User32.SWP_NOACTIVATE | User32.SWP_FRAMECHANGED | User32.SWP_SHOWWINDOW);
            }
            else
            {
                host = IntPtr.Zero;
                // Clear WS_CHILD before leaving the parent, as SetParent's documentation asks.
                User32.SetWindowLong(hwnd, User32.GWL_STYLE, OverlayStyle.Style(mode));
                User32.SetParent(hwnd, IntPtr.Zero);
                User32.SetWindowLong(hwnd, User32.GWL_EXSTYLE, OverlayStyle.ExStyle(mode, clickThrough));
                // A layered window shows nothing until its attributes are set once.
                User32.SetLayeredWindowAttributes(hwnd, 0, 255, User32.LWA_ALPHA);
                User32.SetWindowPos(hwnd, User32.HWND_TOPMOST, Bounds.X, Bounds.Y, Bounds.Width, Bounds.Height,
                    User32.SWP_NOACTIVATE | User32.SWP_FRAMECHANGED | User32.SWP_SHOWWINDOW);
            }
            Placed?.Invoke();
        }

        /// <summary>
        /// Topmost mode only: whether clicks fall through to the windows underneath. In desktop mode the
        /// icon layer takes every click anyway.
        /// </summary>
        public void SetClickThrough(bool value)
        {
            if (clickThrough == value) return;
            clickThrough = value;
            if (Mode == OverlayMode.Topmost)
                User32.SetWindowLong(hwnd, User32.GWL_EXSTYLE, OverlayStyle.ExStyle(Mode, clickThrough));
        }

        /// <summary>Registers Ctrl+Alt+Shift+R system-wide. Returns false when another app already owns it.</summary>
        public bool RegisterModeHotkey()
        {
            if (hotkeyRegistered) return true;
            // A null window posts WM_HOTKEY to this thread's queue, which PumpMessages reads.
            hotkeyRegistered = User32.RegisterHotKey(IntPtr.Zero, ModeHotkeyId,
                User32.MOD_CONTROL | User32.MOD_ALT | User32.MOD_SHIFT | User32.MOD_NOREPEAT, 'R');
            return hotkeyRegistered;
        }

        /// <summary>
        /// Handles every waiting message for this thread and returns false once the app should quit.
        /// In desktop mode it also re-attaches to a new wallpaper host after an explorer restart.
        /// </summary>
        public bool PumpMessages()
        {
            while (User32.PeekMessage(out var msg, IntPtr.Zero, 0, 0, User32.PM_REMOVE))
            {
                if (msg.message == User32.WM_QUIT) return false;
                if (msg.message == User32.WM_HOTKEY && msg.wParam.ToInt32() == ModeHotkeyId)
                {
                    ModeHotkeyPressed?.Invoke();
                    continue;
                }
                User32.TranslateMessage(ref msg);
                User32.DispatchMessage(ref msg);
            }

            // WM_DISPLAYCHANGE only reaches top-level windows, so compare the virtual screen instead.
            if (!ReadVirtualScreen().Equals(Bounds) || (Mode == OverlayMode.Desktop && !User32.IsWindow(host)))
                SetMode(RequestedMode);
            return true;
        }

        /// <summary>
        /// The cursor in the overlay's client pixels, y up (the sprite stage's space). Read from Win32,
        /// since the overlay itself never receives mouse messages.
        /// </summary>
        public bool TryGetCursor(out float x, out float y)
        {
            if (!User32.GetCursorPos(out var p))
            {
                x = y = 0f;
                return false;
            }
            Bounds.ScreenToClientYUp(p.X, p.Y, out x, out y);
            return true;
        }

        /// <summary>Whether Escape is held down right now, whichever app has focus.</summary>
        public static bool IsEscapeDown() => (User32.GetAsyncKeyState(User32.VK_ESCAPE) & 0x8000) != 0;

        public void Dispose()
        {
            if (hotkeyRegistered) User32.UnregisterHotKey(IntPtr.Zero, ModeHotkeyId);
            hotkeyRegistered = false;
            User32.DestroyWindow(hwnd);
        }

        static IntPtr FindOrCreateWallpaperHost()
        {
            var shell = new Win32DesktopShell();
            IntPtr found = WallpaperHostLocator.Find(shell);
            if (found != IntPtr.Zero) return found;

            IntPtr progman = shell.FindWindow(IntPtr.Zero, IntPtr.Zero, DesktopListViewLocator.ProgmanClass);
            if (progman == IntPtr.Zero) return IntPtr.Zero;
            User32.SendMessageTimeout(progman, WallpaperHostLocator.SplitMessage, IntPtr.Zero, IntPtr.Zero,
                User32.SMTO_NORMAL, 1000, out _);
            return WallpaperHostLocator.Find(shell);
        }

        static VirtualScreen ReadVirtualScreen() => new VirtualScreen(
            User32.GetSystemMetrics(User32.SM_XVIRTUALSCREEN), User32.GetSystemMetrics(User32.SM_YVIRTUALSCREEN),
            User32.GetSystemMetrics(User32.SM_CXVIRTUALSCREEN), User32.GetSystemMetrics(User32.SM_CYVIRTUALSCREEN));

        static IntPtr WindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            switch (msg)
            {
                case User32.WM_MOUSEACTIVATE:
                    return new IntPtr(User32.MA_NOACTIVATE);
                case User32.WM_CLOSE:
                    return IntPtr.Zero; // only the app quits the overlay
                case User32.WM_DESTROY:
                    return IntPtr.Zero;
            }
            return User32.DefWindowProc(hWnd, msg, wParam, lParam);
        }
    }
}
