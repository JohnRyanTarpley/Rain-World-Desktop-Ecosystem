using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using NUnit.Framework;
using RWDE.GameAssets.Cache;

namespace RWDE.GameAssets.Tests
{
    public class PngDecoderTests
    {
        [Test]
        public void ReadsWhatTheEncoderWrites()
        {
            var pixels = new byte[5 * 3 * 4];
            for (var i = 0; i < pixels.Length; i++) pixels[i] = (byte)(i * 7);

            var decoded = PngDecoder.Decode(PngEncoder.Encode(5, 3, pixels), out var w, out var h);

            Assert.AreEqual(5, w);
            Assert.AreEqual(3, h);
            CollectionAssert.AreEqual(pixels, decoded);
        }

        [TestCase(1, TestName = "Sub filter")]
        [TestCase(2, TestName = "Up filter")]
        [TestCase(3, TestName = "Average filter")]
        [TestCase(4, TestName = "Paeth filter")]
        public void UndoesEveryFilter(int filter)
        {
            var pixels = new byte[4 * 3 * 4];
            for (var i = 0; i < pixels.Length; i++) pixels[i] = (byte)(i * 37 + 11);

            var png = BuildPng(4, 3, 6, pixels, filter);

            CollectionAssert.AreEqual(pixels, PngDecoder.Decode(png, out _, out _));
        }

        [Test]
        public void ExpandsPaletteWithTransparency()
        {
            var palette = new byte[] { 10, 20, 30, 200, 100, 50 };
            var png = BuildPng(2, 1, 3, new byte[] { 0, 1 }, 0, palette, transparency: new byte[] { 0 });

            var rgba = PngDecoder.Decode(png, out _, out _);

            CollectionAssert.AreEqual(new byte[] { 10, 20, 30, 0, 200, 100, 50, 255 }, rgba);
        }

        [Test]
        public void ExpandsGreyscaleAndRgb()
        {
            CollectionAssert.AreEqual(new byte[] { 9, 9, 9, 255, 250, 250, 250, 255 },
                PngDecoder.Decode(BuildPng(2, 1, 0, new byte[] { 9, 250 }, 0), out _, out _));
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 255 },
                PngDecoder.Decode(BuildPng(1, 1, 2, new byte[] { 1, 2, 3 }, 0), out _, out _));
            CollectionAssert.AreEqual(new byte[] { 7, 7, 7, 128 },
                PngDecoder.Decode(BuildPng(1, 1, 4, new byte[] { 7, 128 }, 0), out _, out _));
        }

        [Test]
        public void RejectsNonPngAndOtherBitDepths()
        {
            Assert.Throws<InvalidDataException>(() => PngDecoder.Decode(Encoding.ASCII.GetBytes("not a png at all, really"), out _, out _));
            Assert.Throws<InvalidDataException>(() => PngDecoder.Decode(BuildPng(1, 1, 0, new byte[] { 0, 0 }, 0, bitDepth: 16), out _, out _));
        }

        // Writes a PNG with every row filtered by `filter`, the way an encoder would.
        static byte[] BuildPng(int width, int height, int colourType, byte[] samples, int filter,
            byte[] palette = null, byte[] transparency = null, int bitDepth = 8)
        {
            int channels = colourType == 2 ? 3 : colourType == 4 ? 2 : colourType == 6 ? 4 : 1;
            int bpp = Math.Max(1, channels * bitDepth / 8);
            int stride = samples.Length / height;
            var raw = new byte[(stride + 1) * height];
            for (var y = 0; y < height; y++)
            {
                raw[y * (stride + 1)] = (byte)filter;
                for (var i = 0; i < stride; i++)
                {
                    int x = samples[y * stride + i];
                    int a = i >= bpp ? samples[y * stride + i - bpp] : 0;
                    int b = y > 0 ? samples[(y - 1) * stride + i] : 0;
                    int c = y > 0 && i >= bpp ? samples[(y - 1) * stride + i - bpp] : 0;
                    int predictor = filter switch
                    {
                        1 => a,
                        2 => b,
                        3 => (a + b) >> 1,
                        4 => Paeth(a, b, c),
                        _ => 0,
                    };
                    raw[y * (stride + 1) + 1 + i] = (byte)(x - predictor);
                }
            }

            var compressed = new MemoryStream();
            using (var z = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
                z.Write(raw, 0, raw.Length);

            var png = new MemoryStream();
            png.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
            var header = new byte[13];
            WriteBigEndian(header, 0, width);
            WriteBigEndian(header, 4, height);
            header[8] = (byte)bitDepth;
            header[9] = (byte)colourType;
            Chunk(png, "IHDR", header);
            if (palette != null) Chunk(png, "PLTE", palette);
            if (transparency != null) Chunk(png, "tRNS", transparency);
            Chunk(png, "IDAT", compressed.ToArray());
            Chunk(png, "IEND", new byte[0]);
            return png.ToArray();
        }

        static int Paeth(int a, int b, int c)
        {
            int p = a + b - c, pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
            if (pa <= pb && pa <= pc) return a;
            return pb <= pc ? b : c;
        }

        // The decoder skips CRCs, so these chunks carry a zero one.
        static void Chunk(Stream s, string type, byte[] data)
        {
            var length = new byte[4];
            WriteBigEndian(length, 0, data.Length);
            s.Write(length);
            s.Write(Encoding.ASCII.GetBytes(type));
            s.Write(data);
            s.Write(new byte[4]);
        }

        static void WriteBigEndian(byte[] b, int o, int v)
        {
            b[o] = (byte)(v >> 24);
            b[o + 1] = (byte)(v >> 16);
            b[o + 2] = (byte)(v >> 8);
            b[o + 3] = (byte)v;
        }
    }
}
