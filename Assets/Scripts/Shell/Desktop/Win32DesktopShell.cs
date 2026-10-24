using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using RWDE.Shell.Native;

namespace RWDE.Shell.Desktop
{
    /// <summary>
    /// <see cref="IDesktopShell"/> over the real desktop. The list view lives in explorer.exe, so its
    /// item rectangles are read across processes: a <c>RECT</c> is allocated inside explorer with
    /// <c>VirtualAllocEx</c>, filled by <c>LVM_GETITEMRECT</c>, and copied back with <c>ReadProcessMemory</c>.
    /// </summary>
    internal sealed class Win32DesktopShell : IDesktopShell
    {
        /// <summary>Per-message timeout, so a hung explorer cannot stall the caller.</summary>
        private const uint MessageTimeoutMs = 200;

        private static readonly UIntPtr RectSize = (UIntPtr)Marshal.SizeOf<User32.RECT>();

        public IntPtr FindWindow(IntPtr parent, IntPtr after, string className) =>
            User32.FindWindowEx(parent, after, className, null);

        public bool IsVisible(IntPtr hwnd) => User32.IsWindowVisible(hwnd);

        public bool TryGetClientOrigin(IntPtr hwnd, out int x, out int y)
        {
            var origin = new User32.POINT();
            bool ok = User32.ClientToScreen(hwnd, ref origin);
            x = origin.X;
            y = origin.Y;
            return ok;
        }

        public bool TryReadItemRects(IntPtr listView, IconRectPart part, List<ScreenRect> into)
        {
            if (!TrySend(listView, User32.LVM_GETITEMCOUNT, IntPtr.Zero, IntPtr.Zero, out IntPtr countResult)) return false;
            int count = countResult.ToInt32();
            if (count <= 0) return true;

            User32.GetWindowThreadProcessId(listView, out uint explorerPid);
            if (explorerPid == 0) return false;

            const uint access = Kernel32.PROCESS_VM_OPERATION | Kernel32.PROCESS_VM_READ | Kernel32.PROCESS_VM_WRITE;
            IntPtr process = Kernel32.OpenProcess(access, false, explorerPid);
            if (process == IntPtr.Zero) return false;

            IntPtr remote = IntPtr.Zero;
            try
            {
                remote = Kernel32.VirtualAllocEx(process, IntPtr.Zero, RectSize,
                    Kernel32.MEM_COMMIT | Kernel32.MEM_RESERVE, Kernel32.PAGE_READWRITE);
                if (remote == IntPtr.Zero) return false;

                for (int i = 0; i < count; i++)
                {
                    // LVM_GETITEMRECT reads which part to measure from rect.left on input.
                    var rect = new User32.RECT { Left = (int)part };
                    if (!Kernel32.WriteProcessMemory(process, remote, ref rect, RectSize, out _)) return false;
                    if (!TrySend(listView, User32.LVM_GETITEMRECT, (IntPtr)i, remote, out IntPtr gotRect)) return false;
                    if (gotRect == IntPtr.Zero) continue;
                    if (!Kernel32.ReadProcessMemory(process, remote, out rect, RectSize, out _)) return false;
                    into.Add(new ScreenRect(rect.Left, rect.Top, rect.Right, rect.Bottom));
                }
                return true;
            }
            finally
            {
                if (remote != IntPtr.Zero) Kernel32.VirtualFreeEx(process, remote, UIntPtr.Zero, Kernel32.MEM_RELEASE);
                Kernel32.CloseHandle(process);
            }
        }

        /// <summary>
        /// The screen position of this process's own window's client area, found by window class
        /// (<c>UnityWndClass</c> for a Unity player). Used to map screen rectangles into the window.
        /// </summary>
        public static bool TryGetOwnClientOrigin(string windowClass, out int x, out int y)
        {
            uint ownPid = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;
            IntPtr hwnd = IntPtr.Zero;
            while ((hwnd = User32.FindWindowEx(IntPtr.Zero, hwnd, windowClass, null)) != IntPtr.Zero)
            {
                User32.GetWindowThreadProcessId(hwnd, out uint pid);
                if (pid != ownPid) continue;
                var origin = new User32.POINT();
                if (!User32.ClientToScreen(hwnd, ref origin)) break;
                x = origin.X;
                y = origin.Y;
                return true;
            }
            x = 0;
            y = 0;
            return false;
        }

        private static bool TrySend(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam, out IntPtr result) =>
            User32.SendMessageTimeout(hwnd, msg, wParam, lParam, User32.SMTO_ABORTIFHUNG, MessageTimeoutMs, out result) != IntPtr.Zero;
    }
}
