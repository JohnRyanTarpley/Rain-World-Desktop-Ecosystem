using System.Collections.Generic;
using NUnit.Framework;
using RWDE.Shell.Native;
using RWDE.Shell.Overlay;

namespace RWDE.Shell.Tests
{
    public class OverlayStyleTests
    {
        // What CreateWindowEx gives a normal captioned, resizable top-level window.
        const long OverlappedWindow = User32.WS_CAPTION | User32.WS_THICKFRAME | User32.WS_SYSMENU
            | User32.WS_MINIMIZEBOX | User32.WS_MAXIMIZEBOX | User32.WS_VISIBLE;

        [Test]
        public void BorderlessStripsTheFrameAndMakesAPopup()
        {
            var style = OverlayStyle.Borderless(OverlappedWindow);

            Assert.AreEqual(User32.WS_POPUP | User32.WS_VISIBLE, style);
        }

        [Test]
        public void BorderlessKeepsUnrelatedBits()
        {
            const long clipChildren = 0x02000000L;

            Assert.AreNotEqual(0, OverlayStyle.Borderless(OverlappedWindow | clipChildren) & clipChildren);
        }

        [Test]
        public void OverlayExIsLayeredToolTopmostAndNotAnAppWindow()
        {
            var ex = OverlayStyle.OverlayEx(User32.WS_EX_APPWINDOW);

            Assert.AreEqual(User32.WS_EX_LAYERED | User32.WS_EX_TOOLWINDOW | User32.WS_EX_TOPMOST, ex);
        }

        [Test]
        public void OverlayExLeavesClickThroughAlone()
        {
            Assert.IsTrue(OverlayStyle.IsClickThrough(OverlayStyle.OverlayEx(User32.WS_EX_TRANSPARENT)));
            Assert.IsFalse(OverlayStyle.IsClickThrough(OverlayStyle.OverlayEx(0)));
        }

        [Test]
        public void ClickThroughTogglesOnlyTheTransparentBit()
        {
            var ex = OverlayStyle.OverlayEx(0);

            var on = OverlayStyle.WithClickThrough(ex, true);
            var off = OverlayStyle.WithClickThrough(on, false);

            Assert.AreEqual(ex | User32.WS_EX_TRANSPARENT, on);
            Assert.AreEqual(ex, off);
            Assert.AreEqual(on, OverlayStyle.WithClickThrough(on, true), "turning it on twice changes nothing");
        }
    }

    public class ClickThroughToggleTests
    {
        sealed class FakeWindow : IExStyleAccess
        {
            public long ExStyle;
            public readonly List<long> Writes = new List<long>();
            public long GetExStyle() => ExStyle;
            public void SetExStyle(long exStyle) { ExStyle = exStyle; Writes.Add(exStyle); }
        }

        [Test]
        public void FirstSetAlwaysWrites()
        {
            var window = new FakeWindow { ExStyle = User32.WS_EX_LAYERED };
            var toggle = new ClickThroughToggle(window);

            Assert.IsNull(toggle.Current);
            Assert.IsTrue(toggle.Set(true));
            Assert.AreEqual(User32.WS_EX_LAYERED | User32.WS_EX_TRANSPARENT, window.ExStyle);
            Assert.AreEqual(true, toggle.Current);
        }

        [Test]
        public void RepeatedFramesWithTheSameStateDoNotTouchTheWindow()
        {
            var window = new FakeWindow();
            var toggle = new ClickThroughToggle(window);

            for (var frame = 0; frame < 10; frame++) toggle.Set(true);

            Assert.AreEqual(1, window.Writes.Count);
        }

        [Test]
        public void HoverOnAndOffFlipsTheTransparentBitEachTime()
        {
            var window = new FakeWindow { ExStyle = OverlayStyle.OverlayEx(0) };
            var toggle = new ClickThroughToggle(window);

            toggle.Set(true);   // over the desktop
            toggle.Set(false);  // over a creature
            toggle.Set(false);
            toggle.Set(true);   // back over the desktop

            Assert.AreEqual(3, window.Writes.Count);
            Assert.IsTrue(OverlayStyle.IsClickThrough(window.Writes[0]));
            Assert.IsFalse(OverlayStyle.IsClickThrough(window.Writes[1]));
            Assert.IsTrue(OverlayStyle.IsClickThrough(window.Writes[2]));
            Assert.AreEqual(OverlayStyle.OverlayEx(0), window.Writes[1], "other overlay bits survive the toggle");
        }

        [Test]
        public void KeepsBitsSomeoneElseSetInTheMeantime()
        {
            var window = new FakeWindow();
            var toggle = new ClickThroughToggle(window);
            toggle.Set(false);

            window.ExStyle |= User32.WS_EX_TOPMOST;
            toggle.Set(true);

            Assert.AreEqual(User32.WS_EX_TOPMOST | User32.WS_EX_TRANSPARENT, window.ExStyle);
        }
    }

    public class VirtualScreenTests
    {
        [Test]
        public void SingleMonitorFlipsY()
        {
            var screen = new VirtualScreen(0, 0, 1920, 1080);

            screen.ScreenToClientYUp(0, 0, out var x, out var y);
            Assert.AreEqual(0f, x);
            Assert.AreEqual(1080f, y, "the top-left screen pixel is the top of client space");

            screen.ScreenToClientYUp(100, 1080, out x, out y);
            Assert.AreEqual(100f, x);
            Assert.AreEqual(0f, y);
        }

        [Test]
        public void MonitorLeftOfAndAbovePrimaryGivesANegativeOrigin()
        {
            // A 1280x1024 monitor left of the primary, its top 200 px above the primary's top.
            var screen = new VirtualScreen(-1280, -200, 1280 + 1920, 1280);

            screen.ScreenToClientYUp(-1280, -200, out var x, out var y);
            Assert.AreEqual(0f, x);
            Assert.AreEqual(1280f, y);

            // The primary's top-left corner.
            screen.ScreenToClientYUp(0, 0, out x, out y);
            Assert.AreEqual(1280f, x);
            Assert.AreEqual(1080f, y);
        }
    }
}
