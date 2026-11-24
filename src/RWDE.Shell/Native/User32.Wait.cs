using System;
using System.Runtime.InteropServices;

namespace RWDE.Shell.Native
{
    /// <summary>user32.dll: sleeping until a message arrives or a timeout passes, so an idle loop uses no CPU.</summary>
    internal static partial class User32
    {
        public const uint QS_ALLINPUT = 0x04FF;
        public const uint MWMO_INPUTAVAILABLE = 0x0004;
        public const uint WAIT_FAILED = 0xFFFFFFFF;

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint MsgWaitForMultipleObjectsEx(uint nCount, IntPtr[] pHandles, uint dwMilliseconds, uint dwWakeMask, uint dwFlags);
    }
}
