using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RWDE.UI.Configuration
{
    /// <summary>Read-only view of the settings for the departments that react to them.</summary>
    public interface ISettingsSource
    {
        /// <summary>The current settings. Treat it as read-only; it is replaced, never changed, on update.</summary>
        Settings Current { get; }

        /// <summary>Raised after every update, on the thread that made it.</summary>
        event EventHandler<SettingsChangedEventArgs> Changed;
    }

    public sealed class SettingsChangedEventArgs : EventArgs
    {
        public Settings Previous { get; }
        public Settings Current { get; }

        public SettingsChangedEventArgs(Settings previous, Settings current)
        {
            Previous = previous;
            Current = current;
        }
    }

    /// <summary>
    /// Loads, saves and publishes the one <see cref="Settings"/> object. The file lives at
    /// <c>%APPDATA%/RainWorldDesktop/settings.json</c>. A missing or unreadable file gives the
    /// defaults, and the file is only written by <see cref="Update"/>.
    /// </summary>
    public sealed class SettingsStore : ISettingsSource
    {
        public const string FolderName = "RainWorldDesktop";
        public const string FileName = "settings.json";

        static readonly JsonSerializerOptions Json = new JsonSerializerOptions
        {
            WriteIndented = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            Converters = { new JsonStringEnumConverter() },
        };

        readonly object gate = new object();
        Settings current;

        public string FilePath { get; }

        public static string DefaultFilePath =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), FolderName, FileName);

        public Settings Current
        {
            get { lock (gate) return current; }
        }

        public event EventHandler<SettingsChangedEventArgs> Changed;

        SettingsStore(string filePath, Settings settings)
        {
            FilePath = filePath;
            current = settings;
        }

        /// <summary>Loads the settings at <paramref name="filePath"/> (default: under %APPDATA%).</summary>
        public static SettingsStore Load(string filePath = null)
        {
            filePath ??= DefaultFilePath;
            return new SettingsStore(filePath, Read(filePath));
        }

        /// <summary>The settings in <paramref name="filePath"/>, or the defaults when it is missing or invalid.</summary>
        public static Settings Read(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    var loaded = JsonSerializer.Deserialize<Settings>(File.ReadAllText(filePath), Json);
                    if (loaded != null) return loaded.Normalize();
                }
            }
            catch (Exception e) when (e is JsonException || e is IOException || e is UnauthorizedAccessException)
            {
            }
            return new Settings();
        }

        public static string Serialize(Settings settings) => JsonSerializer.Serialize(settings, Json);

        /// <summary>
        /// Applies <paramref name="change"/> to a copy of the current settings, normalises and saves
        /// it, then raises <see cref="Changed"/>. Returns the new settings.
        /// </summary>
        public Settings Update(Action<Settings> change)
        {
            if (change == null) throw new ArgumentNullException(nameof(change));
            Settings previous, next;
            lock (gate)
            {
                previous = current;
                next = previous.Clone();
                change(next);
                next.Normalize();
                Write(FilePath, next);
                current = next;
            }
            Changed?.Invoke(this, new SettingsChangedEventArgs(previous, next));
            return next;
        }

        /// <summary>Writes through a temporary file so a crash never leaves a half-written file.</summary>
        static void Write(string filePath, Settings settings)
        {
            var folder = Path.GetDirectoryName(Path.GetFullPath(filePath));
            Directory.CreateDirectory(folder);
            var temp = filePath + ".tmp";
            File.WriteAllText(temp, Serialize(settings));
            File.Move(temp, filePath, true);
        }
    }
}
