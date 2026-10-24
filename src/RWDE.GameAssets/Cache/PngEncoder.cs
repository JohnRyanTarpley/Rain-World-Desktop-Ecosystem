using System.IO;
using System.IO.Compression;
using System.Text;

namespace RWDE.GameAssets.Cache
{
    /// <summary>Writes 8-bit RGBA PNG files without depending on UnityEngine.</summary>
    internal static class PngEncoder
    {
        private static readonly byte[] Signature = { 137, 80, 78, 71, 13, 10, 26, 10 };
        private static uint[] _crcTable;

        /// <param name="rgba">Pixels, top row first, 4 bytes per pixel.</param>
        public static byte[] Encode(int width, int height, byte[] rgba)
        {
            using (var output = new MemoryStream())
            {
                output.Write(Signature, 0, Signature.Length);

                var header = new byte[13];
                WriteBigEndian(header, 0, (uint)width);
                WriteBigEndian(header, 4, (uint)height);
                header[8] = 8; // bit depth
                header[9] = 6; // colour type: RGBA
                WriteChunk(output, "IHDR", header);

                WriteChunk(output, "IDAT", Compress(width, height, rgba));
                WriteChunk(output, "IEND", new byte[0]);
                return output.ToArray();
            }
        }

        private static byte[] Compress(int width, int height, byte[] rgba)
        {
            var stride = width * 4;
            uint adlerA = 1, adlerB = 0;
            using (var zlib = new MemoryStream())
            {
                zlib.WriteByte(0x78);
                zlib.WriteByte(0x9C);
                using (var deflate = new DeflateStream(zlib, CompressionLevel.Optimal, leaveOpen: true))
                {
                    var row = new byte[stride + 1]; // filter type 0 (None) per row
                    for (var y = 0; y < height; y++)
                    {
                        System.Buffer.BlockCopy(rgba, y * stride, row, 1, stride);
                        deflate.Write(row, 0, row.Length);
                        foreach (var b in row)
                        {
                            adlerA = (adlerA + b) % 65521;
                            adlerB = (adlerB + adlerA) % 65521;
                        }
                    }
                }
                var adler = new byte[4];
                WriteBigEndian(adler, 0, (adlerB << 16) | adlerA);
                zlib.Write(adler, 0, 4);
                return zlib.ToArray();
            }
        }

        private static void WriteChunk(Stream output, string type, byte[] data)
        {
            var length = new byte[4];
            WriteBigEndian(length, 0, (uint)data.Length);
            output.Write(length, 0, 4);

            var typeBytes = Encoding.ASCII.GetBytes(type);
            output.Write(typeBytes, 0, 4);
            output.Write(data, 0, data.Length);

            var crc = Crc(Crc(0xFFFFFFFF, typeBytes), data) ^ 0xFFFFFFFF;
            var crcBytes = new byte[4];
            WriteBigEndian(crcBytes, 0, crc);
            output.Write(crcBytes, 0, 4);
        }

        private static uint Crc(uint crc, byte[] data)
        {
            if (_crcTable == null)
            {
                var table = new uint[256];
                for (uint n = 0; n < 256; n++)
                {
                    var c = n;
                    for (var k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
                    table[n] = c;
                }
                _crcTable = table;
            }
            foreach (var b in data) crc = _crcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
            return crc;
        }

        private static void WriteBigEndian(byte[] buffer, int offset, uint value)
        {
            buffer[offset] = (byte)(value >> 24);
            buffer[offset + 1] = (byte)(value >> 16);
            buffer[offset + 2] = (byte)(value >> 8);
            buffer[offset + 3] = (byte)value;
        }
    }
}
