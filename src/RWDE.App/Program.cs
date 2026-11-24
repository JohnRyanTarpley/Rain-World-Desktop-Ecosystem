using System;
using System.Diagnostics;
using System.Linq;
using RWDE.GameAssets;
using RWDE.GameAssets.Install;
using RWDE.Graphics;
using RWDE.Graphics.Direct3D;
using RWDE.Shell.Lifecycle;
using RWDE.Shell.Overlay;
using RWDE.UI.Configuration;
using RWDE.UI.Windows;

namespace RWDE.App
{
    /// <summary>
    /// The composition root. <c>rwde</c> starts the app in the background, <c>rwde --stop</c> ends it
    /// and <c>rwde --settings</c> opens its settings window.
    /// </summary>
    public static class Program
    {
        const int ExitOk = 0;
        const int ExitNotRunning = 1;
        const int ExitAlreadyRunning = 2;
        const int ExitNoGame = 3;

        [STAThread]
        public static int Main(string[] args)
        {
            if (args.Contains("--stop", StringComparer.OrdinalIgnoreCase))
            {
                if (AppInstance.RequestStop())
                {
                    UserMessages.Print("Rain World Desktop Ecosystem is stopping.");
                    return ExitOk;
                }
                UserMessages.Print("Rain World Desktop Ecosystem is not running.");
                return ExitNotRunning;
            }
            if (args.Contains("--settings", StringComparer.OrdinalIgnoreCase))
            {
                if (AppInstance.RequestSettings()) return ExitOk;
                UserMessages.Print("Rain World Desktop Ecosystem is not running. Start it with: rwde");
                return ExitNotRunning;
            }

            using var instance = AppInstance.TryClaim();
            if (instance == null)
            {
                UserMessages.Print("Rain World Desktop Ecosystem is already running. Stop it with: rwde --stop");
                return ExitAlreadyRunning;
            }
            UserMessages.StartLog();

            // Settings first: the saved install path (the settings window's picker) chooses the install,
            // which is how a copy of Rain World outside Steam is found. A new path applies on restart.
            var settings = SettingsStore.Load();
            var assets = GameAssetLibrary.Open(new GameAssetOptions { InstallPathOverride = settings.Current.InstallPath });
            if (!assets.Available)
            {
                UserMessages.ShowError("Rain World Desktop Ecosystem needs an installed copy of Rain World.\n\n"
                    + assets.UnavailableReason);
                return ExitNoGame;
            }
            if (assets.AtlasExtractionError != null) UserMessages.Print(assets.AtlasExtractionError);

            UserMessages.Print("Rain World Desktop Ecosystem is running. Hold Escape for 8 seconds or run `rwde --stop` to quit.");
            Run(instance, assets, settings);
            return ExitOk;
        }

        static void Run(AppInstance instance, GameAssetLibrary assets, SettingsStore settings)
        {
            using var window = OverlayWindow.Create();
            window.SetMode(OverlayMode.Desktop);
            if (window.DesktopModeProblem != null) UserMessages.Print(window.DesktopModeProblem);
            if (!window.RegisterModeHotkey()) UserMessages.Print("Ctrl+Alt+Shift+R is taken by another app, so the display mode can't be switched.");

            using var renderer = new FutileRenderer(window.Handle, window.Bounds.Width, window.Bounds.Height);
            window.Placed += () =>
            {
                renderer.Resize(window.Bounds.Width, window.Bounds.Height);
                renderer.AttachToWindow(window.Handle);
            };
            window.ModeHotkeyPressed += () =>
            {
                window.SetMode(window.RequestedMode.Toggle());
                if (window.DesktopModeProblem != null) UserMessages.Print(window.DesktopModeProblem);
                UserMessages.Log($"Switched to {window.Mode} mode.");
            };

            UserMessages.Log($"Overlay in {window.Mode} mode over {window.Bounds}.");

            renderer.RegisterWhiteElement(Futile.atlasManager);
            Futile.atlasManager.UnknownElement += UserMessages.Log;
            var atlases = GameAtlases.LoadAll(assets, atlas => renderer.LoadAtlas(Futile.atlasManager, atlas));
            foreach (var loaded in atlases.Loaded)
                UserMessages.Log($"Loaded atlas {loaded}: {loaded.elements.Count} elements, {loaded.textureSize.x}x{loaded.textureSize.y}.");
            foreach (var problem in atlases.Problems) UserMessages.Print(problem);

            // As the RoomCamera constructor; the room's own palette replaces it once rooms load one.
            var palettes = new RoomPaletteLoader(assets);
            palettes.ChangeMainPalette(0);
            UserMessages.Log($"Loaded palette {palettes.paletteA}: black {palettes.currentPalette.blackColor}, fog {palettes.currentPalette.fogColor}.");

            // The settings window and tray icon run on their own UI thread (RWDE.UI).
            var commands = new UiCommandQueue();
            var dlc = new DlcAvailability(assets.InstalledDlc.HasFlag(Dlc.Downpour), assets.InstalledDlc.HasFlag(Dlc.Watcher));
            using var settingsUi = SettingsUiHost.Start(settings, dlc, commands);
            window.SettingsHotkeyPressed += settingsUi.ShowSettings;
            if (!window.RegisterSettingsHotkey()) UserMessages.Print("Ctrl+Alt+Shift+S is taken by another app; open settings from the tray icon or with `rwde --settings`.");

            var stage = new FStage();
            var probe = new DesktopProbe(stage, window);
            var escape = new EscapeHoldQuit();
            var clock = Stopwatch.StartNew();

            while (window.PumpMessages())
            {
                if (instance.StopRequested) break;
                if (escape.Update(OverlayWindow.IsEscapeDown(), clock.Elapsed)) break;
                if (instance.TakeSettingsRequest()) settingsUi.ShowSettings();
                if (!HandleUiCommands(commands)) break;
                probe.Update(clock.Elapsed, escape);
                renderer.Render(stage);
            }
        }

        /// <summary>
        /// Drains what the user asked for in the settings window or tray menu. Returns false on Quit.
        /// The app doesn't run a <c>World</c> yet, so spawn and reset are only logged until the sim's
        /// command API exists.
        /// </summary>
        static bool HandleUiCommands(UiCommandQueue commands)
        {
            while (commands.TryTake(out var command))
            {
                switch (command)
                {
                    case QuitCommand:
                        return false;
                    case SpawnNowCommand spawn:
                        UserMessages.Log($"Spawn now: {spawn.InternalType} (the sim has no spawn command yet).");
                        break;
                    case ResetSimCommand:
                        UserMessages.Log("Reset requested (the sim has no reset command yet).");
                        break;
                }
            }
            return true;
        }
    }
}
