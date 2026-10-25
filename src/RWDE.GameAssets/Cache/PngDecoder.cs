using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace RWDE.GameAssets.Cache
{
    /// <summary>
    /// Reads PNG files into 8-bit RGBA, top row first. Covers what the game's atlases and palettes
    /// use: 8-bit greyscale, RGB, palette (with <c>tRNS</c>), greyscale+alpha and RGBA, every
    /// filter type, no interlacing.
    /// </summary>
    public static class PngDecoder
    {
        private static readonly byte[] Signature = { 137, 80, 78, 71, 13, 10, 26, 10 };

        public static byte[] DecodeFile(string path, out int width, out int height) =>
            Decode(File.ReadAllBytes(path), out width, out height);

        /// <exception cref="InvalidDataException">Not a PNG, or a PNG layout this decoder does not cover.</exception>
        public static byte[] Decode(byte[] png, out int width, out int height)
        {
            if (png.Length < Signature.Length + 12 || !png.AsSpan(0, Signature.Length).SequenceEqual(Signature))
                throw new InvalidDataException("Not a PNG file.");

            width = height = 0;
            int bitDepth = 0, colourType = -1, interlace = 0;
            byte[] palette = null, transparency = null;
            var idat = new MemoryStream();

            var offset = Signature.Length;
            while (offset + 8 <= png.Length)
            {
                var length = BigEndian(png, offset);
                var type = Encoding.ASCII.GetString(png, offset + 4, 4);
                var data = offset + 8;
                if (length < 0 || data + length > png.Length) throw new InvalidDataException("Truncated PNG chunk " + type + ".");

                switch (type)
                {
                    case "IHDR":
                        width = BigEndian(png, data);
                        height = BigEndian(png, data + 4);
                        bitDepth = png[data + 8];
                        colourType = png[data + 9];
                        interlace = png[data + 12];
                        break;
                    case "PLTE":
                        palette = png.AsSpan(data, length).ToArray();
                        break;
                    case "tRNS":
                        transparency = png.AsSpan(data, length).ToArray();
                        break;
                    case "IDAT":
                        idat.Write(png, data, length);
                        break;
                }
                if (type == "IEND") break;
                offset = data + length + 4; // skip the CRC
            }

            if (width <= 0 || height <= 0) throw new InvalidDataException("PNG has no valid IHDR.");
            if (bitDepth != 8) throw new InvalidDataException("Unsupported PNG bit depth " + bitDepth + ".");
            if (interlace != 0) throw new InvalidDataException("Interlaced PNGs are not supported.");
            var channels = Channels(colourType);
            if (colourType == 3 && palette == null) throw new InvalidDataException("Palette PNG without PLTE.");

            var stride = width * channels;
            var raw = Inflate(idat.ToArray(), (stride + 1) * height);
            Unfilter(raw, stride, height, channels);
            return ToRgba(raw, width, height, stride, colourType, palette, transparency);
        }

        private static int Channels(int colourType)
        {
            switch (colourType)
            {
                case 0: return 1; // greyscale
                case 2: return 3; // RGB
                case 3: return 1; // palette index
                case 4: return 2; // greyscale + alpha
                case 6: return 4; // RGBA
                default: throw new InvalidDataException("Unknown PNG colour type " + colourType + ".");
            }
        }

        private static byte[] Inflate(byte[] zlib, int expected)
        {
            var raw = new byte[expected];
            using (var inflate = new ZLibStream(new MemoryStream(zlib), CompressionMode.Decompress))
            {
                var read = 0;
                while (read < expected)
                {
                    var n = inflate.Read(raw, read, expected - read);
                    if (n == 0) throw new InvalidDataException("PNG image data is shorter than its size says.");
                    read += n;
                }
            }
            return raw;
        }

        // Undoes the per-row filters in place. Each row is one filter-type byte followed by stride bytes.
        private static void Unfilter(byte[] raw, int stride, int height, int bpp)
        {
            for (var y = 0; y < height; y++)
            {
                var row = y * (stride + 1) + 1;
                var prev = row - (stride + 1);
                var filter = raw[row - 1];
                for (var i = 0; i < stride; i++)
                {
                    int a = i >= bpp ? raw[row + i - bpp] : 0;
                    int b = y > 0 ? raw[prev + i] : 0;
                    int c = y > 0 && i >= bpp ? raw[prev + i - bpp] : 0;
                    int predictor;
                    switch (filter)
                    {
                        case 0: predictor = 0; break;
                        case 1: predictor = a; break;
                        case 2: predictor = b; break;
                        case 3: predictor = (a + b) >> 1; break;
                        case 4: predictor = Paeth(a, b, c); break;
                        default: throw new InvalidDataException("Unknown PNG filter type " + filter + ".");
                    }
                    raw[row + i] = (byte)(raw[row + i] + predictor);
                }
            }
        }

        private static int Paeth(int a, int b, int c)
        {
            int p = a + b - c, pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
            if (pa <= pb && pa <= pc) return a;
            return pb <= pc ? b : c;
        }

        private static byte[] ToRgba(byte[] raw, int width, int height, int stride, int colourType, byte[] palette, byte[] transparency)
        {
            var rgba = new byte[width * height * 4];
            for (var y = 0; y < height; y++)
            {
                var src = y * (stride + 1) + 1;
                var dst = y * width * 4;
                for (var x = 0; x < width; x++, dst += 4)
                {
                    switch (colourType)
                    {
                        case 0:
                        {
                            var g = raw[src + x];
                            rgba[dst] = rgba[dst + 1] = rgba[dst + 2] = g;
                            rgba[dst + 3] = transparency != null && transparency.Length >= 2 && transparency[1] == g ? (byte)0 : (byte)255;
                            break;
                        }
                        case 2:
                        {
                            var s = src + x * 3;
                            rgba[dst] = raw[s];
                            rgba[dst + 1] = raw[s + 1];
                            rgba[dst + 2] = raw[s + 2];
                            rgba[dst + 3] = transparency != null && transparency.Length >= 6
                                && transparency[1] == raw[s] && transparency[3] == raw[s + 1] && transparency[5] == raw[s + 2]
                                ? (byte)0 : (byte)255;
                            break;
                        }
                        case 3:
                        {
                            var index = raw[src + x];
                            if (index * 3 + 2 >= palette.Length) throw new InvalidDataException("PNG palette index out of range.");
                            rgba[dst] = palette[index * 3];
                            rgba[dst + 1] = palette[index * 3 + 1];
                            rgba[dst + 2] = palette[index * 3 + 2];
                            rgba[dst + 3] = transparency != null && index < transparency.Length ? transparency[index] : (byte)255;
                            break;
                        }
                        case 4:
                        {
                            var s = src + x * 2;
                            rgba[dst] = rgba[dst + 1] = rgba[dst + 2] = raw[s];
                            rgba[dst + 3] = raw[s + 1];
                            break;
                        }
                        default:
                            Buffer.BlockCopy(raw, src + x * 4, rgba, dst, 4);
                            break;
                    }
                }
            }
            return rgba;
        }

        private static int BigEndian(byte[] b, int o) => (b[o] << 24) | (b[o + 1] << 16) | (b[o + 2] << 8) | b[o + 3];
    }
}
