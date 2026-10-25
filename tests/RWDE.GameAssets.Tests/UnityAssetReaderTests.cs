using System.IO;
using NUnit.Framework;
using RWDE.GameAssets.Cache;
using RWDE.GameAssets.Tests.Fixtures;
using RWDE.GameAssets.Unity;

namespace RWDE.GameAssets.Tests
{
    public class UnityAssetReaderTests
    {
        private FakeInstall _fake;

        [SetUp]
        public void SetUp() => _fake = new FakeInstall();

        [TearDown]
        public void TearDown() => _fake.Dispose();

        private string AssetsPath => Path.Combine(_fake.DataFolder, "resources.assets");

        [Test]
        public void ReadsObjectTable()
        {
            _fake.WriteAssets(new FakeAssetsFile()
                .AddTextAsset("a", "hello")
                .AddTexture("b", 2, 2, TextureDecoder.RGBA32, FakeInstall.Pixels2x2Rgba())
                .AddTextAsset("c", "world"));

            var file = SerializedFile.Open(AssetsPath);

            Assert.AreEqual(22, file.Version);
            Assert.AreEqual("2020.3.45f1", file.UnityVersion);
            Assert.AreEqual(3, file.Objects.Count);
            Assert.AreEqual(SerializedFile.ClassTextAsset, file.Objects[0].ClassId);
            Assert.AreEqual(SerializedFile.ClassTexture2D, file.Objects[1].ClassId);
            using (var stream = File.OpenRead(AssetsPath))
                Assert.AreEqual("c", file.ReadObjectName(stream, file.Objects[2]));
        }

        [Test]
        public void RejectsUnsupportedVersion()
        {
            new FakeAssetsFile().AddTextAsset("a", "x").Write(AssetsPath, version: 9);

            Assert.Throws<InvalidDataException>(() => SerializedFile.Open(AssetsPath));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ReadsTextureInlineOrStreamed(bool streamed)
        {
            _fake.WriteAssets(new FakeAssetsFile().AddTexture("tex", 2, 2, TextureDecoder.RGBA32, FakeInstall.Pixels2x2Rgba(), streamed));
            var file = SerializedFile.Open(AssetsPath);

            Texture2DData texture;
            using (var stream = File.OpenRead(AssetsPath))
            using (var reader = file.OpenObject(stream, file.Objects[0]))
                texture = Texture2DReader.Read(reader, _fake.DataFolder);

            Assert.AreEqual("tex", texture.Name);
            Assert.AreEqual(2, texture.Width);
            Assert.AreEqual(2, texture.Height);
            Assert.AreEqual(TextureDecoder.RGBA32, texture.Format);
            CollectionAssert.AreEqual(FakeInstall.Pixels2x2Rgba(), texture.TopMip);
        }

        [Test]
        public void ExtractsOnlyFutileAtlasPairs()
        {
            _fake.WriteAssets(new FakeAssetsFile()
                .AddTextAsset("atlasA", FakeInstall.AtlasJson)
                .AddTexture("atlasA", 2, 2, TextureDecoder.RGBA32, FakeInstall.Pixels2x2Rgba())
                .AddTextAsset("notAnAtlas", "just some text")
                .AddTexture("lonelyTexture", 2, 2, TextureDecoder.RGBA32, FakeInstall.Pixels2x2Rgba())
                .AddTextAsset("atlasWithoutTexture", FakeInstall.AtlasJson));
            var output = Path.Combine(_fake.CacheRoot, "atlases");

            var report = AtlasExtractor.Extract(AssetsPath, output);

            CollectionAssert.AreEqual(new[] { "atlasA" }, report.Extracted);
            Assert.AreEqual(1, report.Skipped.Count);
            StringAssert.Contains("atlasWithoutTexture", report.Skipped[0]);
            Assert.AreEqual(FakeInstall.AtlasJson, File.ReadAllText(Path.Combine(output, "atlasA.txt")));

            var rgba = TestPngReader.ReadRgba(File.ReadAllBytes(Path.Combine(output, "atlasA.png")), out var w, out var h);
            Assert.AreEqual(2, w);
            Assert.AreEqual(2, h);
            // Unity stores the bottom row first; the PNG has the top row (blue, white) first.
            CollectionAssert.AreEqual(new byte[] { 0, 0, 255, 255, 255, 255, 255, 128, 255, 0, 0, 255, 0, 255, 0, 255 }, rgba);
            Assert.IsFalse(File.Exists(Path.Combine(output, "lonelyTexture.png")));
        }
    }
}
