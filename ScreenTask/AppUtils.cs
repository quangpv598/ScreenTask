using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Task = System.Threading.Tasks.Task;

namespace ScreenTask
{
    public static class AppUtils
    {
        [DllImport("user32.dll")]
        public static extern bool ShowWindowAsync(HandleRef hWnd, int nCmdShow);
        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr WindowHandle);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr SendMessage(IntPtr hWnd, UInt32 Msg, IntPtr wParam, String lParam);
        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
        public const int SW_RESTORE = 9;

        public static readonly string STARTUP_APPLICATION_PATH = Process.GetCurrentProcess().MainModule?.FileName;

        public const uint WM_COPYDATA = 0x004A;

        public const string START_UP_SUBKEY_PATH = @"Software\Microsoft\Windows\CurrentVersion\Run";
    }
  
}

