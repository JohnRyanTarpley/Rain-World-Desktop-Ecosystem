using System;
using System.Runtime.InteropServices;

namespace RWDE.Shell.Native
{
    /// <summary>P/Invoke into dwmapi.dll.</summary>
    internal static partial class Dwm
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct MARGINS
        {
            public int cxLeftWidth;
            public int cxRightWidth;
            public int cyTopHeight;
            public int cyBottomHeight;

            /// <summary>Margins of -1: the "sheet of glass" that makes the whole client area composited with alpha.</summary>
            public static MARGINS SheetOfGlass => new MARGINS { cxLeftWidth = -1, cxRightWidth = -1, cyTopHeight = -1, cyBottomHeight = -1 };
        }

        /// <summary>Returns an HRESULT; 0 is success.</summary>
        [DllImport("dwmapi.dll", SetLastError = true)]
        public static extern int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS pMarInset);
    }
}
