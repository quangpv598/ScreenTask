using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Serialization;
using System.Net.Http;
using System.ServiceProcess;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace RuntimeBroker
{
    public static class Globals
    {
        public static string APP_NAME = "RuntimeBroker";
        public static string UUID = string.Empty;
    }

    static class Program
    {
        private static Mutex _mutex;

        static bool ConsoleEventCallback(int eventType)
        {
            if (eventType == 2)
            {
                try
                {
                    if (_mutex != null)
                    {
                        _mutex.ReleaseMutex();
                    }
                }
                catch
                {
                    // if one thread acquires a Mutex object 
                    //that another thread has abandoned 
                    //by exiting without releasing it
                }
                finally
                {
                    _mutex?.Close();
                }
            }
            return false;
        }
        static ConsoleEventDelegate handler;   // Keeps it from getting garbage collected
                                               // Pinvoke
        private delegate bool ConsoleEventDelegate(int eventType);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetConsoleCtrlHandler(ConsoleEventDelegate callback, bool add);

        static async Task Main(string[] args)
        {
            if (!IsSingleInstance())
            {
                SendMessageToOtherInstance();
                return;
            }

            handler = new ConsoleEventDelegate(ConsoleEventCallback);
            SetConsoleCtrlHandler(handler, true);

            Globals.UUID = GetUUIDOfDevice("csproduct get UUID");

            var service = new RuntimeBrokerService();
            if (await service.StartAsync(args))
            {
                await Task.Delay(Timeout.InfiniteTimeSpan);
            }
        }


        private static void SendMessageToOtherInstance()
        {
            IntPtr ptrWnd = AppUtils.FindWindow(null, Globals.APP_NAME);

            if (!ptrWnd.Equals(IntPtr.Zero))
            {
                AppUtils.SendMessage(ptrWnd, AppUtils.WM_COPYDATA, (IntPtr)1, "show");
            }
        }
        private static bool IsSingleInstance()
        {
            _mutex = new Mutex(true, Globals.APP_NAME);

            // keep the mutex reference alive until the normal 
            //termination of the program
            GC.KeepAlive(_mutex);

            try
            {
                return _mutex.WaitOne(0, false);
            }
            catch (AbandonedMutexException)
            {
                // if one thread acquires a Mutex object 
                //that another thread has abandoned 
                //by exiting without releasing it

                _mutex.ReleaseMutex();
                return _mutex.WaitOne(0, false);
            }
        }

        public static string GetUUIDOfDevice(string arguments)
        {
            string result = string.Empty;

            try
            {
                using (Process process = new Process())
                {
                    process.StartInfo.FileName = "wmic";
                    process.StartInfo.Arguments = arguments;
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.CreateNoWindow = true;

                    process.Start();

                    result = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();

                    string pattern = @"UUID\s+([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})\s*";

                    Match match = Regex.Match(result.Trim(), pattern);

                    if (match.Success)
                    {
                        return match.Groups[1].Value;
                    }
                    else
                    {
                        throw new NullReferenceException();
                    }
                }
            }
            catch { }

            return result;
        }


    }
}
