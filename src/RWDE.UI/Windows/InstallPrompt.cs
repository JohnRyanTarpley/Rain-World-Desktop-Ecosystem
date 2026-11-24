using System;
using System.Windows.Forms;

namespace RWDE.UI.Windows
{
    /// <summary>
    /// Asks the user for the Rain World folder when startup can't find the install, so a copy outside
    /// Steam (or one that moved) can be set without the settings window, which only exists once the app
    /// is running.
    /// </summary>
    public static class InstallPrompt
    {
        /// <summary>The dialogs <see cref="Ask(string, Func{string, bool}, IDialogs)"/> shows; replaced in tests.</summary>
        public interface IDialogs
        {
            /// <summary>Shows why no install was found; true when the user wants to choose the folder.</summary>
            bool OfferToChoose(string problem);

            /// <summary>A folder picker starting at <paramref name="startPath"/>; null when cancelled.</summary>
            string PickFolder(string startPath);

            /// <summary>Says <paramref name="path"/> is not a Rain World install.</summary>
            void ShowNotAnInstall(string path);
        }

        /// <summary>
        /// Offers to choose the folder, and keeps asking until the user picks a Rain World install or gives
        /// up. Returns the folder, or null when the user declined or cancelled.
        /// </summary>
        /// <param name="problem">Why no install was found, shown to the user.</param>
        /// <param name="isInstall">Whether a folder is a Rain World install (the same check startup uses).</param>
        public static string Ask(string problem, Func<string, bool> isInstall, IDialogs dialogs = null)
        {
            if (isInstall == null) throw new ArgumentNullException(nameof(isInstall));
            dialogs ??= new WinFormsDialogs();
            if (!dialogs.OfferToChoose(problem))
                return null;
            string start = null;
            while (true)
            {
                var path = dialogs.PickFolder(start);
                if (string.IsNullOrWhiteSpace(path))
                    return null;
                if (isInstall(path))
                    return path;
                dialogs.ShowNotAnInstall(path);
                start = path;
            }
        }

        private sealed class WinFormsDialogs : IDialogs
        {
            private const string Title = "Rain World Desktop Ecosystem";

            public WinFormsDialogs()
            {
                Application.EnableVisualStyles();
            }

            public bool OfferToChoose(string problem) =>
                MessageBox.Show(
                    "Rain World Desktop Ecosystem needs an installed copy of Rain World.\n\n" + problem +
                    "\n\nDo you want to choose the Rain World folder now?",
                    Title, MessageBoxButtons.YesNo, MessageBoxIcon.Error) == DialogResult.Yes;

            public string PickFolder(string startPath)
            {
                using var dialog = new FolderBrowserDialog
                {
                    Description = "Choose the Rain World folder (the one with RainWorld.exe)",
                    UseDescriptionForTitle = true,
                    SelectedPath = startPath ?? "",
                };
                return dialog.ShowDialog() == DialogResult.OK ? dialog.SelectedPath : null;
            }

            public void ShowNotAnInstall(string path) =>
                MessageBox.Show(
                    $"That folder isn't a Rain World install:\n{path}\n\nChoose the folder that has RainWorld.exe and RainWorld_Data in it.",
                    Title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
