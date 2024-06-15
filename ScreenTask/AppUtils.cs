using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Task = System.Threading.Tasks.Task;

namespace RuntimeBroker
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

        public static async Task<bool> IsOnline()
        {
            return IsConnectedToInternet() && await PingAsync();
        }
        private static async Task<bool> PingAsync()
        {
            try
            {
                Ping pingSender = new Ping();
                PingReply reply = await pingSender.SendPingAsync("8.8.8.8");

                return reply.Status == IPStatus.Success;

            }
            catch
            {
                return false;
            }
        }

        [DllImport("wininet.dll")]
        private extern static bool InternetGetConnectedState(out int Description, int ReservedValue);
        private static bool IsConnectedToInternet()
        {
            int Desc;
            bool hasInternet = InternetGetConnectedState(out Desc, 0);

            if (!hasInternet)
            {
                return false;
            }
            return true;
        }

        public static string GetTempFile()
        {
            var fileTemp = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp", Guid.NewGuid().ToString() + ".tmp");
            return fileTemp;
        }
    }
  
}

