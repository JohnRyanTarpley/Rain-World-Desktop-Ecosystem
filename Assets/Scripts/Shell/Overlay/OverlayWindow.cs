using System;
using System.Collections;
using UnityEngine;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using RWDE.Shell.Native;
#endif

namespace RWDE.Shell.Overlay
{
    /// <summary>
    /// Turns the Unity player window into a borderless, always-on-top, transparent overlay over the
    /// whole virtual screen, and toggles click-through every frame from <see cref="HitTest"/>.
    /// </summary>
    /// <remarks>
    /// Needs the Player Settings listed in the desktop-shell CONTEXT.md (flip-model swapchain off,
    /// D3D11, windowed, run in background). In the Editor only the camera is set up; the window
    /// is left alone.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class OverlayWindow : MonoBehaviour
    {
        [Tooltip("Camera that draws the overlay. Falls back to Camera.main, then to a camera made here.")]
        [SerializeField] Camera targetCamera;

        ClickThroughToggle toggle;

        /// <summary>
        /// True when the cursor is over something that should take the click (a creature). Gets the
        /// cursor in client pixels, y up. Null means nothing is interactive, so the overlay is
        /// always click-through. interaction-ui supplies the real hit test.
        /// </summary>
        public Func<Vector2, bool> HitTest { get; set; }

        /// <summary>The cursor in client pixels, y up, read this frame.</summary>
        public Vector2 CursorPosition { get; private set; }

        /// <summary>Whether mouse input falls through to the desktop this frame.</summary>
        public bool IsClickThrough { get; private set; } = true;

        /// <summary>The rectangle the overlay covers, in Win32 screen pixels.</summary>
        public VirtualScreen Bounds { get; private set; }

        public Camera Camera => targetCamera;

        void Awake()
        {
            if (targetCamera == null) targetCamera = Camera.main;
            if (targetCamera == null)
            {
                targetCamera = new GameObject("Overlay Camera").AddComponent<Camera>();
                targetCamera.orthographic = true;
                targetCamera.transform.position = new Vector3(0f, 0f, -10f);
            }
            // DWM composites the framebuffer alpha, so everything not drawn must stay alpha 0.
            targetCamera.clearFlags = CameraClearFlags.SolidColor;
            targetCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            targetCamera.allowHDR = false;

            Bounds = new VirtualScreen(0, 0, Screen.width, Screen.height);
            CursorPosition = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        IntPtr hwnd;

        IEnumerator Start()
        {
            hwnd = User32.FindUnityWindow((uint)System.Diagnostics.Process.GetCurrentProcess().Id);
            if (hwnd == IntPtr.Zero)
            {
                Debug.LogError("OverlayWindow: Unity player window not found; staying a normal window.");
                yield break;
            }

            Bounds = new VirtualScreen(
                User32.GetSystemMetrics(User32.SM_XVIRTUALSCREEN), User32.GetSystemMetrics(User32.SM_YVIRTUALSCREEN),
                User32.GetSystemMetrics(User32.SM_CXVIRTUALSCREEN), User32.GetSystemMetrics(User32.SM_CYVIRTUALSCREEN));

            // Unity applies a resolution change over the next frames and moves the window while
            // doing it, so place the window only after that has settled.
            Screen.SetResolution(Bounds.Width, Bounds.Height, FullScreenMode.Windowed);
            yield return null;
            yield return null;

            User32.SetWindowLong(hwnd, User32.GWL_STYLE, OverlayStyle.Borderless(User32.GetWindowLong(hwnd, User32.GWL_STYLE)));
            User32.SetWindowLong(hwnd, User32.GWL_EXSTYLE, OverlayStyle.OverlayEx(User32.GetWindowLong(hwnd, User32.GWL_EXSTYLE)));

            var margins = Dwm.MARGINS.SheetOfGlass;
            var hr = Dwm.DwmExtendFrameIntoClientArea(hwnd, ref margins);
            if (hr != 0) Debug.LogError($"OverlayWindow: DwmExtendFrameIntoClientArea failed, HRESULT 0x{hr:X8}.");

            if (!User32.SetWindowPos(hwnd, User32.HWND_TOPMOST, Bounds.X, Bounds.Y, Bounds.Width, Bounds.Height,
                    User32.SWP_FRAMECHANGED | User32.SWP_SHOWWINDOW | User32.SWP_NOACTIVATE))
                Debug.LogError($"OverlayWindow: SetWindowPos failed, error {System.Runtime.InteropServices.Marshal.GetLastWin32Error()}.");

            toggle = new ClickThroughToggle(new Win32ExStyle(hwnd));
            Debug.Log($"OverlayWindow: covering virtual screen {Bounds}.");
        }

        sealed class Win32ExStyle : IExStyleAccess
        {
            readonly IntPtr hwnd;
            public Win32ExStyle(IntPtr hwnd) { this.hwnd = hwnd; }
            public long GetExStyle() => User32.GetWindowLong(hwnd, User32.GWL_EXSTYLE);
            public void SetExStyle(long exStyle) => User32.SetWindowLong(hwnd, User32.GWL_EXSTYLE, exStyle);
        }

        // While click-through the window gets no mouse messages, so Unity's own mouse position goes
        // stale. Read the cursor from Win32 instead.
        Vector2 ReadCursor()
        {
            if (!User32.GetCursorPos(out var p)) return CursorPosition;
            Bounds.ScreenToClientYUp(p.X, p.Y, out var x, out var y);
            return new Vector2(x, y);
        }
#else
        Vector2 ReadCursor()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.mousePosition;
#else
            return CursorPosition;
#endif
        }
#endif

        void Update()
        {
            CursorPosition = ReadCursor();
            IsClickThrough = HitTest == null || !HitTest(CursorPosition);
            toggle?.Set(IsClickThrough);
        }
    }
}
