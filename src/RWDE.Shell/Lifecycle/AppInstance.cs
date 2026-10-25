using System;
using System.Threading;

namespace RWDE.Shell.Lifecycle
{
    /// <summary>
    /// The one running copy of the app in this Windows session, and the stop signal a second copy
    /// started with <c>--stop</c> uses to end it from a terminal.
    /// </summary>
    public sealed class AppInstance : IDisposable
    {
        // Local\ scopes both names to the user's session.
        const string MutexName = @"Local\RWDE.App";
        const string StopEventName = @"Local\RWDE.App.Stop";

        readonly Mutex mutex;
        readonly EventWaitHandle stop;

        AppInstance(Mutex mutex, EventWaitHandle stop)
        {
            this.mutex = mutex;
            this.stop = stop;
        }

        /// <summary>Claims the single-instance slot, or returns null when the app is already running.</summary>
        public static AppInstance TryClaim()
        {
            var mutex = new Mutex(true, MutexName, out bool created);
            if (!created)
            {
                mutex.Dispose();
                return null;
            }
            var stop = new EventWaitHandle(false, EventResetMode.ManualReset, StopEventName);
            stop.Reset();
            return new AppInstance(mutex, stop);
        }

        /// <summary>True once another process has asked this one to stop.</summary>
        public bool StopRequested => stop.WaitOne(0);

        /// <summary>Asks the running app to stop. Returns false when it isn't running.</summary>
        public static bool RequestStop()
        {
            if (!EventWaitHandle.TryOpenExisting(StopEventName, out var stop)) return false;
            using (stop) stop.Set();
            return true;
        }

        public void Dispose()
        {
            stop.Dispose();
            mutex.ReleaseMutex();
            mutex.Dispose();
        }
    }
}
