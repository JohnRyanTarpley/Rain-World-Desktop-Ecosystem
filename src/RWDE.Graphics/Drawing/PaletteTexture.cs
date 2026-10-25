using System;

namespace RWDE.Graphics
{
    /// <summary>
    /// A small CPU-side image addressed as Unity's <c>Texture2D</c>: (0, 0) is the bottom-left pixel. The
    /// game reads and writes palette textures pixel by pixel, so ported palette code keeps its coordinates.
    /// </summary>
    public sealed class PaletteTexture
    {
        private readonly Color[] _pixels;

        public PaletteTexture(int width, int height)
        {
            if (width <= 0 || height <= 0) throw new ArgumentException("Texture size must be positive");
            this.width = width;
            this.height = height;
            _pixels = new Color[width * height];
        }

        public readonly int width;
        public readonly int height;

        /// <summary>A texture from RGBA bytes stored top row first, as a decoded PNG.</summary>
        public static PaletteTexture FromRgba(byte[] rgba, int width, int height)
        {
            if (rgba.Length != width * height * 4) throw new ArgumentException("Expected width * height RGBA pixels.", nameof(rgba));
            var texture = new PaletteTexture(width, height);
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int i = ((height - 1 - y) * width + x) * 4;
                texture._pixels[y * width + x] = new Color(rgba[i] / 255f, rgba[i + 1] / 255f, rgba[i + 2] / 255f, rgba[i + 3] / 255f);
            }
            return texture;
        }

        public Color GetPixel(int x, int y) => _pixels[Index(x, y)];

        public void SetPixel(int x, int y, Color color) => _pixels[Index(x, y)] = color;

        /// <summary>A block of pixels, row by row from the bottom (Texture2D.GetPixels).</summary>
        public Color[] GetPixels(int x, int y, int blockWidth, int blockHeight)
        {
            var block = new Color[blockWidth * blockHeight];
            for (int j = 0; j < blockHeight; j++)
            for (int i = 0; i < blockWidth; i++)
                block[j * blockWidth + i] = GetPixel(x + i, y + j);
            return block;
        }

        /// <summary>Writes a block laid out as <see cref="GetPixels"/> returns it (Texture2D.SetPixels).</summary>
        public void SetPixels(int x, int y, int blockWidth, int blockHeight, Color[] colors)
        {
            if (colors.Length != blockWidth * blockHeight) throw new ArgumentException("Expected blockWidth * blockHeight colours.", nameof(colors));
            for (int j = 0; j < blockHeight; j++)
            for (int i = 0; i < blockWidth; i++)
                SetPixel(x + i, y + j, colors[j * blockWidth + i]);
        }

        private int Index(int x, int y)
        {
            if (x < 0 || x >= width || y < 0 || y >= height)
                throw new ArgumentOutOfRangeException(nameof(x), $"Pixel ({x}, {y}) is outside {width}x{height}");
            return y * width + x;
        }
    }
}
