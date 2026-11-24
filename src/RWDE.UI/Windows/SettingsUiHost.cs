using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using RWDE.UI.Configuration;

namespace RWDE.UI.Windows
{
    /// <summary>
    /// Runs the tray icon and the settings window on their own UI thread, so WinForms has its own
    /// message loop and never touches the overlay's render loop. The tray icon's menu has Settings
    /// (also a double-click) and Quit. <see cref="ShowSettings"/> can be called from any thread, for
    /// <c>rwde --settings</c> and the hotkey.
    /// </summary>
    public sealed class SettingsUiHost : IDisposable
    {
        public const string AppName = "Rain World Desktop Ecosystem";

        readonly SettingsStore store;
        readonly DlcAvailability installed;
        readonly UiCommandQueue commands;
        readonly Icon icon;
        readonly ManualResetEventSlim ready = new ManualResetEventSlim();
        readonly Thread thread;

        Control dispatcher;
        NotifyIcon tray;
        SettingsForm form;
        Exception startError;

        SettingsUiHost(SettingsStore store, DlcAvailability installed, UiCommandQueue commands, Icon icon)
        {
            this.store = store;
            this.installed = installed;
            this.commands = commands;
            this.icon = icon;
            thread = new Thread(Run) { IsBackground = true, Name = "RWDE settings UI" };
            thread.SetApartmentState(ApartmentState.STA);
        }

        /// <summary>Starts the UI thread and shows the tray icon. <paramref name="icon"/> defaults to the exe's icon.</summary>
        public static SettingsUiHost Start(SettingsStore store, DlcAvailability installed, UiCommandQueue commands, Icon icon = null)
        {
            if (store == null) throw new ArgumentNullException(nameof(store));
            if (commands == null) throw new ArgumentNullException(nameof(commands));
            var host = new SettingsUiHost(store, installed ?? DlcAvailability.BaseGameOnly, commands, icon);
            host.thread.Start();
            host.ready.Wait();
            if (host.startError != null) throw new InvalidOperationException("The settings UI failed to start.", host.startError);
            return host;
        }

        /// <summary>Opens the settings window, or brings it to the front if it is open.</summary>
        public void ShowSettings()
        {
            var d = dispatcher;
            if (d == null || d.IsDisposed) return;
            try { d.BeginInvoke(new Action(ShowOnUiThread)); }
            catch (InvalidOperationException) { } // The UI thread is shutting down.
        }

        void Run()
        {
            try
            {
                Application.EnableVisualStyles();
                dispatcher = new Control();
                dispatcher.CreateControl();
                _ = dispatcher.Handle;

                var menu = new ContextMenuStrip();
                var settingsItem = new ToolStripMenuItem("Settings", null, (s, e) => ShowOnUiThread());
                settingsItem.Font = new Font(settingsItem.Font, FontStyle.Bold);
                menu.Items.Add(settingsItem);
                menu.Items.Add(new ToolStripSeparator());
                menu.Items.Add(new ToolStripMenuItem("Quit", null, (s, e) => commands.Post(new QuitCommand())));

                tray = new NotifyIcon
                {
                    Text = AppName,
                    Icon = icon ?? LoadAppIcon(),
                    ContextMenuStrip = menu,
                    Visible = true,
                };
                tray.DoubleClick += (s, e) => ShowOnUiThread();
            }
            catch (Exception e)
            {
                startError = e;
                ready.Set();
                return;
            }

            ready.Set();
            Application.Run();

            tray.Visible = false;
            tray.Dispose();
            form?.Dispose();
            dispatcher.Dispose();
        }

        void ShowOnUiThread()
        {
            if (form == null || form.IsDisposed)
            {
                form = new SettingsForm(store, installed, commands);
                if (tray?.Icon != null) form.Icon = tray.Icon;
                form.FormClosed += (s, e) => form = null;
            }
            if (form.WindowState == FormWindowState.Minimized) form.WindowState = FormWindowState.Normal;
            form.Show();
            form.Activate();
        }

        static Icon LoadAppIcon()
        {
            try
            {
                var exe = Environment.ProcessPath;
                if (exe != null) return Icon.ExtractAssociatedIcon(exe) ?? SystemIcons.Application;
            }
            catch (Exception e) when (e is ArgumentException || e is System.IO.IOException)
            {
            }
            return SystemIcons.Application;
        }

        /// <summary>Removes the tray icon, closes the window and ends the UI thread.</summary>
        public void Dispose()
        {
            var d = dispatcher;
            if (d != null && !d.IsDisposed && thread.IsAlive)
            {
                try { d.BeginInvoke(new Action(Application.ExitThread)); }
                catch (InvalidOperationException) { }
                thread.Join(TimeSpan.FromSeconds(5));
            }
            ready.Dispose();
        }
    }
}
