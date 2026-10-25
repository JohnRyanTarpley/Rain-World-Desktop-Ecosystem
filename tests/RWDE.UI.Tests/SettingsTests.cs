using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using RWDE.Shell.Overlay;
using RWDE.Tests.Common;
using RWDE.UI.Configuration;

namespace RWDE.UI.Tests
{
    public class SettingsTests
    {
        [Test]
        public void DefaultsMatchTheVision()
        {
            var s = new Settings();
            Assert.That(s.CampaignSpawnTable, Is.EqualTo("All Outskirts creatures"));
            Assert.That(s.DisplayMode, Is.EqualTo(OverlayMode.Desktop));
            Assert.That(s.DownpourEnabled, Is.True);
            Assert.That(s.WatcherEnabled, Is.True);
            Assert.That(s.RainEnabled, Is.True);
            Assert.That(s.PerpetualRain, Is.False);
            Assert.That(s.CycleTimeMinSeconds, Is.EqualTo(400));
            Assert.That(s.CycleTimeMaxSeconds, Is.EqualTo(800));
            Assert.That(s.InstallPath, Is.Null);
            Assert.That(s.DisabledSpecies, Is.Empty);
            Assert.That(s.IsSpeciesEnabled("GreenLizard"), Is.True);
        }

        [Test]
        public void CycleBoundsAreOneMinuteToTwentyThreeFiftyNine()
        {
            Assert.That(Settings.MinCycleSeconds, Is.EqualTo(60));
            Assert.That(Settings.MaxCycleSeconds, Is.EqualTo(86340));
        }

        [TestCase(10, 100000, 60, 86340)]
        [TestCase(-5, 30, 60, 60)]
        [TestCase(900, 300, 300, 900)]
        [TestCase(500, 500, 500, 500)]
        public void NormalizeClampsAndOrdersTheCycle(int min, int max, int expectedMin, int expectedMax)
        {
            var s = new Settings { CycleTimeMinSeconds = min, CycleTimeMaxSeconds = max }.Normalize();
            Assert.That(s.CycleTimeMinSeconds, Is.EqualTo(expectedMin));
            Assert.That(s.CycleTimeMaxSeconds, Is.EqualTo(expectedMax));
        }

        [Test]
        public void NormalizeRepairsOtherValues()
        {
            var s = new Settings
            {
                Volume = 3f,
                SpawnRate = float.NaN,
                PopulationCap = -1,
                CampaignSpawnTable = " ",
                DisplayMode = (OverlayMode)42,
                InstallPath = "",
                DisabledSpecies = new List<string> { "Snail", "Snail", "", null },
            }.Normalize();
            Assert.That(s.Volume, Is.EqualTo(1f));
            Assert.That(s.SpawnRate, Is.EqualTo(1f));
            Assert.That(s.PopulationCap, Is.EqualTo(0));
            Assert.That(s.CampaignSpawnTable, Is.EqualTo(Settings.AllOutskirtsCreatures));
            Assert.That(s.DisplayMode, Is.EqualTo(OverlayMode.Desktop));
            Assert.That(s.InstallPath, Is.Null);
            Assert.That(s.DisabledSpecies, Is.EqualTo(new[] { "Snail" }));
        }

        [Test]
        public void CloneCopiesTheSpeciesList()
        {
            var a = new Settings();
            var b = a.Clone();
            b.DisabledSpecies.Add("Snail");
            Assert.That(a.DisabledSpecies, Is.Empty);
        }

        [Test]
        public void DefaultFileIsUnderAppDataRainWorldDesktop()
        {
            Assert.That(SettingsStore.DefaultFilePath, Does.EndWith(Path.Combine("RainWorldDesktop", "settings.json")));
            Assert.That(SettingsStore.DefaultFilePath, Does.StartWith(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData)));
        }

        [Test]
        public void MissingFileGivesDefaultsAndIsNotCreated()
        {
            using var dir = new TempDirectory();
            var path = dir.Combine("RainWorldDesktop", "settings.json");
            var store = SettingsStore.Load(path);
            Assert.That(store.Current.CycleTimeMinSeconds, Is.EqualTo(400));
            Assert.That(File.Exists(path), Is.False);
        }

        [Test]
        public void CorruptFileGivesDefaults()
        {
            using var dir = new TempDirectory();
            var path = dir.Combine("settings.json");
            File.WriteAllText(path, "{ not json");
            Assert.That(SettingsStore.Load(path).Current.RainEnabled, Is.True);
        }

        [Test]
        public void PartialFileKeepsDefaultsForMissingValuesAndNormalizes()
        {
            using var dir = new TempDirectory();
            var path = dir.Combine("settings.json");
            File.WriteAllText(path, "{ \"RainEnabled\": false, \"DisplayMode\": \"Topmost\", \"CycleTimeMinSeconds\": 5 }");
            var s = SettingsStore.Load(path).Current;
            Assert.That(s.RainEnabled, Is.False);
            Assert.That(s.DisplayMode, Is.EqualTo(OverlayMode.Topmost));
            Assert.That(s.CycleTimeMinSeconds, Is.EqualTo(60));
            Assert.That(s.CycleTimeMaxSeconds, Is.EqualTo(800));
            Assert.That(s.DownpourEnabled, Is.True);
        }

        [Test]
        public void UpdateSavesCreatesTheFolderAndRoundTrips()
        {
            using var dir = new TempDirectory();
            var path = dir.Combine("RainWorldDesktop", "settings.json");
            var store = SettingsStore.Load(path);
            store.Update(s =>
            {
                s.PerpetualRain = true;
                s.WatcherEnabled = false;
                s.CycleTimeMinSeconds = 3600;
                s.CycleTimeMaxSeconds = 7200;
                s.DisabledSpecies.Add("Vulture");
                s.InstallPath = @"D:\Games\Rain World";
            });

            Assert.That(File.ReadAllText(path), Does.Contain("\"DisplayMode\": \"Desktop\""));
            var reloaded = SettingsStore.Load(path).Current;
            Assert.That(reloaded.PerpetualRain, Is.True);
            Assert.That(reloaded.WatcherEnabled, Is.False);
            Assert.That(reloaded.CycleTimeMinSeconds, Is.EqualTo(3600));
            Assert.That(reloaded.CycleTimeMaxSeconds, Is.EqualTo(7200));
            Assert.That(reloaded.IsSpeciesEnabled("Vulture"), Is.False);
            Assert.That(reloaded.InstallPath, Is.EqualTo(@"D:\Games\Rain World"));
            Assert.That(File.Exists(path + ".tmp"), Is.False);
        }

        [Test]
        public void UpdateNotifiesWithPreviousAndNormalizedCurrent()
        {
            using var dir = new TempDirectory();
            var store = SettingsStore.Load(dir.Combine("settings.json"));
            var before = store.Current;
            SettingsChangedEventArgs seen = null;
            ISettingsSource source = store;
            source.Changed += (_, e) => seen = e;

            var after = store.Update(s => s.CycleTimeMaxSeconds = 1);

            Assert.That(seen, Is.Not.Null);
            Assert.That(seen.Previous, Is.SameAs(before));
            Assert.That(seen.Current, Is.SameAs(after));
            Assert.That(source.Current, Is.SameAs(after));
            Assert.That(after.CycleTimeMinSeconds, Is.EqualTo(60));
            Assert.That(after.CycleTimeMaxSeconds, Is.EqualTo(400));
            Assert.That(before.CycleTimeMaxSeconds, Is.EqualTo(800), "the previous snapshot is not mutated");
        }
    }
}
