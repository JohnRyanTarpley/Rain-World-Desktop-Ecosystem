using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RWDE.Tests.Common;
using Type = RWDE.Sim.CreatureTemplate.Type;

namespace RWDE.Sim.Tests.Spawning
{
    // Campaign tables built from the invented region "yy" (tests/Fixtures/world/yy).
    public class RegionSpawnDataTests
    {
        private static RegionSpawnData Fixture(string region = "yy") =>
            new RegionSpawnData(region, Fixtures.ReadLines("world/yy/world_yy.txt"), Fixtures.ReadLines("world/yy/properties.txt"));

        private static List<string> Describe(SpawnTable table) => table.Spawners.Select(s => s.ToString()).ToList();

        private static RegionSpawnData Inline(params string[] lines) => new RegionSpawnData("yy", lines);

        [Test]
        public void OneCampaignKeepsItsOwnAndUnfilteredLines()
        {
            var white = Fixture().BuildTable("White");

            CollectionAssert.AreEquivalent(new[]
            {
                "YY_A01.2: 1 GreenLizard", "YY_A01.3: 2 PinkLizard", "YY_A02.4: 3 SmallCentipede",
                "YY_A02.5: 1 PoleMimic{12}", "lineage YY_A03.2", "OFFSCREEN.0: 3 Scavenger", "YY_A04.2: 2 CicadaB"
            }, Describe(white));
            Assert.AreEqual(Campaign.White, white.Campaigns.Single());
        }

        [Test]
        public void ExcludeFilterAndUnknownRoomsApplyPerCampaign()
        {
            var red = Fixture().BuildTable("Red");

            CollectionAssert.Contains(Describe(red), "YY_A04.4: 1 BlueLizard");
            CollectionAssert.Contains(Describe(red), "lineage YY_A02.3");
            CollectionAssert.DoesNotContain(Describe(red), "OFFSCREEN.0: 3 Scavenger");
            // (Red)YY_NOWHERE names no room in ROOMS.
            Assert.AreEqual(7, red.Spawners.Count);
        }

        [Test]
        public void NonRosterCreaturesAreReported()
        {
            var yellow = Fixture().BuildTable("Yellow");

            CollectionAssert.AreEqual(new[] { "Leech" }, yellow.UnknownCreatures);
            Assert.IsFalse(yellow.Spawners.OfType<World.SimpleSpawner>().Any(s => s.den.abstractNode == 3 && s.den.room == "YY_A04"));
        }

        [Test]
        public void AllOutskirtsCreaturesMergesEveryCampaignWithoutDuplicates()
        {
            var all = Fixture().BuildTable(Campaign.AllOutskirtsCreatures);

            CollectionAssert.AreEquivalent(new[]
            {
                "YY_A01.2: 1 GreenLizard", "YY_A01.3: 2 PinkLizard", "YY_A02.4: 3 SmallCentipede",
                "YY_A02.5: 1 PoleMimic{12}", "lineage YY_A03.2", "OFFSCREEN.0: 3 Scavenger", "YY_A04.2: 2 CicadaB",
                "YY_A04.4: 1 BlueLizard", "lineage YY_A02.3"
            }, Describe(all));
            CollectionAssert.AreEqual(Enumerable.Range(0, all.Spawners.Count), all.Spawners.Select(s => s.SpawnerID));
            CollectionAssert.AreEqual(Campaign.All, all.Campaigns);
            Assert.AreSame(all.Spawners[3], all.GetSpawner(3));
        }

        [Test]
        public void BlankSettingMeansAllOutskirtsCreatures()
        {
            Assert.AreEqual(9, Fixture().BuildTable(null).Spawners.Count);
            Assert.AreEqual(9, Fixture().BuildTable(" ").Spawners.Count);
        }

        [Test]
        public void AllMergesOnlyTheGivenCampaigns()
        {
            var baseOnly = Fixture().BuildTable(Campaign.AllOutskirtsCreatures, new[] { Campaign.White, Campaign.Yellow });

            CollectionAssert.DoesNotContain(Describe(baseOnly), "lineage YY_A02.3");
            Assert.AreEqual(8, baseOnly.Spawners.Count);
        }

        [Test]
        public void DifferentLineagesInOneDenAcrossCampaignsKeepSeparateCounters()
        {
            var all = Inline("ROOMS", "YY_A01 : YY_A02", "END ROOMS", "CREATURES",
                "LINEAGE : YY_A01 : 3 : Green-0",
                "(White)LINEAGE : YY_A01 : 2 : Pink-0",
                "(Red)LINEAGE : YY_A01 : 2 : Red-0",
                "END CREATURES").BuildTable(Campaign.AllOutskirtsCreatures);

            CollectionAssert.AreEqual(new[] { "YY_A01.3", "YY_A01.2", "YY_A01.2;2" },
                all.Spawners.Cast<World.Lineage>().Select(l => l.denString));
        }

        [Test]
        public void ASharedCicadaLineIsResolvedOnceForEveryCampaign()
        {
            // Resolved per campaign, the line could come out CicadaA for one and CicadaB for another and
            // spawn twice in the merged table.
            var data = Inline("ROOMS", "YY_A01 : YY_A02", "END ROOMS", "CREATURES",
                "YY_A01 : 2-cicada-2", "LINEAGE : YY_A01 : 3 : cicada-0", "END CREATURES");
            for (int seed = 0; seed < 32; seed++)
            {
                var all = data.BuildTable(Campaign.AllOutskirtsCreatures, null, new System.Random(seed));
                Assert.AreEqual(2, all.Spawners.Count, "seed " + seed);
            }
        }

        [Test]
        public void RepeatedSpawnersWithinACampaignSurviveTheMerge()
        {
            // Two identical lineages in one den are two creatures with their own counters.
            var data = Inline("ROOMS", "YY_A01 : YY_A02", "END ROOMS", "CREATURES",
                "LINEAGE : YY_A01 : 2 : Pink-0.5, Red-0",
                "LINEAGE : YY_A01 : 2 : Pink-0.5, Red-0",
                "YY_A01 : 3-Green, 3-Green",
                "END CREATURES");

            var white = data.BuildTable("White");
            var whiteOnly = data.BuildTable(Campaign.AllOutskirtsCreatures, new[] { Campaign.White });
            var all = data.BuildTable(Campaign.AllOutskirtsCreatures);

            Assert.AreEqual(4, white.Spawners.Count);
            CollectionAssert.AreEqual(Describe(white), Describe(whiteOnly));
            CollectionAssert.AreEqual(Describe(white), Describe(all));
            CollectionAssert.AreEqual(new[] { "YY_A01.2", "YY_A01.2;2" },
                all.Spawners.OfType<World.Lineage>().Select(l => l.denString));
        }

        [Test]
        public void SwarmRoomsGetTheRegionsBatCount()
        {
            var white = Fixture().BuildTable("White");

            CollectionAssert.AreEqual(new[] { "YY_A02", "YY_A03", "YY_A04" }, white.SwarmRooms);
            CollectionAssert.AreEqual(new[] { 5, 5, 5 }, white.FliesPerSwarmRoom);
        }

        [Test]
        public void BatCountDefaultsToTheGamesWithoutProperties()
        {
            var table = new RegionSpawnData("yy", Fixtures.ReadLines("world/yy/world_yy.txt")).BuildTable("White");

            CollectionAssert.AreEqual(new[] { 10, 10, 10 }, table.FliesPerSwarmRoom);
        }

        [Test]
        public void OutskirtsThirdSwarmRoomFollowsTheCampaignsFoodMeter()
        {
            Assert.AreEqual(5, Fixture("su").BuildTable("White").FliesPerSwarmRoom[2]);
            Assert.AreEqual(7, Fixture("su").BuildTable("Red").FliesPerSwarmRoom[2]);
            Assert.AreEqual(5, Fixture("su").BuildTable(Campaign.AllOutskirtsCreatures).FliesPerSwarmRoom[2]);
            Assert.AreEqual(5, Fixture("yy").BuildTable("Red").FliesPerSwarmRoom[2]);
        }

        [Test]
        public void HiddenAndExclusiveRoomsAreNotSwarmRooms()
        {
            var data = Inline("CONDITIONAL LINKS", "Watcher : HIDEROOM : YY_A01", "Saint : EXCLUSIVEROOM : YY_A03", "END CONDITIONAL LINKS",
                "ROOMS", "YY_A01 : YY_A02 : SWARMROOM", "YY_A02 : YY_A01 : SWARMROOM", "YY_A03 : YY_A02 : SWARMROOM", "END ROOMS");

            CollectionAssert.AreEqual(new[] { "YY_A02" }, data.BuildTable("Watcher").SwarmRooms);
            CollectionAssert.AreEqual(new[] { "YY_A01", "YY_A02", "YY_A03" }, data.BuildTable("Saint").SwarmRooms);
            CollectionAssert.AreEqual(new[] { "YY_A01", "YY_A02" }, data.BuildTable("White").SwarmRooms);
            CollectionAssert.AreEqual(new[] { "YY_A01", "YY_A02", "YY_A03" }, data.BuildTable(Campaign.AllOutskirtsCreatures).SwarmRooms);
        }

        [Test]
        public void SpawnersOfAnUnknownCampaignAreTheUnfilteredOnes()
        {
            var table = Fixture().BuildTable("Nobody");

            Assert.IsEmpty(table.Campaigns);
            CollectionAssert.Contains(Describe(table), "YY_A04.4: 1 BlueLizard");
            Assert.AreEqual(6, table.Spawners.Count);
        }

        [Test]
        public void CampaignListCoversEveryTimelineWithItsDlc()
        {
            CollectionAssert.AreEqual(new[] { "White", "Yellow", "Red", "Gourmand", "Artificer", "Rivulet", "Spear", "Saint", "Inv", "Watcher" },
                Campaign.All.Select(c => c.Timeline));
            Assert.AreEqual(Campaign.Source.Base, Campaign.Find("Red").From);
            Assert.AreEqual(Campaign.Source.Downpour, Campaign.Find("Inv").From);
            Assert.AreEqual(Campaign.Source.Watcher, Campaign.Find("Watcher").From);
            Assert.IsNull(Campaign.Find("red"));
        }

        [Test]
        public void SpawnDataTypesSurviveTheTable()
        {
            var table = Fixture().BuildTable("White");
            var lineage = table.Spawners.OfType<World.Lineage>().Single();

            CollectionAssert.AreEqual(new Type?[] { null, Type.EggBug }, lineage.creatureTypes);
        }
    }
}
