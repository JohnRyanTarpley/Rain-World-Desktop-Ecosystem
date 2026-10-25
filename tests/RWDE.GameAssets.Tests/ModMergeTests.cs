using System.Linq;
using NUnit.Framework;
using RWDE.GameAssets.Merge;

namespace RWDE.GameAssets.Tests
{
    // All world data here is invented; it only follows the game's file format.
    public class ModMergeTests
    {
        private static string[] Lines(params string[] lines) => lines;

        private static string[] Modify(string[] baseLines, params string[] modifyLines) =>
            new ModifyFile("testmod", modifyLines).ApplyModifications(baseLines);

        [Test]
        public void AddAppendsAndAddAtLineInserts()
        {
            var result = Modify(Lines("one", "two"), "[ADD]three", "[ADD_1]between");

            CollectionAssert.AreEqual(Lines("one", "between", "two", "three"), result);
        }

        [Test]
        public void FindReplaceSubstitutesSubstringsEverywhere()
        {
            var result = Modify(Lines("XX_A01 : XX_A02", "XX_A02 : XX_A01"), "[FIND]XX_A02", "[REPLACE]XX_B09");

            CollectionAssert.AreEqual(Lines("XX_A01 : XX_B09", "XX_B09 : XX_A01"), result);
        }

        [Test]
        public void FindLineStartReplacesSecondOccurrenceAndDropsEmptyLines()
        {
            var result = Modify(Lines("XX_A01 : room link", "XX_A01 : 2-Fly", "XX_A01 : 3-Fly"),
                "[FINDLINESTART]XX_A01 :", "[REPLACE_2]");

            CollectionAssert.AreEqual(Lines("XX_A01 : room link", "XX_A01 : 3-Fly"), result);
        }

        [Test]
        public void TargetLineAndAddBeforeAfter()
        {
            var result = Modify(Lines("a", "b", "c"),
                "[TARGETLINE]2", "[ADDBEFORE]<", "[FINDLINE]c", "[ADDAFTER]>");

            CollectionAssert.AreEqual(Lines("a", "<b", "c>"), result);
        }

        [Test]
        public void EscapedNewlineSplitsIntoTwoLines()
        {
            var result = Modify(Lines("a"), "[ADD]b\\nc");

            CollectionAssert.AreEqual(Lines("a", "b", "c"), result);
        }

        [Test]
        public void WorldMergeAddsRoomsSpawnsAndConditionalLinks()
        {
            var baseWorld = Lines(
                "ROOMS",
                "XX_A01 : XX_A02, DISCONNECTED",
                "XX_A02 : XX_A01 : SWARMROOM",
                "END ROOMS",
                "CREATURES",
                "XX_A01 : 2-Green",
                "LINEAGE : XX_A02 : 3 : Pink-0.5, Red-0",
                "END CREATURES");
            var merge = new ModifyFile("testmod", Lines(
                "[MERGE]",
                "CONDITIONAL LINKS",
                "Alpha : HIDEROOM : XX_A02",
                "END CONDITIONAL LINKS",
                "ROOMS",
                "XX_A01 : XX_A02, XX_A03",
                "XX_A03 : XX_A01",
                "END ROOMS",
                "CREATURES",
                "(X-Alpha)XX_A01 : 2-Green",
                "(Alpha)XX_A01 : 2-Yellow-3",
                "END CREATURES",
                "[ENDMERGE]"));

            var result = ModMerge.Apply("xx/world_xx.txt", baseWorld, new[] { merge });

            CollectionAssert.AreEqual(Lines(
                "CONDITIONAL LINKS",
                "Alpha : HIDEROOM : XX_A02",
                "END CONDITIONAL LINKS",
                "",
                "ROOMS",
                "XX_A01 : XX_A02, XX_A03",
                "XX_A02 : XX_A01 : SWARMROOM",
                "XX_A03 : XX_A01",
                "END ROOMS",
                "",
                "CREATURES",
                "LINEAGE : XX_A02 : 3 : Pink-0.5, Red-0",
                "(X-Alpha)XX_A01 : 2-Green",
                "(Alpha)XX_A01 : 2-Yellow-3",
                "END CREATURES",
                "",
                "BAT MIGRATION BLOCKAGES",
                "END BAT MIGRATION BLOCKAGES"), result.Lines);
            Assert.AreEqual(0, result.Warnings.Count);
        }

        [Test]
        public void WorldMergeSplitsMultiCharacterFiltersAndKeepsAttributes()
        {
            var merge = new ModifyFile("testmod", Lines(
                "[MERGE]CREATURES",
                "(Alpha,Beta)XX_A05 : 1-Snail-{Mean:0.5,Seed:3}-2",
                "END CREATURES[ENDMERGE]"));

            var result = ModMerge.Apply("xx/world_xx.txt", Lines("ROOMS", "END ROOMS"), new[] { merge });

            CollectionAssert.IsSubsetOf(Lines("(Alpha)XX_A05 : 1-Snail-{Mean:0.5,Seed:3}-2", "(Beta)XX_A05 : 1-Snail-{Mean:0.5,Seed:3}-2"), result.Lines);
        }

        [Test]
        public void MergesRunBeforeLineEditsAcrossMods()
        {
            var first = new ModifyFile("first", Lines("[FIND]XX_A09", "[REPLACE]XX_Z09"));
            var second = new ModifyFile("second", Lines("[MERGE]", "ROOMS", "XX_A09 : DISCONNECTED", "END ROOMS", "[ENDMERGE]"));

            var result = ModMerge.Apply("xx/world_xx.txt", Lines("ROOMS", "END ROOMS"), new[] { first, second });

            CollectionAssert.Contains(result.Lines, "XX_Z09 : DISCONNECTED");
        }

        [Test]
        public void PropertiesMergeKeepsKeysUniqueAndCollectsSubregions()
        {
            var merge = new ModifyFile("testmod", Lines(
                "[MERGE]",
                "Palette: 7",
                "Room_Attr: XX_A01: Fly-Like",
                "Subregion: Upper",
                "[ENDMERGE]"));

            var result = ModMerge.Apply("xx/properties.txt",
                Lines("Palette: 1", "Room_Attr: XX_A01: Fly-Avoid", "Room_Attr: XX_A02: Fly-Like", "Subregion: Lower"), new[] { merge });

            CollectionAssert.AreEqual(Lines(
                "Palette: 7",
                "Room_Attr: XX_A01: Fly-Like",
                "Room_Attr: XX_A02: Fly-Like",
                "Subregion: Lower",
                "Subregion: Upper"), result.Lines);
        }

        [Test]
        public void RoomSettingsMergeUnionsEffects()
        {
            var merge = new ModifyFile("testmod", Lines("[MERGE]", "Effects: Rain-0-0, Fog-1-1", "Palette: 3", "[ENDMERGE]"));

            var result = ModMerge.Apply("xx-rooms/xx_a01_settings.txt", Lines("Palette: 2", "Effects: Rain-0-0"), new[] { merge });

            Assert.AreEqual("Palette: 3", result.Lines[0]);
            Assert.AreEqual("Effects: Rain-0-0, Fog-1-1, ", result.Lines[1]);
        }

        [Test]
        public void UnportedMergesWarnAndKeepBase()
        {
            var merge = new ModifyFile("testmod", Lines(
                "[MERGE]", "Connection: A,B,1,2,3,4", "[ENDMERGE]",
                "[FILEDESTINATION]map_xx-other.txt", "Connection: C,D,1,2,3,4", "[ENDFILEDESTINATION]"));

            var result = ModMerge.Apply("xx/map_xx.txt", Lines("Connection: A,B,0,0,0,0"), new[] { merge });

            CollectionAssert.AreEqual(Lines("Connection: A,B,0,0,0,0"), result.Lines);
            Assert.AreEqual(2, result.Warnings.Count);
            Assert.IsTrue(result.Warnings.All(w => w.StartsWith("testmod")));
        }
    }
}
