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
using System.Windows;
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
                },
                OutputOptions = new OutputOptions
                {
                    RecorderMode = RecorderMode.Video,
                    //This sets a custom size of the video output, in pixels.
                    OutputFrameSize = new ScreenSize(_appSettings.FrameWidth, _appSettings.FrameHeight),
                    //Stretch controls how the resizing is done, if the new aspect ratio differs.
                    Stretch = StretchMode.Uniform,
                },
            };

            _rec = Recorder.CreateRecorder(options);
            _rec.OnRecordingFailed += _rec_OnRecordingFailed;
        }

        private void _rec_OnRecordingFailed(object sender, RecordingFailedEventArgs e)
        {
            Console.WriteLine(e.Error.ToString());
        }

        public async Task RunAsync()
        {
            while (true)
            {
                try
                {
                    int maxVideoSeconds = (int)TimeSpan.FromSeconds(_appSettings.VideoDuration).TotalMilliseconds;

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

                    await WaitUntil(() => _rec.Status == RecorderStatus.Idle);

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
                                catch (Exception ex) { }
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

        public void Stop()
        {
            try
            {
                if (_rec != null)
                {
                    _rec.Stop();
                }
            }
            catch (Exception ex) { Console.WriteLine(ex.ToString()); }
        }

        /// <summary>
        /// Blocks until condition is true or timeout occurs.
        /// </summary>
        /// <param name="condition">The break condition.</param>
        /// <param name="frequency">The frequency at which the condition will be checked.</param>
        /// <param name="timeout">The timeout in milliseconds.</param>
        /// <returns></returns>
        private static async Task WaitUntil(Func<bool> condition, int frequency = 25, int timeout = -1)
        {
            var waitTask = Task.Run(async () =>
            {
                while (!condition()) await Task.Delay(frequency);
            });

            if (waitTask != await Task.WhenAny(waitTask,
                    Task.Delay(timeout)))
                throw new TimeoutException();
        }
    }
}
