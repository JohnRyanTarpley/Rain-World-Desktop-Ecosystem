using System;
using System.Linq;
using NUnit.Framework;
using RWDE.Sim;
using RWDE.UI.Configuration;
using RWDE.UI.Windows;

namespace RWDE.UI.Tests
{
    public class CycleSliderScaleTests
    {
        [TestCase(0.0, 60)]
        [TestCase(0.6, 3600)]
        [TestCase(1.0, 86340)]
        [TestCase(-1.0, 60)]
        [TestCase(2.0, 86340)]
        public void TrackEndsAndHourMark(double fraction, int seconds)
        {
            Assert.That(CycleSliderScale.ToSeconds(fraction), Is.EqualTo(seconds));
        }

        [Test]
        public void FirstSixtyPercentIsOneMinuteToOneHourLinearly()
        {
            // Halfway along the lower part: 60 + (3600 - 60) / 2 = 1830 s.
            Assert.That(CycleSliderScale.ToSeconds(0.3), Is.EqualTo(1830));
            Assert.That(CycleSliderScale.ToFraction(1830), Is.EqualTo(0.3).Within(1e-9));
        }

        [Test]
        public void LastFortyPercentIsOneHourToTheMaximumLinearly()
        {
            // Halfway along the upper part: 3600 + (86340 - 3600) / 2 = 44970 s, snapped to 44940 or 45000.
            int s = CycleSliderScale.ToSeconds(0.8);
            Assert.That(s % 60, Is.Zero);
            Assert.That(s, Is.EqualTo(44970).Within(30));
            Assert.That(CycleSliderScale.ToFraction(44970), Is.EqualTo(0.8).Within(1e-9));
        }

        [Test]
        public void TheGameDefaultsRoundTrip()
        {
            foreach (var s in new[] { Settings.GameCycleTimeMin, Settings.GameCycleTimeMax, 60, 3600, 86340 })
                Assert.That(CycleSliderScale.ToSeconds(CycleSliderScale.ToFraction(s)), Is.EqualTo(s), s.ToString());
        }

        [Test]
        public void ScaleIsMonotonic()
        {
            int previous = 0;
            for (int i = 0; i <= 1000; i++)
            {
                int s = CycleSliderScale.ToSeconds(i / 1000.0);
                Assert.That(s, Is.GreaterThanOrEqualTo(previous));
                previous = s;
            }
        }

        [TestCase(400, 1, false, 410)]
        [TestCase(400, -1, false, 390)]
        [TestCase(3600, 1, false, 3660)]
        [TestCase(3600, -1, false, 3590)]
        [TestCase(3590, 1, true, 3600)]
        [TestCase(4000, -1, true, 3600)]
        [TestCase(60, -1, false, 60)]
        [TestCase(86340, 1, true, 86340)]
        public void KeyboardSteps(int from, int direction, bool large, int expected)
        {
            Assert.That(CycleSliderScale.Step(from, direction, large), Is.EqualTo(expected));
        }

        [TestCase(60, "1 min")]
        [TestCase(400, "6 min 40 s")]
        [TestCase(800, "13 min 20 s")]
        [TestCase(3600, "1 h")]
        [TestCase(3660, "1 h 1 min")]
        [TestCase(86340, "23 h 59 min")]
        [TestCase(45, "45 s")]
        public void FormatsReadableTimes(int seconds, string text)
        {
            Assert.That(CycleSliderScale.Format(seconds), Is.EqualTo(text));
        }
    }

    public class HoldToConfirmTests
    {
        static TimeSpan S(double seconds) => TimeSpan.FromSeconds(seconds);

        [Test]
        public void FiresOnceAfterThreeSeconds()
        {
            var hold = new HoldToConfirm(S(3));
            hold.Press(S(10));
            Assert.That(hold.Update(S(11.5)), Is.False);
            Assert.That(hold.Progress, Is.EqualTo(0.5).Within(1e-9));
            Assert.That(hold.Update(S(13)), Is.True);
            Assert.That(hold.Progress, Is.EqualTo(1));
            Assert.That(hold.Update(S(20)), Is.False, "fires once per press");
        }

        [Test]
        public void LettingGoEarlyCancels()
        {
            var hold = new HoldToConfirm(S(3));
            hold.Press(S(0));
            hold.Update(S(2.9));
            hold.Release();
            Assert.That(hold.Progress, Is.Zero);
            Assert.That(hold.Update(S(5)), Is.False);

            hold.Press(S(5));
            Assert.That(hold.Update(S(7)), Is.False, "a new press starts from zero");
            Assert.That(hold.Update(S(8)), Is.True);
        }

        [Test]
        public void UpdateWithoutPressDoesNothing()
        {
            var hold = new HoldToConfirm(S(3));
            Assert.That(hold.Update(S(100)), Is.False);
            Assert.That(hold.IsHolding, Is.False);
        }
    }

    public class SettingsWindowModelTests
    {
        static readonly string[] DownpourSpecies =
            { "SpitLizard", "ZoopLizard", "Salamander", "EelLizard", "RedCentipede", "Centiwing", "MirosVulture", "SlugNPC" };

        [Test]
        public void RosterMatchesTheVisionTable()
        {
            Assert.That(OutskirtsRoster.All.Count, Is.EqualTo(33));
            Assert.That(OutskirtsRoster.All.Select(s => s.InternalType), Is.Unique);
            Assert.That(OutskirtsRoster.All.Where(s => s.Source == Campaign.Source.Downpour).Select(s => s.InternalType),
                Is.EquivalentTo(DownpourSpecies));
        }

        [Test]
        public void BaseGameOnlyListsBaseSpeciesAndCampaigns()
        {
            var s = new Settings();
            var species = SettingsWindowModel.VisibleSpecies(DlcAvailability.BaseGameOnly, s).Select(x => x.InternalType).ToList();
            Assert.That(species, Has.None.AnyOf(DownpourSpecies));
            Assert.That(species, Does.Contain("GreenLizard"));

            var campaigns = SettingsWindowModel.VisibleCampaigns(DlcAvailability.BaseGameOnly, s).Select(c => c.Value);
            Assert.That(campaigns, Is.EqualTo(new[] { Settings.AllOutskirtsCreatures, "White", "Yellow", "Red" }));
        }

        [Test]
        public void FullInstallListsEverything()
        {
            var s = new Settings();
            Assert.That(SettingsWindowModel.VisibleSpecies(DlcAvailability.Everything, s).Count, Is.EqualTo(OutskirtsRoster.All.Count));
            var campaigns = SettingsWindowModel.VisibleCampaigns(DlcAvailability.Everything, s);
            Assert.That(campaigns[0].Value, Is.EqualTo(Settings.AllOutskirtsCreatures));
            Assert.That(campaigns.Skip(1).Select(c => c.Value), Is.EqualTo(Campaign.All.Select(c => c.Timeline)));
        }

        [Test]
        public void SwitchedOffDlcIsLeftOut()
        {
            var s = new Settings { DownpourEnabled = false, WatcherEnabled = false };
            var species = SettingsWindowModel.VisibleSpecies(DlcAvailability.Everything, s).Select(x => x.InternalType);
            Assert.That(species, Has.None.AnyOf(DownpourSpecies));
            var campaigns = SettingsWindowModel.VisibleCampaigns(DlcAvailability.Everything, s).Select(c => c.Value);
            Assert.That(campaigns, Does.Not.Contain("Gourmand").And.Not.Contain("Watcher"));
        }

        [Test]
        public void DlcSwitchesShowOnlyWhenInstalled()
        {
            var downpourOnly = new DlcAvailability(true, false);
            Assert.That(SettingsWindowModel.ShowDownpourSwitch(downpourOnly), Is.True);
            Assert.That(SettingsWindowModel.ShowWatcherSwitch(downpourOnly), Is.False);
            Assert.That(SettingsWindowModel.ShowDownpourSwitch(DlcAvailability.BaseGameOnly), Is.False);
        }

        [Test]
        public void PerpetualRainNeedsRain()
        {
            Assert.That(SettingsWindowModel.PerpetualRainEnabled(new Settings { RainEnabled = true }), Is.True);
            Assert.That(SettingsWindowModel.PerpetualRainEnabled(new Settings { RainEnabled = false }), Is.False);
        }

        [Test]
        public void CycleResetGoesBackToTheGameDefault()
        {
            var s = new Settings { CycleTimeMinSeconds = 60, CycleTimeMaxSeconds = 9000 };
            SettingsWindowModel.ResetCycleToGameDefault(s);
            Assert.That(s.CycleTimeMinSeconds, Is.EqualTo(400));
            Assert.That(s.CycleTimeMaxSeconds, Is.EqualTo(800));
        }

        [Test]
        public void CommandQueueKeepsOrder()
        {
            var q = new UiCommandQueue();
            q.Post(new SpawnNowCommand("GreenLizard"));
            q.Post(new ResetSimCommand());
            Assert.That(q.TryTake(out var a) && a is SpawnNowCommand { InternalType: "GreenLizard" }, Is.True);
            Assert.That(q.TryTake(out var b) && b is ResetSimCommand, Is.True);
            Assert.That(q.TryTake(out _), Is.False);
        }
    }
}
