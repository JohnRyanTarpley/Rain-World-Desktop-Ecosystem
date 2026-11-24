using System;
using System.Threading;

namespace RWDE.Shell.Lifecycle
{
    /// <summary>
    /// The one running copy of the app in this Windows session, and the signals a second copy uses to
    /// reach it from a terminal: <c>--stop</c> ends it, <c>--settings</c> opens its settings window.
    /// </summary>
    public sealed class AppInstance : IDisposable
    {
        // Local\ scopes every name to the user's session.
        const string MutexName = @"Local\RWDE.App";
        const string StopEventName = @"Local\RWDE.App.Stop";
        const string SettingsEventName = @"Local\RWDE.App.Settings";

        readonly Mutex mutex;
        readonly EventWaitHandle stop;
        readonly EventWaitHandle settings;

        AppInstance(Mutex mutex, EventWaitHandle stop, EventWaitHandle settings)
        {
            this.mutex = mutex;
            this.stop = stop;
            this.settings = settings;
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
            var settings = new EventWaitHandle(false, EventResetMode.AutoReset, SettingsEventName);
            settings.Reset();
            return new AppInstance(mutex, stop, settings);
        }

        /// <summary>True once another process has asked this one to stop.</summary>
        public bool StopRequested => stop.WaitOne(0);

        /// <summary>True once per <see cref="RequestSettings"/> call from another process.</summary>
        public bool TakeSettingsRequest() => settings.WaitOne(0);

        /// <summary>Asks the running app to stop. Returns false when it isn't running.</summary>
        public static bool RequestStop()
        {
            if (!EventWaitHandle.TryOpenExisting(StopEventName, out var stop)) return false;
            using (stop) stop.Set();
            return true;
        }

        /// <summary>Asks the running app to open its settings window. Returns false when it isn't running.</summary>
        public static bool RequestSettings()
        {
            if (!EventWaitHandle.TryOpenExisting(SettingsEventName, out var signal)) return false;
            using (signal) signal.Set();
            return true;
        }

        public void Dispose()
        {
            settings.Dispose();
            stop.Dispose();
            mutex.ReleaseMutex();
            mutex.Dispose();
        }
    }
}
