using System;
using System.Collections.Generic;
using RWDE.Graphics;
using RWDE.Shell.Desktop;
using RWDE.Shell.Lifecycle;
using RWDE.Shell.Overlay;

namespace RWDE.App
{
    /// <summary>
    /// Stand-in content until creatures exist, for checking the overlay on a real desktop: an outline
    /// around every desktop icon (the future terrain), a game sprite at the cursor tinted by mode
    /// (green on the desktop layer, orange on top), and a bar that fills while Escape is held.
    /// </summary>
    sealed class DesktopProbe
    {
        // Tried in order; the first element the loaded atlases have is used.
        static readonly string[] CursorElements = { "LizardHead0.0", "Circle20", Futile.WhiteElementName };
        static readonly TimeSpan RescanInterval = TimeSpan.FromSeconds(1);
        const float Outline = 2f;

        static readonly Color DesktopTint = new Color(0.45f, 1f, 0.5f, 1f);
        static readonly Color TopmostTint = new Color(1f, 0.6f, 0.2f, 1f);
        static readonly Color IconOutline = new Color(1f, 0.35f, 0.1f, 0.85f);

        readonly OverlayWindow window;
        readonly DesktopIconScanner scanner = new DesktopIconScanner();
        readonly FContainer outlines = new FContainer();
        readonly FSprite cursor;
        readonly FSprite escapeBar;
        TimeSpan nextScan;
        int scans;

        public DesktopProbe(FStage stage, OverlayWindow window)
        {
            this.window = window;
            stage.AddChild(outlines);

            cursor = new FSprite(FirstAvailable(CursorElements));
            if (cursor.element.name == Futile.WhiteElementName) cursor.scale = 2f;
            stage.AddChild(cursor);
            UserMessages.Log($"Cursor sprite: {cursor.element.name} ({cursor.width}x{cursor.height}).");

            escapeBar = new FSprite(Futile.WhiteElementName) { anchorX = 0f, anchorY = 1f };
            stage.AddChild(escapeBar);
        }

        public void Update(TimeSpan now, EscapeHoldQuit escape)
        {
            var bounds = window.Bounds;
            bool cursorRead = window.TryGetCursor(out var x, out var y);
            if (cursorRead)
            {
                cursor.x = x;
                cursor.y = y;
            }
            if (now >= nextScan)
            {
                nextScan = now + RescanInterval;
                var icons = scanner.Scan();
                DrawOutlines(icons, bounds);
                if (scans++ < 3) UserMessages.Log($"Scan {scans}: {icons.Count} icons; cursor {(cursorRead ? $"at stage ({x}, {y})" : "unreadable")}.");
            }
            cursor.color = window.Mode == OverlayMode.Desktop ? DesktopTint : TopmostTint;

            var held = (float)(escape.Held.TotalSeconds / escape.HoldTime.TotalSeconds);
            escapeBar.isVisible = held > 0f;
            escapeBar.x = 0f;
            escapeBar.y = bounds.Height;
            escapeBar.scaleX = bounds.Width * Math.Min(1f, held) / 16f;
            escapeBar.scaleY = 6f / 16f;
        }

        void DrawOutlines(List<ScreenRect> icons, VirtualScreen bounds)
        {
            outlines.RemoveAllChildren();
            foreach (var icon in icons)
            {
                // Screen pixels (y down) to stage pixels (y up).
                float left = icon.Left - bounds.X;
                float right = icon.Right - bounds.X;
                float top = bounds.Height - (icon.Top - bounds.Y);
                float bottom = bounds.Height - (icon.Bottom - bounds.Y);
                Edge(left, bottom, right - left, Outline);
                Edge(left, top - Outline, right - left, Outline);
                Edge(left, bottom, Outline, top - bottom);
                Edge(right - Outline, bottom, Outline, top - bottom);
            }
        }

        void Edge(float x, float y, float width, float height)
        {
            // Futile_White is 16x16 px.
            outlines.AddChild(new FSprite(Futile.WhiteElementName)
            {
                anchorX = 0f,
                anchorY = 0f,
                x = x,
                y = y,
                scaleX = width / 16f,
                scaleY = height / 16f,
                color = IconOutline,
            });
        }

        static string FirstAvailable(string[] names)
        {
            foreach (var name in names)
            {
                if (Futile.atlasManager.DoesContainElementWithName(name)) return name;
            }
            return Futile.WhiteElementName;
        }
    }
}
