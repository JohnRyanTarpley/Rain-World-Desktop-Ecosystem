using System;
using System.Collections.Generic;
using System.Linq;
using RWDE.Shell.Overlay;

namespace RWDE.UI.Configuration
{
    /// <summary>
    /// Every user setting, serialised as one JSON file (see <see cref="SettingsStore"/>). Other
    /// departments read it through <see cref="ISettingsSource"/> and never write it. A new instance
    /// holds the defaults; <see cref="Normalize"/> pulls any value back into its valid range.
    /// </summary>
    public sealed class Settings
    {
        /// <summary>The campaign spawn table that mixes every campaign's Outskirts creatures.</summary>
        public const string AllOutskirtsCreatures = "All Outskirts creatures";

        /// <summary>Shortest cycle the slider allows: 1 min.</summary>
        public const int MinCycleSeconds = 60;

        /// <summary>Longest cycle the slider allows: 23 h 59 min.</summary>
        public const int MaxCycleSeconds = 23 * 3600 + 59 * 60;

        /// <summary>The game's <c>RainWorld.setup.cycleTimeMin</c>: 6 min 40 s.</summary>
        public const int GameCycleTimeMin = 400;

        /// <summary>The game's <c>RainWorld.setup.cycleTimeMax</c>: 13 min 20 s.</summary>
        public const int GameCycleTimeMax = 800;

        /// <summary>Multiplier on the game's spawn rate; 1 is the game's rate.</summary>
        public float SpawnRate { get; set; } = 1f;

        /// <summary>Which campaign's spawn table to use, or <see cref="AllOutskirtsCreatures"/>.</summary>
        public string CampaignSpawnTable { get; set; } = AllOutskirtsCreatures;

        /// <summary>
        /// Internal types (e.g. <c>GreenLizard</c>) the user switched off. Stored as the exceptions so a
        /// species from a newly installed DLC is allowed without a migration.
        /// </summary>
        public List<string> DisabledSpecies { get; set; } = new List<string>();

        /// <summary>Most creatures on the desktop at once.</summary>
        public int PopulationCap { get; set; } = 20;

        /// <summary>Master volume, 0 to 1.</summary>
        public float Volume { get; set; } = 1f;

        public OverlayMode DisplayMode { get; set; } = OverlayMode.Desktop;

        /// <summary>Topmost mode only: app window tops become terrain (exploratory).</summary>
        public bool WindowTopsAsTerrain { get; set; }

        /// <summary>The Rain World install folder, or null to search the Steam libraries.</summary>
        public string InstallPath { get; set; }

        /// <summary>Downpour (<c>mods/moreslugcats</c>) content, when installed.</summary>
        public bool DownpourEnabled { get; set; } = true;

        /// <summary>The Watcher (<c>mods/watcher</c>) content, when installed.</summary>
        public bool WatcherEnabled { get; set; } = true;

        /// <summary>Off: creatures never flee and the cycle never ends.</summary>
        public bool RainEnabled { get; set; } = true;

        /// <summary>"Realistic Rain (Perpetual)": once the hard rain comes it never stops.</summary>
        public bool PerpetualRain { get; set; }

        /// <summary>Shortest time until the hard rain, in seconds.</summary>
        public int CycleTimeMinSeconds { get; set; } = GameCycleTimeMin;

        /// <summary>Longest time until the hard rain, in seconds.</summary>
        public int CycleTimeMaxSeconds { get; set; } = GameCycleTimeMax;

        public bool IsSpeciesEnabled(string internalType) => !DisabledSpecies.Contains(internalType);

        /// <summary>Clamps every value into its valid range and puts the cycle bounds in order.</summary>
        public Settings Normalize()
        {
            SpawnRate = float.IsFinite(SpawnRate) ? Math.Max(0f, SpawnRate) : 1f;
            Volume = float.IsFinite(Volume) ? Math.Clamp(Volume, 0f, 1f) : 1f;
            PopulationCap = Math.Max(0, PopulationCap);
            if (string.IsNullOrWhiteSpace(CampaignSpawnTable)) CampaignSpawnTable = AllOutskirtsCreatures;
            if (!Enum.IsDefined(DisplayMode)) DisplayMode = OverlayMode.Desktop;
            if (string.IsNullOrWhiteSpace(InstallPath)) InstallPath = null;
            DisabledSpecies = (DisabledSpecies ?? new List<string>())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            int min = Math.Clamp(CycleTimeMinSeconds, MinCycleSeconds, MaxCycleSeconds);
            int max = Math.Clamp(CycleTimeMaxSeconds, MinCycleSeconds, MaxCycleSeconds);
            CycleTimeMinSeconds = Math.Min(min, max);
            CycleTimeMaxSeconds = Math.Max(min, max);
            return this;
        }

        public Settings Clone()
        {
            var copy = (Settings)MemberwiseClone();
            copy.DisabledSpecies = new List<string>(DisabledSpecies ?? new List<string>());
            return copy;
        }
    }
}
