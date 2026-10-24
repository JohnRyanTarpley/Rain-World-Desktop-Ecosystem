using System.IO;
using NUnit.Framework;
using RWDE.GameAssets.Install;
using RWDE.GameAssets.Tests.Fixtures;

namespace RWDE.GameAssets.Tests
{
    public class VdfParserTests
    {
        [Test]
        public void ParsesNestedBlocksAndEscapedPaths()
        {
            var root = VdfParser.Parse(
                "\"libraryfolders\"\n{\n\t\"1\"\n\t{\n\t\t\"path\"\t\t\"D:\\\\Big Games\\\\Steam\"\n" +
                "\t\t\"apps\"\n\t\t{\n\t\t\t\"312520\"\t\t\"1234\"\n\t\t}\n\t}\n}\n");

            var library = root["libraryfolders"]["1"];
            Assert.AreEqual(@"D:\Big Games\Steam", library["path"].Value);
            Assert.AreEqual("1234", library["apps"]["312520"].Value);
        }

        [Test]
        public void KeysAreCaseInsensitiveAndCommentsIgnored()
        {
            var root = VdfParser.Parse("// header\n\"LibraryFolders\" { \"ContentStatsID\" \"5\" }");
            Assert.AreEqual("5", root["libraryfolders"]["contentstatsid"].Value);
        }

        [Test]
        public void RejectsUnbalancedBraces()
        {
            Assert.Throws<InvalidDataException>(() => VdfParser.Parse("\"a\" { \"b\" \"c\""));
        }
    }

    public class InstallLocatorTests
    {
        private FakeInstall _fake;

        [SetUp]
        public void SetUp() => _fake = new FakeInstall();

        [TearDown]
        public void TearDown() => _fake.Dispose();

        [Test]
        public void FindsInstallInSecondaryLibrary()
        {
            var lookup = RainWorldInstallLocator.Locate(null, new[] { _fake.SteamRoot });

            Assert.IsTrue(lookup.Found, lookup.Failure);
            Assert.AreEqual(InstallSource.SteamLibrary, lookup.Install.Source);
            Assert.AreEqual(_fake.InstallRoot, lookup.Install.RootPath);
            Assert.AreEqual("v9.9.9", lookup.Install.GameVersion);
        }

        [Test]
        public void LibraryListingRainWorldIsSearchedFirst()
        {
            var libraries = RainWorldInstallLocator.FindLibraries(new[] { _fake.SteamRoot });

            Assert.AreEqual(_fake.LibraryRoot, libraries[0]);
            CollectionAssert.Contains(libraries, _fake.SteamRoot);
        }

        [Test]
        public void StillFindsInstallWhenLibraryDoesNotListAppId()
        {
            _fake.WriteLibraryFolders(_fake.LibraryRoot, listsRainWorld: false);

            Assert.IsTrue(RainWorldInstallLocator.Locate(null, new[] { _fake.SteamRoot }).Found);
        }

        [Test]
        public void SettingsOverrideWinsAndAcceptsDataFolder()
        {
            var lookup = RainWorldInstallLocator.Locate(_fake.DataFolder, new string[0]);

            Assert.IsTrue(lookup.Found, lookup.Failure);
            Assert.AreEqual(InstallSource.SettingsOverride, lookup.Install.Source);
            Assert.AreEqual(_fake.InstallRoot, lookup.Install.RootPath);
        }

        [Test]
        public void InvalidOverrideIsReportedWithoutFallingBackToSteam()
        {
            var lookup = RainWorldInstallLocator.Locate(Path.Combine(_fake.RootFolder, "nowhere"), new[] { _fake.SteamRoot });

            Assert.IsFalse(lookup.Found);
            StringAssert.Contains("Settings", lookup.Failure);
        }

        [Test]
        public void NoSteamLibrariesGivesNotFound()
        {
            var lookup = RainWorldInstallLocator.Locate("", new[] { Path.Combine(_fake.RootFolder, "NoSteam") });

            Assert.IsFalse(lookup.Found);
            Assert.IsNull(lookup.Install);
            Assert.IsNotNull(lookup.Failure);
        }
    }
}
