using System.Collections.Generic;
using System.Linq;
using RWDE.Sim;
using RWDE.UI.Configuration;

namespace RWDE.UI.Windows
{
    /// <summary>Which DLC the Rain World install has. The app fills it from the game assets.</summary>
    public sealed class DlcAvailability
    {
        public static readonly DlcAvailability BaseGameOnly = new DlcAvailability(false, false);
        public static readonly DlcAvailability Everything = new DlcAvailability(true, true);

        public DlcAvailability(bool downpour, bool watcher)
        {
            Downpour = downpour;
            Watcher = watcher;
        }

        public bool Downpour { get; }
        public bool Watcher { get; }
    }

    /// <summary>An entry in the campaign spawn table list: the stored value and what the user sees.</summary>
    public sealed class CampaignChoice
    {
        public CampaignChoice(string value, string label)
        {
            Value = value;
            Label = label;
        }

        /// <summary>The <see cref="Settings.CampaignSpawnTable"/> value: a timeline, or "All Outskirts creatures".</summary>
        public string Value { get; }

        public string Label { get; }

        public override string ToString() => Label;
    }

    /// <summary>
    /// What the settings window shows for a given install and settings, kept apart from the
    /// WinForms controls so it can be tested. Content from a DLC that is missing or switched off is
    /// left out (docs/VISION.md, "Installed DLC").
    /// </summary>
    public static class SettingsWindowModel
    {
        /// <summary>True when content from <paramref name="source"/> is installed and switched on.</summary>
        public static bool IsAvailable(Campaign.Source source, DlcAvailability installed, Settings settings)
        {
            switch (source)
            {
                case Campaign.Source.Base: return true;
                case Campaign.Source.Downpour: return installed.Downpour && settings.DownpourEnabled;
                case Campaign.Source.Watcher: return installed.Watcher && settings.WatcherEnabled;
                default: return false;
            }
        }

        public static IReadOnlyList<RosterSpecies> VisibleSpecies(DlcAvailability installed, Settings settings) =>
            OutskirtsRoster.All.Where(s => IsAvailable(s.Source, installed, settings)).ToList();

        /// <summary>"All Outskirts creatures" first, then each available campaign in story order.</summary>
        public static IReadOnlyList<CampaignChoice> VisibleCampaigns(DlcAvailability installed, Settings settings)
        {
            var list = new List<CampaignChoice> { new CampaignChoice(Settings.AllOutskirtsCreatures, Settings.AllOutskirtsCreatures) };
            foreach (var c in Campaign.All)
                if (IsAvailable(c.From, installed, settings))
                    list.Add(new CampaignChoice(c.Timeline, CampaignLabel(c)));
            return list;
        }

        /// <summary>The campaign's slugcat name as the game's menus show it.</summary>
        public static string CampaignLabel(Campaign campaign)
        {
            switch (campaign.Timeline)
            {
                case "White": return "Survivor";
                case "Yellow": return "Monk";
                case "Red": return "Hunter";
                case "Spear": return "Spearmaster";
                case "Watcher": return "The Watcher";
                default: return campaign.Timeline;
            }
        }

        /// <summary>The Advanced tab's Downpour switch is shown only when Downpour is installed.</summary>
        public static bool ShowDownpourSwitch(DlcAvailability installed) => installed.Downpour;

        /// <summary>The Advanced tab's Watcher switch is shown only when The Watcher is installed.</summary>
        public static bool ShowWatcherSwitch(DlcAvailability installed) => installed.Watcher;

        /// <summary>"Realistic Rain (Perpetual)" means nothing without rain, so it is disabled then.</summary>
        public static bool PerpetualRainEnabled(Settings settings) => settings.RainEnabled;

        /// <summary>Sets the cycle length back to the game's 400–800 s.</summary>
        public static void ResetCycleToGameDefault(Settings settings)
        {
            settings.CycleTimeMinSeconds = Settings.GameCycleTimeMin;
            settings.CycleTimeMaxSeconds = Settings.GameCycleTimeMax;
        }
    }
}
