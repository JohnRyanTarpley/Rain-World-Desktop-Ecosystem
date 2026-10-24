using System;
using System.Collections.Generic;
using System.IO;

namespace RWDE.GameAssets.Unity
{
    /// <summary>One object entry in a Unity serialized file's object table.</summary>
    internal readonly struct SerializedObjectInfo
    {
        public SerializedObjectInfo(long pathId, long byteStart, uint byteSize, int classId)
        {
            PathId = pathId;
            ByteStart = byteStart;
            ByteSize = byteSize;
            ClassId = classId;
        }

        public long PathId { get; }
        public long ByteStart { get; }
        public uint ByteSize { get; }
        public int ClassId { get; }
    }

    /// <summary>
    /// Minimal reader for Unity serialized files (<c>*.assets</c>), format versions 17-22
    /// (Unity 2017.x - 2020.x). It reads the object table only; object layouts are decoded by
    /// type-specific readers. Type trees are skipped, so files built without them are fine.
    /// </summary>
    internal sealed class SerializedFile
    {
        public const int ClassTexture2D = 28;
        public const int ClassTextAsset = 49;

        private const int MinVersion = 17;
        private const int MaxVersion = 22;

        private SerializedFile(string path, int version, string unityVersion, long dataOffset, bool bigEndian,
            List<SerializedObjectInfo> objects)
        {
            FilePath = path;
            Version = version;
            UnityVersion = unityVersion;
            DataOffset = dataOffset;
            BigEndian = bigEndian;
            Objects = objects;
        }

        public string FilePath { get; }
        public int Version { get; }
        public string UnityVersion { get; }
        public long DataOffset { get; }
        public bool BigEndian { get; }
        public IReadOnlyList<SerializedObjectInfo> Objects { get; }

        public static SerializedFile Open(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var reader = new EndianReader(stream, bigEndian: true))
            {
                reader.ReadUInt32(); // metadata size (v<22)
                long fileSize = reader.ReadUInt32();
                var version = reader.ReadInt32();
                long dataOffset = reader.ReadUInt32();
                if (version < MinVersion || version > MaxVersion)
                    throw new InvalidDataException($"Unsupported serialized file version {version} in {path}");

                var bigEndian = reader.ReadByte() != 0;
                reader.ReadBytes(3);
                if (version >= 22)
                {
                    reader.ReadUInt32(); // metadata size
                    fileSize = reader.ReadInt64();
                    dataOffset = reader.ReadInt64();
                    reader.ReadInt64();
                }

                if (fileSize != stream.Length)
                    throw new InvalidDataException($"Serialized file size mismatch in {path}");

                reader.BigEndian = bigEndian;
                var unityVersion = reader.ReadNullTerminatedString();
                reader.ReadInt32(); // target platform
                var enableTypeTree = reader.ReadBool();

                var typeCount = reader.ReadInt32();
                var classIds = new int[typeCount];
                for (var i = 0; i < typeCount; i++)
                    classIds[i] = ReadType(reader, version, enableTypeTree);

                var objectCount = reader.ReadInt32();
                var objects = new List<SerializedObjectInfo>(objectCount);
                for (var i = 0; i < objectCount; i++)
                {
                    reader.Align();
                    var pathId = reader.ReadInt64();
                    var byteStart = version >= 22 ? reader.ReadInt64() : reader.ReadUInt32();
                    var byteSize = reader.ReadUInt32();
                    var typeIndex = reader.ReadInt32();
                    if (typeIndex < 0 || typeIndex >= typeCount)
                        throw new InvalidDataException($"Bad type index {typeIndex} in {path}");
                    objects.Add(new SerializedObjectInfo(pathId, byteStart, byteSize, classIds[typeIndex]));
                }

                return new SerializedFile(path, version, unityVersion, dataOffset, bigEndian, objects);
            }
        }

        /// <summary>Reads one object's raw bytes.</summary>
        public byte[] ReadObjectData(Stream stream, SerializedObjectInfo info)
        {
            stream.Position = DataOffset + info.ByteStart;
            var data = new byte[info.ByteSize];
            var read = 0;
            while (read < data.Length)
            {
                var n = stream.Read(data, read, data.Length - read);
                if (n <= 0) throw new EndOfStreamException();
                read += n;
            }
            return data;
        }

        private static int ReadType(EndianReader reader, int version, bool enableTypeTree)
        {
            var classId = reader.ReadInt32();
            reader.ReadBool(); // is stripped
            reader.ReadInt16(); // script type index
            if (classId == 114)
                reader.ReadBytes(16); // script id
            reader.ReadBytes(16); // old type hash

            if (enableTypeTree)
            {
                var nodeCount = reader.ReadInt32();
                var stringBufferSize = reader.ReadInt32();
                var nodeSize = version >= 19 ? 32 : 24;
                reader.ReadBytes(nodeCount * nodeSize + stringBufferSize);
                if (version >= 21)
                {
                    var dependencyCount = reader.ReadInt32();
                    reader.ReadBytes(dependencyCount * 4);
                }
            }

            return classId;
        }

        /// <summary>Opens a reader over one object's bytes, so alignment is relative to the object start.</summary>
        public EndianReader OpenObject(Stream stream, SerializedObjectInfo info)
        {
            return new EndianReader(new MemoryStream(ReadObjectData(stream, info), writable: false), BigEndian);
        }

        /// <summary>Reads just the <c>m_Name</c> field, which every named object starts with.</summary>
        public string ReadObjectName(Stream stream, SerializedObjectInfo info)
        {
            stream.Position = DataOffset + info.ByteStart;
            using (var reader = new EndianReader(stream, BigEndian))
            {
                var length = reader.ReadInt32();
                if (length < 0 || length > info.ByteSize - 4) throw new InvalidDataException("Bad object name");
                return System.Text.Encoding.UTF8.GetString(reader.ReadBytes(length));
            }
        }
    }
}
