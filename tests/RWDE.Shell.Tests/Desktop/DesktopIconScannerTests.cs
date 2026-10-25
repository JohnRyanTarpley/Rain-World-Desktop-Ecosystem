using System;
using NUnit.Framework;
using RWDE.Shell.Desktop;

namespace RWDE.Shell.Tests.Desktop
{
    public class DesktopIconScannerTests
    {
        private const string Progman = DesktopListViewLocator.ProgmanClass;
        private const string WorkerW = DesktopListViewLocator.WorkerWClass;

        [Test]
        public void FindsListViewUnderProgman()
        {
            var shell = new FakeDesktopShell();
            shell.Add(IntPtr.Zero, WorkerW);
            IntPtr progman = shell.Add(IntPtr.Zero, Progman);
            IntPtr listView = shell.AddDesktopListView(progman);

            Assert.That(DesktopListViewLocator.Find(shell), Is.EqualTo(listView));
        }

        [Test]
        public void FindsListViewUnderTopLevelWorkerW()
        {
            var shell = new FakeDesktopShell();
            shell.Add(IntPtr.Zero, WorkerW); // the empty wallpaper WorkerW
            IntPtr host = shell.Add(IntPtr.Zero, WorkerW);
            IntPtr listView = shell.AddDesktopListView(host);
            shell.Add(IntPtr.Zero, Progman);

            Assert.That(DesktopListViewLocator.Find(shell), Is.EqualTo(listView));
        }

        [Test]
        public void FindsListViewUnderWorkerWNestedInProgman()
        {
            var shell = new FakeDesktopShell();
            IntPtr progman = shell.Add(IntPtr.Zero, Progman);
            shell.Add(progman, WorkerW);
            IntPtr host = shell.Add(progman, WorkerW);
            IntPtr listView = shell.AddDesktopListView(host);

            Assert.That(DesktopListViewLocator.Find(shell), Is.EqualTo(listView));
        }

        [Test]
        public void SkipsDefViewWithoutListView()
        {
            var shell = new FakeDesktopShell();
            IntPtr progman = shell.Add(IntPtr.Zero, Progman);
            shell.Add(progman, DesktopListViewLocator.DefViewClass);
            IntPtr host = shell.Add(IntPtr.Zero, WorkerW);
            IntPtr listView = shell.AddDesktopListView(host);

            Assert.That(DesktopListViewLocator.Find(shell), Is.EqualTo(listView));
        }

        [Test]
        public void NoDesktopFindsNothingAndScansEmpty()
        {
            var shell = new FakeDesktopShell();
            shell.Add(IntPtr.Zero, WorkerW);

            Assert.That(DesktopListViewLocator.Find(shell), Is.EqualTo(IntPtr.Zero));
            Assert.That(new DesktopIconScanner(shell, IconRectPart.Bounds).Scan(), Is.Empty);
        }

        [Test]
        public void ScanOffsetsClientRectsIntoVirtualScreen()
        {
            var shell = new FakeDesktopShell();
            IntPtr listView = shell.AddDesktopListView(shell.Add(IntPtr.Zero, Progman));
            // A monitor left of the primary puts the desktop's client origin at negative x.
            shell.SetItems(listView, -1920, 0,
                new ScreenRect(10, 20, 86, 106),
                new ScreenRect(1930, 20, 2006, 106));

            var icons = new DesktopIconScanner(shell, IconRectPart.Bounds).Scan();

            Assert.That(icons, Is.EqualTo(new[]
            {
                new ScreenRect(-1910, 20, -1834, 106),
                new ScreenRect(10, 20, 86, 106),
            }));
        }

        [Test]
        public void ScanWorksForWorkerWHostedDesktop()
        {
            var shell = new FakeDesktopShell();
            shell.Add(IntPtr.Zero, Progman);
            IntPtr listView = shell.AddDesktopListView(shell.Add(IntPtr.Zero, WorkerW));
            shell.SetItems(listView, 0, 0, new ScreenRect(5, 5, 80, 90));

            Assert.That(new DesktopIconScanner(shell, IconRectPart.Bounds).Scan(),
                Is.EqualTo(new[] { new ScreenRect(5, 5, 80, 90) }));
        }

        [Test]
        public void ScanSkipsEmptyRects()
        {
            var shell = new FakeDesktopShell();
            IntPtr listView = shell.AddDesktopListView(shell.Add(IntPtr.Zero, Progman));
            shell.SetItems(listView, 0, 0,
                new ScreenRect(0, 0, 0, 0),
                new ScreenRect(10, 10, 10, 50),
                new ScreenRect(10, 10, 60, 60));

            Assert.That(new DesktopIconScanner(shell, IconRectPart.Bounds).Scan(),
                Is.EqualTo(new[] { new ScreenRect(10, 10, 60, 60) }));
        }

        [Test]
        public void HiddenDesktopIconsScanEmpty()
        {
            var shell = new FakeDesktopShell();
            IntPtr listView = shell.AddDesktopListView(shell.Add(IntPtr.Zero, Progman));
            shell.SetItems(listView, 0, 0, new ScreenRect(10, 10, 60, 60));
            shell.SetVisible(listView, false);

            Assert.That(new DesktopIconScanner(shell, IconRectPart.Bounds).Scan(), Is.Empty);
        }

        [Test]
        public void FailedReadScansEmpty()
        {
            var shell = new FakeDesktopShell { FailReads = true };
            IntPtr listView = shell.AddDesktopListView(shell.Add(IntPtr.Zero, Progman));
            shell.SetItems(listView, 0, 0, new ScreenRect(10, 10, 60, 60));

            Assert.That(new DesktopIconScanner(shell, IconRectPart.Bounds).Scan(), Is.Empty);
        }

        [Test]
        public void RepeatedScansDoNotAccumulate()
        {
            var shell = new FakeDesktopShell();
            IntPtr listView = shell.AddDesktopListView(shell.Add(IntPtr.Zero, Progman));
            shell.SetItems(listView, 0, 0, new ScreenRect(10, 10, 60, 60));
            var scanner = new DesktopIconScanner(shell, IconRectPart.Bounds);

            scanner.Scan();

            Assert.That(scanner.Scan(), Has.Count.EqualTo(1));
        }

        [TestCase(IconRectPart.Bounds, 0)]
        [TestCase(IconRectPart.Icon, 1)]
        [TestCase(IconRectPart.Label, 2)]
        [TestCase(IconRectPart.SelectBounds, 3)]
        public void PartIsPassedThroughAsLvirCode(IconRectPart part, int lvir)
        {
            var shell = new FakeDesktopShell();
            shell.AddDesktopListView(shell.Add(IntPtr.Zero, Progman));

            new DesktopIconScanner(shell, part).Scan();

            Assert.That(shell.LastPart, Is.EqualTo(part));
            Assert.That((int)part, Is.EqualTo(lvir));
        }

        [Test]
        public void ScreenRectGeometry()
        {
            var r = new ScreenRect(-10, 5, 30, 25);

            Assert.That(r.Width, Is.EqualTo(40));
            Assert.That(r.Height, Is.EqualTo(20));
            Assert.That(r.IsEmpty, Is.False);
            Assert.That(r.Offset(10, -5), Is.EqualTo(new ScreenRect(0, 0, 40, 20)));
            Assert.That(new ScreenRect(5, 5, 5, 9).IsEmpty, Is.True);
            Assert.That(r == new ScreenRect(-10, 5, 30, 25), Is.True);
            Assert.That(r.GetHashCode(), Is.EqualTo(new ScreenRect(-10, 5, 30, 25).GetHashCode()));
        }
    }
}
