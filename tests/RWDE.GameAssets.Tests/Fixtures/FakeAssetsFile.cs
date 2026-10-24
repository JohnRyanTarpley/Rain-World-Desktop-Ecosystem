using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace RWDE.GameAssets.Tests.Fixtures
{
    /// <summary>
    /// Writes small invented Unity serialized files (format 22, no type trees, little-endian objects)
    /// with Texture2D and TextAsset objects laid out the way Unity 2020.3 writes them.
    /// </summary>
    internal sealed class FakeAssetsFile
    {
        private readonly List<(int classId, byte[] data)> _objects = new List<(int, byte[])>();
        private readonly MemoryStream _resS = new MemoryStream();

        public const string ResSFileName = "fake.assets.resS";

        public FakeAssetsFile AddTextAsset(string name, string text)
        {
            var w = new ObjectWriter();
            w.AlignedString(name);
            w.AlignedBytes(Encoding.UTF8.GetBytes(text));
            _objects.Add((49, w.ToArray()));
            return this;
        }

        /// <param name="pixels">Raw pixel bytes in <paramref name="format"/>, bottom row first (Unity order).</param>
        /// <param name="streamed">Store pixels in the <c>.resS</c> file instead of inline.</param>
        public FakeAssetsFile AddTexture(string name, int width, int height, int format, byte[] pixels, bool streamed = false)
        {
            var w = new ObjectWriter();
            w.AlignedString(name);
            w.Int32(-1); // m_ForcedFallbackFormat
            w.Bool(false); // m_DownscaleFallback
            w.Bool(false); // m_IsAlphaChannelOptional
            w.Align();
            w.Int32(width);
            w.Int32(height);
            w.Int32(pixels.Length); // m_CompleteImageSize
            w.Int32(0); // m_MipsStripped
            w.Int32(format);
            w.Int32(1); // m_MipCount
            w.Bool(false); // m_IsReadable
            w.Bool(false); // m_IsPreProcessed
            w.Bool(false); // m_IgnoreMasterTextureLimit
            w.Bool(false); // m_StreamingMipmaps
            w.Align();
            w.Int32(0); // m_StreamingMipmapsPriority
            w.Int32(1); // m_ImageCount
            w.Int32(2); // m_TextureDimension
            w.Int32(1); // m_FilterMode
            w.Int32(1); // m_Aniso
            w.Single(0); // m_MipBias
            w.Int32(1); // m_WrapU
            w.Int32(1); // m_WrapV
            w.Int32(1); // m_WrapW
            w.Int32(0); // m_LightmapFormat
            w.Int32(1); // m_ColorSpace
            w.AlignedBytes(new byte[0]); // m_PlatformBlob
            if (streamed)
            {
                w.AlignedBytes(new byte[0]);
                var offset = _resS.Length;
                _resS.Write(pixels, 0, pixels.Length);
                while (_resS.Length % 16 != 0) _resS.WriteByte(0);
                w.UInt64((ulong)offset);
                w.UInt32((uint)pixels.Length);
                w.AlignedString("archive:/CAB-fake/" + ResSFileName);
            }
            else
            {
                w.AlignedBytes(pixels);
                w.UInt64(0);
                w.UInt32(0);
                w.AlignedString("");
            }
            _objects.Add((28, w.ToArray()));
            return this;
        }

        public void Write(string path, int version = 22)
        {
            var metadata = new ObjectWriter();
            metadata.CString("2020.3.45f1");
            metadata.Int32(19); // StandaloneWindows64
            metadata.Bool(false); // no type trees
            var classIds = new List<int>();
            foreach (var o in _objects)
                if (!classIds.Contains(o.classId)) classIds.Add(o.classId);
            metadata.Int32(classIds.Count);
            foreach (var classId in classIds)
            {
                metadata.Int32(classId);
                metadata.Bool(false);
                metadata.Int16(-1);
                metadata.Bytes(new byte[16]);
            }

            const int headerSize = 48;
            metadata.Int32(_objects.Count);
            long byteStart = 0;
            var starts = new List<long>();
            for (var i = 0; i < _objects.Count; i++)
            {
                metadata.AlignAbsolute(headerSize);
                metadata.Int64(i + 1); // path id
                metadata.Int64(byteStart);
                metadata.UInt32((uint)_objects[i].data.Length);
                metadata.Int32(classIds.IndexOf(_objects[i].classId));
                starts.Add(byteStart);
                byteStart += (_objects[i].data.Length + 7) / 8 * 8;
            }
            metadata.Int32(0); // scripts
            metadata.Int32(0); // externals
            metadata.Int32(0); // ref types
            metadata.CString("");

            var metadataBytes = metadata.ToArray();
            var dataOffset = (headerSize + metadataBytes.Length + 15) / 16 * 16;
            var fileSize = dataOffset + byteStart;

            using (var output = new MemoryStream())
            {
                var header = new BigEndianWriter(output);
                header.UInt32((uint)metadataBytes.Length);
                header.UInt32(0);
                header.UInt32((uint)version);
                header.UInt32(0);
                output.WriteByte(0); // little-endian
                output.Write(new byte[3], 0, 3);
                header.UInt32((uint)metadataBytes.Length);
                header.Int64(fileSize);
                header.Int64(dataOffset);
                header.Int64(0);
                output.Write(metadataBytes, 0, metadataBytes.Length);
                while (output.Length < dataOffset) output.WriteByte(0);
                for (var i = 0; i < _objects.Count; i++)
                {
                    output.Position = dataOffset + starts[i];
                    output.Write(_objects[i].data, 0, _objects[i].data.Length);
                }
                output.SetLength(fileSize);
                File.WriteAllBytes(path, output.ToArray());
            }

            if (_resS.Length > 0)
                File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(path), ResSFileName), _resS.ToArray());
        }

        private sealed class BigEndianWriter
        {
            private readonly Stream _stream;
            public BigEndianWriter(Stream stream) => _stream = stream;

            public void UInt32(uint v)
            {
                _stream.WriteByte((byte)(v >> 24));
                _stream.WriteByte((byte)(v >> 16));
                _stream.WriteByte((byte)(v >> 8));
                _stream.WriteByte((byte)v);
            }

            public void Int64(long v)
            {
                UInt32((uint)((ulong)v >> 32));
                UInt32((uint)v);
            }
        }

        internal sealed class ObjectWriter
        {
            private readonly MemoryStream _stream = new MemoryStream();

            public void Bytes(byte[] bytes) => _stream.Write(bytes, 0, bytes.Length);
            public void Bool(bool v) => _stream.WriteByte(v ? (byte)1 : (byte)0);
            public void Int16(short v) => Bytes(BitConverter.GetBytes(v));
            public void Int32(int v) => Bytes(BitConverter.GetBytes(v));
            public void UInt32(uint v) => Bytes(BitConverter.GetBytes(v));
            public void Int64(long v) => Bytes(BitConverter.GetBytes(v));
            public void UInt64(ulong v) => Bytes(BitConverter.GetBytes(v));
            public void Single(float v) => Bytes(BitConverter.GetBytes(v));

            public void CString(string s)
            {
                Bytes(Encoding.UTF8.GetBytes(s));
                _stream.WriteByte(0);
            }

            public void Align() => AlignAbsolute(0);

            /// <summary>Aligns as if this buffer started at <paramref name="baseOffset"/> in the file.</summary>
            public void AlignAbsolute(int baseOffset)
            {
                while ((baseOffset + _stream.Length) % 4 != 0) _stream.WriteByte(0);
            }

            public void AlignedString(string s) => AlignedBytes(Encoding.UTF8.GetBytes(s));

            public void AlignedBytes(byte[] bytes)
            {
                Int32(bytes.Length);
                Bytes(bytes);
                Align();
            }

            public byte[] ToArray() => _stream.ToArray();
        }
    }
}
