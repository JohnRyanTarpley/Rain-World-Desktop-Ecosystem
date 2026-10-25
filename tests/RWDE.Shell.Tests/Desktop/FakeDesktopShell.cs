using System;
using System.Collections.Generic;
using RWDE.Shell.Desktop;

namespace RWDE.Shell.Tests.Desktop
{
    /// <summary>An in-memory window tree standing in for the real desktop.</summary>
    internal sealed class FakeDesktopShell : IDesktopShell
    {
        private sealed class Window
        {
            public IntPtr Handle;
            public IntPtr Parent;
            public string Class;
            public bool Visible = true;
            public int OriginX;
            public int OriginY;
            public List<ScreenRect> Items = new List<ScreenRect>();
        }

        private readonly List<Window> windows = new List<Window>();
        private int nextHandle = 0x100;

        public IconRectPart? LastPart { get; private set; }
        public bool FailReads { get; set; }

        /// <summary>Adds a window under <paramref name="parent"/> (zero for top-level) and returns its handle.</summary>
        public IntPtr Add(IntPtr parent, string className)
        {
            var w = new Window { Handle = (IntPtr)nextHandle++, Parent = parent, Class = className };
            windows.Add(w);
            return w.Handle;
        }

        /// <summary>Adds the usual SHELLDLL_DefView > SysListView32 pair under <paramref name="host"/> and returns the list view.</summary>
        public IntPtr AddDesktopListView(IntPtr host)
        {
            IntPtr defView = Add(host, DesktopListViewLocator.DefViewClass);
            return Add(defView, DesktopListViewLocator.ListViewClass);
        }

        public void SetItems(IntPtr listView, int originX, int originY, params ScreenRect[] items)
        {
            Window w = Get(listView);
            w.OriginX = originX;
            w.OriginY = originY;
            w.Items = new List<ScreenRect>(items);
        }

        public void SetVisible(IntPtr hwnd, bool visible) => Get(hwnd).Visible = visible;

        public IntPtr FindWindow(IntPtr parent, IntPtr after, string className)
        {
            bool passedAfter = after == IntPtr.Zero;
            foreach (Window w in windows)
            {
                if (w.Parent != parent) continue;
                if (!passedAfter)
                {
                    passedAfter = w.Handle == after;
                    continue;
                }
                if (w.Class == className) return w.Handle;
            }
            return IntPtr.Zero;
        }

        public bool IsVisible(IntPtr hwnd) => Get(hwnd).Visible;

        public bool TryGetClientOrigin(IntPtr hwnd, out int x, out int y)
        {
            Window w = Get(hwnd);
            x = w.OriginX;
            y = w.OriginY;
            return true;
        }

        public bool TryReadItemRects(IntPtr listView, IconRectPart part, List<ScreenRect> into)
        {
            LastPart = part;
            if (FailReads) return false;
            into.AddRange(Get(listView).Items);
            return true;
        }

        private Window Get(IntPtr hwnd) => windows.Find(w => w.Handle == hwnd)
            ?? throw new ArgumentException($"unknown window {hwnd}");
    }
}
