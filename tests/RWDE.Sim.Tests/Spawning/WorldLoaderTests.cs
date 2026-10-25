using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RWDE.Tests.Common;
using Type = RWDE.Sim.CreatureTemplate.Type;

namespace RWDE.Sim.Tests.Spawning
{
    // Expected values follow WorldLoader.Preprocessing, AddSpawnersFromString, AddLineageFromString and
    // CreatureTypeFromString in the decompiled game. All world data is invented (region "yy").
    public class WorldLoaderTests
    {
        private static string Pre(string line, string timeline) => WorldLoader.Preprocessing.PreprocessLine(line, timeline);

        private static List<World.CreatureSpawner> Parse(params string[] lines)
        {
            var spawners = new List<World.CreatureSpawner>();
            foreach (var line in lines)
                Assert.IsTrue(WorldLoader.AddSpawnersFromLine(line, r => r.StartsWith("YY_"), spawners, null), line);
            return spawners;
        }

        [Test]
        public void PreprocessingDropsCommentsAndShortLines()
        {
            Assert.IsNull(Pre("// comment", "White"));
            Assert.IsNull(Pre("x", "White"));
            Assert.AreEqual("YY_A01 : 2-Green", Pre("YY_A01 : 2-Green", "White"));
        }

        [Test]
        public void CampaignFilterKeepsListedCampaignsOnly()
        {
            Assert.AreEqual("YY_A01 : 2-Green", Pre("(White,Red)YY_A01 : 2-Green", "Red"));
            Assert.IsNull(Pre("(White,Red)YY_A01 : 2-Green", "Yellow"));
            Assert.IsNull(Pre("(White)YY_A01 : 2-Green", null));
        }

        [Test]
        public void ExcludeFilterKeepsEveryOtherCampaign()
        {
            Assert.IsNull(Pre("(X-Saint,Inv)YY_A01 : 2-Green", "Inv"));
            Assert.AreEqual("YY_A01 : 2-Green", Pre("(X-Saint,Inv)YY_A01 : 2-Green", "Gourmand"));
            Assert.AreEqual("YY_A01 : 2-Green", Pre("(X-Saint)YY_A01 : 2-Green", null));
        }

        [Test]
        public void NumericFilterEntriesAreOldPlayerNumbers()
        {
            Assert.IsNotNull(Pre("(2)YY_A01 : 2-Green", "Red"));
            Assert.IsNotNull(Pre("(3)YY_A01 : 2-Green", "White"));
            Assert.IsNull(Pre("(0)YY_A01 : 2-Green", "Red"));
        }

        [Test]
        public void EndingConditionsAreFalseWithoutASave()
        {
            Assert.IsNull(Pre("{PostBathScene}YY_A01 : 2-Green", "Watcher"));
            Assert.AreEqual("YY_A01 : 2-Green", Pre("{!PostBathScene}YY_A01 : 2-Green", "Watcher"));
            Assert.AreEqual("YY_A01 : 2-Green", Pre("{PrinceEncounters:2}YY_A01 : 2-Green", "Watcher"));
            Assert.AreEqual("YY_A01 : 2-Green", Pre("(Watcher){!WeaverEnding}YY_A01 : 2-Green", "Watcher"));
        }

        [Test]
        public void CreatureNamesResolveLikeTheGame()
        {
            Assert.AreEqual(Type.GreenLizard, WorldLoader.CreatureTypeFromString("Green"));
            Assert.AreEqual(Type.PoleMimic, WorldLoader.CreatureTypeFromString("Mimic"));
            Assert.AreEqual(Type.SpitLizard, WorldLoader.CreatureTypeFromString("Caramel"));
            Assert.AreEqual(Type.EelLizard, WorldLoader.CreatureTypeFromString("Eel"));
            Assert.AreEqual(Type.MirosVulture, WorldLoader.CreatureTypeFromString("MirosVulture"));
            Assert.AreEqual(Type.DropBug, WorldLoader.CreatureTypeFromString("Dropwig"));
            // Default branch: template names and type names, case-sensitive.
            Assert.AreEqual(Type.SmallCentipede, WorldLoader.CreatureTypeFromString("Small Centipede"));
            Assert.AreEqual(Type.Scavenger, WorldLoader.CreatureTypeFromString("Scavenger"));
            Assert.AreEqual(Type.Salamander, WorldLoader.CreatureTypeFromString("Salamander"));
            Assert.AreEqual(Type.EggBug, WorldLoader.CreatureTypeFromString("EggBug"));
            Assert.IsNull(WorldLoader.CreatureTypeFromString("scavenger"));
            // Real game creatures outside the roster, and nonsense.
            Assert.IsNull(WorldLoader.CreatureTypeFromString("Leech"));
            Assert.IsNull(WorldLoader.CreatureTypeFromString("Wibble"));
        }

        [Test]
        public void CicadaPicksAOrBFromTheGivenRandom()
        {
            Assert.IsNull(WorldLoader.CreatureTypeFromString("cicada"));
            var random = new Random(4);
            var picks = Enumerable.Range(0, 50).Select(_ => WorldLoader.CreatureTypeFromString("cicada", random)).ToList();
            CollectionAssert.Contains(picks, Type.CicadaA);
            CollectionAssert.Contains(picks, Type.CicadaB);
        }

        [Test]
        public void SimpleSpawnersReadDenTypeAmountAndData()
        {
            var spawners = Parse("YY_A01 : 2-Green, 3-Pink-2, 4-Salamander-2-{PreCycle}, 5-Mimic-{12}, 6-Centipede-{0.3-x}-3")
                .Cast<World.SimpleSpawner>().ToList();

            Assert.AreEqual(5, spawners.Count);
            Assert.AreEqual(new World.Den("YY_A01", 2), spawners[0].den);
            Assert.AreEqual(Type.GreenLizard, spawners[0].creatureType);
            Assert.AreEqual(1, spawners[0].amount);
            Assert.IsNull(spawners[0].spawnDataString);
            Assert.AreEqual(2, spawners[1].amount);
            Assert.AreEqual(3, spawners[1].den.abstractNode);
            Assert.AreEqual(2, spawners[2].amount);
            Assert.AreEqual("{PreCycle}", spawners[2].spawnDataString);
            Assert.AreEqual("{12}", spawners[3].spawnDataString);
            // A dash inside the braces belongs to the data; the amount after it still counts.
            Assert.AreEqual("{0.3-x}", spawners[4].spawnDataString);
            Assert.AreEqual(3, spawners[4].amount);
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4 }, spawners.Select(s => s.SpawnerID));
        }

        [Test]
        public void OffscreenSpawnersNeedNoRoom()
        {
            var spawners = Parse("OFFSCREEN : 0-Scavenger-5, 0-MirosVulture-2-{Night}").Cast<World.SimpleSpawner>().ToList();

            Assert.IsTrue(spawners.All(s => s.den.Offscreen));
            Assert.AreEqual(5, spawners[0].amount);
            Assert.AreEqual(2, spawners[1].amount);
            Assert.AreEqual("{Night}", spawners[1].spawnDataString);
        }

        [Test]
        public void SpawnsInUnknownRoomsAndOfUnknownCreaturesAreLeftOut()
        {
            var spawners = new List<World.CreatureSpawner>();
            var unknown = new List<string>();
            WorldLoader.AddSpawnersFromLine("ZZ_A01 : 2-Green", r => r.StartsWith("YY_"), spawners, unknown);
            WorldLoader.AddSpawnersFromLine("LINEAGE : ZZ_A01 : 2 : Green-0", r => r.StartsWith("YY_"), spawners, unknown);
            WorldLoader.AddSpawnersFromLine("YY_A01 : 2-Leech, 3-Pink", r => r.StartsWith("YY_"), spawners, unknown);

            Assert.AreEqual(1, spawners.Count);
            Assert.AreEqual(Type.PinkLizard, ((World.SimpleSpawner)spawners[0]).creatureType);
            CollectionAssert.AreEqual(new[] { "Leech" }, unknown);
        }

        [Test]
        public void UnreadableLinesAreReported()
        {
            var spawners = new List<World.CreatureSpawner>();
            Assert.IsFalse(WorldLoader.AddSpawnersFromLine("YY_A01 : x-Green", r => true, spawners, null));
            Assert.IsFalse(WorldLoader.AddSpawnersFromLine("YY_A01", r => true, spawners, null));
            Assert.IsFalse(WorldLoader.AddSpawnersFromLine("LINEAGE : YY_A01 : 2 : Green-", r => true, spawners, null));
        }

        [Test]
        public void LineagesReadTypesChancesAndData()
        {
            var lineage = (World.Lineage)Parse("LINEAGE : YY_A31 : 3 : NONE-0.1, Small Centipede-1.0, Centipede-{0.3}-1.0, Centipede-0")[0];

            Assert.AreEqual(new World.Den("YY_A31", 3), lineage.den);
            CollectionAssert.AreEqual(new Type?[] { null, Type.SmallCentipede, Type.Centipede, Type.Centipede }, lineage.creatureTypes);
            CollectionAssert.AreEqual(new[] { 0.1f, 1f, 1f, 0f }, lineage.progressionChances);
            CollectionAssert.AreEqual(new[] { null, null, "{0.3}", null }, lineage.spawnData);
            Assert.AreEqual("YY_A31.3", lineage.denString);
            Assert.IsFalse(lineage.nightCreature);
        }

        [Test]
        public void SecondLineageInADenGetsAConflictNumber()
        {
            var spawners = Parse("LINEAGE : YY_A06 : 4 : Red-1", "LINEAGE : YY_A06 : 4 : Blue-0", "LINEAGE : YY_A06 : 3 : Pink-0");

            Assert.AreEqual("YY_A06.4", ((World.Lineage)spawners[0]).denString);
            Assert.AreEqual("YY_A06.4;2", ((World.Lineage)spawners[1]).denString);
            Assert.AreEqual("YY_A06.3", ((World.Lineage)spawners[2]).denString);
        }

        [Test]
        public void LineageProgressesByChanceAndStopsAtTheEnd()
        {
            var lineage = (World.Lineage)Parse("LINEAGE : YY_A01 : 2 : NONE-1, Pink-0, Red-1")[0];
            var random = new Random(1);

            Assert.IsNull(lineage.CurrentType(0));
            Assert.AreEqual(1, lineage.ChanceToProgress(0, random));
            Assert.AreEqual(Type.PinkLizard, lineage.CurrentType(1));
            Assert.AreEqual(1, lineage.ChanceToProgress(1, random));
            // The last step never progresses, whatever its chance.
            Assert.AreEqual(2, lineage.ChanceToProgress(2, random));
            Assert.IsNull(lineage.CurrentType(3));
        }

        [Test]
        public void FixtureBaseFileParsesForEveryCampaign()
        {
            var data = new RegionSpawnData("yy", Fixtures.ReadLines("world/yy/world_yy.txt"), Fixtures.ReadLines("world/yy/properties.txt"));
            foreach (var campaign in Campaign.All)
                Assert.IsEmpty(data.BuildTable(campaign.Timeline).UnreadableLines, campaign.Timeline);
        }
    }
}
