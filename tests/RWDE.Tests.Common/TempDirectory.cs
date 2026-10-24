using System;
using System.IO;

namespace RWDE.Tests.Common
{
    /// <summary>A unique folder under the system temp path, deleted on Dispose.</summary>
    public sealed class TempDirectory : IDisposable
    {
        public string Path { get; }

        public TempDirectory(string prefix = "rwde-test")
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), prefix + "-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        /// <summary>Joins <paramref name="parts"/> onto this folder.</summary>
        public string Combine(params string[] parts)
        {
            var all = new string[parts.Length + 1];
            all[0] = Path;
            Array.Copy(parts, 0, all, 1, parts.Length);
            return System.IO.Path.Combine(all);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, true);
        }
    }
}
