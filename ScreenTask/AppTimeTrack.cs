using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Input;

namespace AppRealtime
{
    public class AppTime
    {
        public long StartTime { get; set; }
        public long EndTime { get; set; }
        public string Windows { get; set; }
    }

    public class MultiAppTime
    {
        public long Id { get; set; }
        public List<AppTime> Collection { get; set; } = new List<AppTime>();
    }

    public static class AppTimeTrack
    {
        private static Queue<long> RemoveQueue = new Queue<long>();
        private static AppTime _lastAppTime = null;
        private static MultiAppTime _lastMultiAppTime = null;
        public static List<MultiAppTime> AppTimes { get; private set; } = new List<MultiAppTime>();

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowText(IntPtr hWnd, StringBuilder textOut, int count); 
        
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        private const int MAX_STRING_BUILDER = 256;

        private static string GetCurrentWindowText()
        {
            IntPtr handle = GetForegroundWindow();
            StringBuilder title = new StringBuilder(MAX_STRING_BUILDER);
            GetWindowText(handle, title, MAX_STRING_BUILDER); //return value > 0 if success
            return title.ToString();
        }

        public static void SetNewAppTrack(long newId)
        {
            // save max 3 log id
            // if has 2 log id previous, then remove the older log id

            if (AppTimes.Count >= 2)
            {
                long maxId = AppTimes.Max(k => k.Id);
                foreach (var item in AppTimes)
                {
                    if (item.Id < maxId)
                    {
                        RemoveQueue.Enqueue(item.Id);
                    }
                }
            }

            _lastMultiAppTime = new MultiAppTime
            {
                Id = newId,
                Collection = new List<AppTime>()
            };
            AppTimes.Add(_lastMultiAppTime);
        }

        public static async Task Run()
        {
            // remove old app time
            while (RemoveQueue.Count > 0)
            {
                var logId = RemoveQueue.Dequeue();
                if (logId > 0)
                {
                    AppTimes.RemoveAll(k => k.Id == logId);
                }
            }

            string lastWindowText = "";
            string tempWindowText = "";

            while (true)
            {
                if (_lastMultiAppTime == null) continue;

                tempWindowText = GetCurrentWindowText();
                if (lastWindowText != tempWindowText)
                {
                    if (_lastAppTime != null)
                    {
                        _lastAppTime.EndTime = ServerTimeHelper.GetUnixTimeSeconds();
                    }
                    
                    lastWindowText = tempWindowText;
                    _lastAppTime = new AppTime
                    {
                        StartTime = ServerTimeHelper.GetUnixTimeSeconds(),
                        Windows = lastWindowText
                    };
                    _lastMultiAppTime.Collection.Add(_lastAppTime);
                    Console.WriteLine("BEGIN WINDOW : " + lastWindowText + "\n");
                }

                await Task.Delay(1000);
            }
        }
    }

}
