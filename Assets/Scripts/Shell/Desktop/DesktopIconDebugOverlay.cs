using System.Collections.Generic;
using UnityEngine;

namespace RWDE.Shell.Desktop
{
    /// <summary>
    /// Manual debug aid: outlines every desktop icon rectangle the scanner finds. Add it to any
    /// GameObject in a Windows player build; it draws with IMGUI on top of everything. In the
    /// Editor it only shows a note, because the Game view does not sit at a known screen position.
    /// </summary>
    public sealed class DesktopIconDebugOverlay : MonoBehaviour
    {
#pragma warning disable CS0414 // unused in the Editor, where the overlay does not draw
        [SerializeField] private IconRectPart part = IconRectPart.Bounds;
        [SerializeField] private float rescanSeconds = 1f;
        [SerializeField] private Color color = new Color(1f, 0.3f, 0.1f, 0.9f);
        [SerializeField] private int thickness = 2;
#pragma warning restore CS0414

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private DesktopIconScanner scanner;
        private List<ScreenRect> icons = new List<ScreenRect>();
        private float nextScan;
        private Texture2D pixel;

        private void Update()
        {
            if (Time.unscaledTime < nextScan) return;
            nextScan = Time.unscaledTime + rescanSeconds;
            if (scanner == null || scanner.Part != part) scanner = new DesktopIconScanner(part);
            icons = scanner.Scan();
        }

        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            if (!Win32DesktopShell.TryGetOwnClientOrigin("UnityWndClass", out int originX, out int originY)) return;

            if (pixel == null)
            {
                pixel = new Texture2D(1, 1);
                pixel.SetPixel(0, 0, Color.white);
                pixel.Apply();
            }

            Color previous = GUI.color;
            GUI.color = color;
            foreach (ScreenRect icon in icons)
            {
                // IMGUI and Win32 both put y down, so mapping into the window is just an offset.
                ScreenRect r = icon.Offset(-originX, -originY);
                GUI.DrawTexture(new Rect(r.Left, r.Top, r.Width, thickness), pixel);
                GUI.DrawTexture(new Rect(r.Left, r.Bottom - thickness, r.Width, thickness), pixel);
                GUI.DrawTexture(new Rect(r.Left, r.Top, thickness, r.Height), pixel);
                GUI.DrawTexture(new Rect(r.Right - thickness, r.Top, thickness, r.Height), pixel);
            }
            GUI.color = previous;
            GUI.Label(new Rect(8, 8, 400, 24), $"Desktop icons: {icons.Count}");
        }

        private void OnDestroy()
        {
            if (pixel != null) Destroy(pixel);
        }
#else
        private void OnGUI()
        {
            GUI.Label(new Rect(8, 8, 400, 24), "Desktop icon overlay: Windows player builds only");
        }
#endif
    }
}
