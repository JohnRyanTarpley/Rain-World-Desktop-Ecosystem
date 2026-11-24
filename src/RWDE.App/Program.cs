using System;
using System.Diagnostics;
using System.Linq;
using RWDE.GameAssets;
using RWDE.GameAssets.Install;
using RWDE.Graphics;
using RWDE.Graphics.Direct3D;
using RWDE.Shell.Lifecycle;
using RWDE.Shell.Overlay;
using RWDE.Shell.Terrain;
using RWDE.Sim;
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

        /// <summary>How often the loop wakes while paused, to notice the fullscreen app leaving, a stop request or Escape.</summary>
        static readonly TimeSpan PausedWait = TimeSpan.FromMilliseconds(100);

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
            var settingsStore = SettingsStore.Load();
            var settings = settingsStore.Current;
            // A switched-off DLC's world files and atlases are never read, so its modify files can't
            // change base-game spawns (the game skips a disabled mod the same way).
            var assets = GameAssetLibrary.Open(new GameAssetOptions
            {
                InstallPathOverride = settings.InstallPath,
                Mods = GameAssetOptions.ModsFor(DlcSelection.SwitchedOn(settings)),
            });
            if (!assets.Available)
            {
                // The settings window isn't up yet, so ask for the folder here; otherwise a copy outside
                // Steam, or a saved path that no longer exists, could never be fixed.
                UserMessages.Print(assets.UnavailableReason);
                var chosen = InstallPrompt.Ask(assets.UnavailableReason,
                    path => RainWorldInstallLocator.Locate(path, Array.Empty<string>()).Found);
                if (chosen == null)
                    return ExitNoGame;
                try
                {
                    settingsStore.Update(x => x.InstallPath = chosen);
                    UserMessages.Log($"Rain World folder set to {chosen}.");
                }
                catch (Exception e) when (e is System.IO.IOException || e is UnauthorizedAccessException)
                {
                    // Still use it for this run; it will be asked for again next time.
                    UserMessages.Print($"Using {chosen} for now, but couldn't save it to {settingsStore.FilePath}: {e.Message}");
                }
                settings = settingsStore.Current;
                assets = GameAssetLibrary.Open(new GameAssetOptions
                {
                    InstallPathOverride = chosen,
                    Mods = GameAssetOptions.ModsFor(DlcSelection.SwitchedOn(settings)),
                });
                if (!assets.Available)
                {
                    UserMessages.ShowError("Rain World Desktop Ecosystem needs an installed copy of Rain World.\n\n"
                        + assets.UnavailableReason);
                    return ExitNoGame;
                }
            }
            if (assets.AtlasExtractionError != null) UserMessages.Print(assets.AtlasExtractionError);

            UserMessages.Print("Rain World Desktop Ecosystem is running. Hold Escape for 8 seconds or run `rwde --stop` to quit.");
            Run(instance, assets, settingsStore);
            return ExitOk;
        }

        static void Run(AppInstance instance, GameAssetLibrary assets, SettingsStore settingsStore)
        {
            var settings = settingsStore.Current;
            using var window = OverlayWindow.Create();
            window.SetMode(settings.DisplayMode);
            if (window.DesktopModeProblem != null) UserMessages.Print(window.DesktopModeProblem);
            if (!window.RegisterModeHotkey()) UserMessages.Print("Ctrl+Alt+Shift+R is taken by another app, so the display mode can't be switched.");

            var gate = new RenderGate();
            using var renderer = new FutileRenderer(window.Handle, window.Bounds.Width, window.Bounds.Height);
            window.Placed += () =>
            {
                renderer.Resize(window.Bounds.Width, window.Bounds.Height);
                renderer.AttachToWindow(window.Handle);
                gate.Invalidate();
            };
            window.ModeHotkeyPressed += () =>
            {
                window.SetMode(window.RequestedMode.Toggle());
                if (window.DesktopModeProblem != null) UserMessages.Print(window.DesktopModeProblem);
                UserMessages.Log($"Switched to {window.Mode} mode.");
            };

            UserMessages.Log($"Overlay in {window.Mode} mode over {window.Bounds}.");

            // Asset cache: the game's atlases and palettes.
            renderer.RegisterWhiteElement(Futile.atlasManager);
            Futile.atlasManager.UnknownElement += UserMessages.Log;
            var enabledDlc = DlcSelection.Enabled(settings, assets.InstalledDlc);
            var atlases = GameAtlases.LoadAll(enabledDlc, assets.GetAtlas, atlas => renderer.LoadAtlas(Futile.atlasManager, atlas));
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
            using var settingsUi = SettingsUiHost.Start(settingsStore, dlc, commands);
            window.SettingsHotkeyPressed += settingsUi.ShowSettings;
            if (!window.RegisterSettingsHotkey()) UserMessages.Print("Ctrl+Alt+Shift+S is taken by another app; open settings from the tray icon or with `rwde --settings`.");

            // Terrain source and world.
            var terrain = new DesktopTerrainSource(new Win32DesktopLayoutSource());
            UserMessages.Log($"Desktop room: {terrain.Width}x{terrain.Height} tiles from {terrain.Layout.Icons.Count} icons.");

            var spawnData = OutskirtsSpawnData.Load(assets);
            if (spawnData == null) UserMessages.Print("The Outskirts world file is missing from the Rain World install, so no creatures will spawn.");
            var stage = new FStage();
            var sim = new Simulation(terrain, StaticWorld.Init(DlcSelection.ForSim(enabledDlc)), spawnData,
                settings.CampaignSpawnTable, Environment.TickCount, stage);
            sim.World.populationCap = settings.PopulationCap;
            sim.Camera.ApplyPalette(palettes.currentPalette);
            if (sim.CampaignSetting != settings.CampaignSpawnTable)
                UserMessages.Log($"\"{settings.CampaignSpawnTable}\" needs a DLC that is missing or switched off; using \"{sim.CampaignSetting}\".");
            UserMessages.Log($"World: {sim.World.Creatures.Count} creatures for \"{sim.CampaignSetting}\".");

            var escape = new EscapeHoldQuit();
            var fullscreen = FullscreenPause.ForCurrentUser();
            var ticks = new FixedTickClock();
            var clock = Stopwatch.StartNew();

            while (window.PumpMessages())
            {
                // Input: stop requests and the Escape hold (the mode hotkey arrives through PumpMessages).
                var now = clock.Elapsed;
                if (instance.StopRequested) break;
                if (escape.Update(OverlayWindow.IsEscapeDown(), now)) break;
                if (instance.TakeSettingsRequest()) settingsUi.ShowSettings();
                if (!HandleUiCommands(commands)) break;

                bool wasPaused = fullscreen.Paused;
                if (fullscreen.Update(now))
                {
                    if (!wasPaused) UserMessages.Log("The desktop is hidden (fullscreen app or locked session); pausing.");
                    ticks.Stop();
                    window.WaitForMessages(PausedWait);
                    continue;
                }
                if (wasPaused)
                {
                    UserMessages.Log("Resuming.");
                    gate.Invalidate();
                }

                for (int due = ticks.Advance(now); due > 0; due--)
                {
                    if (sim.Tick()) gate.Invalidate();
                }

                if (gate.ShouldRender(sim.Animating))
                {
                    sim.DrawUpdate(ticks.Alpha);
                    renderer.Render(stage);
                    gate.Rendered();
                }
                else
                {
                    window.WaitForMessages(ticks.UntilNextTick);
                }
            }
        }

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
