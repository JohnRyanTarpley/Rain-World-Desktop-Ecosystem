using System.Threading;
using NUnit.Framework;
using RWDE.Tests.Common;
using RWDE.UI.Configuration;
using RWDE.UI.Windows;

namespace RWDE.UI.Tests
{
    /// <summary>The window, built but never shown, driven through its controls.</summary>
    [Apartment(ApartmentState.STA)]
    public class SettingsFormTests
    {
        TempDirectory dir;
        SettingsStore store;
        UiCommandQueue commands;

        [SetUp]
        public void SetUp()
        {
            dir = new TempDirectory();
            store = SettingsStore.Load(dir.Combine("settings.json"));
            commands = new UiCommandQueue();
        }

        [TearDown]
        public void TearDown() => dir.Dispose();

        SettingsForm Form(DlcAvailability dlc = null) => new SettingsForm(store, dlc ?? DlcAvailability.Everything, commands);

        [Test]
        public void AdvancedTabIsHiddenByDefault()
        {
            using var form = Form();
            Assert.That(form.AdvancedVisible, Is.False);
            form.SetAdvancedVisible(true);
            Assert.That(form.AdvancedVisible, Is.True);
            form.SetAdvancedVisible(false);
            Assert.That(form.AdvancedVisible, Is.False);
        }

        [Test]
        public void TurningRainOffDisablesPerpetualAndSaves()
        {
            using var form = Form();
            Assert.That(form.PerpetualBox.Enabled, Is.True);
            Assert.That(form.PerpetualBox.Checked, Is.False);
            form.RainBox.Checked = false;
            Assert.That(store.Current.RainEnabled, Is.False);
            Assert.That(form.PerpetualBox.Enabled, Is.False);
            form.RainBox.Checked = true;
            Assert.That(form.PerpetualBox.Enabled, Is.True);
        }

        [Test]
        public void ShowsTheCycleAsReadableTimes()
        {
            using var form = Form();
            Assert.That(form.CycleSlider.LowSeconds, Is.EqualTo(400));
            Assert.That(form.CycleSlider.HighSeconds, Is.EqualTo(800));
            Assert.That(form.CycleText, Does.Contain("6 min 40 s").And.Contain("13 min 20 s"));
        }

        [Test]
        public void CycleResetRestoresTheGameDefault()
        {
            store.Update(s => { s.CycleTimeMinSeconds = 60; s.CycleTimeMaxSeconds = 7200; });
            using var form = Form();
            Assert.That(form.CycleText, Does.Contain("1 min").And.Contain("2 h"));
            form.CycleResetButton.PerformClick();
            Assert.That(store.Current.CycleTimeMinSeconds, Is.EqualTo(400));
            Assert.That(store.Current.CycleTimeMaxSeconds, Is.EqualTo(800));
            Assert.That(form.CycleSlider.LowSeconds, Is.EqualTo(400));
        }

        [Test]
        public void BaseGameOnlyHidesDlcSwitchesAndSpecies()
        {
            using var form = Form(DlcAvailability.BaseGameOnly);
            Assert.That(form.DownpourSwitchShown, Is.False);
            Assert.That(form.WatcherSwitchShown, Is.False);
            Assert.That(form.ListedSpecies, Does.Contain("GreenLizard").And.Not.Contain("SpitLizard"));
            Assert.That(form.CampaignBox.Items.Count, Is.EqualTo(4));
        }

        [Test]
        public void EachDlcSwitchIsShownOnlyWhenThatDlcIsInstalled()
        {
            using (var full = Form())
            {
                Assert.That(full.DownpourSwitchShown, Is.True);
                Assert.That(full.WatcherSwitchShown, Is.True);
            }
            using var downpourOnly = Form(new DlcAvailability(true, false));
            Assert.That(downpourOnly.DownpourSwitchShown, Is.True);
            Assert.That(downpourOnly.WatcherSwitchShown, Is.False);
        }

        [Test]
        public void SwitchingDownpourOffRemovesItsSpecies()
        {
            using var form = Form();
            form.SetAdvancedVisible(true);
            Assert.That(form.ListedSpecies, Does.Contain("SpitLizard"));
            form.DownpourBox.Checked = false;
            Assert.That(store.Current.DownpourEnabled, Is.False);
            Assert.That(form.ListedSpecies, Does.Not.Contain("SpitLizard"));
        }

        [Test]
        public void SpeciesToggleIsSaved()
        {
            using var form = Form();
            form.SpeciesBox("GreenLizard").Checked = false;
            Assert.That(store.Current.IsSpeciesEnabled("GreenLizard"), Is.False);
            form.SpeciesBox("GreenLizard").Checked = true;
            Assert.That(store.Current.IsSpeciesEnabled("GreenLizard"), Is.True);
        }

        [Test]
        public void CampaignChoiceIsSaved()
        {
            using var form = Form();
            foreach (CampaignChoice c in form.CampaignBox.Items)
                if (c.Value == "Red") form.CampaignBox.SelectedItem = c;
            Assert.That(store.Current.CampaignSpawnTable, Is.EqualTo("Red"));
        }

        [Test]
        public void SpawnNowPostsACommand()
        {
            using var form = Form();
            form.SpawnNow("Fly");
            Assert.That(commands.TryTake(out var c) && c is SpawnNowCommand { InternalType: "Fly" }, Is.True);
        }

        [Test]
        public void ResetButtonHoldsForThreeSeconds()
        {
            Assert.That(SettingsForm.ResetHoldTime.TotalSeconds, Is.EqualTo(3));
            using var form = Form();
            Assert.That(form.ResetButton.Progress, Is.Zero);
        }
    }
}
