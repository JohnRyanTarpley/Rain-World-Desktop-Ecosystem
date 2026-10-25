using System;
using System.IO;
using System.IO.Compression;

namespace RWDE.GameAssets.Tests.Fixtures
{
    /// <summary>
    /// A throwaway folder tree shaped like a Steam library holding a Rain World install, filled with
    /// invented files only. Deleted on <see cref="Dispose"/>.
    /// </summary>
    internal sealed class FakeInstall : IDisposable
    {
        public const string AtlasJson = "{\"frames\": {\"dot.png\": {\"frame\": {\"x\":0,\"y\":0,\"w\":2,\"h\":2}}}, \"meta\": {}}";

        public FakeInstall()
        {
            RootFolder = Path.Combine(Path.GetTempPath(), "rwde-tests-" + Guid.NewGuid().ToString("N"));
            SteamRoot = Path.Combine(RootFolder, "Steam");
            LibraryRoot = Path.Combine(RootFolder, "Library");
            InstallRoot = Path.Combine(LibraryRoot, "steamapps", "common", "Rain World");
            CacheRoot = Path.Combine(RootFolder, "cache");
            Directory.CreateDirectory(Path.Combine(SteamRoot, "steamapps"));
            Directory.CreateDirectory(StreamingAssets);
            File.WriteAllText(Path.Combine(StreamingAssets, "GameVersion.txt"), "v9.9.9");
            WriteAssets(new FakeAssetsFile().AddTextAsset("testAtlas", AtlasJson).AddTexture("testAtlas", 2, 2, 4, Pixels2x2Rgba()));
            WriteLibraryFolders(LibraryRoot, listsRainWorld: true);
        }

        public string RootFolder { get; }
        public string SteamRoot { get; }
        public string LibraryRoot { get; }
        public string InstallRoot { get; }
        public string CacheRoot { get; }
        public string DataFolder => Path.Combine(InstallRoot, "RainWorld_Data");
        public string StreamingAssets => Path.Combine(DataFolder, "StreamingAssets");

        /// <summary>2x2 RGBA32, bottom row first: red, green / blue, white.</summary>
        public static byte[] Pixels2x2Rgba() => new byte[]
        {
            255, 0, 0, 255, 0, 255, 0, 255,
            0, 0, 255, 255, 255, 255, 255, 128,
        };

        public void WriteAssets(FakeAssetsFile assets) => assets.Write(Path.Combine(DataFolder, "resources.assets"));

        public void WriteLibraryFolders(string libraryPath, bool listsRainWorld)
        {
            var escaped = libraryPath.Replace("\\", "\\\\");
            var apps = listsRainWorld ? "\"312520\"\t\"123\"" : "\"228980\"\t\"1\"";
            var vdf = "\"libraryfolders\"\n{\n" +
                      "\t\"0\"\n\t{\n\t\t\"path\"\t\t\"" + SteamRoot.Replace("\\", "\\\\") + "\"\n\t\t\"apps\"\n\t\t{\n\t\t}\n\t}\n" +
                      "\t\"1\"\n\t{\n\t\t\"path\"\t\t\"" + escaped + "\"\n\t\t\"apps\"\n\t\t{\n\t\t\t" + apps + "\n\t\t}\n\t}\n}\n";
            File.WriteAllText(Path.Combine(SteamRoot, "steamapps", "libraryfolders.vdf"), vdf);
        }

        /// <summary>Writes a file under StreamingAssets (path uses forward slashes).</summary>
        public string WriteStreamingFile(string relativePath, string content)
        {
            var path = Path.Combine(StreamingAssets, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, content);
            return path;
        }

        public GameAssetOptions Options() => new GameAssetOptions
        {
            SteamRoots = new[] { SteamRoot },
            CacheRoot = CacheRoot,
        };

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(RootFolder)) Directory.Delete(RootFolder, recursive: true);
            }
            catch (IOException)
            {
                // Best effort; temp folders are cleaned up by the OS eventually.
            }
        }
    }

    /// <summary>Reads back PNGs written by the encoder (8-bit RGBA, filter type None).</summary>
    internal static class TestPngReader
    {
        public static byte[] ReadRgba(byte[] png, out int width, out int height)
        {
            width = height = 0;
            var idat = new MemoryStream();
            var offset = 8;
            while (offset < png.Length)
            {
                var length = BigEndian(png, offset);
                var type = System.Text.Encoding.ASCII.GetString(png, offset + 4, 4);
                if (type == "IHDR")
                {
                    width = BigEndian(png, offset + 8);
                    height = BigEndian(png, offset + 12);
                }
                else if (type == "IDAT")
                {
                    idat.Write(png, offset + 8, length);
                }
                offset += 12 + length;
            }

            var zlib = idat.ToArray();
            var raw = new MemoryStream();
            using (var inflate = new DeflateStream(new MemoryStream(zlib, 2, zlib.Length - 6), CompressionMode.Decompress))
                inflate.CopyTo(raw);

            var stride = width * 4;
            var bytes = raw.ToArray();
            var pixels = new byte[stride * height];
            for (var y = 0; y < height; y++)
            {
                if (bytes[y * (stride + 1)] != 0) throw new InvalidDataException("Unexpected PNG filter");
                Buffer.BlockCopy(bytes, y * (stride + 1) + 1, pixels, y * stride, stride);
            }
            return pixels;
        }

        private static int BigEndian(byte[] b, int o) => (b[o] << 24) | (b[o + 1] << 16) | (b[o + 2] << 8) | b[o + 3];
    }
}
