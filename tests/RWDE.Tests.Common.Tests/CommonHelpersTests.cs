using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace RWDE.Tests.Common.Tests
{
    public class FixturesTests
    {
        [Test]
        public void WorldFileHasClosedSections()
        {
            var lines = Fixtures.ReadLines("world/xx/world_xx.txt");
            foreach (var section in new[] { "ROOMS", "CREATURES", "BAT MIGRATION BLOCKAGES" })
            {
                int start = System.Array.IndexOf(lines, section);
                int end = System.Array.IndexOf(lines, "END " + section);
                Assert.That(start, Is.GreaterThanOrEqualTo(0), section);
                Assert.That(end, Is.GreaterThan(start), "END " + section);
            }
        }

        [Test]
        public void ModifyFileMergeBlockIsClosed()
        {
            var text = Fixtures.ReadText("modify/world/xx/world_xx.txt");
            Assert.That(text.IndexOf("[ENDMERGE]"), Is.GreaterThan(text.IndexOf("[MERGE]")));
        }

        [Test]
        public void AtlasFramesFitInsideTheTexture()
        {
            var json = Fixtures.ReadText("atlases/fixtureAtlas.txt");
            var frames = System.Text.RegularExpressions.Regex.Matches(json,
                "\"frame\": \\{\"x\":(\\d+),\"y\":(\\d+),\"w\":(\\d+),\"h\":(\\d+)\\}");
            Assert.That(frames.Count, Is.EqualTo(3));
            foreach (System.Text.RegularExpressions.Match f in frames)
            {
                int x = int.Parse(f.Groups[1].Value), y = int.Parse(f.Groups[2].Value);
                int w = int.Parse(f.Groups[3].Value), h = int.Parse(f.Groups[4].Value);
                Assert.That(x + w, Is.LessThanOrEqualTo(64));
                Assert.That(y + h, Is.LessThanOrEqualTo(32));
            }
        }

        [Test]
        public void MissingFixtureThrows()
        {
            Assert.Throws<FileNotFoundException>(() => Fixtures.PathOf("does/not/exist.txt"));
        }
    }

    public class TempDirectoryTests
    {
        [Test]
        public void IsDeletedOnDispose()
        {
            string path;
            using (var temp = new TempDirectory())
            {
                path = temp.Path;
                File.WriteAllText(temp.Combine("a.txt"), "x");
                Assert.That(Directory.Exists(path), Is.True);
            }
            Assert.That(Directory.Exists(path), Is.False);
        }
    }

    public class GameFormatWritersTests
    {
        [Test]
        public void PalettePngHasPaletteSize()
        {
            using (var temp = new TempDirectory())
            {
                var path = temp.Combine("palette0.png");
                GameFormatWriters.WritePalettePng(path, 0);
                var bytes = File.ReadAllBytes(path);

                CollectionAssert.AreEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, bytes.Take(8).ToArray());
                Assert.That(Encoding.ASCII.GetString(bytes, 12, 4), Is.EqualTo("IHDR"));
                Assert.That(BigEndian(bytes, 16), Is.EqualTo(GameFormatWriters.PaletteWidth));
                Assert.That(BigEndian(bytes, 20), Is.EqualTo(GameFormatWriters.PaletteHeight));
            }
        }

        [Test]
        public void PngChunksHaveValidCrcs()
        {
            using (var temp = new TempDirectory())
            {
                var path = temp.Combine("solid.png");
                GameFormatWriters.WriteSolidPng(path, 4, 2, 10, 20, 30);
                var bytes = File.ReadAllBytes(path);
                var chunks = new List<string>();
                for (int i = 8; i < bytes.Length;)
                {
                    int length = BigEndian(bytes, i);
                    var type = Encoding.ASCII.GetString(bytes, i + 4, 4);
                    chunks.Add(type);
                    Assert.That((uint)BigEndian(bytes, i + 8 + length), Is.EqualTo(Crc32(bytes, i + 4, length + 4)), type);
                    i += 12 + length;
                }
                CollectionAssert.AreEqual(new[] { "IHDR", "IDAT", "IEND" }, chunks);
            }
        }

        [Test]
        public void WavHasPcmHeader()
        {
            using (var temp = new TempDirectory())
            {
                var path = temp.Combine("tone.wav");
                GameFormatWriters.WriteWav(path, 22050, 100);
                var bytes = File.ReadAllBytes(path);
                Assert.That(Encoding.ASCII.GetString(bytes, 0, 4), Is.EqualTo("RIFF"));
                Assert.That(Encoding.ASCII.GetString(bytes, 8, 4), Is.EqualTo("WAVE"));
                Assert.That(bytes.Length, Is.EqualTo(44 + 200));
            }
        }

        static int BigEndian(byte[] b, int i) => (b[i] << 24) | (b[i + 1] << 16) | (b[i + 2] << 8) | b[i + 3];

        static uint Crc32(byte[] data, int offset, int count)
        {
            uint crc = 0xFFFFFFFF;
            for (int i = offset; i < offset + count; i++)
            {
                crc ^= data[i];
                for (int k = 0; k < 8; k++)
                    crc = (crc & 1) != 0 ? 0xEDB88320 ^ (crc >> 1) : crc >> 1;
            }
            return crc ^ 0xFFFFFFFF;
        }
    }

    public class FakeGameInstallTests
    {
        [Test]
        public void BuildsTheInstallLayout()
        {
            using (var install = FakeGameInstall.Create())
            {
                Assert.That(File.Exists(install.LibraryFoldersVdf), Is.True);
                Assert.That(File.Exists(Path.Combine(install.LibraryRoot, "steamapps", "appmanifest_312520.acf")), Is.True);
                foreach (var asset in new[]
                {
                    "world/xx/world_xx.txt",
                    "mods/moreslugcats/modify/world/xx/world_xx.txt",
                    "palettes/palette0.png",
                    "palettes/palette-1.png",
                    "mods/moreslugcats/atlases/fixtureAtlas.txt",
                    "mods/moreslugcats/atlases/fixtureAtlas.png",
                    "soundeffects/fixture_chirp.wav",
                    "loadedsoundeffects/fixture_hum.wav",
                })
                    Assert.That(File.Exists(install.StreamingAssetPath(asset)), Is.True, asset);
            }
        }

        [Test]
        public void LibraryFoldersPointsAtTheGameLibrary()
        {
            using (var install = FakeGameInstall.Create())
            {
                var vdf = File.ReadAllText(install.LibraryFoldersVdf);
                Assert.That(vdf, Does.Not.Contain("{LIBRARY_ROOT}"));
                var escaped = install.LibraryRoot.Replace("\\", "\\\\");
                Assert.That(vdf, Does.Contain("\"path\"\t\t\"" + escaped + "\""));
                Assert.That(vdf.IndexOf(FakeGameInstall.SteamAppId), Is.GreaterThan(vdf.IndexOf(escaped)));
            }
        }

        [Test]
        public void DisposeDeletesEverything()
        {
            string root;
            using (var install = FakeGameInstall.Create())
                root = install.Root;
            Assert.That(Directory.Exists(root), Is.False);
        }
    }

    public class ProjectRulesTests
    {
        static ProjectRules.Project Make(string name, params string[] refs) =>
            new ProjectRules.Project { Name = name, References = refs };

        [Test]
        public void EverySourceProjectIsADepartment()
        {
            var names = ProjectRules.LoadAll().Select(p => p.Name).ToList();
            CollectionAssert.AreEquivalent(ProjectRules.Allowed.Keys, names);
        }

        [Test]
        public void ForbiddenReferenceIsReported()
        {
            var projects = new List<ProjectRules.Project>
            {
                Make(ProjectRules.Sim, ProjectRules.Shell),
                Make(ProjectRules.Shell, ProjectRules.Sim),
            };
            var errors = ProjectRules.Violations(ProjectRules.Sim, projects);
            Assert.That(errors, Has.Some.Contains("must not reference RWDE.Shell"));
            Assert.That(errors, Has.Some.Contains("dependency cycle"));
        }

        [Test]
        public void SimMustStayPortable()
        {
            var sim = new ProjectRules.Project
            {
                Name = ProjectRules.Sim,
                TargetFramework = "net8.0-windows",
                Packages = new[] { "Vortice.Direct3D11" },
            };
            var errors = ProjectRules.Violations(ProjectRules.Sim, new List<ProjectRules.Project> { sim });
            Assert.That(errors, Has.Some.Contains("plain net8.0"));
            Assert.That(errors, Has.Some.Contains("must not use NuGet packages"));
        }

        [Test]
        public void AllowedReferencesPass()
        {
            var graphics = Make(ProjectRules.Graphics, ProjectRules.Sim, ProjectRules.GameAssets);
            graphics.TargetFramework = "net8.0-windows";
            graphics.Packages = new[] { "Vortice.Direct3D11" };
            var projects = new List<ProjectRules.Project>
            {
                graphics,
                Make(ProjectRules.Sim),
                Make(ProjectRules.GameAssets),
            };
            Assert.That(ProjectRules.Violations(ProjectRules.Graphics, projects), Is.Empty);
        }
    }
}
