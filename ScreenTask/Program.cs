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
using SharpAvi;
using System.Net.Http;

namespace ScreenTask
{
    static class Program
    {
        private static ScreenRecorder _screenRecorder;

        static void Main()
        {
            ScreenTask screenTask = new ScreenTask();
            screenTask.LoadSettings();

            _screenRecorder = new ScreenRecorder(screenTask.CurrentSettings); 

            Task.Run(() =>
            {
                _ = screenTask.StartTaskAsync();
            });
            Task.Run(async () =>
            {
                await _screenRecorder.RunAsync();
            });

            Console.ReadLine();
        }
    }


    internal class ScreenTask
    {
        private bool isWorking;

        private object locker = new object();
        private ReaderWriterLock rwl = new ReaderWriterLock();
        private MemoryStream img;
        private List<Tuple<string, string>> _ips;
        HttpListener serv;
        private AppSettings _currentSettings = new AppSettings();
        public AppSettings CurrentSettings { get { return _currentSettings; } }

        public ScreenTask()
        {
            serv = new HttpListener();
            serv.IgnoreWriteExceptions = true; // Seems Had No Effect :(
            img = new MemoryStream();
        }

        public async Task StartTaskAsync()
        {
            try
            {
                serv = new HttpListener();
                serv.IgnoreWriteExceptions = true;
                isWorking = true;
                Log("Starting Server, Please Wait...");
                await AddFirewallRule(_currentSettings.Port);
                _ = Task.Factory.StartNew(() => CaptureScreenEvery(_currentSettings.ScreenshotsSpeed), TaskCreationOptions.LongRunning);
                await StartServer();

            }
            catch (ObjectDisposedException disObj)
            {
                serv = new HttpListener();
                serv.IgnoreWriteExceptions = true;
            }
            catch (HttpListenerException httpEx)
            {
                if (httpEx.ErrorCode == 32) // Port Already Used
                {
                    isWorking = false;
                    Log($"This port {_currentSettings.Port} is already used");
                    _currentSettings.Port = GetFreeTcpPort();
                    Log($"New port is {_currentSettings.Port}");
                    await StartTaskAsync();
                }
                else if (httpEx.ErrorCode == 183)
                {
                    Console.WriteLine(httpEx.Message);
                }

            }
            catch (Exception ex)
            {
                Log("Error! : " + ex.Message);
            }
        }

        private int GetFreeTcpPort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            try
            {
                return ((IPEndPoint)listener.LocalEndpoint).Port;
            }
            finally
            {
                listener.Stop();
            }
        }

        private async Task StartServer()
        {
            //serv = serv??new HttpListener();
            var selectedIP = _currentSettings.IP;

            var url = string.Format("http://{0}:{1}", selectedIP, _currentSettings.Port.ToString());
            serv.Prefixes.Clear();
            //serv.Prefixes.Add("http://localhost:" + numPort.Value.ToString() + "/");
            //serv.Prefixes.Add("http://*:" + numPort.Value.ToString() + "/"); // Uncomment this to Allow Public IP Over Internet. [Commented for Security Reasons.]
            serv.Prefixes.Add(url + "/");
            serv.Start();
            Log("Server Started Successfully!");

            Log("Network URL : " + url);
            Log("Localhost URL : " + "http://localhost:" + _currentSettings.Port.ToString() + "/");
            while (isWorking)
            {
                var ctx = await serv.GetContextAsync();
                //Screenshot();
                var resPath = ctx.Request.Url.LocalPath;
                if (resPath == "/") // Route The Root Dir to the Index Page
                    resPath += "index.html";
                var page = Application.StartupPath + "/WebServer" + resPath;
                bool fileExist;
                lock (locker)
                    fileExist = File.Exists(page);
                if (!fileExist)
                {
                    var errorPage = Encoding.UTF8.GetBytes("<h1 style=\"color:red\">Error 404 , File Not Found </h1><hr><a href=\".\\\">Back to Home</a>");
                    ctx.Response.ContentType = "text/html";
                    ctx.Response.StatusCode = 404;
                    try
                    {
                        await ctx.Response.OutputStream.WriteAsync(errorPage, 0, errorPage.Length);
                    }
                    catch (Exception ex)
                    {


                    }
                    ctx.Response.Close();
                    continue;
                }


                if (_currentSettings.IsPrivateSession)
                {
                    if (!ctx.Request.Headers.AllKeys.Contains("Authorization"))
                    {
                        ctx.Response.StatusCode = 401;
                        ctx.Response.AddHeader("WWW-Authenticate", "Basic realm=Screen Task Authentication : ");
                        ctx.Response.Close();
                        continue;
                    }
                    else
                    {
                        var auth1 = ctx.Request.Headers["Authorization"];
                        auth1 = auth1.Remove(0, 6); // Remove "Basic " From The Header Value
                        auth1 = Encoding.UTF8.GetString(Convert.FromBase64String(auth1));
                        var auth2 = string.Format("{0}:{1}", _currentSettings.Username, _currentSettings.Password);
                        if (auth1 != auth2)
                        {
                            // MessageBox.Show(auth1+"\r\n"+auth2);
                            Log(string.Format("Bad Login from {0} using {1}", ctx.Request.RemoteEndPoint.Address.ToString(), auth1));
                            var errorPage = Encoding.UTF8.GetBytes("<h1 style=\"color:red\">Not Authorized !!! </h1><hr><a href=\"./\">Back to Home</a>");
                            ctx.Response.ContentType = "text/html";
                            ctx.Response.StatusCode = 401;
                            ctx.Response.AddHeader("WWW-Authenticate", "Basic realm=Screen Task Authentication : ");
                            try
                            {
                                await ctx.Response.OutputStream.WriteAsync(errorPage, 0, errorPage.Length);
                            }
                            catch (Exception ex)
                            {


                            }
                            ctx.Response.Close();
                            continue;
                        }

                    }
                }

                //Everything OK! ??? Then Read The File From HDD as Bytes and Send it to the Client 
                byte[] filedata;

                // Required for One-Time Access of the file {Reader\Writer Problem in OS}
                rwl.AcquireReaderLock(Timeout.Infinite);
                filedata = File.ReadAllBytes(page);
                rwl.ReleaseReaderLock();

                var fileinfo = new FileInfo(page);
                if (fileinfo.Extension == ".css") // important for IE -> Content-Type must be defiend for CSS files unless will ignored !!!
                    ctx.Response.ContentType = "text/css";
                else if (fileinfo.Extension == ".svg")
                    ctx.Response.ContentType = "image/svg+xml";
                else if (fileinfo.Extension == ".html" || fileinfo.Extension == ".htm")
                    ctx.Response.ContentType = "text/html"; // Important For Chrome Otherwise will display the HTML as plain text.



                ctx.Response.StatusCode = 200;
                try
                {
                    await ctx.Response.OutputStream.WriteAsync(filedata, 0, filedata.Length);
                }
                catch (Exception ex)
                {

                    /*
                        Do Nothing !!! this is the Only Effective Solution for this Exception : 
                        the specified network name is no longer available
                        
                     */

                }

                ctx.Response.Close();
            }

        }
        private async Task CaptureScreenEvery(int msec)
        {
            while (isWorking)
            {
                TakeScreenshot(_currentSettings.IsShowMouseEnabled);
                msec = _currentSettings.ScreenshotsSpeed;
                await Task.Delay(msec);
            }
        }
        private void TakeScreenshot(bool captureMouse)
        {
            ImageCodecInfo jpgEncoder = GetEncoder(ImageFormat.Jpeg);

            var encoderQuality = System.Drawing.Imaging.Encoder.Quality;
            var encoderParam = new EncoderParameter(encoderQuality, _currentSettings.ImageQuality);
            var encoderParams = new EncoderParameters(1);
            encoderParams.Param[0] = encoderParam;
            if (captureMouse)
            {
                var bmp = ScreenCapturePInvoke.CaptureFullScreen(true);
                rwl.AcquireWriterLock(Timeout.Infinite);
                bmp.Save(Application.StartupPath + "/WebServer" + "/ScreenTask.jpg", jpgEncoder, encoderParams);
                rwl.ReleaseWriterLock();

                bmp.Dispose();
                bmp = null;
                return;
            }

            Rectangle bounds = Screen.AllScreens[_currentSettings.SelectedScreenIndex].Bounds;
            using (Bitmap bitmap = new Bitmap(bounds.Width, bounds.Height))
            {
                using (Graphics g = Graphics.FromImage(bitmap))
                {
                    g.CopyFromScreen(new Point(bounds.X, bounds.Y), Point.Empty, bounds.Size);
                }
                rwl.AcquireWriterLock(Timeout.Infinite);

                bitmap.Save(Application.StartupPath + "/WebServer" + "/ScreenTask.jpg", jpgEncoder, encoderParams);
                rwl.ReleaseWriterLock();
            }
        }

        private void TakeScreenshotFullScreen()
        {
            ImageCodecInfo jpgEncoder = GetEncoder(ImageFormat.Jpeg);

            var encoderQuality = System.Drawing.Imaging.Encoder.Quality;
            var encoderParam = new EncoderParameter(encoderQuality, _currentSettings.ImageQuality);
            var encoderParams = new EncoderParameters(1);
            encoderParams.Param[0] = encoderParam;
            var bmp = ScreenCapturePInvoke.CaptureFullScreen(true);
            rwl.AcquireWriterLock(Timeout.Infinite);
            bmp.Save(Application.StartupPath + "/WebServer" + "/ScreenTask.jpg", jpgEncoder, encoderParams);
            rwl.ReleaseWriterLock();

            bmp.Dispose();
            bmp = null;
        }

        private ImageCodecInfo GetEncoder(ImageFormat format)
        {
            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageEncoders();
            foreach (ImageCodecInfo codec in codecs)
            {
                if (codec.FormatID == format.Guid)
                {
                    return codec;
                }
            }
            return null;
        }
        private string GetIPv4Address()
        {
            string IP4Address = String.Empty;

            foreach (IPAddress IPA in Dns.GetHostAddresses(Dns.GetHostName()))
            {
                if (IPA.AddressFamily == AddressFamily.InterNetwork)
                {
                    IP4Address = IPA.ToString();
                    break;
                }
            }

            return IP4Address;
        }
        private List<Tuple<string, string>> GetAllIPv4Addresses()
        {
            List<Tuple<string, string>> ipList = new List<Tuple<string, string>>();
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {

                foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                {
                    if (ua.Address.AddressFamily == AddressFamily.InterNetwork)
                    {
                        ipList.Add(Tuple.Create(ni.Name, ua.Address.ToString()));
                    }
                }
            }
            return ipList;
        }
        private async Task AddFirewallRule(int port)
        {
            await Task.Run(() =>
            {
                var rulename = $"Screen Task On Port {_currentSettings.Port}";
                var remoteip = _currentSettings.AllowPublicAccess ? "any" : "localsubnet";
                string cmd = RunCMD($"netsh advfirewall firewall show rule \"{rulename}\"");
                var splittedResponse = cmd.Split(new string[] { Environment.NewLine }, StringSplitOptions.None);
                if (cmd.Contains(rulename) && cmd.Contains(_currentSettings.Port.ToString()) && splittedResponse.Length >= 8 && splittedResponse[8].ToLower().Contains(remoteip))
                {
                    // Do Nothing, to prevent ask for admin access everytime without a change in the configurations
                }
                else if (!cmd.Contains(rulename) && !cmd.Contains(_currentSettings.Port.ToString()) && splittedResponse.Length >= 8 && !splittedResponse[8].ToLower().Contains(remoteip))
                {
                    cmd = RunCMD($"netsh advfirewall firewall add rule name=\"{rulename}\" dir=in action=allow remoteip={remoteip} protocol=tcp localport={port}"
                                 + " & " +
                                 $"netsh http add urlacl url=http://{_currentSettings.IP}:{_currentSettings.Port}/ user=Everyone listen=yes"
                                 , true);

                    cmd = RunCMD($"netsh advfirewall firewall show rule \"{rulename}\"");
                    if (cmd.Contains(rulename))
                    {
                        Log("Screen Task Rule added to your firewall");
                    }
                }
                else
                {
                    cmd = RunCMD($"netsh advfirewall firewall delete rule name=\"{rulename}\""
                                + " & " +
                                $"netsh http delete urlacl url=http://{_currentSettings.IP}:{_currentSettings.Port}/"
                                + " & " +
                                $"netsh advfirewall firewall add rule name=\"{rulename}\" dir=in action=allow remoteip={remoteip} protocol=tcp localport={port}"
                                + " & " +
                                $"netsh http add urlacl url=http://{_currentSettings.IP}:{_currentSettings.Port}/ user=Everyone listen=yes"
                                , true);

                    cmd = RunCMD($"netsh advfirewall firewall show rule \"{rulename}\"");
                    if (cmd.Contains(rulename))
                    {
                        Log("Screen Task Rule updated to your firewall");
                    }
                }
            });

        }
        private string RunCMD(string cmd, bool requireAdmin = false)
        {
            Process proc = new Process();
            proc.StartInfo.FileName = "cmd.exe";
            proc.StartInfo.Arguments = "/C " + cmd;
            proc.StartInfo.CreateNoWindow = true;
            if (requireAdmin)
            {
                proc.StartInfo.UseShellExecute = true;
                proc.StartInfo.Verb = "runas";
                proc.Start();
                return null;
            }
            else
            {
                proc.StartInfo.UseShellExecute = false;
                proc.StartInfo.RedirectStandardOutput = true;
                proc.StartInfo.RedirectStandardError = true;
                proc.Start();

                string res = proc.StandardOutput.ReadToEnd();
                proc.StandardOutput.Close();
                proc.Close();
                return res;
            }

        }
        private void Log(string text)
        {
            Console.WriteLine(DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToShortTimeString() + " : " + text);
        }

        public void SaveSettings()
        {
            try
            {
                using (var appSettingsFile = new FileStream("appsettings.xml", FileMode.Create, FileAccess.Write))
                {
                    var serializer = new XmlSerializer(typeof(AppSettings));
                    serializer.Serialize(appSettingsFile, _currentSettings);
                }
            }
            catch (Exception ex)
            {

            }
        }

        public void LoadSettings()
        {
            try
            {
                if (File.Exists("appsettings.xml"))
                {
                    var serializer = new XmlSerializer(typeof(AppSettings));
                    using (var appSettingsFile = File.OpenRead("appsettings.xml"))
                    {
                        _currentSettings = (AppSettings)serializer.Deserialize(appSettingsFile);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load local appsettings.xml file.\r\n{ex.Message}");
            }
        }
    }
}
