using System;

namespace RWDE.GameAssets.Unity
{
    /// <summary>
    /// Converts Unity texture formats to straight RGBA32, top row first (PNG order).
    /// Unity stores rows bottom-up, so rows are flipped here.
    /// </summary>
    internal static class TextureDecoder
    {
        // UnityEngine.TextureFormat values.
        public const int Alpha8 = 1;
        public const int ARGB4444 = 2;
        public const int RGB24 = 3;
        public const int RGBA32 = 4;
        public const int ARGB32 = 5;
        public const int RGB565 = 7;
        public const int R16 = 9;
        public const int DXT1 = 10;
        public const int DXT5 = 12;
        public const int RGBA4444 = 13;
        public const int BGRA32 = 14;
        public const int R8 = 63;

        public static bool IsSupported(int format)
        {
            switch (format)
            {
                case Alpha8:
                case ARGB4444:
                case RGB24:
                case RGBA32:
                case ARGB32:
                case RGB565:
                case R16:
                case DXT1:
                case DXT5:
                case RGBA4444:
                case BGRA32:
                case R8:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>Byte size of one mip level of the given size.</summary>
        public static int ByteSize(int format, int width, int height)
        {
            switch (format)
            {
                case Alpha8:
                case R8:
                    return width * height;
                case ARGB4444:
                case RGBA4444:
                case RGB565:
                case R16:
                    return width * height * 2;
                case RGB24:
                    return width * height * 3;
                case RGBA32:
                case ARGB32:
                case BGRA32:
                    return width * height * 4;
                case DXT1:
                    return ((width + 3) / 4) * ((height + 3) / 4) * 8;
                case DXT5:
                    return ((width + 3) / 4) * ((height + 3) / 4) * 16;
                default:
                    throw new NotSupportedException("Unsupported texture format " + format);
            }
        }

        /// <summary>Decodes to RGBA32 with the top row first.</summary>
        public static byte[] ToRgba32TopDown(int format, int width, int height, byte[] data)
        {
            var bottomUp = format == DXT1 || format == DXT5
                ? DecodeBlocks(format, width, height, data)
                : DecodePixels(format, width, height, data);
            return FlipRows(bottomUp, width, height);
        }

        private static byte[] DecodePixels(int format, int width, int height, byte[] src)
        {
            var count = width * height;
            var dst = new byte[count * 4];
            for (var i = 0; i < count; i++)
            {
                byte r, g, b, a;
                switch (format)
                {
                    case Alpha8:
                        r = g = b = 255; a = src[i];
                        break;
                    case R8:
                        r = src[i]; g = b = 0; a = 255;
                        break;
                    case R16:
                        r = src[i * 2 + 1]; g = b = 0; a = 255;
                        break;
                    case RGB24:
                        r = src[i * 3]; g = src[i * 3 + 1]; b = src[i * 3 + 2]; a = 255;
                        break;
                    case RGBA32:
                        r = src[i * 4]; g = src[i * 4 + 1]; b = src[i * 4 + 2]; a = src[i * 4 + 3];
                        break;
                    case ARGB32:
                        a = src[i * 4]; r = src[i * 4 + 1]; g = src[i * 4 + 2]; b = src[i * 4 + 3];
                        break;
                    case BGRA32:
                        b = src[i * 4]; g = src[i * 4 + 1]; r = src[i * 4 + 2]; a = src[i * 4 + 3];
                        break;
                    case RGB565:
                    {
                        var v = src[i * 2] | (src[i * 2 + 1] << 8);
                        r = Expand5(v >> 11); g = Expand6((v >> 5) & 0x3F); b = Expand5(v & 0x1F); a = 255;
                        break;
                    }
                    case ARGB4444:
                    {
                        var v = src[i * 2] | (src[i * 2 + 1] << 8);
                        a = Expand4(v >> 12); r = Expand4((v >> 8) & 0xF); g = Expand4((v >> 4) & 0xF); b = Expand4(v & 0xF);
                        break;
                    }
                    case RGBA4444:
                    {
                        var v = src[i * 2] | (src[i * 2 + 1] << 8);
                        r = Expand4(v >> 12); g = Expand4((v >> 8) & 0xF); b = Expand4((v >> 4) & 0xF); a = Expand4(v & 0xF);
                        break;
                    }
                    default:
                        throw new NotSupportedException("Unsupported texture format " + format);
                }
                dst[i * 4] = r;
                dst[i * 4 + 1] = g;
                dst[i * 4 + 2] = b;
                dst[i * 4 + 3] = a;
            }
            return dst;
        }

        private static byte[] DecodeBlocks(int format, int width, int height, byte[] src)
        {
            var dst = new byte[width * height * 4];
            var blockSize = format == DXT1 ? 8 : 16;
            var blocksX = (width + 3) / 4;
            var blocksY = (height + 3) / 4;
            var colours = new byte[16];
            var alphas = new byte[8];
            for (var by = 0; by < blocksY; by++)
            for (var bx = 0; bx < blocksX; bx++)
            {
                var offset = (by * blocksX + bx) * blockSize;
                ulong alphaBits = 0;
                if (format == DXT5)
                {
                    alphas[0] = src[offset];
                    alphas[1] = src[offset + 1];
                    FillAlphaPalette(alphas);
                    for (var k = 0; k < 6; k++) alphaBits |= (ulong)src[offset + 2 + k] << (8 * k);
                    offset += 8;
                }

                var c0 = src[offset] | (src[offset + 1] << 8);
                var c1 = src[offset + 2] | (src[offset + 3] << 8);
                FillColourPalette(c0, c1, format == DXT1, colours);
                var indices = (uint)(src[offset + 4] | (src[offset + 5] << 8) | (src[offset + 6] << 16) | (src[offset + 7] << 24));

                for (var py = 0; py < 4; py++)
                for (var px = 0; px < 4; px++)
                {
                    var x = bx * 4 + px;
                    var y = by * 4 + py;
                    if (x >= width || y >= height) continue;
                    var p = py * 4 + px;
                    var ci = (int)((indices >> (2 * p)) & 3);
                    var d = (y * width + x) * 4;
                    dst[d] = colours[ci * 4];
                    dst[d + 1] = colours[ci * 4 + 1];
                    dst[d + 2] = colours[ci * 4 + 2];
                    dst[d + 3] = format == DXT5 ? alphas[(int)((alphaBits >> (3 * p)) & 7)] : colours[ci * 4 + 3];
                }
            }
            return dst;
        }

        private static void FillColourPalette(int c0, int c1, bool dxt1, byte[] palette)
        {
            SetRgb565(palette, 0, c0);
            SetRgb565(palette, 1, c1);
            for (var c = 0; c < 3; c++)
            {
                int a = palette[c], b = palette[4 + c];
                if (!dxt1 || c0 > c1)
                {
                    palette[8 + c] = (byte)((2 * a + b) / 3);
                    palette[12 + c] = (byte)((a + 2 * b) / 3);
                }
                else
                {
                    palette[8 + c] = (byte)((a + b) / 2);
                    palette[12 + c] = 0;
                }
            }
            palette[11] = 255;
            palette[15] = (byte)(!dxt1 || c0 > c1 ? 255 : 0);
        }

        private static void SetRgb565(byte[] palette, int index, int v)
        {
            palette[index * 4] = Expand5(v >> 11);
            palette[index * 4 + 1] = Expand6((v >> 5) & 0x3F);
            palette[index * 4 + 2] = Expand5(v & 0x1F);
            palette[index * 4 + 3] = 255;
        }

        private static void FillAlphaPalette(byte[] a)
        {
            if (a[0] > a[1])
            {
                for (var i = 1; i <= 6; i++) a[i + 1] = (byte)(((7 - i) * a[0] + i * a[1]) / 7);
            }
            else
            {
                for (var i = 1; i <= 4; i++) a[i + 1] = (byte)(((5 - i) * a[0] + i * a[1]) / 5);
                a[6] = 0;
                a[7] = 255;
            }
        }

        private static byte[] FlipRows(byte[] pixels, int width, int height)
        {
            var stride = width * 4;
            var flipped = new byte[pixels.Length];
            for (var y = 0; y < height; y++)
                Buffer.BlockCopy(pixels, y * stride, flipped, (height - 1 - y) * stride, stride);
            return flipped;
        }

        private static byte Expand4(int v) => (byte)(v * 17);
        private static byte Expand5(int v) => (byte)((v << 3) | (v >> 2));
        private static byte Expand6(int v) => (byte)((v << 2) | (v >> 4));
    }
}
