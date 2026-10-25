using System;
using NUnit.Framework;
using RWDE.Shell.Lifecycle;
using RWDE.Shell.Native;
using RWDE.Shell.Overlay;
using RWDE.Shell.Tests.Desktop;

namespace RWDE.Shell.Tests
{
    public class OverlayStyleTests
    {
        const long FrameBits = User32.WS_CAPTION | User32.WS_THICKFRAME | User32.WS_SYSMENU
            | User32.WS_MINIMIZEBOX | User32.WS_MAXIMIZEBOX;

        [Test]
        public void DesktopModeIsAVisibleChildWithoutAFrame()
        {
            var style = OverlayStyle.Style(OverlayMode.Desktop);

            Assert.AreNotEqual(0, style & User32.WS_CHILD);
            Assert.AreEqual(0, style & User32.WS_POPUP);
            Assert.AreNotEqual(0, style & User32.WS_VISIBLE);
            Assert.AreEqual(0, style & FrameBits);
        }

        [Test]
        public void TopmostModeIsAVisiblePopupWithoutAFrame()
        {
            var style = OverlayStyle.Style(OverlayMode.Topmost);

            Assert.AreNotEqual(0, style & User32.WS_POPUP);
            Assert.AreEqual(0, style & User32.WS_CHILD);
            Assert.AreEqual(0, style & FrameBits);
        }

        [TestCase(OverlayMode.Desktop)]
        [TestCase(OverlayMode.Topmost)]
        public void NeverInAltTabOrTheTaskbarAndNeverActivated(OverlayMode mode)
        {
            var ex = OverlayStyle.ExStyle(mode, clickThrough: true);

            Assert.AreNotEqual(0, ex & User32.WS_EX_TOOLWINDOW, "tool windows have no taskbar button or Alt+Tab entry");
            Assert.AreEqual(0, ex & User32.WS_EX_APPWINDOW);
            Assert.AreNotEqual(0, ex & User32.WS_EX_NOACTIVATE);
            Assert.AreNotEqual(0, ex & User32.WS_EX_NOREDIRECTIONBITMAP, "DirectComposition supplies the pixels");
        }

        [Test]
        public void DesktopModeNeedsNoLayeringOrClickThrough()
        {
            var ex = OverlayStyle.ExStyle(OverlayMode.Desktop, clickThrough: true);

            Assert.AreEqual(0, ex & User32.WS_EX_LAYERED);
            Assert.IsFalse(OverlayStyle.IsClickThrough(ex));
        }

        [Test]
        public void TopmostModeIsLayeredAndFollowsClickThrough()
        {
            var through = OverlayStyle.ExStyle(OverlayMode.Topmost, clickThrough: true);
            var catches = OverlayStyle.ExStyle(OverlayMode.Topmost, clickThrough: false);

            Assert.AreNotEqual(0, through & User32.WS_EX_LAYERED);
            Assert.IsTrue(OverlayStyle.IsClickThrough(through));
            Assert.IsFalse(OverlayStyle.IsClickThrough(catches));
            Assert.AreEqual(through & ~User32.WS_EX_TRANSPARENT, catches, "only the transparent bit differs");
        }

        [Test]
        public void ClickThroughTogglesOnlyTheTransparentBit()
        {
            var ex = User32.WS_EX_LAYERED | User32.WS_EX_TOOLWINDOW;

            var on = OverlayStyle.WithClickThrough(ex, true);
            var off = OverlayStyle.WithClickThrough(on, false);

            Assert.AreEqual(ex | User32.WS_EX_TRANSPARENT, on);
            Assert.AreEqual(ex, off);
            Assert.AreEqual(on, OverlayStyle.WithClickThrough(on, true), "turning it on twice changes nothing");
        }

        [Test]
        public void ToggleSwapsModes()
        {
            Assert.AreEqual(OverlayMode.Topmost, OverlayMode.Desktop.Toggle());
            Assert.AreEqual(OverlayMode.Desktop, OverlayMode.Topmost.Toggle());
        }
    }

    public class WallpaperHostLocatorTests
    {
        const string Progman = "Progman";
        const string WorkerW = "WorkerW";
        const string DefView = "SHELLDLL_DefView";

        [Test]
        public void SplitDesktopUsesTheWorkerWAfterTheIconLayer()
        {
            var shell = new FakeDesktopShell();
            shell.Add(IntPtr.Zero, WorkerW); // an unrelated WorkerW in front
            var icons = shell.Add(IntPtr.Zero, WorkerW);
            shell.Add(icons, DefView);
            var host = shell.Add(IntPtr.Zero, WorkerW);
            shell.Add(IntPtr.Zero, Progman);

            Assert.AreEqual(host, WallpaperHostLocator.Find(shell));
        }

        [Test]
        public void UnsplitDesktopHasNoHost()
        {
            var shell = new FakeDesktopShell();
            var progman = shell.Add(IntPtr.Zero, Progman);
            shell.Add(progman, DefView);
            shell.Add(IntPtr.Zero, WorkerW);

            Assert.AreEqual(IntPtr.Zero, WallpaperHostLocator.Find(shell));
        }

        [Test]
        public void NewerWindows11NestsTheHostInProgman()
        {
            var shell = new FakeDesktopShell();
            var progman = shell.Add(IntPtr.Zero, Progman);
            shell.Add(progman, DefView);
            var host = shell.Add(progman, WorkerW);

            Assert.AreEqual(host, WallpaperHostLocator.Find(shell));
        }

        [Test]
        public void NoDesktopNoHost()
        {
            Assert.AreEqual(IntPtr.Zero, WallpaperHostLocator.Find(new FakeDesktopShell()));
        }
    }

    public class EscapeHoldQuitTests
    {
        static TimeSpan S(double seconds) => TimeSpan.FromSeconds(seconds);

        [Test]
        public void QuitsAfterEightSecondsHeld()
        {
            var quit = new EscapeHoldQuit();

            Assert.IsFalse(quit.Update(true, S(10)));
            Assert.IsFalse(quit.Update(true, S(17.9)));
            Assert.IsTrue(quit.Update(true, S(18)));
        }

        [Test]
        public void ReleasingStartsOver()
        {
            var quit = new EscapeHoldQuit();
            quit.Update(true, S(0));
            quit.Update(true, S(7));

            Assert.IsFalse(quit.Update(false, S(7.5)));
            Assert.AreEqual(TimeSpan.Zero, quit.Held);
            Assert.IsFalse(quit.Update(true, S(8)));
            Assert.IsFalse(quit.Update(true, S(15)));
            Assert.IsTrue(quit.Update(true, S(16)));
        }

        [Test]
        public void ReportsHowLongItHasBeenHeld()
        {
            var quit = new EscapeHoldQuit(S(2));
            quit.Update(true, S(1));
            quit.Update(true, S(2.5));

            Assert.AreEqual(S(1.5), quit.Held);
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

        [Test]
        public void EqualWhenEveryEdgeMatches()
        {
            Assert.AreEqual(new VirtualScreen(0, 0, 1920, 1080), new VirtualScreen(0, 0, 1920, 1080));
            Assert.AreNotEqual(new VirtualScreen(0, 0, 1920, 1080), new VirtualScreen(0, 0, 2560, 1080));
        }
    }
}
