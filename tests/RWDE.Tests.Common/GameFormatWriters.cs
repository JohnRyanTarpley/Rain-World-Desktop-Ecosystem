using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace RWDE.Tests.Common
{
    /// <summary>
    /// Writes small binary files in the formats the game ships (PNG images, PCM WAV sounds),
    /// so tests can build invented palettes, atlas textures and sounds on the fly instead of
    /// committing binaries.
    /// </summary>
    public static class GameFormatWriters
    {
        public const int PaletteWidth = 32;
        public const int PaletteHeight = 16;

        /// <summary>Writes an 8-bit RGBA PNG. <paramref name="rgba"/> is row-major, top row first.</summary>
        public static void WritePng(string path, int width, int height, byte[] rgba)
        {
            if (rgba.Length != width * height * 4)
                throw new ArgumentException("rgba must hold width * height * 4 bytes", nameof(rgba));

            var raw = new byte[height * (width * 4 + 1)];
            for (int y = 0; y < height; y++)
            {
                int row = y * (width * 4 + 1);
                raw[row] = 0; // filter: none
                Buffer.BlockCopy(rgba, y * width * 4, raw, row + 1, width * 4);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (var file = new FileStream(path, FileMode.Create))
            {
                file.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, 0, 8);

                var ihdr = new byte[13];
                WriteBigEndian(ihdr, 0, (uint)width);
                WriteBigEndian(ihdr, 4, (uint)height);
                ihdr[8] = 8; // bit depth
                ihdr[9] = 6; // colour type: RGBA
                WriteChunk(file, "IHDR", ihdr);
                WriteChunk(file, "IDAT", Zlib(raw));
                WriteChunk(file, "IEND", new byte[0]);
            }
        }

        /// <summary>Writes a solid-colour RGBA PNG, e.g. a stand-in atlas texture.</summary>
        public static void WriteSolidPng(string path, int width, int height, byte r, byte g, byte b, byte a = 255)
        {
            var rgba = new byte[width * height * 4];
            for (int i = 0; i < rgba.Length; i += 4)
            {
                rgba[i] = r;
                rgba[i + 1] = g;
                rgba[i + 2] = b;
                rgba[i + 3] = a;
            }
            WritePng(path, width, height, rgba);
        }

        /// <summary>
        /// Writes a palette-shaped PNG (32x16, the size of the game's <c>palette&lt;N&gt;.png</c>)
        /// filled with an invented gradient derived from <paramref name="seed"/>.
        /// </summary>
        public static void WritePalettePng(string path, int seed)
        {
            var rgba = new byte[PaletteWidth * PaletteHeight * 4];
            for (int y = 0; y < PaletteHeight; y++)
            for (int x = 0; x < PaletteWidth; x++)
            {
                int i = (y * PaletteWidth + x) * 4;
                rgba[i] = (byte)(x * 8 + seed);
                rgba[i + 1] = (byte)(y * 16 + seed);
                rgba[i + 2] = (byte)(seed * 37);
                rgba[i + 3] = 255;
            }
            WritePng(path, PaletteWidth, PaletteHeight, rgba);
        }

        /// <summary>Writes a mono 16-bit PCM WAV containing a sine tone.</summary>
        public static void WriteWav(string path, int sampleRate = 22050, int sampleCount = 2205, double frequency = 440)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (var w = new BinaryWriter(new FileStream(path, FileMode.Create)))
            {
                int dataBytes = sampleCount * 2;
                w.Write(Encoding.ASCII.GetBytes("RIFF"));
                w.Write(36 + dataBytes);
                w.Write(Encoding.ASCII.GetBytes("WAVE"));
                w.Write(Encoding.ASCII.GetBytes("fmt "));
                w.Write(16);
                w.Write((short)1); // PCM
                w.Write((short)1); // mono
                w.Write(sampleRate);
                w.Write(sampleRate * 2);
                w.Write((short)2);
                w.Write((short)16);
                w.Write(Encoding.ASCII.GetBytes("data"));
                w.Write(dataBytes);
                for (int i = 0; i < sampleCount; i++)
                    w.Write((short)(Math.Sin(2 * Math.PI * frequency * i / sampleRate) * short.MaxValue * 0.5));
            }
        }

        static byte[] Zlib(byte[] data)
        {
            using (var ms = new MemoryStream())
            {
                ms.WriteByte(0x78);
                ms.WriteByte(0x9C);
                using (var deflate = new DeflateStream(ms, CompressionMode.Compress, true))
                    deflate.Write(data, 0, data.Length);
                var adler = new byte[4];
                WriteBigEndian(adler, 0, Adler32(data));
                ms.Write(adler, 0, 4);
                return ms.ToArray();
            }
        }

        static void WriteChunk(Stream s, string type, byte[] data)
        {
            var header = new byte[8];
            WriteBigEndian(header, 0, (uint)data.Length);
            Encoding.ASCII.GetBytes(type, 0, 4, header, 4);
            s.Write(header, 0, 8);
            s.Write(data, 0, data.Length);
            var crc = new byte[4];
            WriteBigEndian(crc, 0, Crc32(header, 4, 4, data));
            s.Write(crc, 0, 4);
        }

        static void WriteBigEndian(byte[] buffer, int offset, uint value)
        {
            buffer[offset] = (byte)(value >> 24);
            buffer[offset + 1] = (byte)(value >> 16);
            buffer[offset + 2] = (byte)(value >> 8);
            buffer[offset + 3] = (byte)value;
        }

        static uint Adler32(byte[] data)
        {
            uint a = 1, b = 0;
            foreach (var d in data)
            {
                a = (a + d) % 65521;
                b = (b + a) % 65521;
            }
            return (b << 16) | a;
        }

        static uint Crc32(byte[] prefix, int offset, int count, byte[] data)
        {
            uint crc = 0xFFFFFFFF;
            for (int i = offset; i < offset + count; i++) crc = CrcStep(crc, prefix[i]);
            foreach (var d in data) crc = CrcStep(crc, d);
            return crc ^ 0xFFFFFFFF;
        }

        static uint CrcStep(uint crc, byte b)
        {
            crc ^= b;
            for (int k = 0; k < 8; k++)
                crc = (crc & 1) != 0 ? 0xEDB88320 ^ (crc >> 1) : crc >> 1;
            return crc;
        }
    }
}
