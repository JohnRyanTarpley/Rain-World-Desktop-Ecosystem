using System.Collections.Generic;
using NUnit.Framework;
using RWDE.UI.Windows;

namespace RWDE.UI.Tests
{
    /// <summary>The startup prompt for the Rain World folder, driven through fake dialogs.</summary>
    public class InstallPromptTests
    {
        const string Problem = "Rain World was not found in any Steam library.";

        sealed class FakeDialogs : InstallPrompt.IDialogs
        {
            public bool choose = true;
            public readonly Queue<string> picks = new Queue<string>();
            public readonly List<string> offered = new List<string>();
            public readonly List<string> pickerStarts = new List<string>();
            public readonly List<string> rejected = new List<string>();

            public bool OfferToChoose(string problem)
            {
                offered.Add(problem);
                return choose;
            }

            public string PickFolder(string startPath)
            {
                pickerStarts.Add(startPath);
                return picks.Count > 0 ? picks.Dequeue() : null;
            }

            public void ShowNotAnInstall(string path) => rejected.Add(path);
        }

        static bool IsInstall(string path) => path == @"D:\Games\Rain World";

        [Test]
        public void DecliningLeavesWithoutAPicker()
        {
            var dialogs = new FakeDialogs { choose = false };
            Assert.That(InstallPrompt.Ask(Problem, IsInstall, dialogs), Is.Null);
            Assert.That(dialogs.offered, Is.EqualTo(new[] { Problem }));
            Assert.That(dialogs.pickerStarts, Is.Empty);
        }

        [Test]
        public void AnInstallIsReturned()
        {
            var dialogs = new FakeDialogs();
            dialogs.picks.Enqueue(@"D:\Games\Rain World");
            Assert.That(InstallPrompt.Ask(Problem, IsInstall, dialogs), Is.EqualTo(@"D:\Games\Rain World"));
            Assert.That(dialogs.rejected, Is.Empty);
        }

        [Test]
        public void AWrongFolderIsRejectedAndThePickerOpensThereAgain()
        {
            var dialogs = new FakeDialogs();
            dialogs.picks.Enqueue(@"D:\Games");
            dialogs.picks.Enqueue(@"D:\Games\Rain World");

            Assert.That(InstallPrompt.Ask(Problem, IsInstall, dialogs), Is.EqualTo(@"D:\Games\Rain World"));
            Assert.That(dialogs.rejected, Is.EqualTo(new[] { @"D:\Games" }));
            Assert.That(dialogs.pickerStarts, Is.EqualTo(new[] { null, @"D:\Games" }));
        }

        [Test]
        public void CancellingThePickerGivesUp()
        {
            var dialogs = new FakeDialogs();
            dialogs.picks.Enqueue(@"C:\Nope");
            // The second picker is cancelled (no more picks queued).
            Assert.That(InstallPrompt.Ask(Problem, IsInstall, dialogs), Is.Null);
            Assert.That(dialogs.rejected, Is.EqualTo(new[] { @"C:\Nope" }));
            Assert.That(dialogs.pickerStarts.Count, Is.EqualTo(2));
        }
    }
}
