using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using RWDE.GameAssets;
using RWDE.GameAssets.Install;

namespace RWDE.Graphics.Tests
{
    public class GameAtlasesTests
    {
        private static readonly string[] BaseAtlases = { "rainWorld", "uiSprites", "shelterGate", "regionGate", "waterSprites" };
        private static readonly string[] SharedDlcAtlases = { "rainWorldMSC", "uiSpritesMSC" };
        private static readonly string[] WatcherAtlases = { "rainworldwatcher", "uispriteswatcher", "au_buildings" };

        private static string[] Names(Dlc installed) => GameAtlases.For(installed).Select(e => e.Name).ToArray();

        [Test]
        public void BaseGameLoadsOnlyTheBaseAtlases()
        {
            Assert.AreEqual(BaseAtlases, Names(Dlc.None));
        }

        [Test]
        public void DownpourAddsTheSharedDlcAtlases()
        {
            Assert.AreEqual(BaseAtlases.Concat(SharedDlcAtlases).ToArray(), Names(Dlc.Downpour));
        }

        [Test]
        public void WatcherAloneAlsoLoadsTheSharedDlcAtlases()
        {
            // ModManager.DLCShared is true with either DLC.
            Assert.AreEqual(BaseAtlases.Concat(SharedDlcAtlases).Concat(WatcherAtlases).ToArray(), Names(Dlc.Watcher));
            Assert.AreEqual(Names(Dlc.Watcher), Names(Dlc.Downpour | Dlc.Watcher));
        }

        [Test]
        public void AbsentDlcAtlasesAreNotProblems()
        {
            var find = Finder(BaseAtlases);
            var manager = new FAtlasManager();

            var report = GameAtlases.LoadAll(Dlc.None, find, a => Load(manager, a));

            CollectionAssert.IsEmpty(report.Problems);
            Assert.AreEqual(BaseAtlases, report.Loaded.Select(a => a.name).ToArray());
            Assert.IsTrue(manager.DoesContainElementWithName("rainWorld_element"));
            Assert.IsFalse(manager.DoesContainAtlas("rainWorldMSC"));
        }

        [Test]
        public void InstalledDlcLoadsItsAtlases()
        {
            var find = Finder(BaseAtlases.Concat(SharedDlcAtlases).Concat(WatcherAtlases));
            var manager = new FAtlasManager();

            var report = GameAtlases.LoadAll(Dlc.Watcher, find, a => Load(manager, a));

            CollectionAssert.IsEmpty(report.Problems);
            Assert.IsTrue(manager.DoesContainElementWithName("rainWorldMSC_element"));
            Assert.IsTrue(manager.DoesContainElementWithName("au_buildings_element"));
        }

        [Test]
        public void MissingExpectedAtlasIsReportedAndTheRestStillLoad()
        {
            var find = Finder(BaseAtlases.Where(n => n != "uiSprites").Concat(SharedDlcAtlases.Take(1)));
            var manager = new FAtlasManager();

            var report = GameAtlases.LoadAll(Dlc.Downpour, find, a => Load(manager, a));

            Assert.AreEqual(2, report.Problems.Count);
            StringAssert.Contains("uiSprites", report.Problems[0]);
            StringAssert.Contains("uiSpritesMSC", report.Problems[1]);
            Assert.AreEqual(5, report.Loaded.Count);
        }

        [Test]
        public void AtlasThatFailsToLoadIsReported()
        {
            var report = GameAtlases.LoadAll(Dlc.None, Finder(BaseAtlases),
                a => a.Name == "regionGate" ? throw new InvalidDataException("bad png") : Load(new FAtlasManager(), a));

            Assert.AreEqual(1, report.Problems.Count);
            StringAssert.Contains("regionGate", report.Problems[0]);
            StringAssert.Contains("bad png", report.Problems[0]);
        }

        // Atlases with one element named "<atlas>_element"; no files are read.
        private static System.Func<string, AssetResult<AtlasAsset>> Finder(IEnumerable<string> present)
        {
            var atlases = present.ToDictionary(n => n, n => new AtlasAsset(n, n + ".png", n + ".txt", "base"),
                System.StringComparer.OrdinalIgnoreCase);
            return name => atlases.TryGetValue(name, out var atlas)
                ? AssetResult<AtlasAsset>.Of(atlas)
                : AssetResult<AtlasAsset>.NotFound($"No atlas named '{name}'");
        }

        private static FAtlas Load(FAtlasManager manager, AtlasAsset asset) =>
            manager.LoadAtlasFromJson(asset.Name, TestAtlases.OtherJson.Replace("other.png", asset.Name + "_element.png"), null, 4, 4);
    }
}
