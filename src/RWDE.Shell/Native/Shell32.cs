using System;
using System.Runtime.InteropServices;

namespace RWDE.Shell.Native
{
    /// <summary>P/Invoke for shell32.dll. Partial so each feature can add its own entry points.</summary>
    internal static partial class Shell32
    {
        public const uint ABM_GETTASKBARPOS = 0x00000005;

        [StructLayout(LayoutKind.Sequential)]
        public struct APPBARDATA
        {
            public uint cbSize;
            public IntPtr hWnd;
            public uint uCallbackMessage;
            public uint uEdge;
            public User32.RECT rc;
            public IntPtr lParam;
        }

        /// <summary>For <see cref="ABM_GETTASKBARPOS"/>, nonzero on success.</summary>
        [DllImport("shell32.dll", SetLastError = true)]
        public static extern UIntPtr SHAppBarMessage(uint dwMessage, ref APPBARDATA pData);
    }
}
