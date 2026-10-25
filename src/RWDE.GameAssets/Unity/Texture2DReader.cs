using System;
using System.IO;

namespace RWDE.GameAssets.Unity
{
    /// <summary>Header fields and pixel bytes of a serialized <c>Texture2D</c>.</summary>
    internal sealed class Texture2DData
    {
        public string Name;
        public int Width;
        public int Height;
        public int Format;
        public int MipCount;
        public int ImageCount;
        /// <summary>Bytes of the top mip level of the first image, still in <see cref="Format"/>.</summary>
        public byte[] TopMip;
    }

    /// <summary>
    /// Reads <c>Texture2D</c> objects serialized without type trees by Unity 2020.2-2020.3,
    /// including pixel data streamed from a <c>.resS</c> file next to the asset file.
    /// </summary>
    internal static class Texture2DReader
    {
        public static Texture2DData Read(EndianReader reader, string assetFileDirectory)
        {
            var texture = new Texture2DData { Name = reader.ReadAlignedString() };
            reader.ReadInt32(); // m_ForcedFallbackFormat
            reader.ReadBool(); // m_DownscaleFallback
            reader.ReadBool(); // m_IsAlphaChannelOptional
            reader.Align();
            texture.Width = reader.ReadInt32();
            texture.Height = reader.ReadInt32();
            reader.ReadInt32(); // m_CompleteImageSize
            reader.ReadInt32(); // m_MipsStripped
            texture.Format = reader.ReadInt32();
            texture.MipCount = reader.ReadInt32();
            reader.ReadBool(); // m_IsReadable
            reader.ReadBool(); // m_IsPreProcessed
            reader.ReadBool(); // m_IgnoreMasterTextureLimit
            reader.ReadBool(); // m_StreamingMipmaps
            reader.Align();
            reader.ReadInt32(); // m_StreamingMipmapsPriority
            texture.ImageCount = reader.ReadInt32();
            reader.ReadInt32(); // m_TextureDimension
            reader.ReadInt32(); // m_FilterMode
            reader.ReadInt32(); // m_Aniso
            reader.ReadSingle(); // m_MipBias
            reader.ReadInt32(); // m_WrapU
            reader.ReadInt32(); // m_WrapV
            reader.ReadInt32(); // m_WrapW
            reader.ReadInt32(); // m_LightmapFormat
            reader.ReadInt32(); // m_ColorSpace
            reader.ReadAlignedBytes(); // m_PlatformBlob
            var inline = reader.ReadAlignedBytes(); // image data
            var streamOffset = reader.ReadUInt64();
            var streamSize = reader.ReadUInt32();
            var streamPath = reader.ReadAlignedString();

            if (texture.Width <= 0 || texture.Height <= 0 || texture.Width > 16384 || texture.Height > 16384)
                throw new InvalidDataException($"Implausible texture size {texture.Width}x{texture.Height}");

            var topMipSize = TextureDecoder.ByteSize(texture.Format, texture.Width, texture.Height);
            byte[] data;
            if (inline.Length > 0)
            {
                data = inline;
            }
            else
            {
                if (string.IsNullOrEmpty(streamPath) || streamSize == 0)
                    throw new InvalidDataException($"Texture '{texture.Name}' has no image data");
                data = ReadStreamed(assetFileDirectory, streamPath, streamOffset, Math.Min(streamSize, (uint)topMipSize));
            }

            if (data.Length < topMipSize)
                throw new InvalidDataException($"Texture '{texture.Name}' (format {texture.Format}, {texture.Width}x{texture.Height}) data is shorter than its top mip");
            texture.TopMip = new byte[topMipSize];
            Buffer.BlockCopy(data, 0, texture.TopMip, 0, topMipSize);
            return texture;
        }

        private static byte[] ReadStreamed(string directory, string streamPath, ulong offset, uint size)
        {
            // Paths look like "resources.assets.resS" or "archive:/CAB-.../CAB-....resS".
            var fileName = streamPath.Replace('\\', '/');
            var slash = fileName.LastIndexOf('/');
            if (slash >= 0) fileName = fileName.Substring(slash + 1);
            var fullPath = Path.Combine(directory, fileName);
            using (var stream = File.OpenRead(fullPath))
            {
                if ((long)offset + size > stream.Length)
                    throw new InvalidDataException($"Streamed data out of range in {fullPath}");
                stream.Position = (long)offset;
                var data = new byte[size];
                var read = 0;
                while (read < data.Length)
                {
                    var n = stream.Read(data, read, data.Length - read);
                    if (n <= 0) throw new EndOfStreamException();
                    read += n;
                }
                return data;
            }
        }
    }
}
