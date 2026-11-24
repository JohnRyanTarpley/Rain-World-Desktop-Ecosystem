using NUnit.Framework;
using RWDE.GameAssets.Install;
using RWDE.UI.Configuration;

namespace RWDE.App.Tests
{
    public class DlcSelectionTests
    {
        static Settings With(bool downpour, bool watcher) =>
            new Settings { DownpourEnabled = downpour, WatcherEnabled = watcher };

        [TestCase(true, true, Dlc.Downpour | Dlc.Watcher, Dlc.Downpour | Dlc.Watcher)]
        [TestCase(false, true, Dlc.Downpour | Dlc.Watcher, Dlc.Watcher)]
        [TestCase(true, false, Dlc.Downpour | Dlc.Watcher, Dlc.Downpour)]
        [TestCase(false, false, Dlc.Downpour | Dlc.Watcher, Dlc.None)]
        [TestCase(true, true, Dlc.None, Dlc.None)]
        [TestCase(true, true, Dlc.Watcher, Dlc.Watcher)]
        public void EnabledNeedsInstalledAndSwitchedOn(bool downpour, bool watcher, Dlc installed, Dlc expected)
        {
            Assert.AreEqual(expected, DlcSelection.Enabled(With(downpour, watcher), installed));
        }

        [Test]
        public void SwitchedOnIgnoresWhatIsInstalled()
        {
            Assert.AreEqual(Dlc.Downpour | Dlc.Watcher, DlcSelection.SwitchedOn(new Settings()));
            Assert.AreEqual(Dlc.Watcher, DlcSelection.SwitchedOn(With(false, true)));
        }

        [Test]
        public void ForSimMapsEachDlc()
        {
            var sim = DlcSelection.ForSim(Dlc.Watcher);
            Assert.IsFalse(sim.Downpour);
            Assert.IsTrue(sim.Watcher);
            Assert.IsTrue(DlcSelection.ForSim(Dlc.Downpour | Dlc.Watcher).Downpour);
        }
    }
}
