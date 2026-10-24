using System;
using System.Diagnostics;
using System.Linq;
using RWDE.GameAssets;
using RWDE.Graphics;
using RWDE.Graphics.Direct3D;
using RWDE.Shell.Lifecycle;
using RWDE.Shell.Overlay;

namespace RWDE.App
{
    /// <summary>
    /// The composition root. <c>rwde</c> starts the app in the background; <c>rwde --stop</c> ends it.
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

            using var instance = AppInstance.TryClaim();
            if (instance == null)
            {
                UserMessages.Print("Rain World Desktop Ecosystem is already running. Stop it with: rwde --stop");
                return ExitAlreadyRunning;
            }

            var assets = GameAssetLibrary.Open();
            if (!assets.Available)
            {
                UserMessages.ShowError("Rain World Desktop Ecosystem needs an installed copy of Rain World.\n\n"
                    + assets.UnavailableReason);
                return ExitNoGame;
            }
            if (assets.AtlasExtractionError != null) UserMessages.Print(assets.AtlasExtractionError);

            UserMessages.Print("Rain World Desktop Ecosystem is running. Hold Escape for 8 seconds or run `rwde --stop` to quit.");
            Run(instance, assets);
            return ExitOk;
        }

        static void Run(AppInstance instance, GameAssetLibrary assets)
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
            var rainWorld = assets.GetAtlas("rainWorld");
            if (rainWorld.TryGet(out var atlas))
            {
                var loaded = renderer.LoadAtlas(Futile.atlasManager, atlas);
                UserMessages.Log($"Loaded atlas {atlas}: {loaded.elements.Count} elements, {loaded.textureSize.x}x{loaded.textureSize.y}.");
            }
            else
            {
                UserMessages.Log("No rainWorld atlas: " + rainWorld.Missing);
            }

            var stage = new FStage();
            var probe = new DesktopProbe(stage, window);
            var escape = new EscapeHoldQuit();
            var clock = Stopwatch.StartNew();

            while (window.PumpMessages())
            {
                if (instance.StopRequested) break;
                if (escape.Update(OverlayWindow.IsEscapeDown(), clock.Elapsed)) break;
                probe.Update(clock.Elapsed, escape);
                renderer.Render(stage);
            }
        }
    }
}
