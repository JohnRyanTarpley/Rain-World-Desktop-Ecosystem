using System.Collections.Concurrent;

namespace RWDE.UI.Windows
{
    /// <summary>Something the user asked for in the settings window or tray menu.</summary>
    public abstract record UiCommand;

    /// <summary>"Spawn now": bring one creature of <paramref name="InternalType"/> onto the desktop.</summary>
    public sealed record SpawnNowCommand(string InternalType) : UiCommand;

    /// <summary>The held reset button: the sim's reset command.</summary>
    public sealed record ResetSimCommand : UiCommand;

    /// <summary>The tray menu's Quit.</summary>
    public sealed record QuitCommand : UiCommand;

    /// <summary>
    /// Carries commands from the window's UI thread to the app's main loop, which drains it each
    /// frame and passes them to the sim. Safe to use from any thread.
    /// </summary>
    public sealed class UiCommandQueue
    {
        readonly ConcurrentQueue<UiCommand> queue = new ConcurrentQueue<UiCommand>();

        public void Post(UiCommand command)
        {
            if (command != null) queue.Enqueue(command);
        }

        public bool TryTake(out UiCommand command) => queue.TryDequeue(out command);
    }
}
