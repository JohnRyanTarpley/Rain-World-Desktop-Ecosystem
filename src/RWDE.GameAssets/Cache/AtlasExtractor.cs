using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using RWDE.GameAssets.Unity;

namespace RWDE.GameAssets.Cache
{
    /// <summary>
    /// Extracts the Futile atlases packed in the game's <c>resources.assets</c>: every <c>TextAsset</c> holding
    /// TexturePacker JSON (<c>{"frames": ...}</c>) paired with the <c>Texture2D</c> of the same name.
    /// Each atlas is written as <c>name.png</c> + <c>name.txt</c>, the same pair Futile loads from disk.
    /// </summary>
    internal static class AtlasExtractor
    {
        public sealed class Report
        {
            public readonly List<string> Extracted = new List<string>();
            public readonly List<string> Skipped = new List<string>();
        }

        /// <param name="include">Optional filter on the packed atlas name; atlases it rejects are left out silently.</param>
        public static Report Extract(string assetsFilePath, string outputDirectory, Func<string, bool> include = null)
        {
            var report = new Report();
            var file = SerializedFile.Open(assetsFilePath);
            Directory.CreateDirectory(outputDirectory);

            using (var stream = File.OpenRead(assetsFilePath))
            {
                var textures = new Dictionary<string, SerializedObjectInfo>(StringComparer.Ordinal);
                var atlasData = new Dictionary<string, byte[]>(StringComparer.Ordinal);

                foreach (var info in file.Objects)
                {
                    if (info.ClassId == SerializedFile.ClassTexture2D)
                    {
                        var name = file.ReadObjectName(stream, info);
                        if (!textures.ContainsKey(name)) textures.Add(name, info);
                    }
                    else if (info.ClassId == SerializedFile.ClassTextAsset)
                    {
                        using (var reader = file.OpenObject(stream, info))
                        {
                            var name = reader.ReadAlignedString();
                            var script = reader.ReadAlignedBytes();
                            if (IsFutileAtlasJson(script) && !atlasData.ContainsKey(name)) atlasData.Add(name, script);
                        }
                    }
                }

                foreach (var pair in atlasData)
                {
                    if (include != null && !include(pair.Key)) continue;
                    if (!textures.TryGetValue(pair.Key, out var textureInfo))
                    {
                        report.Skipped.Add(pair.Key + " (no texture with the same name)");
                        continue;
                    }

                    Texture2DData texture;
                    using (var reader = file.OpenObject(stream, textureInfo))
                        texture = Texture2DReader.Read(reader, Path.GetDirectoryName(assetsFilePath));

                    if (!TextureDecoder.IsSupported(texture.Format))
                    {
                        report.Skipped.Add($"{pair.Key} (texture format {texture.Format} not supported)");
                        continue;
                    }

                    var rgba = TextureDecoder.ToRgba32TopDown(texture.Format, texture.Width, texture.Height, texture.TopMip);
                    var safeName = SafeFileName(pair.Key);
                    WriteAtomically(Path.Combine(outputDirectory, safeName + ".png"), PngEncoder.Encode(texture.Width, texture.Height, rgba));
                    WriteAtomically(Path.Combine(outputDirectory, safeName + ".txt"), pair.Value);
                    report.Extracted.Add(pair.Key);
                }
            }

            return report;
        }

        internal static bool IsFutileAtlasJson(byte[] script)
        {
            var head = Encoding.UTF8.GetString(script, 0, Math.Min(script.Length, 64)).TrimStart('﻿', ' ', '\t', '\r', '\n');
            return head.StartsWith("{") && head.Contains("\"frames\"");
        }

        private static string SafeFileName(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return name;
        }

        private static void WriteAtomically(string path, byte[] bytes)
        {
            var temp = path + ".tmp";
            File.WriteAllBytes(temp, bytes);
            if (File.Exists(path)) File.Delete(path);
            File.Move(temp, path);
        }
    }
}
