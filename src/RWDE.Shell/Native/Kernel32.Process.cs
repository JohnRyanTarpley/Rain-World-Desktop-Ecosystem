using System;
using System.Runtime.InteropServices;

namespace RWDE.Shell.Native
{
    /// <summary>kernel32.dll entry points for the app process itself.</summary>
    internal static partial class Kernel32
    {
        public const uint ATTACH_PARENT_PROCESS = 0xFFFFFFFF;

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern IntPtr GetModuleHandle(string lpModuleName);

        /// <summary>Lets a GUI-subsystem process print to the terminal that started it.</summary>
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool AttachConsole(uint dwProcessId);
    }
}
