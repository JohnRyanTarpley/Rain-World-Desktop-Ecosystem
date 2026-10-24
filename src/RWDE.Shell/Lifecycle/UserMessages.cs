using System;
using System.IO;
using RWDE.Shell.Native;

namespace RWDE.Shell.Lifecycle
{
    /// <summary>
    /// How the app talks to the user outside the overlay. The app is a GUI program, so it has no
    /// console of its own: text goes to the terminal that started it, if there is one, and to
    /// <see cref="LogPath"/>. Errors that stop the app also get a message box.
    /// </summary>
    public static class UserMessages
    {
        static bool attached;

        /// <summary>The log of the latest run, replaced at each start.</summary>
        public static string LogPath { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RainWorldDesktop", "rwde.log");

        /// <summary>
        /// Empties the log for a new run. Only the process that owns the app instance calls this, so
        /// <c>rwde --stop</c> and a refused second launch append to the running instance's log instead.
        /// </summary>
        public static void StartLog()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath));
                File.WriteAllText(LogPath, "");
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        /// <summary>Writes a line to the starting terminal (if any) and the log.</summary>
        public static void Print(string text)
        {
            if (!attached) attached = Kernel32.AttachConsole(Kernel32.ATTACH_PARENT_PROCESS);
            if (attached) Console.WriteLine(text);
            Log(text);
        }

        /// <summary>Writes a line to the log only.</summary>
        public static void Log(string text)
        {
            try
            {
                File.AppendAllText(LogPath, DateTime.Now.ToString("HH:mm:ss.fff") + " " + text + Environment.NewLine);
            }
            catch (IOException)
            {
                // Logging must never stop the app.
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        /// <summary>Prints <paramref name="text"/> and shows it in a message box.</summary>
        public static void ShowError(string text)
        {
            Print(text);
            User32.MessageBox(IntPtr.Zero, text, "Rain World Desktop Ecosystem", User32.MB_OK | User32.MB_ICONERROR);
        }
    }
}
