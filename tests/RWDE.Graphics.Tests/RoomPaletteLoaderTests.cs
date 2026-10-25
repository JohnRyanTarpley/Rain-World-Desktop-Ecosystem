using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using RWDE.GameAssets;
using RWDE.Tests.Common;

namespace RWDE.Graphics.Tests
{
    public class RoomPaletteLoaderTests
    {
        private TempDirectory _temp;
        private Dictionary<string, FileAsset> _files;
        private RoomPaletteLoader _loader;

        [SetUp]
        public void SetUp()
        {
            _temp = new TempDirectory("rwde-palettes");
            _files = new Dictionary<string, FileAsset>(StringComparer.OrdinalIgnoreCase);
            AddPalette("palette0", 0);
            AddPalette("palette4", 4);
            AddPalette("palette-1", 99);
            AddEffectColors();
            _loader = new RoomPaletteLoader(name => _files.TryGetValue(name, out var file)
                ? AssetResult<FileAsset>.Of(file)
                : AssetResult<FileAsset>.NotFound($"No palette named '{name}'"));
        }

        [TearDown]
        public void TearDown() => _temp.Dispose();

        // GameFormatWriters.WritePalettePng: r = x * 8 + seed, g = pngRow * 16 + seed, b = seed * 37.
        private static Color PalettePixel(int x, int pngRow, int seed) =>
            new Color((byte)(x * 8 + seed) / 255f, (byte)(pngRow * 16 + seed) / 255f, (byte)(seed * 37) / 255f, 1f);

        [Test]
        public void ReadsTheTopRowOfTheUpperHalf()
        {
            _loader.ChangeMainPalette(4);
            var p = _loader.currentPalette;

            // Unity row 7 of the 32x8 palette texture is the png's top row.
            AssertColor(PalettePixel(0, 0, 4), p.skyColor);
            AssertColor(PalettePixel(1, 0, 4), p.fogColor);
            AssertColor(PalettePixel(2, 0, 4), p.blackColor);
            AssertColor(PalettePixel(13, 0, 4), p.shortCutSymbol);
            AssertColor(PalettePixel(11, 0, 4), p.shortcutColors[1]);
            Assert.AreEqual(1f - PalettePixel(9, 0, 4).r, p.fogAmount, 1e-6f);
            Assert.AreEqual(1f - PalettePixel(30, 0, 4).r, p.darkness, 1e-6f);
            Assert.AreSame(_loader.paletteTexture, p.texture);
            Assert.AreEqual(32, _loader.paletteTexture.width);
            Assert.AreEqual(8, _loader.paletteTexture.height);
        }

        [Test]
        public void DarkPaletteFadesTowardTheLowerHalf()
        {
            _loader.ChangeMainPalette(4);
            RoomPalette? changed = null;
            _loader.PaletteChanged += p => changed = p;

            _loader.darkPalette = 1f;

            Assert.IsTrue(changed.HasValue);
            AssertColor(PalettePixel(0, 8, 4), _loader.currentPalette.skyColor);
            _loader.darkPalette = 0.5f;
            AssertColor(Color.Lerp(PalettePixel(0, 0, 4), PalettePixel(0, 8, 4), 0.5f), _loader.currentPalette.skyColor);
        }

        [Test]
        public void FadePaletteBlendsTheTwoPalettes()
        {
            _loader.ChangeBothPalettes(0, 4, 0.25f);

            AssertColor(Color.Lerp(PalettePixel(2, 0, 0), PalettePixel(2, 0, 4), 0.25f), _loader.currentPalette.blackColor);
            _loader.paletteBlend = 1f;
            AssertColor(PalettePixel(2, 0, 4), _loader.currentPalette.blackColor);
        }

        [Test]
        public void MissingPaletteFallsBackToPaletteMinusOne()
        {
            _loader.ChangeMainPalette(17);

            Assert.AreEqual(17, _loader.paletteA);
            AssertColor(PalettePixel(2, 0, 99), _loader.currentPalette.blackColor);
        }

        [Test]
        public void EffectColorsGoIntoTheLastTwoColumns()
        {
            _loader.ApplyEffectColorsToAllPaletteTextures(3, 1);
            _loader.ChangeMainPalette(4);
            var t = _loader.paletteTexture;

            // Effect colour A: Unity rows 12-13 of the palette (upper half) from effectColors rows 2-3.
            AssertColor(EffectPixel(6, 2), t.GetPixel(30, 4));
            AssertColor(EffectPixel(7, 3), t.GetPixel(31, 5));
            // Effect colour B: rows 10-11.
            AssertColor(EffectPixel(2, 2), t.GetPixel(30, 2));
            AssertColor(EffectPixel(3, 3), t.GetPixel(31, 3));
            // Other columns keep the palette.
            AssertColor(PalettePixel(29, 3, 4), t.GetPixel(29, 4));

            // Dark half: rows 4-5 and 2-3 from effectColors rows 0-1.
            _loader.darkPalette = 1f;
            AssertColor(EffectPixel(6, 0), t.GetPixel(30, 4));
            AssertColor(EffectPixel(2, 1), t.GetPixel(30, 3));
        }

        [Test]
        public void EffectColorsApplyToAnAlreadyLoadedPalette()
        {
            _loader.ChangeMainPalette(4);
            _loader.ApplyEffectColorsToAllPaletteTextures(2, -1);

            AssertColor(EffectPixel(4, 2), _loader.paletteTexture.GetPixel(30, 4));
            AssertColor(PalettePixel(30, 5, 4), _loader.paletteTexture.GetPixel(30, 2));
        }

        [Test]
        public void WrongSizePaletteThrows()
        {
            var path = _temp.Combine("palette5.png");
            GameFormatWriters.WriteSolidPng(path, 16, 16, 1, 2, 3);
            _files["palette5"] = new FileAsset("palette5", path);

            Assert.Throws<InvalidDataException>(() => _loader.ChangeMainPalette(5));
        }

        [Test]
        public void NoPaletteAtAllThrows()
        {
            _files.Remove("palette-1");

            Assert.Throws<FileNotFoundException>(() => _loader.ChangeMainPalette(17));
        }

        private void AddPalette(string name, int seed)
        {
            var path = _temp.Combine(name + ".png");
            GameFormatWriters.WritePalettePng(path, seed);
            _files[name] = new FileAsset(name, path);
        }

        // effectColors.png: 40x4, r = x * 6, g = pngRow * 60, b = 200.
        private static Color EffectPixel(int x, int unityRow) =>
            new Color(x * 6 / 255f, (3 - unityRow) * 60 / 255f, 200 / 255f, 1f);

        private void AddEffectColors()
        {
            var rgba = new byte[40 * 4 * 4];
            for (int y = 0; y < 4; y++)
            for (int x = 0; x < 40; x++)
            {
                int i = (y * 40 + x) * 4;
                rgba[i] = (byte)(x * 6);
                rgba[i + 1] = (byte)(y * 60);
                rgba[i + 2] = 200;
                rgba[i + 3] = 255;
            }
            var path = _temp.Combine("effectColors.png");
            GameFormatWriters.WritePng(path, 40, 4, rgba);
            _files["effectColors"] = new FileAsset("effectColors", path);
        }

        private static void AssertColor(Color expected, Color actual)
        {
            Assert.AreEqual(expected.r, actual.r, 1e-5f, "r");
            Assert.AreEqual(expected.g, actual.g, 1e-5f, "g");
            Assert.AreEqual(expected.b, actual.b, 1e-5f, "b");
            Assert.AreEqual(expected.a, actual.a, 1e-5f, "a");
        }
    }
}
