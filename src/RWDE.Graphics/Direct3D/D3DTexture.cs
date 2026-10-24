using System;
using Vortice.Direct3D11;
using Vortice.DXGI;

namespace RWDE.Graphics.Direct3D
{
    /// <summary>
    /// A GPU texture for one atlas: what <see cref="FAtlas.texture"/> holds when <see cref="FutileRenderer"/>
    /// draws. Pixels are uploaded top row first, so the vertex shader flips Futile's bottom-up v.
    /// </summary>
    public sealed class D3DTexture : IDisposable
    {
        internal unsafe D3DTexture(ID3D11Device device, string name, int width, int height, byte[] rgba)
        {
            if (rgba.Length != width * height * 4) throw new ArgumentException("Expected width * height RGBA pixels.", nameof(rgba));
            Name = name;
            Width = width;
            Height = height;
            var description = new Texture2DDescription(Format.R8G8B8A8_UNorm, (uint)width, (uint)height, 1, 1,
                BindFlags.ShaderResource, ResourceUsage.Immutable, CpuAccessFlags.None, 1, 0, ResourceOptionFlags.None);
            fixed (byte* pixels = rgba)
                Texture = device.CreateTexture2D(description, new SubresourceData(pixels, (uint)(width * 4)));
            View = device.CreateShaderResourceView(Texture);
        }

        public string Name { get; }
        public int Width { get; }
        public int Height { get; }

        internal ID3D11Texture2D Texture { get; }
        internal ID3D11ShaderResourceView View { get; }

        public void Dispose()
        {
            View.Dispose();
            Texture.Dispose();
        }

        public override string ToString() => $"{Name} ({Width}x{Height})";
    }
}
