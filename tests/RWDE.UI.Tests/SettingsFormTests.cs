using System;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
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
        public void AHugeSavedSpawnRateOpensAtTheMaximum()
        {
            // Settings accept any finite rate >= 0; decimal overflows above ~7.9e28.
            store.Update(s => s.SpawnRate = 1e30f);
            using var form = Form();
            Assert.That(form.SpawnRateBox.Value, Is.EqualTo(form.SpawnRateBox.Maximum));
        }

        [Test]
        public void ASaveFailureKeepsTheSavedValueAndSaysWhy()
        {
            store.Update(s => { }); // writes the file
            var file = store.FilePath;
            using var form = Form();
            string error = null;
            form.ShowSaveError = message => error = message;
            System.IO.File.SetAttributes(file, System.IO.FileAttributes.ReadOnly);
            try
            {
                Assert.DoesNotThrow(() => form.RainBox.Checked = false);
                Assert.That(store.Current.RainEnabled, Is.True, "nothing was saved");
                Assert.That(form.RainBox.Checked, Is.True, "the control shows the saved value again");
                Assert.That(error, Does.Contain("Couldn't save").And.Contain(file));
            }
            finally
            {
                System.IO.File.SetAttributes(file, System.IO.FileAttributes.Normal);
            }
        }

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
        public void CampaignChangedOutsideTheWindowIsShown()
        {
            using var form = Form();
            store.Update(s => s.CampaignSpawnTable = "Red");
            Assert.That(((CampaignChoice)form.CampaignBox.SelectedItem).Value, Is.EqualTo("Red"));
            store.Update(s => s.CampaignSpawnTable = "White");
            Assert.That(((CampaignChoice)form.CampaignBox.SelectedItem).Value, Is.EqualTo("White"));
        }

        [Test]
        public void CycleDragEndsWhenMouseCaptureIsLost()
        {
            using var slider = new CycleRangeSlider { Width = 400 };
            slider.SetValues(400, 800);
            int committed = 0;
            slider.ValuesCommitted += (s, e) => committed++;
            int lowX = (int)Call(slider, "XFor", 400);

            Call(slider, "OnMouseDown", new MouseEventArgs(MouseButtons.Left, 1, lowX, 5, 0));
            Call(slider, "OnMouseCaptureChanged", EventArgs.Empty);
            int lowAfterDrag = slider.LowSeconds;
            // Back over the slider with no button held: the handle must stay put.
            Call(slider, "OnMouseMove", new MouseEventArgs(MouseButtons.None, 0, lowX + 60, 5, 0));

            Assert.That(committed, Is.EqualTo(1), "losing capture commits the drag once");
            Assert.That(slider.LowSeconds, Is.EqualTo(lowAfterDrag));
            Call(slider, "OnMouseUp", new MouseEventArgs(MouseButtons.Left, 1, lowX + 60, 5, 0));
            Assert.That(committed, Is.EqualTo(1), "a late MouseUp doesn't commit again");
        }

        static object Call(object target, string method, params object[] args) =>
            target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                .Invoke(target, args);

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
