using RWDE.GameAssets.Install;
using RWDE.Sim;
using RWDE.UI.Configuration;

namespace RWDE.App
{
    /// <summary>
    /// Which DLC the app uses: a DLC counts only when it is installed and switched on in settings. A
    /// switched-off DLC is treated as absent everywhere (asset library, atlases, creature templates),
    /// as the game treats a disabled mod.
    /// </summary>
    public static class DlcSelection
    {
        /// <summary>The DLC switched on in <paramref name="settings"/>, installed or not.</summary>
        public static Dlc SwitchedOn(Settings settings)
        {
            var dlc = Dlc.None;
            if (settings.DownpourEnabled) dlc |= Dlc.Downpour;
            if (settings.WatcherEnabled) dlc |= Dlc.Watcher;
            return dlc;
        }

        /// <summary>The DLC that are both installed and switched on.</summary>
        public static Dlc Enabled(Settings settings, Dlc installed) => SwitchedOn(settings) & installed;

        /// <summary>The sim's view of <paramref name="enabled"/>, for <see cref="StaticWorld.Init"/>.</summary>
        public static StaticWorld.Dlc ForSim(Dlc enabled) =>
            new StaticWorld.Dlc(enabled.HasFlag(Dlc.Downpour), enabled.HasFlag(Dlc.Watcher));
    }
}
