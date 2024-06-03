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
using AppRealtime;
using System.Net.Http;
using System.ServiceProcess;

namespace ScreenTask
{
    static class Program
    {
        static void Main(string[] args)
        {
            var service = new AppRealtimeService();
//#if DEBUG
            service.Start(args);
            Console.ReadLine();
//#else
//            ServiceBase.Run(service);
//#endif
        }
    }
}
