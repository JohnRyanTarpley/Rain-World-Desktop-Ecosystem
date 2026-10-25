using System.IO;
using NUnit.Framework;
using RWDE.GameAssets.Install;
using RWDE.GameAssets.Tests.Fixtures;
using RWDE.GameAssets.Unity;

namespace RWDE.GameAssets.Tests
{
    public class InstalledDlcTests
    {
        private FakeInstall _fake;

        [SetUp]
        public void SetUp()
        {
            _fake = new FakeInstall();
            // A base atlas plus one packed atlas the game only loads with a DLC (ModManager.DLCShared).
            _fake.WriteAssets(new FakeAssetsFile()
                .AddTextAsset("testAtlas", FakeInstall.AtlasJson)
                .AddTexture("testAtlas", 2, 2, TextureDecoder.RGBA32, FakeInstall.Pixels2x2Rgba())
                .AddTextAsset("rainWorldMSC", FakeInstall.AtlasJson)
                .AddTexture("rainWorldMSC", 2, 2, TextureDecoder.RGBA32, FakeInstall.Pixels2x2Rgba()));
        }

        [TearDown]
        public void TearDown() => _fake.Dispose();

        [TestCase(Dlc.None)]
        [TestCase(Dlc.Downpour)]
        [TestCase(Dlc.Watcher)]
        [TestCase(Dlc.Downpour | Dlc.Watcher)]
        public void DetectsDlcFromFoldersAndExtractsOnlyTheirPackedAtlases(Dlc installed)
        {
            InstallDlc(installed);

            var library = GameAssetLibrary.Open(_fake.Options());

            Assert.IsTrue(library.Available, library.UnavailableReason);
            Assert.AreEqual(installed, library.InstalledDlc);
            Assert.IsNull(library.AtlasExtractionError);
            Assert.IsTrue(library.GetAtlas("testAtlas").Found, "base atlases are always extracted");
            Assert.AreEqual(installed != Dlc.None, library.GetAtlas("rainWorldMSC").Found);
            CollectionAssert.IsEmpty(library.Cache.SkippedAtlases, "a missing DLC is not reported as a problem");
        }

        [Test]
        public void OtherModFoldersAreNotDlc()
        {
            _fake.WriteStreamingFile("mods/expedition/modinfo.json", "{}");

            var library = GameAssetLibrary.Open(_fake.Options());

            Assert.AreEqual(Dlc.None, library.InstalledDlc);
        }

        [Test]
        public void InstallingADlcLaterReExtracts()
        {
            Assert.IsFalse(GameAssetLibrary.Open(_fake.Options()).GetAtlas("rainWorldMSC").Found);

            InstallDlc(Dlc.Watcher);
            var library = GameAssetLibrary.Open(_fake.Options());

            Assert.IsTrue(library.Cache.ExtractedThisRun);
            Assert.IsTrue(library.GetAtlas("rainWorldMSC").Found);
        }

        [Test]
        public void MissingInstallHasNoDlc()
        {
            var library = GameAssetLibrary.Open(new GameAssetOptions
            {
                SteamRoots = new[] { Path.Combine(_fake.RootFolder, "NoSteam") },
                CacheRoot = _fake.CacheRoot,
            });

            Assert.AreEqual(Dlc.None, library.InstalledDlc);
        }

        private void InstallDlc(Dlc installed)
        {
            foreach (var dlc in new[] { Dlc.Downpour, Dlc.Watcher })
            {
                if ((installed & dlc) != 0) _fake.WriteStreamingFile("mods/" + DlcDetector.FolderName(dlc) + "/modinfo.json", "{}");
            }
        }
    }
}
