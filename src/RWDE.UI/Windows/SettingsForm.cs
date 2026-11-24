using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using RWDE.UI.Configuration;

namespace RWDE.UI.Windows
{
    /// <summary>
    /// The settings and spawn window. Every change is written straight to the <see cref="SettingsStore"/>;
    /// "Spawn now" and the held reset go to the <see cref="UiCommandQueue"/> for the app to pass to
    /// the sim. The Advanced settings tab is hidden until the user asks for it.
    /// </summary>
    public sealed class SettingsForm : Form
    {
        /// <summary>How long the reset button must be held.</summary>
        public static readonly TimeSpan ResetHoldTime = TimeSpan.FromSeconds(3);

        readonly SettingsStore store;
        readonly DlcAvailability installed;
        readonly UiCommandQueue commands;

        readonly TabControl tabs = new TabControl { Dock = DockStyle.Fill };
        readonly TabPage mainPage = new TabPage("Settings");
        readonly TabPage advancedPage = new TabPage("Advanced settings");
        readonly CheckBox showAdvanced = new CheckBox { Text = "Show advanced settings", AutoSize = true };

        // Main page
        readonly CheckBox rain = new CheckBox { Text = "Rain", AutoSize = true };
        readonly NumericUpDown spawnRate = new NumericUpDown { Minimum = 0, Maximum = 10, DecimalPlaces = 1, Increment = 0.1m };
        readonly ComboBox campaign = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        readonly NumericUpDown populationCap = new NumericUpDown { Minimum = 0, Maximum = 500 };
        readonly TrackBar volume = new TrackBar { Minimum = 0, Maximum = 100, TickFrequency = 10 };
        readonly Label volumeLabel = new Label { AutoSize = true, Anchor = AnchorStyles.Left };
        readonly TextBox installPath = new TextBox { ReadOnly = true };
        readonly Button browse = new Button { Text = "Browse…", AutoSize = true };
        readonly Button findAutomatically = new Button { Text = "Find automatically", AutoSize = true };
        readonly TableLayoutPanel speciesTable = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Dock = DockStyle.Top };
        readonly HoldButton reset = new HoldButton(ResetHoldTime) { Text = "Hold 3 s to reset the ecosystem" };

        // Advanced page
        readonly CheckBox downpour = new CheckBox { Text = "Downpour", AutoSize = true };
        readonly CheckBox watcher = new CheckBox { Text = "The Watcher", AutoSize = true };
        readonly CheckBox perpetual = new CheckBox { Text = "Realistic Rain (Perpetual)", AutoSize = true };
        readonly CycleRangeSlider cycle = new CycleRangeSlider();
        readonly Label cycleLabel = new Label { AutoSize = true };
        readonly Button cycleReset = new Button { Text = "Reset to game default", AutoSize = true };

        readonly Dictionary<string, CheckBox> speciesBoxes = new Dictionary<string, CheckBox>();
        bool syncing;
        string speciesKey;
        string campaignKey;

        public SettingsForm(SettingsStore store, DlcAvailability installed, UiCommandQueue commands)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.installed = installed ?? DlcAvailability.BaseGameOnly;
            this.commands = commands ?? throw new ArgumentNullException(nameof(commands));

            Text = "Rain World Desktop Ecosystem";
            StartPosition = FormStartPosition.CenterScreen;
            // The app is per-monitor DPI aware and fonts already follow the monitor, so only fixed
            // pixel sizes are scaled, by hand (WinForms auto-scaling would scale them twice).
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new Size(Px(560), Px(720));
            MinimumSize = new Size(Px(480), Px(400));
            spawnRate.Width = Px(80);
            campaign.Width = Px(260);
            populationCap.Width = Px(80);
            volume.Width = Px(200);
            installPath.Width = Px(220);
            reset.Size = new Size(Px(260), Font.Height * 2);

            BuildMainPage();
            BuildAdvancedPage();
            tabs.TabPages.Add(mainPage);

            var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(8) };
            bottom.Controls.Add(showAdvanced);
            Controls.Add(tabs);
            Controls.Add(bottom);

            showAdvanced.CheckedChanged += (s, e) => SetAdvancedVisible(showAdvanced.Checked);
            WireEvents();
            Sync(store.Current);
            store.Changed += OnStoreChanged;
        }

        /// <summary>A size in 96-DPI pixels, in this monitor's pixels.</summary>
        int Px(int logical) => LogicalToDeviceUnits(logical);

        internal bool AdvancedVisible => tabs.TabPages.Contains(advancedPage);
        internal CheckBox RainBox => rain;
        internal NumericUpDown SpawnRateBox => spawnRate;
        internal CheckBox PerpetualBox => perpetual;
        internal CheckBox DownpourBox => downpour;
        internal CheckBox WatcherBox => watcher;
        internal bool DownpourSwitchShown => downpour.Parent != null;
        internal bool WatcherSwitchShown => watcher.Parent != null;
        internal ComboBox CampaignBox => campaign;
        internal CycleRangeSlider CycleSlider => cycle;
        internal string CycleText => cycleLabel.Text;
        internal Button CycleResetButton => cycleReset;
        internal HoldButton ResetButton => reset;
        internal IReadOnlyCollection<string> ListedSpecies => speciesBoxes.Keys;
        internal CheckBox SpeciesBox(string internalType) => speciesBoxes[internalType];

        internal void SetAdvancedVisible(bool visible)
        {
            if (showAdvanced.Checked != visible) showAdvanced.Checked = visible;
            if (visible && !AdvancedVisible) tabs.TabPages.Add(advancedPage);
            if (!visible && AdvancedVisible) tabs.TabPages.Remove(advancedPage);
        }

        /// <summary>Raises "Spawn now" for <paramref name="internalType"/>, as its button does.</summary>
        internal void SpawnNow(string internalType) => commands.Post(new SpawnNowCommand(internalType));

        void BuildMainPage()
        {
            var layout = NewTable();
            AddRow(layout, null, rain);
            AddRow(layout, "Spawn rate", Inline(spawnRate, new Label { Text = "× the game's rate", AutoSize = true, Anchor = AnchorStyles.Left }));
            AddRow(layout, "Campaign spawn table", campaign);
            AddRow(layout, "Population cap", populationCap);
            AddRow(layout, "Volume", Inline(volume, volumeLabel));
            AddRow(layout, "Rain World folder", Inline(installPath, browse, findAutomatically));

            var speciesGroup = new GroupBox { Text = "Creatures", Dock = DockStyle.Fill, Padding = new Padding(6) };
            var scroller = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            speciesTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            speciesTable.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            scroller.Controls.Add(speciesTable);
            speciesGroup.Controls.Add(scroller);
            layout.Controls.Add(speciesGroup, 0, layout.RowCount);
            layout.SetColumnSpan(speciesGroup, 2);
            // Row styles aren't auto-scaled, so scale this one by hand.
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, Px(330)));
            layout.RowCount++;

            AddRow(layout, null, reset);
            mainPage.Controls.Add(layout);
        }

        void BuildAdvancedPage()
        {
            var layout = NewTable();
            // A DLC switch is only on the page when that DLC is installed.
            if (SettingsWindowModel.ShowDownpourSwitch(installed)) AddRow(layout, null, downpour);
            if (SettingsWindowModel.ShowWatcherSwitch(installed)) AddRow(layout, null, watcher);
            AddRow(layout, null, perpetual);
            AddRow(layout, "Cycle length", null);
            layout.Controls.Add(cycle, 0, layout.RowCount);
            layout.SetColumnSpan(cycle, 2);
            cycle.Dock = DockStyle.Top;
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowCount++;
            AddRow(layout, null, Inline(cycleLabel));
            AddRow(layout, null, cycleReset);
            advancedPage.Controls.Add(layout);
        }

        void WireEvents()
        {
            rain.CheckedChanged += (s, e) => Change(x => x.RainEnabled = rain.Checked);
            spawnRate.ValueChanged += (s, e) => Change(x => x.SpawnRate = (float)spawnRate.Value);
            campaign.SelectedIndexChanged += (s, e) =>
            {
                if (campaign.SelectedItem is CampaignChoice choice) Change(x => x.CampaignSpawnTable = choice.Value);
            };
            populationCap.ValueChanged += (s, e) => Change(x => x.PopulationCap = (int)populationCap.Value);
            volume.ValueChanged += (s, e) =>
            {
                volumeLabel.Text = volume.Value + "%";
                Change(x => x.Volume = volume.Value / 100f);
            };
            browse.Click += (s, e) => BrowseForInstall();
            findAutomatically.Click += (s, e) => Change(x => x.InstallPath = null);
            reset.Confirmed += (s, e) => commands.Post(new ResetSimCommand());

            downpour.CheckedChanged += (s, e) => Change(x => x.DownpourEnabled = downpour.Checked);
            watcher.CheckedChanged += (s, e) => Change(x => x.WatcherEnabled = watcher.Checked);
            perpetual.CheckedChanged += (s, e) => Change(x => x.PerpetualRain = perpetual.Checked);
            cycle.ValuesChanged += (s, e) => cycleLabel.Text = FormatCycle(cycle.LowSeconds, cycle.HighSeconds);
            cycle.ValuesCommitted += (s, e) => Change(x =>
            {
                x.CycleTimeMinSeconds = cycle.LowSeconds;
                x.CycleTimeMaxSeconds = cycle.HighSeconds;
            });
            cycleReset.Click += (s, e) => Change(SettingsWindowModel.ResetCycleToGameDefault);
        }

        void BrowseForInstall()
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = "Choose the Rain World folder (the one with RainWorld.exe)",
                UseDescriptionForTitle = true,
                SelectedPath = store.Current.InstallPath ?? "",
            };
            if (dialog.ShowDialog(this) == DialogResult.OK) Change(x => x.InstallPath = dialog.SelectedPath);
        }

        /// <summary>Shows why a change couldn't be saved; replaced in tests.</summary>
        internal Action<string> ShowSaveError;

        /// <summary>
        /// Applies a user change unless the controls are being filled from the settings. If the settings
        /// file can't be written (read-only, disk full), the change isn't kept: the controls go back to
        /// what is saved and the user is told why.
        /// </summary>
        void Change(Action<Settings> change)
        {
            if (syncing) return;
            try
            {
                store.Update(change);
            }
            catch (Exception e) when (e is System.IO.IOException || e is UnauthorizedAccessException)
            {
                Sync(store.Current);
                var message = $"Couldn't save the settings, so the change wasn't kept.\n\n{e.Message}\n\n"
                    + $"Check that this file can be written:\n{store.FilePath}";
                if (ShowSaveError != null) ShowSaveError(message);
                else MessageBox.Show(this, message, "Rain World Desktop Ecosystem", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        void OnStoreChanged(object sender, SettingsChangedEventArgs e)
        {
            if (IsDisposed) return;
            if (InvokeRequired) BeginInvoke(new Action(() => Sync(e.Current)));
            else Sync(e.Current);
        }

        /// <summary>Fills every control from <paramref name="s"/>.</summary>
        void Sync(Settings s)
        {
            if (IsDisposed) return;
            syncing = true;
            try
            {
                rain.Checked = s.RainEnabled;
                // Clamp before converting: a saved rate can be any finite value, and decimal overflows above ~7.9e28.
                spawnRate.Value = (decimal)Math.Clamp(Math.Round((double)s.SpawnRate, 1), (double)spawnRate.Minimum, (double)spawnRate.Maximum);
                populationCap.Value = Math.Clamp(s.PopulationCap, (int)populationCap.Minimum, (int)populationCap.Maximum);
                volume.Value = Math.Clamp((int)Math.Round(s.Volume * 100), volume.Minimum, volume.Maximum);
                volumeLabel.Text = volume.Value + "%";
                installPath.Text = s.InstallPath ?? "(found automatically)";

                downpour.Checked = s.DownpourEnabled;
                watcher.Checked = s.WatcherEnabled;
                perpetual.Checked = s.PerpetualRain;
                perpetual.Enabled = SettingsWindowModel.PerpetualRainEnabled(s);
                cycle.SetValues(s.CycleTimeMinSeconds, s.CycleTimeMaxSeconds);
                cycleLabel.Text = FormatCycle(s.CycleTimeMinSeconds, s.CycleTimeMaxSeconds);

                SyncCampaigns(s);
                SyncSpecies(s);
            }
            finally
            {
                syncing = false;
            }
        }

        void SyncCampaigns(Settings s)
        {
            var choices = SettingsWindowModel.VisibleCampaigns(installed, s);
            var key = string.Join("|", choices.Select(c => c.Value));
            if (key != campaignKey)
            {
                campaignKey = key;
                campaign.Items.Clear();
                foreach (var c in choices) campaign.Items.Add(c);
            }
            // Select the box's own item: the choices above are new objects on every sync, and the box
            // only accepts an item it holds. A campaign from a DLC that is now off isn't listed; show the
            // merged table (the first item) in its place.
            var items = campaign.Items.Cast<CampaignChoice>().ToList();
            campaign.SelectedItem = items.FirstOrDefault(c => c.Value == s.CampaignSpawnTable) ?? items[0];
        }

        void SyncSpecies(Settings s)
        {
            var visible = SettingsWindowModel.VisibleSpecies(installed, s);
            var key = string.Join("|", visible.Select(v => v.InternalType));
            if (key != speciesKey)
            {
                speciesKey = key;
                RebuildSpeciesRows(visible);
            }
            foreach (var pair in speciesBoxes) pair.Value.Checked = s.IsSpeciesEnabled(pair.Key);
        }

        void RebuildSpeciesRows(IReadOnlyList<RosterSpecies> visible)
        {
            speciesTable.SuspendLayout();
            foreach (Control c in speciesTable.Controls.Cast<Control>().ToList()) c.Dispose();
            speciesTable.Controls.Clear();
            speciesTable.RowStyles.Clear();
            speciesBoxes.Clear();
            speciesTable.RowCount = 0;

            foreach (var species in visible)
            {
                var type = species.InternalType;
                var box = new CheckBox { Text = species.Name, AutoSize = true, Anchor = AnchorStyles.Left };
                box.CheckedChanged += (s, e) => Change(x =>
                {
                    x.DisabledSpecies.Remove(type);
                    if (!box.Checked) x.DisabledSpecies.Add(type);
                });
                var spawn = new Button { Text = "Spawn now", AutoSize = true };
                spawn.Click += (s, e) => SpawnNow(type);
                speciesTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                speciesTable.Controls.Add(box, 0, speciesTable.RowCount);
                speciesTable.Controls.Add(spawn, 1, speciesTable.RowCount);
                speciesTable.RowCount++;
                speciesBoxes[type] = box;
            }
            speciesTable.ResumeLayout();
        }

        static string FormatCycle(int min, int max) =>
            "Hard rain after " + CycleSliderScale.Format(min) + " to " + CycleSliderScale.Format(max);

        static TableLayoutPanel NewTable()
        {
            var t = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, AutoScroll = true, Padding = new Padding(10) };
            t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            return t;
        }

        static void AddRow(TableLayoutPanel table, string label, Control control)
        {
            int row = table.RowCount;
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            if (label != null)
                table.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 8, 12, 3) }, 0, row);
            if (control != null)
            {
                control.Margin = new Padding(3, 4, 3, 4);
                table.Controls.Add(control, 1, row);
                if (label == null)
                {
                    table.SetColumn(control, 0);
                    table.SetColumnSpan(control, 2);
                }
            }
            table.RowCount = row + 1;
        }

        static FlowLayoutPanel Inline(params Control[] controls)
        {
            var flow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
            foreach (var c in controls) flow.Controls.Add(c);
            return flow;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                store.Changed -= OnStoreChanged;
                // The hidden tab isn't in the control tree, so dispose it here.
                if (!AdvancedVisible) advancedPage.Dispose();
                if (downpour.Parent == null) downpour.Dispose();
                if (watcher.Parent == null) watcher.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
