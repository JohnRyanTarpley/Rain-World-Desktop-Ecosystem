using System.IO;
using System.Linq;
using NUnit.Framework;
using RWDE.GameAssets;
using RWDE.Sim;
using RWDE.Tests.Common;

namespace RWDE.App.Tests
{
    // The invented region "yy" read through the game asset cache: base file plus both DLC modify files.
    public class OutskirtsSpawnDataTests
    {
        private FakeGameInstall install;
        private TempDirectory cache;

        [SetUp]
        public void SetUp()
        {
            install = FakeGameInstall.Create();
            cache = new TempDirectory("rwde-spawn-cache");
            // The locator needs a resources.assets; an empty one only costs the atlases.
            File.WriteAllBytes(Path.Combine(install.GameRoot, "RainWorld_Data", "resources.assets"), new byte[0]);
            Fixtures.CopyTo("world/yy/world_yy.txt", install.StreamingAssetPath("world/yy/world_yy.txt"));
            Fixtures.CopyTo("world/yy/properties.txt", install.StreamingAssetPath("world/yy/properties.txt"));
            Fixtures.CopyTo("modify/moreslugcats/world/yy/world_yy.txt", install.StreamingAssetPath("mods/moreslugcats/modify/world/yy/world_yy.txt"));
            Fixtures.CopyTo("modify/watcher/world/yy/world_yy.txt", install.StreamingAssetPath("mods/watcher/modify/world/yy/world_yy.txt"));
        }

        [TearDown]
        public void TearDown()
        {
            install.Dispose();
            cache.Dispose();
        }

        private RegionSpawnData Load()
        {
            var library = GameAssetLibrary.Open(new GameAssetOptions
            {
                InstallPathOverride = install.GameRoot,
                CacheRoot = cache.Path,
                Regions = new[] { "yy" },
            });
            Assert.IsTrue(library.Available, library.UnavailableReason);
            var data = OutskirtsSpawnData.Load(library, "yy");
            Assert.IsNotNull(data, string.Join(", ", library.WorldFileNames));
            return data;
        }

        private static string[] Describe(SpawnTable table) => table.Spawners.Select(s => s.ToString()).ToArray();

        [Test]
        public void ModifyFilesAndCampaignFiltersReachTheSpawnTable()
        {
            var data = Load();

            var saint = Describe(data.BuildTable("Saint"));
            CollectionAssert.Contains(saint, "YY_A01.2: 1 SpitLizard{PreCycle}");
            CollectionAssert.Contains(saint, "YY_A01.3: 4 YellowLizard");
            CollectionAssert.Contains(saint, "OFFSCREEN.0: 2 MirosVulture{Night}");
            CollectionAssert.DoesNotContain(saint, "YY_A01.2: 1 GreenLizard");

            // The Downpour file's [FINDLINESTART] removed the unfiltered line; (X-Saint) put it back for others.
            var white = Describe(data.BuildTable("White"));
            Assert.AreEqual(1, white.Count(s => s == "YY_A01.2: 1 GreenLizard"));

            CollectionAssert.Contains(Describe(data.BuildTable("Watcher")), "OFFSCREEN.0: 1 KingVulture");
        }

        [Test]
        public void ConditionalLinksAndPropertiesComeThrough()
        {
            var data = Load();

            CollectionAssert.AreEqual(new[] { "YY_A02", "YY_A04" }, data.BuildTable("Watcher").SwarmRooms);
            CollectionAssert.AreEqual(new[] { 5, 5, 5 }, data.BuildTable("White").FliesPerSwarmRoom);
        }

        [Test]
        public void AllOutskirtsCreaturesHasEveryCampaignsCreatures()
        {
            var types = Load().BuildTable(Campaign.AllOutskirtsCreatures).Spawners.OfType<Sim.World.SimpleSpawner>()
                .Select(s => s.creatureType).Distinct().ToList();

            CollectionAssert.IsSubsetOf(new[]
            {
                CreatureTemplate.Type.GreenLizard, CreatureTemplate.Type.SpitLizard, CreatureTemplate.Type.MirosVulture,
                CreatureTemplate.Type.KingVulture, CreatureTemplate.Type.Scavenger, CreatureTemplate.Type.BlueLizard
            }, types);
        }

        [Test]
        public void MissingRegionGivesNoData()
        {
            var library = GameAssetLibrary.Open(new GameAssetOptions { InstallPathOverride = install.GameRoot, CacheRoot = cache.Path });

            Assert.IsNull(OutskirtsSpawnData.Load(library, "zz"));
            Assert.IsFalse(Directory.Exists(install.StreamingAssetPath("world/zz")));
        }
    }
}
