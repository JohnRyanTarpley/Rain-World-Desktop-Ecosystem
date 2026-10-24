using System.IO;
using System.Linq;
using NUnit.Framework;
using RWDE.GameAssets.Tests.Fixtures;
using RWDE.GameAssets.Unity;

namespace RWDE.GameAssets.Tests
{
    public class GameAssetLibraryTests
    {
        private FakeInstall _fake;

        [SetUp]
        public void SetUp() => _fake = new FakeInstall();

        [TearDown]
        public void TearDown() => _fake.Dispose();

        [Test]
        public void MissingInstallMakesEveryLookupNotFound()
        {
            var library = GameAssetLibrary.Open(new GameAssetOptions
            {
                SteamRoots = new[] { Path.Combine(_fake.RootFolder, "NoSteam") },
                CacheRoot = _fake.CacheRoot,
            });

            Assert.IsFalse(library.Available);
            Assert.IsNotNull(library.UnavailableReason);
            Assert.IsFalse(library.GetAtlas("testAtlas").Found);
            Assert.AreEqual(library.UnavailableReason, library.GetPalette(1).Missing);
            Assert.IsFalse(library.GetSound("x").Found);
            Assert.IsFalse(library.GetWorldFile("xx/world_xx.txt").Found);
            Assert.IsFalse(Directory.Exists(_fake.CacheRoot));
        }

        [Test]
        public void ExtractsBaseAtlasesIntoCacheAndIndexesModAtlases()
        {
            _fake.WriteStreamingFile("mods/watcher/atlases/modAtlas.png", "png");
            _fake.WriteStreamingFile("mods/watcher/atlases/modAtlas.txt", FakeInstall.AtlasJson);
            _fake.WriteStreamingFile("mods/watcher/atlases/noData.png", "png");

            var library = GameAssetLibrary.Open(_fake.Options());

            Assert.IsTrue(library.Available, library.UnavailableReason);
            Assert.IsNull(library.AtlasExtractionError);
            var baseAtlas = library.GetAtlas("Atlases/testAtlas");
            Assert.IsTrue(baseAtlas.Found, baseAtlas.Missing);
            Assert.AreEqual("base", baseAtlas.Value.Source);
            StringAssert.StartsWith(_fake.CacheRoot, baseAtlas.Value.ImagePath);
            Assert.IsTrue(File.Exists(baseAtlas.Value.ImagePath));
            Assert.IsTrue(File.Exists(baseAtlas.Value.DataPath));

            var modAtlas = library.GetAtlas("MODATLAS");
            Assert.IsTrue(modAtlas.Found);
            Assert.AreEqual("watcher", modAtlas.Value.Source);
            Assert.IsFalse(library.GetAtlas("noData").Found);
            StringAssert.Contains("missingAtlas", library.GetAtlas("missingAtlas").Missing);
        }

        [Test]
        public void CacheIsReusedUntilGameVersionChanges()
        {
            Assert.IsTrue(GameAssetLibrary.Open(_fake.Options()).Cache.ExtractedThisRun);
            Assert.IsFalse(GameAssetLibrary.Open(_fake.Options()).Cache.ExtractedThisRun);

            File.WriteAllText(Path.Combine(_fake.StreamingAssets, "GameVersion.txt"), "v9.9.10");
            _fake.WriteAssets(new FakeAssetsFile()
                .AddTextAsset("renamedAtlas", FakeInstall.AtlasJson)
                .AddTexture("renamedAtlas", 2, 2, TextureDecoder.RGBA32, FakeInstall.Pixels2x2Rgba()));

            var library = GameAssetLibrary.Open(_fake.Options());

            Assert.IsTrue(library.Cache.ExtractedThisRun);
            Assert.IsTrue(library.GetAtlas("renamedAtlas").Found);
            Assert.IsFalse(library.GetAtlas("testAtlas").Found, "stale atlases are removed on re-extract");
        }

        [Test]
        public void BrokenAssetsFileStillLeavesLooseFilesAvailable()
        {
            File.WriteAllBytes(Path.Combine(_fake.DataFolder, "resources.assets"), new byte[64]);
            _fake.WriteStreamingFile("palettes/palette3.png", "png");

            var library = GameAssetLibrary.Open(_fake.Options());

            Assert.IsTrue(library.Available);
            Assert.IsNotNull(library.AtlasExtractionError);
            StringAssert.Contains(library.AtlasExtractionError, library.GetAtlas("testAtlas").Missing);
            Assert.IsTrue(library.GetPalette(3).Found);
        }

        [Test]
        public void FindsPalettesAndSounds()
        {
            _fake.WriteStreamingFile("palettes/palette12.png", "png");
            _fake.WriteStreamingFile("palettes/effectColors.png", "png");
            _fake.WriteStreamingFile("loadedsoundeffects/testChirp.wav", "wav");
            _fake.WriteStreamingFile("loadedsoundeffects/ambient/testWind.ogg", "ogg");
            _fake.WriteStreamingFile("loadedsoundeffects/notes.txt", "not a sound");
            _fake.WriteStreamingFile("soundeffects/sounds.txt", "Test_Chirp : testChirp");

            var library = GameAssetLibrary.Open(_fake.Options());

            Assert.IsTrue(library.GetPalette(12).Found);
            Assert.IsTrue(library.GetPalette("palettes/effectcolors.png").Found);
            Assert.IsFalse(library.GetPalette(99).Found);
            StringAssert.EndsWith("testChirp.wav", library.GetSound("testChirp").Value.Path);
            Assert.IsTrue(library.GetSound("testWind").Found);
            Assert.IsFalse(library.GetSound("notes").Found);
            StringAssert.EndsWith("sounds.txt", library.SoundDefinitionsPath);
        }

        [Test]
        public void WorldFileMergesModsInLoadOrder()
        {
            _fake.WriteStreamingFile("world/xx/world_xx.txt", "ROOMS\nXX_A01 : XX_A02\nXX_A02 : XX_A01\nEND ROOMS\nCREATURES\nXX_A01 : 1-Green\nEND CREATURES\n");
            _fake.WriteStreamingFile("mods/moreslugcats/modify/world/xx/world_xx.txt",
                "[MERGE]\nCREATURES\n(Alpha)XX_A02 : 1-Pink\nEND CREATURES\n[ENDMERGE]\n");
            _fake.WriteStreamingFile("mods/watcher/modify/world/xx/WORLD_XX.txt", "[FIND]1-Pink\n[REPLACE]1-Cyan\n");
            var options = _fake.Options();
            options.Regions = new[] { "xx" };

            var library = GameAssetLibrary.Open(options);
            var world = library.GetWorldFile("world/xx/world_xx.txt");

            Assert.IsTrue(world.Found, world.Missing);
            CollectionAssert.AreEqual(new[] { "moreslugcats", "watcher" }, world.Value.AppliedMods);
            CollectionAssert.Contains(world.Value.Lines, "XX_A01 : 1-Green");
            CollectionAssert.Contains(world.Value.Lines, "(Alpha)XX_A02 : 1-Cyan");
            Assert.AreEqual(0, world.Value.Warnings.Length);
            Assert.IsTrue(library.WorldFileNames.Contains("xx/world_xx.txt"));
        }

        [Test]
        public void ModWorldFileReplacesBaseAndUnmergedFilesPassThrough()
        {
            _fake.WriteStreamingFile("world/xx/properties.txt", "Palette: 1\n");
            _fake.WriteStreamingFile("world/xx/displayname.txt", "Base Name\n");
            _fake.WriteStreamingFile("mods/moreslugcats/world/xx/displayname.txt", "Mod Name\n");
            var options = _fake.Options();
            options.Regions = new[] { "xx" };

            var library = GameAssetLibrary.Open(options);

            CollectionAssert.AreEqual(new[] { "Palette: 1" }, library.GetWorldFile("xx/properties.txt").Value.Lines);
            CollectionAssert.AreEqual(new[] { "Mod Name" }, library.GetWorldFile("xx/displayname.txt").Value.Lines);
            Assert.IsFalse(library.GetWorldFile("xx/world_missing.txt").Found);
            Assert.IsFalse(library.GetWorldFile("yy/world_yy.txt").Found, "regions outside the options are not indexed");
        }
    }
}
