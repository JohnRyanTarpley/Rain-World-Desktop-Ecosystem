using System;
using NUnit.Framework;
using RWDE.Sim;

namespace RWDE.Graphics.Tests
{
    public class AtlasTests
    {
        private FAtlasManager _manager;

        [SetUp]
        public void SetUp()
        {
            _manager = TestAtlases.Install();
        }

        [Test]
        public void ElementsAreNamedWithoutTheFileExtension()
        {
            Assert.IsTrue(_manager.DoesContainElementWithName("square"));
            Assert.IsFalse(_manager.DoesContainElementWithName("square.png"));
            // Only the last extension goes, as Futile strips from the last dot.
            Assert.IsTrue(_manager.DoesContainElementWithName("Lizard.Head0"));
        }

        [Test]
        public void UvRectIsMeasuredFromTheBottom()
        {
            var dot = _manager.GetElementWithName("dot");

            // Frame (32, 0, 8, 8) from the top of a 64x32 texture.
            Assert.AreEqual(0.5f, dot.uvRect.x, 1e-6f);
            Assert.AreEqual(0.75f, dot.uvRect.y, 1e-6f);
            Assert.AreEqual(0.125f, dot.uvRect.width, 1e-6f);
            Assert.AreEqual(0.25f, dot.uvRect.height, 1e-6f);
            Assert.AreEqual(new Vector2(0.5f, 1f), dot.uvTopLeft);
            Assert.AreEqual(new Vector2(0.625f, 0.75f), dot.uvBottomRight);
        }

        [Test]
        public void TrimmedElementKeepsItsSourceSize()
        {
            var dot = _manager.GetElementWithName("dot");

            Assert.IsTrue(dot.isTrimmed);
            Assert.AreEqual(new Vector2(12f, 12f), dot.sourceSize);
            Assert.AreEqual(1f, dot.sourceRect.x);
            Assert.AreEqual(3f, dot.sourceRect.y);
            Assert.AreEqual(8f, dot.sourceRect.width);
        }

        [Test]
        public void ElementsKnowTheirAtlasAndFileOrder()
        {
            var atlas = _manager.GetAtlasWithName("testAtlas");

            Assert.AreSame(atlas, _manager.GetElementWithName("dot").atlas);
            Assert.AreEqual(1, _manager.GetElementWithName("dot").indexInAtlas);
            Assert.AreEqual(new[] { "square", "dot", "Lizard.Head0" }, new[] { atlas.elements[0].name, atlas.elements[1].name, atlas.elements[2].name });
            Assert.AreNotEqual(atlas.index, _manager.GetAtlasWithName("otherAtlas").index);
        }

        [Test]
        public void SingleImageAtlasIsOneElementCoveringTheTexture()
        {
            var white = _manager.GetElementWithName(Futile.WhiteElementName);

            Assert.AreEqual(new Vector2(16f, 16f), white.sourceSize);
            Assert.AreEqual(new Vector2(0f, 1f), white.uvTopLeft);
            Assert.AreEqual(new Vector2(1f, 0f), white.uvBottomRight);
        }

        [Test]
        public void UnknownElementThrowsWithASuggestion()
        {
            _manager.throwOnUnknownElement = true;

            var e = Assert.Throws<FutileException>(() => _manager.GetElementWithName("Atlases/squ.png"));
            StringAssert.Contains("'Atlases/squ.png'", e.Message);
            StringAssert.Contains("Did you mean 'square'", e.Message);

            e = Assert.Throws<FutileException>(() => _manager.GetElementWithName("nothingLikeIt"));
            StringAssert.DoesNotContain("Did you mean", e.Message);
        }

        [Test]
        public void UnknownElementsFailLoudlyInDevelopmentBuilds()
        {
#if DEBUG
            Assert.IsTrue(FAtlasManager.DevelopmentBuild);
#else
            Assert.IsFalse(FAtlasManager.DevelopmentBuild);
#endif
            Assert.AreEqual(FAtlasManager.DevelopmentBuild, new FAtlasManager().throwOnUnknownElement);
        }

        [Test]
        public void ReleaseBuildsReportAnUnknownElementOnceAndDrawWhite()
        {
            _manager.throwOnUnknownElement = false;
            var reports = new System.Collections.Generic.List<string>();
            _manager.UnknownElement += reports.Add;

            var first = _manager.GetElementWithName("squ");
            var again = new FSprite("squ").element;

            Assert.AreSame(_manager.GetElementWithName(Futile.WhiteElementName), first);
            Assert.AreSame(first, again);
            Assert.AreEqual(1, reports.Count);
            StringAssert.Contains("Did you mean 'square'", reports[0]);
        }

        [Test]
        public void ReleaseBuildsStillThrowWithoutTheWhiteElement()
        {
            var manager = new FAtlasManager { throwOnUnknownElement = false };
            manager.LoadAtlasFromJson("testAtlas", TestAtlases.Json, null, 64, 32);

            Assert.Throws<FutileException>(() => manager.GetElementWithName("nothingLikeIt"));
        }

        [Test]
        public void ReloadingAnAtlasByNameReturnsTheLoadedOne()
        {
            var again = _manager.LoadAtlasFromJson("testAtlas", TestAtlases.Json, null, 64, 32);

            Assert.AreSame(_manager.GetAtlasWithName("testAtlas"), again);
        }

        [Test]
        public void DuplicateElementNamesAcrossAtlasesThrow()
        {
            Assert.Throws<FutileException>(() => _manager.LoadAtlasFromJson("copy", TestAtlases.Json, null, 64, 32));
            Assert.IsFalse(_manager.DoesContainAtlas("copy"));
        }

        [Test]
        public void UnloadRemovesTheElements()
        {
            _manager.UnloadAtlas("otherAtlas");

            Assert.IsFalse(_manager.DoesContainAtlas("otherAtlas"));
            Assert.IsFalse(_manager.DoesContainElementWithName("other"));
            Assert.IsTrue(_manager.DoesContainElementWithName("square"));
        }

        [Test]
        public void RotatedFramesAreRejected()
        {
            var json = TestAtlases.OtherJson.Replace("\"rotated\": false", "\"rotated\": true");

            Assert.Throws<NotSupportedException>(() => FAtlas.FromTexturePackerJson("rotated", json, null, 4, 4));
        }

        [Test]
        public void MalformedJsonThrowsFutileException()
        {
            Assert.Throws<FutileException>(() => FAtlas.FromTexturePackerJson("bad", "{\"frames\": {", null, 4, 4));
            Assert.Throws<FutileException>(() => FAtlas.FromTexturePackerJson("bad", "{\"meta\": {}}", null, 4, 4));
        }
    }
}
