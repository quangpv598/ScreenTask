using ScreenRecorderLib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Media.Media3D;

namespace AppRealtime
{
    public class ScreenRecorder
    {
        private readonly AppSettings _appSettings;
        private readonly Recorder _rec;

        public ScreenRecorder(AppSettings appSettings)
        {
            _appSettings = appSettings;

            var sources = new List<RecordingSourceBase>();
            sources.AddRange(Recorder.GetDisplays());
            RecorderOptions options = new RecorderOptions
            {
                SourceOptions = new SourceOptions
                {
                    RecordingSources = sources
                }
            };

            _rec = Recorder.CreateRecorder(options);
        }

        public async Task RunAsync()
        {
            while (true)
            {
                try
                {
                    int maxVideoSeconds = (int)TimeSpan.FromSeconds(60).TotalMilliseconds;

                    if (!Directory.Exists("Videos"))
                    {
                        Directory.CreateDirectory("Videos");
                    }
                    var now = ServerTimeHelper.GetUnixTimeSeconds();
                    string fileName = $"{now}_{now + maxVideoSeconds}.mp4";
                    string videoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Videos", fileName);

                    Console.WriteLine("Record:" + videoPath);
                    _rec.Record(videoPath);

                    await Task.Delay(maxVideoSeconds);

                    _rec.Stop();

                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            const int MAX_RETRIES = 5;
                            for (int i = 0; i < MAX_RETRIES; i++)
                            {
                                if (i >= MAX_RETRIES)
                                {
                                    break;
                                }

                                try
                                {
                                    var client = new HttpClient();
                                    var request = new HttpRequestMessage(HttpMethod.Post, _appSettings.Host);
                                    request.Headers.Add("accept", "*/*");
                                    var content = new MultipartFormDataContent();
                                    content.Add(new StreamContent(File.OpenRead(videoPath)), "file", videoPath);
                                    content.Add(new StringContent($"{DateTime.Now.ToString("yyyy-MM-dd")}"), "createdDate");
                                    content.Add(new StringContent(_appSettings.IP), "comName");
                                    request.Content = content;
                                    var response = await client.SendAsync(request);
                                    response.EnsureSuccessStatusCode();
                                    if ((await response.Content.ReadAsStringAsync()).Contains("Files uploaded successfully"))
                                    {
                                        break;
                                    }
                                }
                                catch { }
                            }
                        }
                        finally
                        {
                            File.Delete(videoPath);
                        }
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }
            }
        }
    }
}
