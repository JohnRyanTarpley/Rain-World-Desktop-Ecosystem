using NUnit.Framework;
using RWDE.GameAssets.Cache;
using RWDE.GameAssets.Tests.Fixtures;
using RWDE.GameAssets.Unity;

namespace RWDE.GameAssets.Tests
{
    public class TextureDecoderTests
    {
        [Test]
        public void FlipsRowsToTopDown()
        {
            var rgba = TextureDecoder.ToRgba32TopDown(TextureDecoder.RGB24, 1, 2, new byte[] { 1, 2, 3, 4, 5, 6 });

            CollectionAssert.AreEqual(new byte[] { 4, 5, 6, 255, 1, 2, 3, 255 }, rgba);
        }

        [Test]
        public void ReordersArgbAndBgra()
        {
            CollectionAssert.AreEqual(new byte[] { 2, 3, 4, 1 },
                TextureDecoder.ToRgba32TopDown(TextureDecoder.ARGB32, 1, 1, new byte[] { 1, 2, 3, 4 }));
            CollectionAssert.AreEqual(new byte[] { 3, 2, 1, 4 },
                TextureDecoder.ToRgba32TopDown(TextureDecoder.BGRA32, 1, 1, new byte[] { 1, 2, 3, 4 }));
        }

        [Test]
        public void Alpha8IsWhiteWithAlpha()
        {
            CollectionAssert.AreEqual(new byte[] { 255, 255, 255, 77 },
                TextureDecoder.ToRgba32TopDown(TextureDecoder.Alpha8, 1, 1, new byte[] { 77 }));
        }

        [Test]
        public void DecodesDxt1Block()
        {
            // c0 = pure red (0xF800), c1 = pure blue (0x001F); every texel uses index 1 (blue).
            var block = new byte[] { 0x00, 0xF8, 0x1F, 0x00, 0x55, 0x55, 0x55, 0x55 };

            var rgba = TextureDecoder.ToRgba32TopDown(TextureDecoder.DXT1, 4, 4, block);

            for (var i = 0; i < 16; i++)
                CollectionAssert.AreEqual(new byte[] { 0, 0, 255, 255 }, new[] { rgba[i * 4], rgba[i * 4 + 1], rgba[i * 4 + 2], rgba[i * 4 + 3] });
        }

        [Test]
        public void DecodesDxt5AlphaAndColourIndices()
        {
            // Alpha endpoints 255 and 0, all alpha indices 1 (=0); colour index 0 (white) everywhere.
            var block = new byte[]
            {
                255, 0, 0x49, 0x92, 0x24, 0x49, 0x92, 0x24,
                0xFF, 0xFF, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            };

            var rgba = TextureDecoder.ToRgba32TopDown(TextureDecoder.DXT5, 4, 4, block);

            for (var i = 0; i < 16; i++)
            {
                Assert.AreEqual(255, rgba[i * 4]);
                Assert.AreEqual(0, rgba[i * 4 + 3]);
            }
        }

        [Test]
        public void BlockFormatSizesRoundUpToWholeBlocks()
        {
            Assert.AreEqual(8, TextureDecoder.ByteSize(TextureDecoder.DXT1, 3, 3));
            Assert.AreEqual(32, TextureDecoder.ByteSize(TextureDecoder.DXT5, 5, 4));
        }

        [Test]
        public void PngRoundTrips()
        {
            var pixels = new byte[3 * 2 * 4];
            for (var i = 0; i < pixels.Length; i++) pixels[i] = (byte)(i * 11);

            var png = PngEncoder.Encode(3, 2, pixels);

            CollectionAssert.AreEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, new[] { png[0], png[1], png[2], png[3], png[4], png[5], png[6], png[7] });
            // The IEND chunk's CRC is a fixed value.
            CollectionAssert.AreEqual(new byte[] { 0xAE, 0x42, 0x60, 0x82 }, new[] { png[png.Length - 4], png[png.Length - 3], png[png.Length - 2], png[png.Length - 1] });
            CollectionAssert.AreEqual(pixels, TestPngReader.ReadRgba(png, out var w, out var h));
            Assert.AreEqual(3, w);
            Assert.AreEqual(2, h);
        }
    }
}
