using System;
using System.IO;

namespace RWDE.Tests.Common
{
    /// <summary>
    /// Builds a throwaway Steam setup with a Rain World install in a temp folder, filled with the
    /// invented fixtures from <c>Fixtures~</c> plus generated palettes, atlas textures and sounds.
    /// It mirrors the real layout so install discovery and asset loading can be tested without the game.
    /// <code>
    /// &lt;temp&gt;/Steam/steamapps/libraryfolders.vdf          (two libraries; the game is in the second)
    /// &lt;temp&gt;/Library2/steamapps/appmanifest_312520.acf
    /// &lt;temp&gt;/Library2/steamapps/common/Rain World/RainWorld_Data/StreamingAssets/...
    /// </code>
    /// </summary>
    public sealed class FakeGameInstall : IDisposable
    {
        /// <summary>Rain World's Steam app id.</summary>
        public const string SteamAppId = "312520";
        public const string RegionCode = "xx";
        public const string AtlasName = "fixtureAtlas";

        readonly TempDirectory temp;

        public string Root => temp.Path;
        public string SteamRoot { get; }
        public string LibraryRoot { get; }
        public string LibraryFoldersVdf { get; }
        /// <summary>The <c>Rain World</c> folder, i.e. what a Settings path override would point at.</summary>
        public string GameRoot { get; }
        public string StreamingAssets { get; }

        FakeGameInstall()
        {
            temp = new TempDirectory("rwde-fake-install");
            SteamRoot = temp.Combine("Steam");
            LibraryRoot = temp.Combine("Library2");
            LibraryFoldersVdf = Path.Combine(SteamRoot, "steamapps", "libraryfolders.vdf");
            GameRoot = Path.Combine(LibraryRoot, "steamapps", "common", "Rain World");
            StreamingAssets = Path.Combine(GameRoot, "RainWorld_Data", "StreamingAssets");
        }

        /// <summary>Creates the full fake install. Dispose it to delete everything.</summary>
        public static FakeGameInstall Create()
        {
            var install = new FakeGameInstall();
            try
            {
                install.Populate();
                return install;
            }
            catch
            {
                install.Dispose();
                throw;
            }
        }

        /// <summary>Writes <paramref name="contents"/> to a path relative to StreamingAssets (forward slashes).</summary>
        public string WriteStreamingAsset(string relativePath, string contents)
        {
            var path = StreamingAssetPath(relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, contents);
            return path;
        }

        public string StreamingAssetPath(string relativePath) =>
            Path.Combine(StreamingAssets, relativePath.Replace('/', Path.DirectorySeparatorChar));

        public void Dispose() => temp.Dispose();

        void Populate()
        {
            Directory.CreateDirectory(Path.Combine(SteamRoot, "steamapps"));
            File.WriteAllText(LibraryFoldersVdf, Fixtures.ReadText("steam/libraryfolders.vdf")
                .Replace("{STEAM_ROOT}", VdfEscape(SteamRoot))
                .Replace("{LIBRARY_ROOT}", VdfEscape(LibraryRoot)));
            Fixtures.CopyTo("steam/appmanifest_312520.acf",
                Path.Combine(LibraryRoot, "steamapps", "appmanifest_" + SteamAppId + ".acf"));

            Fixtures.CopyTo("world/xx/world_xx.txt", StreamingAssetPath("world/xx/world_xx.txt"));
            Fixtures.CopyTo("modify/world/xx/world_xx.txt", StreamingAssetPath("mods/moreslugcats/modify/world/xx/world_xx.txt"));

            GameFormatWriters.WritePalettePng(StreamingAssetPath("palettes/palette0.png"), 0);
            GameFormatWriters.WritePalettePng(StreamingAssetPath("palettes/palette1.png"), 1);
            GameFormatWriters.WritePalettePng(StreamingAssetPath("palettes/palette-1.png"), 2);

            Fixtures.CopyTo("atlases/fixtureAtlas.txt", StreamingAssetPath("mods/moreslugcats/atlases/" + AtlasName + ".txt"));
            GameFormatWriters.WriteSolidPng(StreamingAssetPath("mods/moreslugcats/atlases/" + AtlasName + ".png"), 64, 32, 200, 40, 90);

            GameFormatWriters.WriteWav(StreamingAssetPath("soundeffects/fixture_chirp.wav"), frequency: 880);
            GameFormatWriters.WriteWav(StreamingAssetPath("loadedsoundeffects/fixture_hum.wav"), frequency: 110);
        }

        static string VdfEscape(string path) => path.Replace("\\", "\\\\");
    }
}
