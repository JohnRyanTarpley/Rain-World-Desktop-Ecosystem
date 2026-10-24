using System;
using System.IO;
using System.Text;

namespace RWDE.GameAssets.Unity
{
    /// <summary>Binary reader with switchable endianness and Unity's 4-byte alignment rules.</summary>
    internal sealed class EndianReader : IDisposable
    {
        private readonly BinaryReader _reader;
        private readonly byte[] _buffer = new byte[8];

        public EndianReader(Stream stream, bool bigEndian)
        {
            _reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
            BigEndian = bigEndian;
        }

        public bool BigEndian { get; set; }

        public long Position
        {
            get => _reader.BaseStream.Position;
            set => _reader.BaseStream.Position = value;
        }

        public long Length => _reader.BaseStream.Length;

        public byte ReadByte() => _reader.ReadByte();

        public bool ReadBool() => _reader.ReadByte() != 0;

        public byte[] ReadBytes(int count)
        {
            var bytes = _reader.ReadBytes(count);
            if (bytes.Length != count) throw new EndOfStreamException();
            return bytes;
        }

        public short ReadInt16() => (short)ReadUInt16();

        public ushort ReadUInt16()
        {
            Fill(2);
            return BigEndian
                ? (ushort)((_buffer[0] << 8) | _buffer[1])
                : (ushort)(_buffer[0] | (_buffer[1] << 8));
        }

        public int ReadInt32() => (int)ReadUInt32();

        public uint ReadUInt32()
        {
            Fill(4);
            return BigEndian
                ? ((uint)_buffer[0] << 24) | ((uint)_buffer[1] << 16) | ((uint)_buffer[2] << 8) | _buffer[3]
                : _buffer[0] | ((uint)_buffer[1] << 8) | ((uint)_buffer[2] << 16) | ((uint)_buffer[3] << 24);
        }

        public long ReadInt64() => (long)ReadUInt64();

        public ulong ReadUInt64()
        {
            ulong a = ReadUInt32();
            ulong b = ReadUInt32();
            return BigEndian ? (a << 32) | b : (b << 32) | a;
        }

        public float ReadSingle()
        {
            var bits = ReadInt32();
            return BitConverter.ToSingle(BitConverter.GetBytes(bits), 0);
        }

        public string ReadNullTerminatedString()
        {
            var bytes = new MemoryStream();
            byte b;
            while ((b = _reader.ReadByte()) != 0) bytes.WriteByte(b);
            return Encoding.UTF8.GetString(bytes.ToArray());
        }

        /// <summary>Reads a length-prefixed string and skips to the next 4-byte boundary.</summary>
        public string ReadAlignedString()
        {
            var length = ReadInt32();
            if (length < 0 || length > Length - Position) throw new InvalidDataException("Bad string length " + length);
            var value = Encoding.UTF8.GetString(ReadBytes(length));
            Align();
            return value;
        }

        /// <summary>Reads a length-prefixed byte array and skips to the next 4-byte boundary.</summary>
        public byte[] ReadAlignedBytes()
        {
            var length = ReadInt32();
            if (length < 0 || length > Length - Position) throw new InvalidDataException("Bad array length " + length);
            var value = ReadBytes(length);
            Align();
            return value;
        }

        /// <summary>Aligns relative to the stream start, which callers set to the object start.</summary>
        public void Align(int alignment = 4)
        {
            var mod = Position % alignment;
            if (mod != 0) Position += alignment - mod;
        }

        public void Dispose() => _reader.Dispose();

        private void Fill(int count)
        {
            if (_reader.Read(_buffer, 0, count) != count) throw new EndOfStreamException();
        }
    }
}
